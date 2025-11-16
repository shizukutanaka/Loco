/**
 * Defer History Snapshot Utility
 *
 * Provides a helper function to defer workflow history snapshots.
 * This pattern is used to ensure history is captured after state changes have been applied.
 */

/**
 * Defers a history snapshot operation to the next event loop tick
 * This ensures state changes are fully applied before capturing history
 *
 * @param callback - Function that captures history snapshot
 */
export function deferHistorySnapshot(callback: () => void): void {
  // Use setTimeout(..., 0) to defer to next event loop tick
  // This ensures all state updates are processed before capturing history
  setTimeout(callback, 0);
}

/**
 * Create a deferred history snapshot function
 * Useful for creating consistent history capture across multiple scenarios
 *
 * @param getState - Function to get current state
 * @returns Function that defers history snapshot
 */
export function createDeferredHistorySnapshot(
  getState: () => any
): () => void {
  return () => {
    deferHistorySnapshot(() => getState().pushToHistory());
  };
}
