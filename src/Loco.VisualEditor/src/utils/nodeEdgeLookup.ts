/**
 * Node and Edge Lookup Utilities
 *
 * Provides optimized O(1) lookups for nodes and edges using Map-based indexing.
 * Replaces linear O(n) Array.find() operations for better performance.
 */

import { Node, Edge } from 'reactflow';

/**
 * Creates a Map-based index of nodes for O(1) lookups by ID
 * @param nodes - Array of nodes to index
 * @returns Map where keys are node IDs and values are nodes
 */
export function createNodeIndex(nodes: Node[]): Map<string, Node> {
  const index = new Map<string, Node>();
  for (const node of nodes) {
    index.set(node.id, node);
  }
  return index;
}

/**
 * Creates a Map-based index of edges for O(1) lookups by ID
 * @param edges - Array of edges to index
 * @returns Map where keys are edge IDs and values are edges
 */
export function createEdgeIndex(edges: Edge[]): Map<string, Edge> {
  const index = new Map<string, Edge>();
  for (const edge of edges) {
    index.set(edge.id, edge);
  }
  return index;
}

/**
 * Finds a node by ID using Map-based lookup (O(1))
 * @param nodes - Array or Map of nodes
 * @param nodeId - ID to search for
 * @returns Node if found, undefined otherwise
 */
export function findNodeById(nodes: Node[] | Map<string, Node>, nodeId: string): Node | undefined {
  if (nodes instanceof Map) {
    return nodes.get(nodeId);
  }

  // Fallback to array find if Map not provided
  return nodes.find((n) => n.id === nodeId);
}

/**
 * Finds an edge by ID using Map-based lookup (O(1))
 * @param edges - Array or Map of edges
 * @param edgeId - ID to search for
 * @returns Edge if found, undefined otherwise
 */
export function findEdgeById(edges: Edge[] | Map<string, Edge>, edgeId: string): Edge | undefined {
  if (edges instanceof Map) {
    return edges.get(edgeId);
  }

  // Fallback to array find if Map not provided
  return edges.find((e) => e.id === edgeId);
}

/**
 * Gets all edges connected to a node (either as source or target)
 * @param edges - Array of edges or edge index Map
 * @param nodeId - ID of the node
 * @returns Array of connected edges
 */
export function getConnectedEdges(edges: Edge[] | Map<string, Edge>, nodeId: string): Edge[] {
  const edgeArray = edges instanceof Map ? Array.from(edges.values()) : edges;
  return edgeArray.filter((e) => e.source === nodeId || e.target === nodeId);
}

/**
 * Gets all outgoing edges from a node
 * @param edges - Array of edges or edge index Map
 * @param nodeId - ID of the source node
 * @returns Array of outgoing edges
 */
export function getOutgoingEdges(edges: Edge[] | Map<string, Edge>, nodeId: string): Edge[] {
  const edgeArray = edges instanceof Map ? Array.from(edges.values()) : edges;
  return edgeArray.filter((e) => e.source === nodeId);
}

/**
 * Gets all incoming edges to a node
 * @param edges - Array of edges or edge index Map
 * @param nodeId - ID of the target node
 * @returns Array of incoming edges
 */
export function getIncomingEdges(edges: Edge[] | Map<string, Edge>, nodeId: string): Edge[] {
  const edgeArray = edges instanceof Map ? Array.from(edges.values()) : edges;
  return edgeArray.filter((e) => e.target === nodeId);
}

/**
 * Gets all nodes that are directly connected to a given node
 * @param nodes - Array of nodes or node index Map
 * @param edges - Array of edges or edge index Map
 * @param nodeId - ID of the center node
 * @param direction - 'in' for incoming, 'out' for outgoing, 'both' for both
 * @returns Array of connected nodes
 */
export function getConnectedNodes(
  nodes: Node[] | Map<string, Node>,
  edges: Edge[] | Map<string, Edge>,
  nodeId: string,
  direction: 'in' | 'out' | 'both' = 'both'
): Node[] {
  const nodeMap = nodes instanceof Map ? nodes : createNodeIndex(nodes);
  const edgeArray = edges instanceof Map ? Array.from(edges.values()) : edges;

  const connectedNodeIds = new Set<string>();

  for (const edge of edgeArray) {
    if (direction === 'out' && edge.source === nodeId) {
      connectedNodeIds.add(edge.target);
    } else if (direction === 'in' && edge.target === nodeId) {
      connectedNodeIds.add(edge.source);
    } else if (direction === 'both') {
      if (edge.source === nodeId) {
        connectedNodeIds.add(edge.target);
      } else if (edge.target === nodeId) {
        connectedNodeIds.add(edge.source);
      }
    }
  }

  const result: Node[] = [];
  for (const id of connectedNodeIds) {
    const node = nodeMap.get(id);
    if (node) {
      result.push(node);
    }
  }

  return result;
}
