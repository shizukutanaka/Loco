using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loco.Core.Models;

namespace Loco.Core.Interfaces
{
    /// <summary>
    /// ルールの永続化ストアのインターフェース
    /// Rule persistence store interface
    /// </summary>
    public interface IRuleStore
    {
        /// <summary>
        /// すべてのルールを取得
        /// Get all rules
        /// </summary>
        Task<List<SimpleRule>> GetRulesAsync();

        /// <summary>
        /// 特定のルールを取得
        /// Get a specific rule by ID
        /// </summary>
        Task<SimpleRule?> GetRuleAsync(string ruleId);

        /// <summary>
        /// ルールを追加または更新
        /// Add or update a rule
        /// </summary>
        Task UpsertRuleAsync(SimpleRule rule);

        /// <summary>
        /// 複数のルールをバッチで追加・更新（パフォーマンス最適化）
        /// Batch upsert multiple rules for better performance
        /// </summary>
        Task UpsertRulesAsync(IEnumerable<SimpleRule> rules);

        /// <summary>
        /// ルールを削除
        /// Delete a rule
        /// </summary>
        Task DeleteRuleAsync(string ruleId);

        /// <summary>
        /// すべてのルールを削除
        /// Delete all rules
        /// </summary>
        Task ClearRulesAsync();

        /// <summary>
        /// ルールが存在するかチェック
        /// Check if a rule exists
        /// </summary>
        Task<bool> RuleExistsAsync(string ruleId);

        /// <summary>
        /// 有効なルールのみを取得
        /// Get only enabled rules
        /// </summary>
        Task<List<SimpleRule>> GetEnabledRulesAsync();
    }
}
