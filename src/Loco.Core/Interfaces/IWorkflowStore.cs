using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Workflows;

namespace Loco.Core.Interfaces
{
    /// <summary>
    /// ワークフロー定義の永続化ストアのインターフェース
    /// Persistence store for visual workflow definitions.
    ///
    /// The stored shape (<see cref="StoredWorkflow"/>) is the Visual Editor's own
    /// JSON contract, persisted losslessly; see StoredWorkflow.cs for rationale.
    /// </summary>
    public interface IWorkflowStore
    {
        /// <summary>
        /// Get one page of workflows plus the total count across all pages.
        /// Pages are 1-based, matching the frontend's page/pageSize contract.
        /// Ordering is by UpdatedAt descending (most recently edited first).
        /// </summary>
        Task<(IReadOnlyList<StoredWorkflow> Items, int Total)> GetPageAsync(
            int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>Get a workflow by id, or null when it does not exist.</summary>
        Task<StoredWorkflow?> GetAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>Create or replace a workflow (keyed by <see cref="StoredWorkflow.Id"/>).</summary>
        Task UpsertAsync(StoredWorkflow workflow, CancellationToken cancellationToken = default);

        /// <summary>Delete a workflow. Returns false when the id did not exist (no error).</summary>
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>Check existence without loading the full document.</summary>
        Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);
    }
}
