/**
 * Auto Layout Utility
 *
 * Uses dagre graph layout library to automatically arrange nodes
 * in a hierarchical layout that minimizes edge crossings.
 */

import dagre from 'dagre';
import { Node, Edge } from 'reactflow';

// ============================================================================
// Types
// ============================================================================

export type LayoutDirection = 'TB' | 'BT' | 'LR' | 'RL';

interface LayoutOptions {
  direction?: LayoutDirection;
  nodeSpacing?: number;
  rankSpacing?: number;
  animate?: boolean;
}

// ============================================================================
// Layout Functions
// ============================================================================

/**
 * Automatically layout nodes using dagre algorithm
 *
 * @param nodes - Array of React Flow nodes
 * @param edges - Array of React Flow edges
 * @param options - Layout configuration options
 * @returns Updated nodes with new positions
 */
export function getAutoLayoutedNodes(
  nodes: Node[],
  edges: Edge[],
  options: LayoutOptions = {}
): Node[] {
  const {
    direction = 'TB',
    nodeSpacing = 50,
    rankSpacing = 100,
  } = options;

  // Create a new directed graph
  const dagreGraph = new dagre.graphlib.Graph();
  dagreGraph.setDefaultEdgeLabel(() => ({}));

  // Configure graph settings
  dagreGraph.setGraph({
    rankdir: direction,
    nodesep: nodeSpacing,
    ranksep: rankSpacing,
    marginx: 20,
    marginy: 20,
  });

  // Add nodes to the graph
  nodes.forEach((node) => {
    // Default node dimensions (can be customized per node type)
    const width = node.width || 180;
    const height = node.height || 80;

    dagreGraph.setNode(node.id, { width, height });
  });

  // Add edges to the graph
  edges.forEach((edge) => {
    dagreGraph.setEdge(edge.source, edge.target);
  });

  // Calculate the layout
  dagre.layout(dagreGraph);

  // Apply the calculated positions to nodes
  const layoutedNodes = nodes.map((node) => {
    const nodeWithPosition = dagreGraph.node(node.id);

    if (nodeWithPosition) {
      // dagre returns center position, we need top-left
      const newX = nodeWithPosition.x - (node.width || 180) / 2;
      const newY = nodeWithPosition.y - (node.height || 80) / 2;

      return {
        ...node,
        position: {
          x: newX,
          y: newY,
        },
      };
    }

    return node;
  });

  return layoutedNodes;
}

/**
 * Analyze workflow complexity and suggest best layout direction
 *
 * @param nodes - Array of React Flow nodes
 * @param edges - Array of React Flow edges
 * @returns Suggested layout direction
 */
export function suggestLayoutDirection(
  nodes: Node[],
  edges: Edge[]
): LayoutDirection {
  // Count trigger nodes (usually at the start)
  // const triggerNodes = nodes.filter(n => n.type === 'trigger').length;

  // Count depth of the graph
  const depths = calculateGraphDepth(nodes, edges);
  const maxDepth = Math.max(...depths.values());

  // Count width at each level
  const levelWidths = calculateLevelWidths(nodes, edges, depths);
  const maxWidth = Math.max(...levelWidths.values());

  // Decide based on graph shape
  if (maxDepth > maxWidth * 1.5) {
    // Deep graph - use horizontal layout
    return 'LR';
  } else if (maxWidth > maxDepth * 2) {
    // Wide graph - use vertical layout
    return 'TB';
  } else {
    // Default to top-bottom for most workflows
    return 'TB';
  }
}

/**
 * Calculate depth of each node in the graph
 */
function calculateGraphDepth(
  nodes: Node[],
  edges: Edge[]
): Map<string, number> {
  const depths = new Map<string, number>();
  const adjacencyList = new Map<string, string[]>();
  const inDegree = new Map<string, number>();

  // Initialize
  nodes.forEach(node => {
    adjacencyList.set(node.id, []);
    inDegree.set(node.id, 0);
    depths.set(node.id, 0);
  });

  // Build adjacency list and in-degree
  edges.forEach(edge => {
    const neighbors = adjacencyList.get(edge.source) || [];
    neighbors.push(edge.target);
    adjacencyList.set(edge.source, neighbors);

    const currentInDegree = inDegree.get(edge.target) || 0;
    inDegree.set(edge.target, currentInDegree + 1);
  });

  // Find starting nodes (no incoming edges)
  const queue: string[] = [];
  nodes.forEach(node => {
    if ((inDegree.get(node.id) || 0) === 0) {
      queue.push(node.id);
      depths.set(node.id, 0);
    }
  });

  // BFS to calculate depths
  while (queue.length > 0) {
    const currentId = queue.shift()!;
    const currentDepth = depths.get(currentId) || 0;
    const neighbors = adjacencyList.get(currentId) || [];

    neighbors.forEach(neighborId => {
      const neighborDepth = depths.get(neighborId) || 0;
      depths.set(neighborId, Math.max(neighborDepth, currentDepth + 1));

      const neighborInDegree = inDegree.get(neighborId) || 0;
      inDegree.set(neighborId, neighborInDegree - 1);

      if (inDegree.get(neighborId) === 0) {
        queue.push(neighborId);
      }
    });
  }

  return depths;
}

/**
 * Calculate width at each depth level
 */
function calculateLevelWidths(
  _nodes: Node[],
  _edges: Edge[],
  depths: Map<string, number>
): Map<number, number> {
  const levelWidths = new Map<number, number>();

  depths.forEach((depth, _nodeId) => {
    const currentWidth = levelWidths.get(depth) || 0;
    levelWidths.set(depth, currentWidth + 1);
  });

  return levelWidths;
}

/**
 * Apply spring physics-based adjustments to reduce overlaps
 * (Optional enhancement for better visual results)
 */
export function applyForceDirectedAdjustments(
  nodes: Node[],
  edges: Edge[],
  iterations: number = 50
): Node[] {
  const adjustedNodes = [...nodes];
  const nodeMap = new Map<string, Node>();

  adjustedNodes.forEach(node => {
    nodeMap.set(node.id, node);
  });

  // Simple force-directed adjustments
  for (let i = 0; i < iterations; i++) {
    const forces = new Map<string, { x: number; y: number }>();

    // Initialize forces
    adjustedNodes.forEach(node => {
      forces.set(node.id, { x: 0, y: 0 });
    });

    // Repulsion between all nodes
    for (let j = 0; j < adjustedNodes.length; j++) {
      for (let k = j + 1; k < adjustedNodes.length; k++) {
        const node1 = adjustedNodes[j];
        const node2 = adjustedNodes[k];

        const dx = node2.position.x - node1.position.x;
        const dy = node2.position.y - node1.position.y;
        const distance = Math.sqrt(dx * dx + dy * dy) || 1;

        const repulsion = 1000 / (distance * distance);
        const fx = (dx / distance) * repulsion;
        const fy = (dy / distance) * repulsion;

        const force1 = forces.get(node1.id)!;
        const force2 = forces.get(node2.id)!;

        force1.x -= fx;
        force1.y -= fy;
        force2.x += fx;
        force2.y += fy;
      }
    }

    // Attraction along edges
    edges.forEach(edge => {
      const source = nodeMap.get(edge.source);
      const target = nodeMap.get(edge.target);

      if (source && target) {
        const dx = target.position.x - source.position.x;
        const dy = target.position.y - source.position.y;
        const distance = Math.sqrt(dx * dx + dy * dy) || 1;

        const attraction = distance * 0.001;
        const fx = (dx / distance) * attraction;
        const fy = (dy / distance) * attraction;

        const sourceForce = forces.get(source.id)!;
        const targetForce = forces.get(target.id)!;

        sourceForce.x += fx;
        sourceForce.y += fy;
        targetForce.x -= fx;
        targetForce.y -= fy;
      }
    });

    // Apply forces with damping
    adjustedNodes.forEach(node => {
      const force = forces.get(node.id)!;
      node.position.x += force.x * 0.01;
      node.position.y += force.y * 0.01;
    });
  }

  return adjustedNodes;
}