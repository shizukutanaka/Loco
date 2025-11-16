/**
 * Detect Changes Utility
 *
 * Provides generic utilities for detecting changes between two snapshots of arrays.
 */

/**
 * Represents a snapshot of items with IDs
 */
export interface Snapshot<T extends { id: string }> {
  items: T[];
}

/**
 * Generic function to detect changed items between two snapshots
 * Compares items by ID and detects additions, removals, and modifications
 *
 * @param previousSnapshot - Previous snapshot
 * @param currentSnapshot - Current snapshot
 * @param getKey - Optional function to extract comparison key (defaults to id)
 * @returns Set of IDs of changed items
 */
export function detectChanged<T extends { id: string }>(
  previousSnapshot: Snapshot<T>,
  currentSnapshot: Snapshot<T>,
  getKey?: (item: T) => string
): Set<string> {
  const changed = new Set<string>();
  const keyFn = getKey || ((item: T) => item.id);

  // Create maps for quick lookup
  const previousMap = new Map(previousSnapshot.items.map((item) => [keyFn(item), item]));
  const currentMap = new Map(currentSnapshot.items.map((item) => [keyFn(item), item]));

  // Check for removed or modified items
  previousMap.forEach((prevItem, key) => {
    if (!currentMap.has(key)) {
      // Item was removed
      changed.add(key);
    } else {
      const currItem = currentMap.get(key);
      if (JSON.stringify(prevItem) !== JSON.stringify(currItem)) {
        // Item was modified
        changed.add(key);
      }
    }
  });

  // Check for added items
  currentMap.forEach((_currItem, key) => {
    if (!previousMap.has(key)) {
      changed.add(key);
    }
  });

  return changed;
}

/**
 * Detect if any items changed between two snapshots
 * More efficient than detectChanged when you only need a boolean result
 *
 * @param previousSnapshot - Previous snapshot
 * @param currentSnapshot - Current snapshot
 * @returns true if any changes detected
 */
export function hasChanges<T extends { id: string }>(
  previousSnapshot: Snapshot<T>,
  currentSnapshot: Snapshot<T>
): boolean {
  // Quick length check
  if (previousSnapshot.items.length !== currentSnapshot.items.length) {
    return true;
  }

  // Check if any item changed
  return detectChanged(previousSnapshot, currentSnapshot).size > 0;
}

/**
 * Group changed items by type of change
 *
 * @param previousSnapshot - Previous snapshot
 * @param currentSnapshot - Current snapshot
 * @returns Object with added, removed, and modified IDs
 */
export function groupChanges<T extends { id: string }>(
  previousSnapshot: Snapshot<T>,
  currentSnapshot: Snapshot<T>
): {
  added: Set<string>;
  removed: Set<string>;
  modified: Set<string>;
} {
  const added = new Set<string>();
  const removed = new Set<string>();
  const modified = new Set<string>();

  const previousMap = new Map(previousSnapshot.items.map((item) => [item.id, item]));
  const currentMap = new Map(currentSnapshot.items.map((item) => [item.id, item]));

  // Detect changes
  previousMap.forEach((prevItem, id) => {
    if (!currentMap.has(id)) {
      removed.add(id);
    } else {
      const currItem = currentMap.get(id);
      if (JSON.stringify(prevItem) !== JSON.stringify(currItem)) {
        modified.add(id);
      }
    }
  });

  currentMap.forEach((_currItem, id) => {
    if (!previousMap.has(id)) {
      added.add(id);
    }
  });

  return { added, removed, modified };
}
