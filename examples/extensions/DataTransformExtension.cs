using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Extensibility;
using Loco.Core.Extensibility.Hooks;

namespace Loco.Examples.Extensions
{
    /// <summary>
    /// Advanced extension demonstrating file transformation and logging hooks
    /// </summary>
    [Extension("data-transform", "Data Transform Extension")]
    public class DataTransformExtension : ExtensionBase
    {
        public override string Id => "data-transform";
        public override string Name => "Data Transform Extension";
        public override string Version => "2.0.0";
        public override string Description => "Automatically transforms JSON data and adds logging enhancements";
        public override string Author => "Loco Community";
        public override string License => "MIT";
        public override string Url => "https://github.com/loco/data-transform-extension";
        public override IEnumerable<string> Tags => new[] { "data", "transformation", "json", "logging" };
        public override string MinimumLocoVersion => "1.0.0";

        private readonly Dictionary<string, int> _transformStats = new();

        public override async Task InitializeAsync(IExtensionContext context, CancellationToken cancellationToken = default)
        {
            await base.InitializeAsync(context, cancellationToken);

            Console.WriteLine($"🔧 {Name} v{Version} initializing...");

            // Register file operation hook for JSON transformation
            context.RegisterHook(new JsonTransformHook(_transformStats));

            // Register logging hook for enhanced formatting
            context.RegisterHook(new EnhancedLogHook());

            // Subscribe to configuration changes
            context.SubscribeToEvent("config.changed", async (data) =>
            {
                Console.WriteLine("📝 Configuration changed, reloading transform rules...");
                await ReloadTransformRulesAsync();
            });

            // Load transform rules from config
            await ReloadTransformRulesAsync();

            Console.WriteLine("✅ Data Transform Extension ready!");
        }

        public override async Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"💾 Saving transform statistics...");

            // Save statistics before shutdown
            var statsFile = Path.Combine(Context!.DataDirectory, "transform-stats.json");
            var json = JsonSerializer.Serialize(_transformStats, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(statsFile, json, cancellationToken);

            Console.WriteLine($"✅ Saved statistics to {statsFile}");

            await base.ShutdownAsync(cancellationToken);
        }

        public override async Task<ExtensionHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var totalTransforms = _transformStats.Values.Sum();

            return new ExtensionHealth
            {
                Status = HealthStatus.Healthy,
                Message = $"Processed {totalTransforms} transformations",
                Data = new Dictionary<string, object>
                {
                    ["totalTransformations"] = totalTransforms,
                    ["transformsByType"] = new Dictionary<string, int>(_transformStats)
                }
            };
        }

        private async Task ReloadTransformRulesAsync()
        {
            // In a real extension, load rules from configuration
            Console.WriteLine("✅ Transform rules reloaded");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Hook that transforms JSON files during read/write operations
        /// </summary>
        private class JsonTransformHook : IFileOperationHook
        {
            private readonly Dictionary<string, int> _stats;

            public JsonTransformHook(Dictionary<string, int> stats)
            {
                _stats = stats;
            }

            public async Task OnBeforeReadAsync(string filePath, CancellationToken cancellationToken)
            {
                // Log file access
                if (filePath.EndsWith(".json"))
                {
                    Console.WriteLine($"📖 Reading JSON file: {Path.GetFileName(filePath)}");
                }
                await Task.CompletedTask;
            }

            public async Task<string> OnAfterReadAsync(string filePath, string content, CancellationToken cancellationToken)
            {
                // Transform JSON after reading
                if (filePath.EndsWith(".json"))
                {
                    try
                    {
                        var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                        if (json != null)
                        {
                            // Add metadata
                            json["_readAt"] = DateTime.UtcNow.ToString("o");
                            json["_transformedBy"] = "data-transform-extension";

                            var transformed = JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });

                            // Track statistics
                            IncrementStat("reads");

                            Console.WriteLine($"✨ Transformed JSON on read: {Path.GetFileName(filePath)}");

                            return transformed;
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"⚠️ Could not transform JSON: {ex.Message}");
                    }
                }

                return content;
            }

            public async Task<string> OnBeforeWriteAsync(string filePath, string content, CancellationToken cancellationToken)
            {
                // Transform JSON before writing
                if (filePath.EndsWith(".json"))
                {
                    try
                    {
                        var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                        if (json != null)
                        {
                            // Add metadata
                            json["_writtenAt"] = DateTime.UtcNow.ToString("o");
                            json["_version"] = "2.0";

                            // Format with consistent indentation
                            var transformed = JsonSerializer.Serialize(json, new JsonSerializerOptions
                            {
                                WriteIndented = true,
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            });

                            // Track statistics
                            IncrementStat("writes");

                            Console.WriteLine($"✨ Transformed JSON on write: {Path.GetFileName(filePath)}");

                            return transformed;
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"⚠️ Could not transform JSON: {ex.Message}");
                    }
                }

                return content;
            }

            public async Task OnAfterWriteAsync(string filePath, CancellationToken cancellationToken)
            {
                if (filePath.EndsWith(".json"))
                {
                    Console.WriteLine($"💾 JSON file written: {Path.GetFileName(filePath)}");
                }
                await Task.CompletedTask;
            }

            private void IncrementStat(string key)
            {
                lock (_stats)
                {
                    if (!_stats.ContainsKey(key))
                    {
                        _stats[key] = 0;
                    }
                    _stats[key]++;
                }
            }
        }

        /// <summary>
        /// Hook that enhances log messages with emoji and formatting
        /// </summary>
        private class EnhancedLogHook : ILogHook
        {
            private static readonly Dictionary<LogLevel, string> LevelEmojis = new()
            {
                [LogLevel.Trace] = "🔍",
                [LogLevel.Debug] = "🐛",
                [LogLevel.Information] = "ℹ️",
                [LogLevel.Warning] = "⚠️",
                [LogLevel.Error] = "❌",
                [LogLevel.Critical] = "🔥"
            };

            public async Task<LogHookResult> OnLogAsync(
                LogLevel level,
                string message,
                Exception? exception,
                CancellationToken cancellationToken)
            {
                // Add emoji prefix based on log level
                var emoji = LevelEmojis.GetValueOrDefault(level, "📝");

                // Add timestamp
                var timestamp = DateTime.Now.ToString("HH:mm:ss");

                // Format enhanced message
                var enhancedMessage = $"{emoji} [{timestamp}] {message}";

                // Add exception details if present
                if (exception != null)
                {
                    enhancedMessage += $"\n   💥 {exception.GetType().Name}: {exception.Message}";
                }

                return new LogHookResult
                {
                    ShouldLog = true,
                    ModifiedMessage = enhancedMessage
                };
            }
        }
    }
}
