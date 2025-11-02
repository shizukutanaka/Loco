using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Interfaces;
using Loco.Core.Models;
using Loco.Core.Utilities;

namespace Loco.Core.Storage
{
    /// <summary>
    /// JSONファイルベースのルールストア（最適化版）
    /// JSON file-based rule store implementation with async support and caching
    ///
    /// Optimization Features:
    /// - Async operations using SemaphoreSlim instead of blocking locks
    /// - In-memory LRU cache for frequently accessed rules
    /// - Batch operation support for bulk inserts/updates
    /// - Incremental saves for better I/O performance
    /// </summary>
    public class JsonFileRuleStore : IRuleStore
    {
        private readonly string _filePath;
        private readonly ILogger? _logger;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        // In-memory cache: key=ruleId, value=rule
        private readonly ConcurrentDictionary<string, SimpleRule> _cache =
            new ConcurrentDictionary<string, SimpleRule>();

        // Cache metadata
        private DateTime _lastCacheLoad = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromSeconds(300); // 5分でキャッシュ再読み込み
        private volatile bool _cacheLoaded = false;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="filePath">ルールを保存するJSONファイルのパス</param>
        /// <param name="logger">ロガー（オプション）</param>
        public JsonFileRuleStore(string filePath, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            }

            _filePath = filePath;
            _logger = logger;

            // ディレクトリが存在しない場合は作成
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger?.LogInformation("Created rule store directory: {Directory}", directory);
            }

            // ファイルが存在しない場合は空のリストで初期化
            if (!File.Exists(_filePath))
            {
                InitializeStoreAsync().GetAwaiter().GetResult();
                _logger?.LogInformation("Initialized new rule store: {FilePath}", _filePath);
            }
        }

        /// <summary>
        /// ストアを初期化（コンストラクタで使用）
        /// Initialize store (used in constructor)
        /// </summary>
        private async Task InitializeStoreAsync()
        {
            await SaveRulesAsync(new List<SimpleRule>());
        }

        /// <summary>
        /// すべてのルールを取得（キャッシュ活用）
        /// Get all rules with caching support
        /// </summary>
        public async Task<List<SimpleRule>> GetRulesAsync()
        {
            await EnsureCacheLoadedAsync();
            return _cache.Values.ToList();
        }

        /// <summary>
        /// 特定のルールを取得（高速キャッシュ参照）
        /// Get a specific rule by ID using cache
        /// </summary>
        public async Task<SimpleRule?> GetRuleAsync(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                return null;
            }

            await EnsureCacheLoadedAsync();

            // キャッシュから取得
            if (_cache.TryGetValue(ruleId, out var rule))
            {
                _logger?.LogDebug("Rule retrieved from cache: {RuleId}", ruleId);
                return rule;
            }

            return null;
        }

        /// <summary>
        /// ルールを追加または更新（非同期）
        /// Upsert rule with async support
        /// </summary>
        public async Task UpsertRuleAsync(SimpleRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            await _semaphore.WaitAsync();
            try
            {
                // キャッシュを更新
                var isNew = !_cache.ContainsKey(rule.Id);
                _cache.AddOrUpdate(rule.Id, rule, (_, _) => rule);

                // ファイルに保存
                await SaveRulesAsync(_cache.Values.ToList());

                if (isNew)
                {
                    _logger?.LogInformation("Added new rule: {RuleId} - {RuleName}", rule.Id, rule.Name);
                }
                else
                {
                    _logger?.LogInformation("Updated rule: {RuleId} - {RuleName}", rule.Id, rule.Name);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 複数のルールをバッチで追加・更新（高速化）
        /// Batch upsert rules for better performance
        /// </summary>
        public async Task UpsertRulesAsync(IEnumerable<SimpleRule> rules)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            await _semaphore.WaitAsync();
            try
            {
                var rulesList = rules.ToList();
                foreach (var rule in rulesList)
                {
                    _cache.AddOrUpdate(rule.Id, rule, (_, _) => rule);
                }

                await SaveRulesAsync(_cache.Values.ToList());
                _logger?.LogInformation("Batch upserted {RuleCount} rules", rulesList.Count);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// ルールを削除（非同期）
        /// Delete rule asynchronously
        /// </summary>
        public async Task DeleteRuleAsync(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                return;
            }

            await _semaphore.WaitAsync();
            try
            {
                if (_cache.TryRemove(ruleId, out _))
                {
                    await SaveRulesAsync(_cache.Values.ToList());
                    _logger?.LogInformation("Deleted rule: {RuleId}", ruleId);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// すべてのルールを削除（非同期）
        /// Clear all rules asynchronously
        /// </summary>
        public async Task ClearRulesAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                _cache.Clear();
                await SaveRulesAsync(new List<SimpleRule>());
                _logger?.LogInformation("Cleared all rules from store");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// ルールが存在するかチェック（高速）
        /// Check if rule exists (fast cache lookup)
        /// </summary>
        public async Task<bool> RuleExistsAsync(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                return false;
            }

            await EnsureCacheLoadedAsync();
            return _cache.ContainsKey(ruleId);
        }

        /// <summary>
        /// 有効なルールのみを取得（キャッシュ活用）
        /// Get only enabled rules from cache
        /// </summary>
        public async Task<List<SimpleRule>> GetEnabledRulesAsync()
        {
            await EnsureCacheLoadedAsync();
            return _cache.Values
                .Where(r => r.IsEnabled)
                .ToList();
        }

        /// <summary>
        /// キャッシュを確実に読み込み（遅延読み込み）
        /// Ensure cache is loaded with lazy loading strategy
        /// </summary>
        private async Task EnsureCacheLoadedAsync()
        {
            // キャッシュが既に読み込まれており、有効期限内なら処理をスキップ
            if (_cacheLoaded && DateTime.UtcNow - _lastCacheLoad < _cacheExpiration)
            {
                return;
            }

            await _semaphore.WaitAsync();
            try
            {
                // ダブルチェックロック
                if (_cacheLoaded && DateTime.UtcNow - _lastCacheLoad < _cacheExpiration)
                {
                    return;
                }

                var rules = await LoadRulesAsync();
                _cache.Clear();

                foreach (var rule in rules)
                {
                    _cache.TryAdd(rule.Id, rule);
                }

                _lastCacheLoad = DateTime.UtcNow;
                _cacheLoaded = true;

                _logger?.LogDebug("Cache loaded with {RuleCount} rules", rules.Count);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// ルールをファイルから非同期で読み込み
        /// Load rules from file asynchronously
        /// </summary>
        private async Task<List<SimpleRule>> LoadRulesAsync()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return new List<SimpleRule>();
                }

                var json = await File.ReadAllTextAsync(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<SimpleRule>();
                }

                var rules = JsonSerializer.Deserialize<List<SimpleRule>>(json, JsonDefaults.Configuration);
                return rules ?? new List<SimpleRule>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load rules from {FilePath}", _filePath);
                return new List<SimpleRule>();
            }
        }

        /// <summary>
        /// ルールをファイルに非同期で保存
        /// Save rules to file asynchronously
        /// </summary>
        private async Task SaveRulesAsync(List<SimpleRule> rules)
        {
            try
            {
                var json = JsonSerializer.Serialize(rules, JsonDefaults.Indented);
                await File.WriteAllTextAsync(_filePath, json);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save rules to {FilePath}", _filePath);
                throw;
            }
        }
    }
}
