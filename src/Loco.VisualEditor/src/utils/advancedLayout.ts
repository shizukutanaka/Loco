/**
 * Advanced Layout Algorithms
 *
 * Multiple layout algorithms for workflow visualization:
 * - Hierarchical (Dagre-based)
 * - Circular
 * - Force-directed
 * - Tree-based
 */

import { Node, Edge } from 'reactflow';
import { getAutoLayoutedNodes } from './autoLayout';

// ============================================================================
// Types
// ============================================================================

export type LayoutAlgorithm = 'hierarchical' | 'circular' | 'force-directed' | 'tree';

export interface LayoutOptions {
  nodeSpacing?: number;
  rankSpacing?: number;
  animate?: boolean;
  iterations?: number;
  strength?: number;
  distance?: number;
}

// ============================================================================
// Circular Layout
// ============================================================================

/**
 * Arrange nodes in a circular pattern
 */
export function getCircularLayout(
  nodes: Node[],
  _edges: Edge[],
  _options: LayoutOptions = {}
): Node[] {
  if (nodes.length === 0) return nodes;

  const radius = Math.max(300, nodes.length * 50);
  const angleSlice = (Math.PI * 2) / nodes.length;

  return nodes.map((node, index) => {
    const angle = index * angleSlice;
    const x = Math.cos(angle) * radius;
    const y = Math.sin(angle) * radius;

    return {
      ...node,
      position: { x, y },
    };
  });
}

// ============================================================================
// Tree Layout
// ============================================================================

/**
 * Arrange nodes in a tree/hierarchy structure
 */
export function getTreeLayout(
  nodes: Node[],
  edges: Edge[],
  options: LayoutOptions = {}
): Node[] {
  const nodeSpacing = options.nodeSpacing || 50;
  const rankSpacing = options.rankSpacing || 100;

  if (nodes.length === 0) return nodes;

  // Build adjacency list
  const adjacencyList = new Map<string, string[]>();
  const inDegree = new Map<string, number>();

  nodes.forEach((node) => {
    adjacencyList.set(node.id, []);
    inDegree.set(node.id, 0);
  });

  edges.forEach((edge) => {
    const neighbors = adjacencyList.get(edge.source) || [];
    neighbors.push(edge.target);
    adjacencyList.set(edge.source, neighbors);

    const currentInDegree = inDegree.get(edge.target) || 0;
    inDegree.set(edge.target, currentInDegree + 1);
  });

  // Find root nodes (no incoming edges)
  const roots: string[] = [];
  nodes.forEach((node) => {
    if ((inDegree.get(node.id) || 0) === 0) {
      roots.push(node.id);
    }
  });

  // If no roots found, use first node
  if (roots.length === 0) {
    roots.push(nodes[0].id);
  }

  // Position nodes using BFS
  const positions = new Map<string, { x: number; y: number }>();
  const visited = new Set<string>();
  const levelMap = new Map<number, string[]>();
  let maxLevel = 0;

  const queue: { id: string; level: number }[] = roots.map((id) => ({
    id,
    level: 0,
  }));

  while (queue.length > 0) {
    const { id, level } = queue.shift()!;

    if (visited.has(id)) continue;
    visited.add(id);

    // Track level
    if (!levelMap.has(level)) {
      levelMap.set(level, []);
    }
    levelMap.get(level)!.push(id);
    maxLevel = Math.max(maxLevel, level);

    // Add children to queue
    const children = adjacencyList.get(id) || [];
    children.forEach((childId) => {
      queue.push({ id: childId, level: level + 1 });
    });
  }

  // Calculate positions based on levels
  let y = 0;
  levelMap.forEach((nodeIds) => {
    const levelWidth = nodeIds.length * (180 + nodeSpacing);
    let x = -levelWidth / 2;

    nodeIds.forEach((nodeId) => {
      positions.set(nodeId, { x, y });
      x += 180 + nodeSpacing;
    });

    y += rankSpacing;
  });

  // Apply positions to nodes
  return nodes.map((node) => {
    const position = positions.get(node.id) || { x: 0, y: 0 };
    return {
      ...node,
      position,
    };
  });
}

// ============================================================================
// Force-Directed Layout
// ============================================================================

/**
 * Force-directed layout with spring physics
 */
export function getForceDirectedLayout(
  nodes: Node[],
  edges: Edge[],
  options: LayoutOptions = {}
): Node[] {
  const iterations = options.iterations || 50;
  const strength = options.strength || 1;
  const distance = options.distance || 200;

  if (nodes.length === 0) return nodes;

  // Initialize positions randomly
  const positions = new Map<string, { x: number; y: number }>();
  const velocities = new Map<string, { x: number; y: number }>();

  nodes.forEach((node) => {
    positions.set(node.id, {
      x: Math.random() * 400 - 200,
      y: Math.random() * 400 - 200,
    });
    velocities.set(node.id, { x: 0, y: 0 });
  });

  // Simulation iterations
  for (let iteration = 0; iteration < iterations; iteration++) {
    const forces = new Map<string, { x: number; y: number }>();

    // Initialize forces
    nodes.forEach((node) => {
      forces.set(node.id, { x: 0, y: 0 });
    });

    // Repulsive forces (all pairs)
    for (let i = 0; i < nodes.length; i++) {
      for (let j = i + 1; j < nodes.length; j++) {
        const node1 = nodes[i];
        const node2 = nodes[j];

        const pos1 = positions.get(node1.id)!;
        const pos2 = positions.get(node2.id)!;

        const dx = pos2.x - pos1.x;
        const dy = pos2.y - pos1.y;
        const dist = Math.sqrt(dx * dx + dy * dy) || 1;

        const repulsion = (100 * strength) / (dist * dist);
        const fx = (dx / dist) * repulsion;
        const fy = (dy / dist) * repulsion;

        const force1 = forces.get(node1.id)!;
        const force2 = forces.get(node2.id)!;

        force1.x -= fx;
        force1.y -= fy;
        force2.x += fx;
        force2.y += fy;
      }
    }

    // Attractive forces (edges only)
    edges.forEach((edge) => {
      const pos1 = positions.get(edge.source);
      const pos2 = positions.get(edge.target);

      if (!pos1 || !pos2) return;

      const dx = pos2.x - pos1.x;
      const dy = pos2.y - pos1.y;
      const dist = Math.sqrt(dx * dx + dy * dy) || 1;

      const attraction = ((dist - distance) * strength) / 50;
      const fx = (dx / dist) * attraction;
      const fy = (dy / dist) * attraction;

      const force1 = forces.get(edge.source)!;
      const force2 = forces.get(edge.target)!;

      force1.x += fx;
      force1.y += fy;
      force2.x -= fx;
      force2.y -= fy;
    });

    // Update velocities and positions
    const damping = 0.5;
    nodes.forEach((node) => {
      const force = forces.get(node.id)!;
      const velocity = velocities.get(node.id)!;
      const position = positions.get(node.id)!;

      velocity.x = (velocity.x + force.x) * damping;
      velocity.y = (velocity.y + force.y) * damping;

      position.x += velocity.x * 0.1;
      position.y += velocity.y * 0.1;

      // Keep nodes within bounds
      const maxBounds = 1000;
      position.x = Math.max(-maxBounds, Math.min(maxBounds, position.x));
      position.y = Math.max(-maxBounds, Math.min(maxBounds, position.y));
    });

    // Early exit if converged
    if (iteration % 10 === 0) {
      let maxVelocity = 0;
      velocities.forEach((vel) => {
        const speed = Math.sqrt(vel.x * vel.x + vel.y * vel.y);
        maxVelocity = Math.max(maxVelocity, speed);
      });

      if (maxVelocity < 0.1) break;
    }
  }

  // Apply positions to nodes
  return nodes.map((node) => {
    const position = positions.get(node.id) || { x: 0, y: 0 };
    return {
      ...node,
      position,
    };
  });
}

// ============================================================================
// Layout Selection & Application
// ============================================================================

/**
 * Apply selected layout algorithm
 */
export function applyLayout(
  nodes: Node[],
  edges: Edge[],
  algorithm: LayoutAlgorithm,
  options: LayoutOptions = {}
): Node[] {
  switch (algorithm) {
    case 'circular':
      return getCircularLayout(nodes, edges, options);

    case 'tree':
      return getTreeLayout(nodes, edges, options);

    case 'force-directed':
      return getForceDirectedLayout(nodes, edges, options);

    case 'hierarchical':
    default:
      // Use dagre-based hierarchical layout from autoLayout
      return getAutoLayoutedNodes(nodes, edges, options);
  }
}

/**
 * Get recommended layout based on graph analysis
 */
export function recommendLayout(
  nodes: Node[],
  edges: Edge[]
): LayoutAlgorithm {
  if (nodes.length === 0) return 'hierarchical';

  // Calculate graph metrics
  const edgeCount = edges.length;
  const nodeCount = nodes.length;
  const density = edgeCount / (nodeCount * (nodeCount - 1));

  // Analyze structure
  const adjacencyList = new Map<string, number>();
  nodes.forEach((node) => {
    adjacencyList.set(node.id, 0);
  });

  edges.forEach((edge) => {
    const current = adjacencyList.get(edge.source) || 0;
    adjacencyList.set(edge.source, current + 1);
  });

  const avgDegree =
    Array.from(adjacencyList.values()).reduce((a, b) => a + b, 0) / nodeCount;

  // Recommendation logic
  if (density > 0.5) {
    // Dense graph: force-directed
    return 'force-directed';
  } else if (avgDegree < 1.5) {
    // Sparse, tree-like: tree layout
    return 'tree';
  } else if (nodeCount > 30) {
    // Large graph: circular
    return 'circular';
  } else {
    // Default: hierarchical
    return 'hierarchical';
  }
}