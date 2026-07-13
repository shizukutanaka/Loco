using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Interfaces;
using Loco.Core.Utilities;
using Loco.Core.Workflows;

namespace Loco.Core.Storage
{
    /// <summary>
    /// JSONファイルベースのワークフローストア
    /// File-per-workflow JSON store, modeled on <see cref="JsonFileRuleStore"/>
    /// (SemaphoreSlim serialization + in-memory cache) with two upgrades:
    ///
    /// 1. One file per workflow ({storeDirectory}/{id}.json) instead of a single
    ///    array file, so saving one workflow never rewrites all of them.
    /// 2. Atomic writes: serialize to {id}.json.tmp then File.Move(overwrite),
    ///    so a crash mid-write can never corrupt an existing workflow file.
    ///
    /// Workflow ids are client-supplied (the Visual Editor generates them), so
    /// they are validated against a strict allowlist before ever being used in a
    /// file path - anything else is rejected, making path traversal impossible.
    /// </summary>
    public class JsonFileWorkflowStore : IWorkflowStore
    {
        // Ids become file names. Allow only safe characters, no dots (blocks "..").
        private static readonly Regex SafeId = new("^[A-Za-z0-9_-]{1,128}$", RegexOptions.Compiled);

        private readonly string _storeDirectory;
        private readonly ILogger? _logger;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        // key = workflow id, value = cached deserialized workflow
        private readonly ConcurrentDictionary<string, StoredWorkflow> _cache = new();
        private volatile bool _cacheLoaded;

        public JsonFileWorkflowStore(string storeDirectory, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(storeDirectory))
            {
                throw new ArgumentException("Store directory cannot be null or empty", nameof(storeDirectory));
            }

            _storeDirectory = storeDirectory;
            _logger = logger;

            if (!Directory.Exists(_storeDirectory))
            {
                Directory.CreateDirectory(_storeDirectory);
                _logger?.LogInformation("Created workflow store directory: {Directory}", _storeDirectory);
            }
        }

        /// <summary>Validates a client-supplied id; throws for anything path-unsafe.</summary>
        public static void EnsureValidId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !SafeId.IsMatch(id))
            {
                throw new ArgumentException(
                    "Workflow id must be 1-128 characters of A-Z, a-z, 0-9, '-' or '_'.", nameof(id));
            }
        }

        public async Task<(IReadOnlyList<StoredWorkflow> Items, int Total)> GetPageAsync(
            int page, int pageSize, CancellationToken cancellationToken = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 200) pageSize = 200;

            await EnsureCacheLoadedAsync(cancellationToken);

            // Most recently edited first; UpdatedAt is an ISO-8601 string so ordinal
            // string comparison sorts chronologically. Ties broken by id for stability.
            var ordered = _cache.Values
                .OrderByDescending(w => w.UpdatedAt, StringComparer.Ordinal)
                .ThenBy(w => w.Id, StringComparer.Ordinal)
                .ToList();

            var pageItems = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (pageItems, ordered.Count);
        }

        public async Task<StoredWorkflow?> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id) || !SafeId.IsMatch(id))
            {
                return null;
            }

            await EnsureCacheLoadedAsync(cancellationToken);
            return _cache.TryGetValue(id, out var workflow) ? workflow : null;
        }

        public async Task UpsertAsync(StoredWorkflow workflow, CancellationToken cancellationToken = default)
        {
            if (workflow == null) throw new ArgumentNullException(nameof(workflow));
            EnsureValidId(workflow.Id);

            await EnsureCacheLoadedAsync(cancellationToken);

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                await WriteWorkflowFileAsync(workflow, cancellationToken);
                _cache.AddOrUpdate(workflow.Id, workflow, (_, _) => workflow);
                _logger?.LogInformation("Saved workflow {WorkflowId} ({WorkflowName})", workflow.Id, workflow.Name);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id) || !SafeId.IsMatch(id))
            {
                return false;
            }

            await EnsureCacheLoadedAsync(cancellationToken);

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var removed = _cache.TryRemove(id, out _);
                var path = PathFor(id);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    removed = true;
                }

                if (removed)
                {
                    _logger?.LogInformation("Deleted workflow {WorkflowId}", id);
                }

                return removed;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id) || !SafeId.IsMatch(id))
            {
                return false;
            }

            await EnsureCacheLoadedAsync(cancellationToken);
            return _cache.ContainsKey(id);
        }

        private string PathFor(string id) => Path.Combine(_storeDirectory, id + ".json");

        private async Task WriteWorkflowFileAsync(StoredWorkflow workflow, CancellationToken cancellationToken)
        {
            var finalPath = PathFor(workflow.Id);
            var tmpPath = finalPath + ".tmp";

            var json = JsonSerializer.Serialize(workflow, JsonDefaults.Indented);
            await File.WriteAllTextAsync(tmpPath, json, cancellationToken);
            File.Move(tmpPath, finalPath, overwrite: true);
        }

        private async Task EnsureCacheLoadedAsync(CancellationToken cancellationToken)
        {
            if (_cacheLoaded)
            {
                return;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_cacheLoaded)
                {
                    return;
                }

                foreach (var file in Directory.EnumerateFiles(_storeDirectory, "*.json"))
                {
                    // Skip leftover temp files from interrupted writes.
                    if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        var json = await File.ReadAllTextAsync(file, cancellationToken);
                        if (string.IsNullOrWhiteSpace(json))
                        {
                            continue;
                        }

                        var workflow = JsonSerializer.Deserialize<StoredWorkflow>(json, JsonDefaults.Configuration);
                        if (workflow == null || string.IsNullOrWhiteSpace(workflow.Id))
                        {
                            _logger?.LogWarning("Skipping workflow file with no id: {File}", file);
                            continue;
                        }

                        _cache.TryAdd(workflow.Id, workflow);
                    }
                    catch (JsonException ex)
                    {
                        // One corrupt file must not take the whole store down; log and continue.
                        _logger?.LogError(ex, "Skipping corrupt workflow file: {File}", file);
                    }
                }

                _cacheLoaded = true;
                _logger?.LogDebug("Workflow cache loaded with {Count} workflows", _cache.Count);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
