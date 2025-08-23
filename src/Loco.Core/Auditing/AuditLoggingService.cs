using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loco.Core.Auditing
{
    public interface IAuditLoggingService
    {
        Task LogAsync(AuditEntry entry);
        Task LogAsync(string action, object data = null, AuditSeverity severity = AuditSeverity.Information);
        Task<IEnumerable<AuditEntry>> GetAuditLogsAsync(AuditLogQuery query);
        Task<AuditStatistics> GetStatisticsAsync(DateTime from, DateTime to);
        Task<bool> ArchiveOldLogsAsync(DateTime before);
        Task<byte[]> ExportLogsAsync(ExportFormat format, AuditLogQuery query);
    }

    public class AuditLoggingService : IAuditLoggingService, IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditLoggingService> _logger;
        private readonly AuditConfiguration _configuration;
        private readonly Channel<AuditEntry> _auditChannel;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task _processingTask;

        public AuditLoggingService(
            IServiceProvider serviceProvider,
            IHttpContextAccessor httpContextAccessor,
            IOptions<AuditConfiguration> configuration,
            ILogger<AuditLoggingService> logger)
        {
            _serviceProvider = serviceProvider;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration.Value;
            _logger = logger;
            _auditChannel = Channel.CreateUnbounded<AuditEntry>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _processingTask = ProcessAuditEntriesAsync(_cancellationTokenSource.Token);
            _logger.LogInformation("Audit logging service started");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Cancel();
            _auditChannel.Writer.Complete();

            if (_processingTask != null)
            {
                await _processingTask;
            }

            _logger.LogInformation("Audit logging service stopped");
        }

        public async Task LogAsync(AuditEntry entry)
        {
            EnrichAuditEntry(entry);
            
            if (!ShouldLog(entry))
            {
                return;
            }

            // Sanitize sensitive data
            SanitizeSensitiveData(entry);

            // Write to channel for async processing
            await _auditChannel.Writer.WriteAsync(entry);
        }

        public async Task LogAsync(string action, object data = null, AuditSeverity severity = AuditSeverity.Information)
        {
            var entry = new AuditEntry
            {
                Action = action,
                Data = data != null ? JsonSerializer.Serialize(data) : null,
                Severity = severity,
                Timestamp = DateTime.UtcNow
            };

            await LogAsync(entry);
        }

        public async Task<IEnumerable<AuditEntry>> GetAuditLogsAsync(AuditLogQuery query)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

            var queryable = dbContext.AuditEntries.AsQueryable();

            // Apply filters
            if (query.From.HasValue)
                queryable = queryable.Where(e => e.Timestamp >= query.From.Value);

            if (query.To.HasValue)
                queryable = queryable.Where(e => e.Timestamp <= query.To.Value);

            if (!string.IsNullOrEmpty(query.UserId))
                queryable = queryable.Where(e => e.UserId == query.UserId);

            if (!string.IsNullOrEmpty(query.Action))
                queryable = queryable.Where(e => e.Action.Contains(query.Action));

            if (query.Severity.HasValue)
                queryable = queryable.Where(e => e.Severity == query.Severity.Value);

            if (!string.IsNullOrEmpty(query.EntityType))
                queryable = queryable.Where(e => e.EntityType == query.EntityType);

            if (!string.IsNullOrEmpty(query.EntityId))
                queryable = queryable.Where(e => e.EntityId == query.EntityId);

            // Apply ordering
            queryable = query.OrderBy switch
            {
                "timestamp_desc" => queryable.OrderByDescending(e => e.Timestamp),
                "timestamp_asc" => queryable.OrderBy(e => e.Timestamp),
                "severity_desc" => queryable.OrderByDescending(e => e.Severity),
                _ => queryable.OrderByDescending(e => e.Timestamp)
            };

            // Apply pagination
            if (query.Skip > 0)
                queryable = queryable.Skip(query.Skip);

            if (query.Take > 0)
                queryable = queryable.Take(query.Take);

            return await queryable.ToListAsync();
        }

        public async Task<AuditStatistics> GetStatisticsAsync(DateTime from, DateTime to)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

            var entries = await dbContext.AuditEntries
                .Where(e => e.Timestamp >= from && e.Timestamp <= to)
                .ToListAsync();

            return new AuditStatistics
            {
                TotalEntries = entries.Count,
                EntriesBySeverity = entries.GroupBy(e => e.Severity)
                    .ToDictionary(g => g.Key, g => g.Count()),
                EntriesByAction = entries.GroupBy(e => e.Action)
                    .ToDictionary(g => g.Key, g => g.Count()),
                EntriesByUser = entries.GroupBy(e => e.UserId ?? "anonymous")
                    .ToDictionary(g => g.Key, g => g.Count()),
                EntriesByDay = entries.GroupBy(e => e.Timestamp.Date)
                    .ToDictionary(g => g.Key, g => g.Count()),
                MostActiveUsers = entries
                    .Where(e => !string.IsNullOrEmpty(e.UserId))
                    .GroupBy(e => e.UserId)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new UserActivity { UserId = g.Key, ActionCount = g.Count() })
                    .ToList()
            };
        }

        public async Task<bool> ArchiveOldLogsAsync(DateTime before)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

            var entriesToArchive = await dbContext.AuditEntries
                .Where(e => e.Timestamp < before)
                .ToListAsync();

            if (!entriesToArchive.Any())
            {
                return true;
            }

            // Archive to file or cold storage
            var archiveFile = $"audit_archive_{before:yyyyMMdd}_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
            var json = JsonSerializer.Serialize(entriesToArchive, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // Save to archive storage (simplified - would use blob storage in production)
            await System.IO.File.WriteAllTextAsync($"archives/{archiveFile}", json);

            // Remove from database
            dbContext.AuditEntries.RemoveRange(entriesToArchive);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Archived {Count} audit entries before {Date}", 
                entriesToArchive.Count, before);

            return true;
        }

        public async Task<byte[]> ExportLogsAsync(ExportFormat format, AuditLogQuery query)
        {
            var entries = await GetAuditLogsAsync(query);

            return format switch
            {
                ExportFormat.Json => ExportAsJson(entries),
                ExportFormat.Csv => ExportAsCsv(entries),
                ExportFormat.Xml => ExportAsXml(entries),
                _ => throw new NotSupportedException($"Export format {format} is not supported")
            };
        }

        private async Task ProcessAuditEntriesAsync(CancellationToken cancellationToken)
        {
            var batch = new List<AuditEntry>();
            var batchTimer = new System.Timers.Timer(_configuration.BatchIntervalMs);
            batchTimer.Elapsed += async (sender, e) => await FlushBatchAsync(batch);
            batchTimer.Start();

            try
            {
                await foreach (var entry in _auditChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    batch.Add(entry);

                    if (batch.Count >= _configuration.BatchSize)
                    {
                        await FlushBatchAsync(batch);
                        batch.Clear();
                    }
                }
            }
            finally
            {
                batchTimer.Stop();
                if (batch.Any())
                {
                    await FlushBatchAsync(batch);
                }
            }
        }

        private async Task FlushBatchAsync(List<AuditEntry> batch)
        {
            if (!batch.Any())
                return;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

                await dbContext.AuditEntries.AddRangeAsync(batch.ToList());
                await dbContext.SaveChangesAsync();

                _logger.LogDebug("Flushed {Count} audit entries to database", batch.Count);

                // Send critical entries to SIEM
                var criticalEntries = batch.Where(e => e.Severity == AuditSeverity.Critical);
                foreach (var entry in criticalEntries)
                {
                    await SendToSiemAsync(entry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing audit batch");
                
                // Fallback to file logging
                await FallbackToFileLoggingAsync(batch);
            }
        }

        private void EnrichAuditEntry(AuditEntry entry)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                entry.UserId = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                entry.UserName = httpContext.User?.Identity?.Name;
                entry.IpAddress = GetClientIpAddress(httpContext);
                entry.UserAgent = httpContext.Request.Headers["User-Agent"].ToString();
                entry.RequestId = httpContext.TraceIdentifier;
                entry.SessionId = httpContext.Session?.Id;
                entry.TenantId = httpContext.Items["TenantId"]?.ToString();
            }

            entry.Id = Guid.NewGuid();
            entry.Timestamp = DateTime.UtcNow;
            entry.MachineName = Environment.MachineName;
            entry.ProcessId = Process.GetCurrentProcess().Id;
            entry.ThreadId = Thread.CurrentThread.ManagedThreadId;
            entry.ApplicationName = _configuration.ApplicationName;
            entry.Environment = _configuration.Environment;
        }

        private bool ShouldLog(AuditEntry entry)
        {
            // Check if action is in exclude list
            if (_configuration.ExcludedActions?.Contains(entry.Action) == true)
                return false;

            // Check minimum severity level
            if (entry.Severity < _configuration.MinimumSeverity)
                return false;

            // Check if user is in exclude list
            if (_configuration.ExcludedUsers?.Contains(entry.UserId) == true)
                return false;

            return true;
        }

        private void SanitizeSensitiveData(AuditEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Data))
                return;

            try
            {
                var data = JsonDocument.Parse(entry.Data);
                var sanitized = SanitizeJsonElement(data.RootElement);
                entry.Data = JsonSerializer.Serialize(sanitized);
            }
            catch
            {
                // If not JSON, apply basic sanitization
                foreach (var field in _configuration.SensitiveFields)
                {
                    entry.Data = System.Text.RegularExpressions.Regex.Replace(
                        entry.Data,
                        $@"(""{field}""\s*:\s*""[^""]+"")",
                        $@"""{field}"":""***REDACTED***""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
            }
        }

        private object SanitizeJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        if (_configuration.SensitiveFields.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            obj[property.Name] = "***REDACTED***";
                        }
                        else
                        {
                            obj[property.Name] = SanitizeJsonElement(property.Value);
                        }
                    }
                    return obj;

                case JsonValueKind.Array:
                    return element.EnumerateArray().Select(SanitizeJsonElement).ToList();

                default:
                    return element.ToString();
            }
        }

        private string GetClientIpAddress(HttpContext context)
        {
            // Check for proxy headers
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',').First().Trim();
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }

        private async Task SendToSiemAsync(AuditEntry entry)
        {
            // Send to SIEM system (e.g., Splunk, ELK, etc.)
            _logger.LogInformation("Sending critical audit entry to SIEM: {EntryId}", entry.Id);
            await Task.CompletedTask;
        }

        private async Task FallbackToFileLoggingAsync(List<AuditEntry> entries)
        {
            var fileName = $"audit_fallback_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
            var json = JsonSerializer.Serialize(entries);
            await System.IO.File.WriteAllTextAsync($"logs/{fileName}", json);
            _logger.LogWarning("Audit entries written to fallback file: {FileName}", fileName);
        }

        private byte[] ExportAsJson(IEnumerable<AuditEntry> entries)
        {
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        private byte[] ExportAsCsv(IEnumerable<AuditEntry> entries)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Id,Timestamp,Action,UserId,UserName,Severity,EntityType,EntityId,IpAddress");

            foreach (var entry in entries)
            {
                csv.AppendLine($"{entry.Id},{entry.Timestamp:O},{entry.Action},{entry.UserId}," +
                    $"{entry.UserName},{entry.Severity},{entry.EntityType},{entry.EntityId},{entry.IpAddress}");
            }

            return System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        }

        private byte[] ExportAsXml(IEnumerable<AuditEntry> entries)
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(List<AuditEntry>));
            using var stream = new System.IO.MemoryStream();
            serializer.Serialize(stream, entries.ToList());
            return stream.ToArray();
        }
    }

    // Models
    public class AuditEntry
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Action { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public AuditSeverity Severity { get; set; }
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string Data { get; set; }
        public string RequestId { get; set; }
        public string SessionId { get; set; }
        public string TenantId { get; set; }
        public string MachineName { get; set; }
        public int ProcessId { get; set; }
        public int ThreadId { get; set; }
        public string ApplicationName { get; set; }
        public string Environment { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
    }

    public enum AuditSeverity
    {
        Debug,
        Information,
        Warning,
        Error,
        Critical
    }

    public class AuditLogQuery
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string UserId { get; set; }
        public string Action { get; set; }
        public AuditSeverity? Severity { get; set; }
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public string OrderBy { get; set; } = "timestamp_desc";
        public int Skip { get; set; }
        public int Take { get; set; } = 100;
    }

    public class AuditStatistics
    {
        public int TotalEntries { get; set; }
        public Dictionary<AuditSeverity, int> EntriesBySeverity { get; set; }
        public Dictionary<string, int> EntriesByAction { get; set; }
        public Dictionary<string, int> EntriesByUser { get; set; }
        public Dictionary<DateTime, int> EntriesByDay { get; set; }
        public List<UserActivity> MostActiveUsers { get; set; }
    }

    public class UserActivity
    {
        public string UserId { get; set; }
        public int ActionCount { get; set; }
    }

    public enum ExportFormat
    {
        Json,
        Csv,
        Xml
    }

    public class AuditConfiguration
    {
        public string ApplicationName { get; set; } = "Loco";
        public string Environment { get; set; } = "Production";
        public AuditSeverity MinimumSeverity { get; set; } = AuditSeverity.Information;
        public int BatchSize { get; set; } = 100;
        public int BatchIntervalMs { get; set; } = 5000;
        public List<string> SensitiveFields { get; set; } = new()
        {
            "password", "token", "secret", "apikey", "creditcard", "ssn", "email"
        };
        public List<string> ExcludedActions { get; set; } = new();
        public List<string> ExcludedUsers { get; set; } = new();
        public bool EnableCompression { get; set; } = true;
        public int RetentionDays { get; set; } = 90;
    }

    // DbContext
    public class AuditDbContext : DbContext
    {
        public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

        public DbSet<AuditEntry> AuditEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AuditEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => new { e.EntityType, e.EntityId });
                entity.Property(e => e.Data).HasColumnType("nvarchar(max)");
            });
        }
    }
}