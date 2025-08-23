using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;
using Loco.Core.Validation;

namespace Loco.Core.Services
{
    /// <summary>
    /// Export/Import service for automation rules and configurations
    /// Follows Clean Architecture with secure serialization
    /// </summary>
    public sealed class ExportImportService
    {
        private readonly ILogger<ExportImportService> _logger;
        private readonly ComprehensiveValidator _validator;
        private readonly JsonSerializerOptions _jsonOptions;
        private const string ExportVersion = "1.0.0";
        private const int MaxFileSize = 50 * 1024 * 1024; // 50MB

        public ExportImportService(ILogger<ExportImportService> logger = null)
        {
            _logger = logger;
            _validator = new ComprehensiveValidator();
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        /// <summary>
        /// Export rules to a file
        /// </summary>
        public async Task<ExportResult> ExportRulesToFileAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            string filePath,
            ExportFormat format = ExportFormat.Json,
            bool compress = false)
        {
            try
            {
                if (rules == null || !rules.Any())
                {
                    return new ExportResult
                    {
                        Success = false,
                        Message = "No rules to export"
                    };
                }

                // Validate all rules before export
                var validationTasks = rules.Select(r => _validator.ValidateAutomationRuleAsync(r));
                var validationResults = await Task.WhenAll(validationTasks);
                
                var invalidRules = validationResults.Where(r => !r.IsValid).ToList();
                if (invalidRules.Any())
                {
                    _logger?.LogWarning("Some rules failed validation during export");
                }

                // Create export package
                var package = new ExportPackage
                {
                    Version = ExportVersion,
                    ExportDate = DateTime.UtcNow,
                    Rules = rules.ToList(),
                    Metadata = new ExportMetadata
                    {
                        TotalRules = rules.Count(),
                        MachineName = Environment.MachineName,
                        UserName = Environment.UserName,
                        Platform = Environment.OSVersion.Platform.ToString()
                    }
                };

                // Serialize based on format
                byte[] data = format switch
                {
                    ExportFormat.Json => await SerializeToJsonAsync(package),
                    ExportFormat.Xml => await SerializeToXmlAsync(package),
                    ExportFormat.Binary => await SerializeToBinaryAsync(package),
                    _ => throw new NotSupportedException($"Export format {format} is not supported")
                };

                // Compress if requested
                if (compress)
                {
                    data = await CompressDataAsync(data);
                    filePath = EnsureCompressedExtension(filePath);
                }

                // Write to file
                await File.WriteAllBytesAsync(filePath, data);

                _logger?.LogInformation("Exported {Count} rules to {FilePath}", rules.Count(), filePath);

                return new ExportResult
                {
                    Success = true,
                    Message = $"Successfully exported {rules.Count()} rules",
                    FilePath = filePath,
                    ExportedCount = rules.Count(),
                    FileSize = data.Length
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to export rules");
                return new ExportResult
                {
                    Success = false,
                    Message = $"Export failed: {ex.Message}",
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Import rules from a file
        /// </summary>
        public async Task<ImportResult> ImportRulesFromFileAsync(
            string filePath,
            ImportOptions options = null)
        {
            options ??= new ImportOptions();

            try
            {
                if (!File.Exists(filePath))
                {
                    return new ImportResult
                    {
                        Success = false,
                        Message = "File not found"
                    };
                }

                // Check file size
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > MaxFileSize)
                {
                    return new ImportResult
                    {
                        Success = false,
                        Message = $"File size exceeds maximum allowed size of {MaxFileSize / (1024 * 1024)}MB"
                    };
                }

                // Read file
                var data = await File.ReadAllBytesAsync(filePath);

                // Decompress if needed
                if (IsCompressed(filePath) || IsCompressedData(data))
                {
                    data = await DecompressDataAsync(data);
                }

                // Detect format and deserialize
                var format = DetectFormat(data);
                var package = format switch
                {
                    ExportFormat.Json => await DeserializeFromJsonAsync(data),
                    ExportFormat.Xml => await DeserializeFromXmlAsync(data),
                    ExportFormat.Binary => await DeserializeFromBinaryAsync(data),
                    _ => throw new NotSupportedException("Unknown file format")
                };

                if (package == null || package.Rules == null)
                {
                    return new ImportResult
                    {
                        Success = false,
                        Message = "Invalid or corrupt file"
                    };
                }

                // Version compatibility check
                if (!IsVersionCompatible(package.Version))
                {
                    if (!options.IgnoreVersionMismatch)
                    {
                        return new ImportResult
                        {
                            Success = false,
                            Message = $"Incompatible version: {package.Version}"
                        };
                    }
                }

                // Validate imported rules
                var importedRules = new List<AutomationDsl.Rule>();
                var skippedRules = new List<string>();
                var errors = new List<string>();

                foreach (var rule in package.Rules)
                {
                    if (options.ValidateBeforeImport)
                    {
                        var validationResult = await _validator.ValidateAutomationRuleAsync(rule);
                        if (!validationResult.IsValid)
                        {
                            if (options.SkipInvalidRules)
                            {
                                skippedRules.Add($"{rule.Name}: {string.Join(", ", validationResult.Errors.Select(e => e.Message))}");
                                continue;
                            }
                            else
                            {
                                errors.Add($"Rule '{rule.Name}' validation failed: {string.Join(", ", validationResult.Errors.Select(e => e.Message))}");
                            }
                        }
                    }

                    // Check for duplicates
                    if (options.PreventDuplicates && importedRules.Any(r => r.Id == rule.Id))
                    {
                        if (options.GenerateNewIds)
                        {
                            rule.Id = GenerateNewId(rule.Id);
                        }
                        else
                        {
                            skippedRules.Add($"{rule.Name}: Duplicate ID");
                            continue;
                        }
                    }

                    importedRules.Add(rule);
                }

                if (errors.Any() && !options.SkipInvalidRules)
                {
                    return new ImportResult
                    {
                        Success = false,
                        Message = "Import failed due to validation errors",
                        Errors = errors
                    };
                }

                _logger?.LogInformation("Imported {Count} rules from {FilePath}", importedRules.Count, filePath);

                return new ImportResult
                {
                    Success = true,
                    Message = $"Successfully imported {importedRules.Count} rules",
                    ImportedRules = importedRules,
                    ImportedCount = importedRules.Count,
                    SkippedCount = skippedRules.Count,
                    SkippedRules = skippedRules,
                    Metadata = package.Metadata
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to import rules from {FilePath}", filePath);
                return new ImportResult
                {
                    Success = false,
                    Message = $"Import failed: {ex.Message}",
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Export rules to string (for clipboard or API)
        /// </summary>
        public async Task<string> ExportRulesToStringAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            ExportFormat format = ExportFormat.Json)
        {
            var package = new ExportPackage
            {
                Version = ExportVersion,
                ExportDate = DateTime.UtcNow,
                Rules = rules.ToList(),
                Metadata = new ExportMetadata
                {
                    TotalRules = rules.Count()
                }
            };

            return format switch
            {
                ExportFormat.Json => JsonSerializer.Serialize(package, _jsonOptions),
                ExportFormat.Xml => await SerializeToXmlStringAsync(package),
                _ => JsonSerializer.Serialize(package, _jsonOptions)
            };
        }

        /// <summary>
        /// Import rules from string (from clipboard or API)
        /// </summary>
        public async Task<ImportResult> ImportRulesFromStringAsync(
            string data,
            ImportOptions options = null)
        {
            options ??= new ImportOptions();

            try
            {
                // Try to parse as JSON first
                ExportPackage package = null;
                try
                {
                    package = JsonSerializer.Deserialize<ExportPackage>(data, _jsonOptions);
                }
                catch
                {
                    // Try XML if JSON fails
                    package = await DeserializeFromXmlStringAsync(data);
                }

                if (package == null || package.Rules == null)
                {
                    return new ImportResult
                    {
                        Success = false,
                        Message = "Invalid data format"
                    };
                }

                // Create temporary file and use file import logic
                var tempFile = Path.GetTempFileName();
                try
                {
                    await File.WriteAllTextAsync(tempFile, data);
                    return await ImportRulesFromFileAsync(tempFile, options);
                }
                finally
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            }
            catch (Exception ex)
            {
                return new ImportResult
                {
                    Success = false,
                    Message = $"Import failed: {ex.Message}",
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Backup all rules
        /// </summary>
        public async Task<BackupResult> BackupRulesAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            string backupDirectory = null)
        {
            try
            {
                backupDirectory ??= Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Loco", "Backups");

                Directory.CreateDirectory(backupDirectory);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"loco_backup_{timestamp}.json.gz";
                var filePath = Path.Combine(backupDirectory, fileName);

                var result = await ExportRulesToFileAsync(rules, filePath, ExportFormat.Json, compress: true);

                if (result.Success)
                {
                    // Clean old backups
                    await CleanOldBackupsAsync(backupDirectory, maxBackups: 10);
                }

                return new BackupResult
                {
                    Success = result.Success,
                    BackupPath = result.FilePath,
                    Message = result.Message
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Backup failed");
                return new BackupResult
                {
                    Success = false,
                    Message = $"Backup failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Restore rules from backup
        /// </summary>
        public async Task<ImportResult> RestoreFromBackupAsync(string backupPath, ImportOptions options = null)
        {
            if (!File.Exists(backupPath))
            {
                return new ImportResult
                {
                    Success = false,
                    Message = "Backup file not found"
                };
            }

            return await ImportRulesFromFileAsync(backupPath, options);
        }

        // Helper methods
        private async Task<byte[]> SerializeToJsonAsync(ExportPackage package)
        {
            var json = JsonSerializer.Serialize(package, _jsonOptions);
            return Encoding.UTF8.GetBytes(json);
        }

        private async Task<byte[]> SerializeToXmlAsync(ExportPackage package)
        {
            using var stream = new MemoryStream();
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ExportPackage));
            serializer.Serialize(stream, package);
            return stream.ToArray();
        }

        private async Task<string> SerializeToXmlStringAsync(ExportPackage package)
        {
            using var writer = new StringWriter();
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ExportPackage));
            serializer.Serialize(writer, package);
            return writer.ToString();
        }

        private async Task<ExportPackage> DeserializeFromXmlStringAsync(string xml)
        {
            using var reader = new StringReader(xml);
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ExportPackage));
            return serializer.Deserialize(reader) as ExportPackage;
        }

        private async Task<byte[]> SerializeToBinaryAsync(ExportPackage package)
        {
            // Use JSON for binary format (can be replaced with MessagePack or Protobuf)
            return await SerializeToJsonAsync(package);
        }

        private async Task<ExportPackage> DeserializeFromJsonAsync(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            return JsonSerializer.Deserialize<ExportPackage>(json, _jsonOptions);
        }

        private async Task<ExportPackage> DeserializeFromXmlAsync(byte[] data)
        {
            using var stream = new MemoryStream(data);
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ExportPackage));
            return serializer.Deserialize(stream) as ExportPackage;
        }

        private async Task<ExportPackage> DeserializeFromBinaryAsync(byte[] data)
        {
            // Use JSON for binary format
            return await DeserializeFromJsonAsync(data);
        }

        private async Task<byte[]> CompressDataAsync(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
            {
                await gzip.WriteAsync(data, 0, data.Length);
            }
            return output.ToArray();
        }

        private async Task<byte[]> DecompressDataAsync(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            {
                await gzip.CopyToAsync(output);
            }
            return output.ToArray();
        }

        private bool IsCompressed(string filePath)
        {
            return filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsCompressedData(byte[] data)
        {
            // Check for GZIP magic number
            return data.Length > 2 && data[0] == 0x1f && data[1] == 0x8b;
        }

        private string EnsureCompressedExtension(string filePath)
        {
            if (!filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            {
                return filePath + ".gz";
            }
            return filePath;
        }

        private ExportFormat DetectFormat(byte[] data)
        {
            // Check for JSON
            if (data.Length > 0 && (data[0] == '{' || data[0] == '['))
            {
                return ExportFormat.Json;
            }

            // Check for XML
            if (data.Length > 5 && data[0] == '<' && data[1] == '?')
            {
                return ExportFormat.Xml;
            }

            // Default to JSON
            return ExportFormat.Json;
        }

        private bool IsVersionCompatible(string version)
        {
            if (string.IsNullOrEmpty(version))
                return false;

            try
            {
                var current = Version.Parse(ExportVersion);
                var imported = Version.Parse(version);
                
                // Compatible if same major version
                return current.Major == imported.Major;
            }
            catch
            {
                return false;
            }
        }

        private string GenerateNewId(string originalId)
        {
            return $"{originalId}_{Guid.NewGuid():N}".Substring(0, 32);
        }

        private async Task CleanOldBackupsAsync(string backupDirectory, int maxBackups)
        {
            try
            {
                var files = Directory.GetFiles(backupDirectory, "loco_backup_*.json.gz")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .Skip(maxBackups)
                    .ToList();

                foreach (var file in files)
                {
                    file.Delete();
                    _logger?.LogDebug("Deleted old backup: {FileName}", file.Name);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to clean old backups");
            }
        }
    }

    // Supporting classes
    public enum ExportFormat
    {
        Json,
        Xml,
        Binary
    }

    public class ExportPackage
    {
        public string Version { get; set; }
        public DateTime ExportDate { get; set; }
        public List<AutomationDsl.Rule> Rules { get; set; }
        public ExportMetadata Metadata { get; set; }
    }

    public class ExportMetadata
    {
        public int TotalRules { get; set; }
        public string MachineName { get; set; }
        public string UserName { get; set; }
        public string Platform { get; set; }
        public Dictionary<string, string> CustomData { get; set; }
    }

    public class ExportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string FilePath { get; set; }
        public int ExportedCount { get; set; }
        public long FileSize { get; set; }
        public Exception Exception { get; set; }
    }

    public class ImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<AutomationDsl.Rule> ImportedRules { get; set; }
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> SkippedRules { get; set; }
        public List<string> Errors { get; set; }
        public ExportMetadata Metadata { get; set; }
        public Exception Exception { get; set; }
    }

    public class ImportOptions
    {
        public bool ValidateBeforeImport { get; set; } = true;
        public bool SkipInvalidRules { get; set; } = false;
        public bool PreventDuplicates { get; set; } = true;
        public bool GenerateNewIds { get; set; } = false;
        public bool IgnoreVersionMismatch { get; set; } = false;
        public bool MergeWithExisting { get; set; } = false;
    }

    public class BackupResult
    {
        public bool Success { get; set; }
        public string BackupPath { get; set; }
        public string Message { get; set; }
    }
}
