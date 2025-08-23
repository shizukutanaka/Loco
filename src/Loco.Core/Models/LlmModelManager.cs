using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Models;

/// <summary>
/// LLM model management - Secure download, verification, and storage
/// Following John Carmack's performance focus with Rob Pike's simplicity
/// </summary>
public class LlmModelManager
{
    private readonly ILogger<LlmModelManager> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _modelsPath;
    private readonly Dictionary<string, ModelInfo> _models = new();
    private readonly SemaphoreSlim _downloadLock = new(1, 1);

    /// <summary>
    /// Raised when the model registry changes (added/removed/loaded), so UIs can refresh.
    /// </summary>
    public event EventHandler ModelsChanged;

    public LlmModelManager(ILogger<LlmModelManager> logger, HttpClient httpClient, string modelsPath)
    {
        _logger = logger;
        _httpClient = httpClient;
        _modelsPath = modelsPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loco", "models");
        Directory.CreateDirectory(_modelsPath);
        LoadModelRegistry();
    }

    /// <summary>
    /// Download and verify a model with security checks
    /// </summary>
    public async Task<ModelDownloadResult> DownloadModelAsync(
        string url,
        string expectedHash = null,
        IProgress<float> progress = null,
        CancellationToken cancellationToken = default)
    {
        await _downloadLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Starting model download from {Url}", url);
            
            // Parse URL and get filename
            var uri = new Uri(url);
            var filename = Path.GetFileName(uri.LocalPath);
            if (string.IsNullOrEmpty(filename))
                filename = $"model_{DateTime.UtcNow.Ticks}.gguf";
            
            var tempPath = Path.Combine(_modelsPath, $".{filename}.downloading");
            var finalPath = Path.Combine(_modelsPath, filename);
            
            // Check if already exists
            if (File.Exists(finalPath))
            {
                if (!string.IsNullOrEmpty(expectedHash))
                {
                    var existingHash = await CalculateFileHashAsync(finalPath);
                    if (existingHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Model already exists with correct hash");
                        return new ModelDownloadResult
                        {
                            Success = true,
                            FilePath = finalPath,
                            Hash = existingHash,
                            Message = "Model already exists"
                        };
                    }
                }
            }
            
            // Download with progress
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1 && progress != null;
            
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            
            var buffer = new byte[8192];
            var totalRead = 0L;
            var read = 0;
            
            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                totalRead += read;
                
                if (canReportProgress)
                {
                    progress.Report((float)totalRead / totalBytes);
                }
            }
            
            await fileStream.FlushAsync(cancellationToken);
            fileStream.Close();
            
            // Verify hash
            var actualHash = await CalculateFileHashAsync(tempPath);
            if (!string.IsNullOrEmpty(expectedHash) && !actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(tempPath);
                _logger.LogError("Hash verification failed. Expected: {Expected}, Actual: {Actual}", expectedHash, actualHash);
                return new ModelDownloadResult
                {
                    Success = false,
                    Message = "Hash verification failed"
                };
            }
            
            // Move to final location
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(tempPath, finalPath);
            
            // Register model
            var modelInfo = new ModelInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = Path.GetFileNameWithoutExtension(filename),
                FilePath = finalPath,
                Hash = actualHash,
                Size = new FileInfo(finalPath).Length,
                DownloadedAt = DateTime.UtcNow,
                Source = url
            };
            
            _models[modelInfo.Id] = modelInfo;
            await SaveModelRegistryAsync();
            RaiseModelsChanged();
            
            _logger.LogInformation("Model downloaded successfully: {Name}", modelInfo.Name);
            
            return new ModelDownloadResult
            {
                Success = true,
                FilePath = finalPath,
                Hash = actualHash,
                ModelInfo = modelInfo,
                Message = "Download completed successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading model");
            return new ModelDownloadResult
            {
                Success = false,
                Message = ex.Message
            };
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    /// <summary>
    /// Calculate SHA256 hash of a file
    /// </summary>
    private async Task<string> CalculateFileHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await Task.Run(() => sha256.ComputeHash(stream));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Get all registered models
    /// </summary>
    public IEnumerable<ModelInfo> GetModels()
    {
        return _models.Values.OrderBy(m => m.Name);
    }

    /// <summary>
    /// Get model by ID
    /// </summary>
    public ModelInfo GetModel(string modelId)
    {
        return _models.TryGetValue(modelId, out var model) ? model : null;
    }

    /// <summary>
    /// Delete a model
    /// </summary>
    public async Task<bool> DeleteModelAsync(string modelId)
    {
        if (!_models.TryGetValue(modelId, out var model))
            return false;
        
        try
        {
            if (File.Exists(model.FilePath))
                File.Delete(model.FilePath);
            
            _models.Remove(modelId);
            await SaveModelRegistryAsync();
            RaiseModelsChanged();
            
            _logger.LogInformation("Model deleted: {Name}", model.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model");
            return false;
        }
    }

    /// <summary>
    /// Verify model integrity
    /// </summary>
    public async Task<bool> VerifyModelAsync(string modelId)
    {
        if (!_models.TryGetValue(modelId, out var model))
            return false;
        
        if (!File.Exists(model.FilePath))
        {
            model.Status = ModelStatus.Missing;
            return false;
        }
        
        var actualHash = await CalculateFileHashAsync(model.FilePath);
        var isValid = actualHash.Equals(model.Hash, StringComparison.OrdinalIgnoreCase);
        
        model.Status = isValid ? ModelStatus.Ready : ModelStatus.Corrupted;
        model.LastVerified = DateTime.UtcNow;
        
        await SaveModelRegistryAsync();
        return isValid;
    }

    /// <summary>
    /// Load model registry from disk
    /// </summary>
    private void LoadModelRegistry()
    {
        var registryPath = Path.Combine(_modelsPath, "registry.json");
        if (!File.Exists(registryPath))
            return;
        
        try
        {
            var json = File.ReadAllText(registryPath);
            var models = JsonSerializer.Deserialize<List<ModelInfo>>(json);
            
            foreach (var model in models)
            {
                _models[model.Id] = model;
            }
            
            _logger.LogInformation("Loaded {Count} models from registry", _models.Count);
            RaiseModelsChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading model registry");
        }
    }

    /// <summary>
    /// Save model registry to disk
    /// </summary>
    private async Task SaveModelRegistryAsync()
    {
        var registryPath = Path.Combine(_modelsPath, "registry.json");
        
        try
        {
            var json = JsonSerializer.Serialize(_models.Values.ToList(), new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(registryPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving model registry");
        }
    }

    private void RaiseModelsChanged()
    {
        try
        {
            ModelsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error raising ModelsChanged event");
        }
    }
}

/// <summary>
/// Model information
/// </summary>
public class ModelInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string FilePath { get; set; }
    public string Hash { get; set; }
    public long Size { get; set; }
    public string Format { get; set; } = "GGUF";
    public string License { get; set; }
    public DateTime DownloadedAt { get; set; }
    public DateTime? LastVerified { get; set; }
    public string Source { get; set; }
    public ModelStatus Status { get; set; } = ModelStatus.Ready;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Model status
/// </summary>
public enum ModelStatus
{
    Ready,
    Downloading,
    Verifying,
    Corrupted,
    Missing,
    Loading,
    Loaded,
    Error
}

/// <summary>
/// Model download result
/// </summary>
public class ModelDownloadResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string FilePath { get; set; }
    public string Hash { get; set; }
    public ModelInfo ModelInfo { get; set; }
}