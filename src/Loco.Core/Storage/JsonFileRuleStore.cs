using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Interfaces;
using Loco.Core.Models;
using Loco.Core.Utilities;

namespace Loco.Core.Storage
{
    /// <summary>
    /// JSONファイルベースのルールストア
    /// JSON file-based rule store implementation
    /// </summary>
    public class JsonFileRuleStore : IRuleStore
    {
        private readonly string _filePath;
        private readonly ILogger? _logger;
        private readonly object _lock = new object();

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
                SaveRules(new List<SimpleRule>());
                _logger?.LogInformation("Initialized new rule store: {FilePath}", _filePath);
            }
        }

        /// <summary>
        /// すべてのルールを取得
        /// </summary>
        public async Task<List<SimpleRule>> GetRulesAsync()
        {
            return await Task.Run(() => LoadRules());
        }

        /// <summary>
        /// 特定のルールを取得
        /// </summary>
        public async Task<SimpleRule?> GetRuleAsync(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                return null;
            }

            var rules = await GetRulesAsync();
            return rules.FirstOrDefault(r => r.Id == ruleId);
        }

        /// <summary>
        /// ルールを追加または更新
        /// </summary>
        public async Task UpsertRuleAsync(SimpleRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    var rules = LoadRules();
                    var existingIndex = rules.FindIndex(r => r.Id == rule.Id);

                    if (existingIndex >= 0)
                    {
                        // 更新
                        rules[existingIndex] = rule;
                        _logger?.LogInformation("Updated rule: {RuleId} - {RuleName}", rule.Id, rule.Name);
                    }
                    else
                    {
                        // 追加
                        rules.Add(rule);
                        _logger?.LogInformation("Added new rule: {RuleId} - {RuleName}", rule.Id, rule.Name);
                    }

                    SaveRules(rules);
                }
            });
        }

        /// <summary>
        /// ルールを削除
        /// </summary>
        public async Task DeleteRuleAsync(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                return;
            }

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    var rules = LoadRules();
                    var removed = rules.RemoveAll(r => r.Id == ruleId);

                    if (removed > 0)
                    {
                        SaveRules(rules);
                        _logger?.LogInformation("Deleted rule: {RuleId}", ruleId);
                    }
                }
            });
        }

        /// <summary>
        /// すべてのルールを削除
        /// </summary>
        public async Task ClearRulesAsync()
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    SaveRules(new List<SimpleRule>());
                    _logger?.LogInformation("Cleared all rules from store");
                }
            });
        }

        /// <summary>
        /// ルールが存在するかチェック
        /// </summary>
        public async Task<bool> RuleExistsAsync(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                return false;
            }

            var rules = await GetRulesAsync();
            return rules.Any(r => r.Id == ruleId);
        }

        /// <summary>
        /// 有効なルールのみを取得
        /// </summary>
        public async Task<List<SimpleRule>> GetEnabledRulesAsync()
        {
            var rules = await GetRulesAsync();
            return rules.Where(r => r.IsEnabled).ToList();
        }

        /// <summary>
        /// ルールをファイルから読み込み
        /// </summary>
        private List<SimpleRule> LoadRules()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_filePath))
                    {
                        return new List<SimpleRule>();
                    }

                    var json = File.ReadAllText(_filePath);
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
                    // 破損したファイルの場合は空のリストを返す
                    return new List<SimpleRule>();
                }
            }
        }

        /// <summary>
        /// ルールをファイルに保存
        /// </summary>
        private void SaveRules(List<SimpleRule> rules)
        {
            lock (_lock)
            {
                try
                {
                    var json = JsonSerializer.Serialize(rules, JsonDefaults.Indented);
                    File.WriteAllText(_filePath, json);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to save rules to {FilePath}", _filePath);
                    throw;
                }
            }
        }
    }
}
