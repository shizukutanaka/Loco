using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;

namespace Loco.Core.Marketplace;

/// <summary>
/// Flow Marketplace - Share and discover automation flows
/// Following Rob Pike's simplicity principle
/// </summary>
public partial class FlowMarketplace
{
    private readonly ILogger<FlowMarketplace> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _localRepository;
    private readonly string _marketplaceUrl;
    private readonly Dictionary<string, MarketplaceFlow> _cache = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    
    public FlowMarketplace(ILogger<FlowMarketplace> logger, HttpClient httpClient, string localRepository = null)
    {
        _logger = logger;
        _httpClient = httpClient;
        _localRepository = localRepository ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Loco", "Marketplace");
        _marketplaceUrl = "https://api.loco-marketplace.com/v1";
        
        Directory.CreateDirectory(_localRepository);
        Directory.CreateDirectory(Path.Combine(_localRepository, "flows"));
        Directory.CreateDirectory(Path.Combine(_localRepository, "packs"));
        Directory.CreateDirectory(Path.Combine(_localRepository, "templates"));
    }
    
    /// <summary>
    /// Search flows in marketplace
    /// </summary>
    public async Task<List<MarketplaceFlow>> SearchAsync(string query, MarketplaceCategory? category = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // First, search local repository
            var localResults = SearchLocal(query, category);
            
            // Then, search online if available
            if (await IsOnlineAsync())
            {
                var onlineResults = await SearchOnlineAsync(query, category, cancellationToken);
                
                // Merge results, prioritizing online
                var merged = new Dictionary<string, MarketplaceFlow>();
                foreach (var flow in localResults)
                    merged[flow.Id] = flow;
                foreach (var flow in onlineResults)
                    merged[flow.Id] = flow;
                
                return merged.Values.OrderByDescending(f => f.Downloads).ToList();
            }
            
            return localResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching marketplace");
            return new List<MarketplaceFlow>();
        }
    }
    
    /// <summary>
    /// Install flow from marketplace
    /// </summary>
    public async Task<InstallResult> InstallAsync(string flowId, CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Installing flow: {FlowId}", flowId);
            
            // Check if already installed
            var localPath = Path.Combine(_localRepository, "flows", $"{flowId}.json");
            if (File.Exists(localPath))
            {
                return new InstallResult
                {
                    Success = true,
                    Message = "Flow already installed",
                    LocalPath = localPath,
                    Flow = await LoadFlowAsync(localPath)
                };
            }
            
            // Download from marketplace
            var downloadUrl = $"{_marketplaceUrl}/flows/{flowId}/download";
            var response = await _httpClient.GetAsync(downloadUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return new InstallResult
                {
                    Success = false,
                    Message = $"Failed to download flow: {response.StatusCode}"
                };
            }
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var marketplaceFlow = JsonSerializer.Deserialize<MarketplaceFlow>(content);
            
            // Validate flow
            if (!ValidateFlow(marketplaceFlow))
            {
                return new InstallResult
                {
                    Success = false,
                    Message = "Flow validation failed"
                };
            }
            
            // Save to local repository
            await File.WriteAllTextAsync(localPath, content, cancellationToken);
            
            // Update metadata
            await UpdateMetadataAsync(marketplaceFlow);
            
            return new InstallResult
            {
                Success = true,
                Message = "Flow installed successfully",
                LocalPath = localPath,
                Flow = marketplaceFlow.FlowDefinition
            };
        }
        finally
        {
            _syncLock.Release();
        }
    }
    
    /// <summary>
    /// Install from URL (one-click install)
    /// </summary>
    public async Task<InstallResult> InstallFromUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Installing from URL: {Url}", url);
            
            // Parse URL formats:
            // - loco://install/flow-id
            // - https://loco-marketplace.com/flow/flow-id
            // - https://github.com/user/repo/flow.json
            
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                return new InstallResult
                {
                    Success = false,
                    Message = "Invalid URL"
                };
            }
            
            // Handle loco:// protocol
            if (uri.Scheme == "loco")
            {
                var flowId = uri.LocalPath.TrimStart('/');
                return await InstallAsync(flowId, cancellationToken);
            }
            
            // Download from HTTP/HTTPS
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new InstallResult
                {
                    Success = false,
                    Message = $"Failed to download: {response.StatusCode}"
                };
            }
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var flow = JsonSerializer.Deserialize<FlowDefinition>(content);
            
            if (flow == null)
            {
                return new InstallResult
                {
                    Success = false,
                    Message = "Invalid flow format"
                };
            }
            
            // Save locally
            var filename = Path.GetFileName(uri.LocalPath) ?? $"flow_{Guid.NewGuid()}.json";
            var localPath = Path.Combine(_localRepository, "flows", filename);
            await File.WriteAllTextAsync(localPath, content, cancellationToken);
            
            return new InstallResult
            {
                Success = true,
                Message = "Flow installed from URL",
                LocalPath = localPath,
                Flow = flow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing from URL");
            return new InstallResult
            {
                Success = false,
                Message = $"Installation failed: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// Share flow to marketplace
    /// </summary>
    public async Task<ShareResult> ShareAsync(FlowDefinition flow, MarketplaceMetadata metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sharing flow: {Name}", flow.Name);
            
            var marketplaceFlow = new MarketplaceFlow
            {
                Id = Guid.NewGuid().ToString(),
                FlowDefinition = flow,
                Metadata = metadata,
                UploadedAt = DateTime.UtcNow,
                Version = "1.0.0"
            };
            
            // Validate before sharing
            if (!ValidateFlow(marketplaceFlow))
            {
                return new ShareResult
                {
                    Success = false,
                    Message = "Flow validation failed"
                };
            }
            
            // Upload to marketplace
            var json = JsonSerializer.Serialize(marketplaceFlow);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_marketplaceUrl}/flows", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return new ShareResult
                {
                    Success = false,
                    Message = $"Upload failed: {response.StatusCode}"
                };
            }
            
            var result = await response.Content.ReadAsStringAsync(cancellationToken);
            var shareInfo = JsonSerializer.Deserialize<ShareInfo>(result);
            
            return new ShareResult
            {
                Success = true,
                Message = "Flow shared successfully",
                ShareUrl = shareInfo.ShareUrl,
                FlowId = shareInfo.FlowId,
                QrCode = GenerateQrCode(shareInfo.ShareUrl)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing flow");
            return new ShareResult
            {
                Success = false,
                Message = $"Share failed: {ex.Message}"
            };
        }
    }
}