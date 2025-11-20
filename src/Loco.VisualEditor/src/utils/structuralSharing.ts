/**
 * Structural Sharing Utilities
 *
 * Implements efficient structural sharing for history management.
 * Instead of deep cloning entire arrays, only clones when necessary.
 *
 * This is based on modern immutable patterns used by libraries like Immer.
 * Reference: https://immerjs.github.io/immer/structural-sharing
 */

import { Node, Edge } from 'reactflow';


/**
 * Optimized history snapshot that only clones when array structure changed
 * Reuses node/edge objects if their content hasn't changed
 *
 * @param nodes - Nodes array to snapshot
 * @param edges - Edges array to snapshot
 * @param previousNodes - Previous snapshot for comparison (optional)
 * @param previousEdges - Previous snapshot for comparison (optional)
 * @returns Optimized snapshot with structural sharing
 */
export function createOptimizedHistorySnapshot(
  nodes: Node[],
  edges: Edge[],
  previousNodes?: Node[],
  previousEdges?: Edge[]
): { nodes: Node[]; edges: Edge[] } {
  // If no previous state, we must clone
  if (!previousNodes || !previousEdges) {
    return {
      nodes: nodes.slice(), // Shallow copy of array
      edges: edges.slice(), // Shallow copy of array
    };
  }

  // Check if arrays have same length and content
  const nodesCopyNeeded = nodes.length !== previousNodes.length || hasNodeChanges(nodes, previousNodes);
  const edgesCopyNeeded = edges.length !== previousEdges.length || hasEdgeChanges(edges, previousEdges);

  return {
    nodes: nodesCopyNeeded ? nodes.slice() : previousNodes,
    edges: edgesCopyNeeded ? edges.slice() : previousEdges,
  };
}

/**
 * Checks if nodes have changed (quick comparison)
 * Focuses on ID-based checks first before deep comparison
 *
 * @param newNodes - New nodes array
 * @param oldNodes - Previous nodes array
 * @returns true if nodes have changed
 */
function hasNodeChanges(newNodes: Node[], oldNodes: Node[]): boolean {
  if (newNodes.length !== oldNodes.length) return true;

  for (let i = 0; i < newNodes.length; i++) {
    const newNode = newNodes[i];
    const oldNode = oldNodes[i];

    // Quick ID check
    if (newNode.id !== oldNode.id) return true;

    // Check if same reference (no change)
    if (newNode === oldNode) continue;

    // Quick position check (most common change in canvas)
    if (
      newNode.position?.x !== oldNode.position?.x ||
      newNode.position?.y !== oldNode.position?.y
    ) {
      return true;
    }

    // Check selected state
    if (newNode.selected !== oldNode.selected) return true;

    // Check if data object reference is different
    if (newNode.data !== oldNode.data) {
      // Only deep check if reference differs
      if (JSON.stringify(newNode.data) !== JSON.stringify(oldNode.data)) {
        return true;
      }
    }
  }

  return false;
}

/**
 * Checks if edges have changed (quick comparison)
 *
 * @param newEdges - New edges array
 * @param oldEdges - Previous edges array
 * @returns true if edges have changed
 */
function hasEdgeChanges(newEdges: Edge[], oldEdges: Edge[]): boolean {
  if (newEdges.length !== oldEdges.length) return true;

  for (let i = 0; i < newEdges.length; i++) {
    const newEdge = newEdges[i];
    const oldEdge = oldEdges[i];

    // Quick ID check
    if (newEdge.id !== oldEdge.id) return true;

    // Check if same reference (no change)
    if (newEdge === oldEdge) continue;

    // Check core edge properties
    if (
      newEdge.source !== oldEdge.source ||
      newEdge.target !== oldEdge.target ||
      newEdge.selected !== oldEdge.selected
    ) {
      return true;
    }

    // Check data if reference differs
    if (newEdge.data !== oldEdge.data) {
      if (JSON.stringify(newEdge.data) !== JSON.stringify(oldEdge.data)) {
        return true;
      }
    }
  }

  return false;
}

