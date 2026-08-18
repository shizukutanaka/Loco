/**
 * Comprehensive Workflow Validation Service
 *
 * Integrates multiple validation engines:
 * - Schema validation
 * - Connection analysis
 * - Data flow analysis
 * - Performance estimation
 * - Test coverage analysis
 */

import { Node, Edge } from 'reactflow';
import {
  analyzeDataFlow,
  ACTION_PARAMETER_REQUIREMENTS,
  ParameterDefinition,
} from './dataFlowAnalyzer';

// ============================================================================
// Types
// ============================================================================

export type ValidationCategory =
  | 'structure'
  | 'data_flow'
  | 'performance'
  | 'configuration'
  | 'best_practices'
  | 'security';

export interface ValidationIssue {
  id: string;
  category: ValidationCategory;
  severity: 'error' | 'warning' | 'info';
  title: string;
  description: string;
  nodeId?: string;
  edgeId?: string;
  suggestion?: string;
  affectedNodes?: string[];
}

export interface ValidationMetric {
  name: string;
  value: string | number;
  status: 'good' | 'warning' | 'critical';
  threshold?: { good: number; warning: number; critical: number };
  unit?: string;
}

export interface PerformanceEstimate {
  estimatedDuration: number; // milliseconds
  estimatedDataVolume: number; // bytes
  estimatedMemoryUsage: number; // bytes
  bottlenecks: Array<{ nodeId: string; reason: string; impact: 'high' | 'medium' | 'low' }>;
  parallelizationOpportunities: number;
}

export interface ValidationReport {
  overallScore: number; // 0-100
  isValid: boolean;
  issues: ValidationIssue[];
  metrics: ValidationMetric[];
  performance: PerformanceEstimate;
  coverage: {
    nodesCovered: number;
    totalNodes: number;
    edgesCovered: number;
    totalEdges: number;
    percentage: number;
  };
  recommendations: string[];
  testedAt: number;
}

// ============================================================================
// Configuration
// ============================================================================

const PERFORMANCE_THRESHOLDS = {
  duration: { good: 5000, warning: 15000, critical: 30000 }, // ms
  memory: { good: 50 * 1024 * 1024, warning: 200 * 1024 * 1024, critical: 500 * 1024 * 1024 }, // bytes
  dataVolume: { good: 10 * 1024 * 1024, warning: 50 * 1024 * 1024, critical: 100 * 1024 * 1024 }, // bytes
};

const NODE_TYPE_DURATIONS = {
  trigger: 0,
  condition: 5,
  transform: 50,
  action: 200,
  loop: 100,
  delay: 1000,
};

const NODE_TYPE_MEMORY = {
  trigger: 1024, // 1 KB
  condition: 5 * 1024, // 5 KB
  transform: 50 * 1024, // 50 KB
  action: 100 * 1024, // 100 KB
  loop: 200 * 1024, // 200 KB
  delay: 1024, // 1 KB
};

// ============================================================================
// Core Validation Functions
// ============================================================================

/**
 * Validate workflow structure and connections
 */
function validateStructure(nodes: Node[], edges: Edge[]): ValidationIssue[] {
  const issues: ValidationIssue[] = [];
  const nodeIds = new Set(nodes.map((n) => n.id));

  // Check for missing node references in edges
  edges.forEach((edge, index) => {
    if (!nodeIds.has(edge.source)) {
      issues.push({
        id: `edge-source-${index}`,
        category: 'structure',
        severity: 'error',
        title: 'Missing Source Node',
        description: `Edge references non-existent source node "${edge.source}"`,
        edgeId: edge.id,
      });
    }

    if (!nodeIds.has(edge.target)) {
      issues.push({
        id: `edge-target-${index}`,
        category: 'structure',
        severity: 'error',
        title: 'Missing Target Node',
        description: `Edge references non-existent target node "${edge.target}"`,
        edgeId: edge.id,
      });
    }
  });

  // Detect cycles
  const hasCycle = detectCycle(nodes, edges);
  if (hasCycle) {
    issues.push({
      id: 'cycle-detected',
      category: 'structure',
      severity: 'error',
      title: 'Circular Dependency Detected',
      description:
        'Workflow contains a cycle that could cause infinite loops. Use parallel execution or conditions instead.',
      suggestion: 'Add a condition to break the cycle or restructure the workflow',
    });
  }

  // Check for disconnected components
  const disconnected = findDisconnectedNodes(nodes, edges);
  disconnected.forEach((nodeId) => {
    const node = nodes.find((n) => n.id === nodeId);
    if (node && node.type !== 'trigger') {
      issues.push({
        id: `disconnected-${nodeId}`,
        category: 'structure',
        severity: 'warning',
        title: 'Disconnected Node',
        description: `Node "${node.data.label}" is not connected to any trigger`,
        nodeId,
        suggestion: 'Connect this node to the main workflow or remove it',
      });
    }
  });

  // Check for missing trigger
  const hasTrigger = nodes.some((n) => n.type === 'trigger');
  if (!hasTrigger) {
    issues.push({
      id: 'missing-trigger',
      category: 'structure',
      severity: 'error',
      title: 'Missing Trigger Node',
      description: 'Workflow must have at least one trigger node to start execution',
      suggestion: 'Add a trigger node (e.g., Webhook, Schedule, Manual) to the workflow',
    });
  }

  return issues;
}

/**
 * Validate node configurations
 */
function validateConfiguration(nodes: Node[]): ValidationIssue[] {
  const issues: ValidationIssue[] = [];

  nodes.forEach((node) => {
    // node.type, not node.data.type: React Flow owns the node's type and the
    // canvas drop handler sets it there. Nothing in the app ever writes
    // data.type, so every type check in this file was comparing undefined -
    // "Missing Trigger Node" fired on every workflow, and the action- and
    // transform-specific rules below never ran at all.
    const type = node.type;
    const { label } = node.data;
    const config = node.data.config || {};
    // Read the fields where they are actually written. The canvas drop handler
    // sets data.integration (NOT config.integration) and PropertyPanel sets
    // config.action (NOT config.actionType) - the same shape WorkflowMapper
    // reads on the server. Checking the wrong paths made "Missing Integration"
    // fire on every correctly configured action node while the action and
    // parameter checks below could never fire at all.
    const integration = node.data.integration as string | undefined;
    const action = config.action as string | undefined;

    // Check for missing label
    if (!label || label.trim() === '') {
      issues.push({
        id: `label-${node.id}`,
        category: 'configuration',
        severity: 'warning',
        title: 'Missing Node Label',
        description: `Node of type "${type}" has no label`,
        nodeId: node.id,
        suggestion: 'Add a descriptive label to identify this node',
      });
    }

    // Type-specific validation
    if (type === 'action') {
      if (!integration) {
        issues.push({
          id: `integration-${node.id}`,
          category: 'configuration',
          severity: 'error',
          title: 'Missing Integration',
          description: `Action "${label}" has no integration selected`,
          nodeId: node.id,
          suggestion: 'Select an integration for this action',
        });
      }

      if (!action && integration) {
        issues.push({
          id: `action-type-${node.id}`,
          category: 'configuration',
          severity: 'error',
          title: 'Missing Action Type',
          description: `Action "${label}" has no action type selected`,
          nodeId: node.id,
          suggestion: 'Select an action type for the integration',
        });
      }

      // Validate required parameters. ACTION_PARAMETER_REQUIREMENTS is keyed by
      // integration id (http/database/email), not by action id.
      const expectedParams = ACTION_PARAMETER_REQUIREMENTS[integration as string] || [];
      expectedParams.forEach((param: ParameterDefinition) => {
        if (param.required && !config.parameters?.[param.name]) {
          issues.push({
            id: `param-${node.id}-${param.name}`,
            category: 'configuration',
            severity: 'error',
            title: `Missing Required Parameter: ${param.name}`,
            description: `Action "${label}" requires parameter "${param.name}"`,
            nodeId: node.id,
            suggestion: `Provide a value for the required parameter "${param.name}"`,
          });
        }
      });
    }

    if (type === 'condition') {
      if (!config.expression || config.expression.trim() === '') {
        issues.push({
          id: `condition-expr-${node.id}`,
          category: 'configuration',
          severity: 'error',
          title: 'Missing Condition Expression',
          description: `Condition "${label}" has no expression defined`,
          nodeId: node.id,
          suggestion: 'Enter a condition expression (e.g., status === "active")',
        });
      }
    }

    if (type === 'loop') {
      if (!config.variable || config.variable.trim() === '') {
        issues.push({
          id: `loop-var-${node.id}`,
          category: 'configuration',
          severity: 'error',
          title: 'Missing Loop Variable',
          description: `Loop "${label}" has no iteration variable`,
          nodeId: node.id,
          suggestion: 'Define the variable that will hold each iteration value',
        });
      }

      if (!config.arrayExpression || config.arrayExpression.trim() === '') {
        issues.push({
          id: `loop-array-${node.id}`,
          category: 'configuration',
          severity: 'error',
          title: 'Missing Loop Array',
          description: `Loop "${label}" has no array expression`,
          nodeId: node.id,
          suggestion: 'Specify the array or collection to iterate over',
        });
      }
    }

    if (type === 'delay') {
      // `seconds` is what the panel writes and the engine reads.
      const duration = Number(config.seconds ?? 0);
      if (!duration || duration <= 0) {
        issues.push({
          id: `delay-${node.id}`,
          category: 'configuration',
          severity: 'warning',
          title: 'Invalid Delay Duration',
          description: `Delay "${label}" has no valid duration`,
          nodeId: node.id,
          suggestion: 'Set a positive duration in milliseconds',
        });
      }

      if (duration > 3600000) {
        issues.push({
          id: `delay-long-${node.id}`,
          category: 'best_practices',
          severity: 'warning',
          title: 'Long Delay',
          description: `Delay "${label}" is very long (${Math.round(duration / 1000)}s). Consider using a scheduled trigger instead.`,
          nodeId: node.id,
        });
      }
    }
  });

  return issues;
}

/**
 * Validate data flow between nodes
 */
function validateDataFlow(nodes: Node[], edges: Edge[]): ValidationIssue[] {
  const issues: ValidationIssue[] = [];
  const analysis = analyzeDataFlow(nodes, edges);

  // Convert data flow issues to validation issues
  analysis.issues.forEach((flowIssue) => {
    issues.push({
      id: `flow-${flowIssue.type}-${flowIssue.sourceNodeId}`,
      category: 'data_flow',
      severity: flowIssue.severity === 'error' ? 'error' : 'warning',
      title: flowIssue.type.replace(/_/g, ' ').toUpperCase(),
      description: flowIssue.message,
      nodeId: flowIssue.sourceNodeId,
      edgeId: undefined,
      suggestion:
        flowIssue.type === 'incompatible_connection'
          ? 'Map the output correctly or adjust the target node parameters'
          : flowIssue.type === 'unused_output'
            ? 'Remove unused outputs or add nodes that use them'
            : 'Check the node configuration and connections',
    });
  });

  return issues;
}

/**
 * Validate best practices
 */
function validateBestPractices(nodes: Node[], edges: Edge[]): ValidationIssue[] {
  const issues: ValidationIssue[] = [];

  // Check for missing error handlers
  const actionNodes = nodes.filter((n) => n.type === 'action');
  actionNodes.forEach((node) => {
    const hasErrorHandler = edges.some(
      (e) => e.source === node.id && e.data?.type === 'error'
    );

    if (!hasErrorHandler) {
      issues.push({
        id: `error-handler-${node.id}`,
        category: 'best_practices',
        severity: 'warning',
        title: 'Missing Error Handler',
        description: `Action "${node.data.label}" has no error handling configured`,
        nodeId: node.id,
        suggestion: 'Add an error path to handle failures gracefully',
      });
    }
  });

  // Check for too many nested conditions
  const conditionDepth = findMaxConditionDepth(nodes, edges);
  if (conditionDepth > 5) {
    issues.push({
      id: 'deep-nesting',
      category: 'best_practices',
      severity: 'warning',
      title: 'Deep Condition Nesting',
      description: `Workflow has ${conditionDepth} levels of nested conditions. Consider simplifying.`,
      suggestion: 'Refactor to use a simpler decision tree or separate workflows',
    });
  }

  // Check for unused variables in transforms
  nodes
    .filter((n) => n.type === 'transform')
    .forEach((node) => {
      // Transform nodes carry a JSON literal in `json`; `code` was removed
      // when the panel stopped offering a C# editor the engine cannot run.
      const json = String(node.data.config?.json ?? '');
      if (json.trim() === '') {
        issues.push({
          id: `transform-empty-${node.id}`,
          category: 'best_practices',
          severity: 'warning',
          title: 'Empty Transform',
          description: `Transform "${node.data.label}" has no code`,
          nodeId: node.id,
        });
      }
    });

  return issues;
}

/**
 * Validate security aspects
 */
function validateSecurity(nodes: Node[]): ValidationIssue[] {
  const issues: ValidationIssue[] = [];

  nodes.forEach((node) => {
    const { type } = node.data;
    const config = node.data.config || {};

    if (type === 'action') {
      // Check for hardcoded credentials
      const paramsStr = JSON.stringify(config.parameters || {});
      if (
        paramsStr.match(/password|secret|api[_-]?key|token|credential/i) &&
        paramsStr.match(/[a-zA-Z0-9]{20,}/)
      ) {
        issues.push({
          id: `hardcoded-secret-${node.id}`,
          category: 'security',
          severity: 'error',
          title: 'Hardcoded Secret Detected',
          description: `Action "${node.data.label}" appears to have hardcoded credentials`,
          nodeId: node.id,
          suggestion:
            'Use environment variables or secure credential storage instead of hardcoding secrets',
        });
      }
    }

    // A transform-node security scan for eval() / require() used to live here.
    // It read config.code, which nothing writes any more: the transform node
    // carries a JSON literal that the engine DESERIALIZES and never executes,
    // so there is no code path for those patterns to reach. Scanning for them
    // was theatre against a threat this design does not have.
  });

  return issues;
}

// ============================================================================
// Performance Analysis
// ============================================================================

/**
 * Estimate workflow performance
 */
function estimatePerformance(nodes: Node[], edges: Edge[]): PerformanceEstimate {
  let estimatedDuration = 0;
  let estimatedMemoryUsage = 0;
  const bottlenecks: Array<{
    nodeId: string;
    reason: string;
    impact: 'high' | 'medium' | 'low';
  }> = [];

  // Estimate duration - find critical path
  const criticalPathDuration = calculateCriticalPath(nodes, edges);
  estimatedDuration = criticalPathDuration;

  // Identify bottlenecks in critical path
  nodes.forEach((node) => {
    const nodeType = node.type;
    const typeMemory = NODE_TYPE_MEMORY[nodeType as keyof typeof NODE_TYPE_MEMORY] || 10240;

    estimatedMemoryUsage += typeMemory;

    if (nodeType === 'action' || nodeType === 'loop') {
      bottlenecks.push({
        nodeId: node.id,
        reason:
          nodeType === 'loop'
            ? 'Loops can cause exponential growth in execution time'
            : 'Actions typically have the highest latency',
        impact: nodeType === 'loop' ? 'high' : 'medium',
      });
    }

    // `seconds`, not `duration`: that is what the panel writes and what the
    // engine's delay handler reads. This check never fired before.
    const delaySeconds = Number(node.data.config?.seconds ?? 0);
    if (nodeType === 'delay' && delaySeconds > 5) {
      bottlenecks.push({
        nodeId: node.id,
        reason: `Large delay (${delaySeconds}s)`,
        impact: 'high',
      });
    }
  });

  // Count parallelization opportunities (independent branches)
  const parallelBranches = countParallelBranches(nodes, edges);

  return {
    estimatedDuration,
    estimatedDataVolume: calculateDataVolume(nodes, edges),
    estimatedMemoryUsage,
    bottlenecks: bottlenecks.sort((a, b) => {
      const impactOrder = { high: 0, medium: 1, low: 2 };
      return (
        impactOrder[a.impact] - impactOrder[b.impact]
      );
    }),
    parallelizationOpportunities: parallelBranches,
  };
}

/**
 * Calculate estimated total data volume
 */
function calculateDataVolume(nodes: Node[], _edges: Edge[]): number {
  let totalVolume = 0;

  // Estimate based on node types
  const nodeTypeVolumeMap: Record<string, number> = {
    trigger: 512,
    action: 2048,
    condition: 256,
    transform: 4096,
    loop: 8192,
    delay: 128,
  };

  nodes.forEach((node) => {
    const nodeType = node.type ?? 'unknown';
    totalVolume += nodeTypeVolumeMap[nodeType] || 1024;
  });

  return totalVolume;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Detect cycles using DFS
 */
function detectCycle(nodes: Node[], edges: Edge[]): boolean {
  const graph = new Map<string, string[]>();

  // Build adjacency list
  nodes.forEach((node) => {
    graph.set(node.id, []);
  });

  edges.forEach((edge) => {
    if (graph.has(edge.source)) {
      graph.get(edge.source)!.push(edge.target);
    }
  });

  const visited = new Set<string>();
  const recursionStack = new Set<string>();

  function hasCycle(nodeId: string): boolean {
    visited.add(nodeId);
    recursionStack.add(nodeId);

    const neighbors = graph.get(nodeId) || [];
    for (const neighbor of neighbors) {
      if (!visited.has(neighbor)) {
        if (hasCycle(neighbor)) return true;
      } else if (recursionStack.has(neighbor)) {
        return true;
      }
    }

    recursionStack.delete(nodeId);
    return false;
  }

  for (const node of nodes) {
    if (!visited.has(node.id) && hasCycle(node.id)) {
      return true;
    }
  }

  return false;
}

/**
 * Find disconnected nodes
 */
function findDisconnectedNodes(nodes: Node[], edges: Edge[]): string[] {
  const graph = new Map<string, Set<string>>();
  nodes.forEach((n) => graph.set(n.id, new Set()));

  edges.forEach((e) => {
    graph.get(e.source)?.add(e.target);
    graph.get(e.target)?.add(e.source);
  });

  const visited = new Set<string>();

  function dfs(nodeId: string) {
    visited.add(nodeId);
    for (const neighbor of graph.get(nodeId) || []) {
      if (!visited.has(neighbor)) {
        dfs(neighbor);
      }
    }
  }

  // Find a trigger or first node to start DFS
  const startNode = nodes.find((n) => n.type === 'trigger') || nodes[0];
  if (startNode) {
    dfs(startNode.id);
  }

  return nodes
    .filter((n) => !visited.has(n.id))
    .map((n) => n.id);
}

/**
 * Find max nesting depth of conditions
 */
function findMaxConditionDepth(nodes: Node[], edges: Edge[]): number {
  const graph = new Map<string, string[]>();
  nodes.forEach((n) => graph.set(n.id, []));
  edges.forEach((e) => {
    if (graph.has(e.source)) {
      graph.get(e.source)!.push(e.target);
    }
  });

  let maxDepth = 0;

  function calculateDepth(nodeId: string, currentDepth: number): number {
    const node = nodes.find((n) => n.id === nodeId);
    if (!node) return currentDepth;

    let newDepth = currentDepth;
    if (node.type === 'condition') {
      newDepth = currentDepth + 1;
    }

    const neighbors = graph.get(nodeId) || [];
    let depth = newDepth;
    for (const neighbor of neighbors) {
      depth = Math.max(depth, calculateDepth(neighbor, newDepth));
    }

    return depth;
  }

  const startNode = nodes.find((n) => n.type === 'trigger') || nodes[0];
  if (startNode) {
    maxDepth = calculateDepth(startNode.id, 0);
  }

  return maxDepth;
}

/**
 * Calculate critical path duration
 */
function calculateCriticalPath(nodes: Node[], edges: Edge[]): number {
  const durations = new Map<string, number>();
  const nodeType = new Map<string, string>();

  nodes.forEach((n) => {
    nodeType.set(
      n.id,
      n.type ?? 'unknown'
    );
    const typeDuration =
      NODE_TYPE_DURATIONS[n.type as keyof typeof NODE_TYPE_DURATIONS] || 10;
    durations.set(n.id, typeDuration);
  });

  let totalDuration = 0;
  let currentNode: Node | undefined = nodes.find((n) => n.type === 'trigger');

  const visited = new Set<string>();
  while (currentNode && !visited.has(currentNode.id)) {
    visited.add(currentNode.id);
    totalDuration += durations.get(currentNode.id) || 0;

    const outgoing = edges.find((e) => e.source === currentNode!.id);
    if (outgoing) {
      currentNode = nodes.find((n) => n.id === outgoing.target);
    } else {
      break;
    }
  }

  return totalDuration;
}

/**
 * Count parallel branches
 */
function countParallelBranches(nodes: Node[], edges: Edge[]): number {
  let maxBranches = 0;

  nodes.forEach((node) => {
    const outgoing = edges.filter((e) => e.source === node.id);
    maxBranches = Math.max(maxBranches, outgoing.length - 1);
  });

  return maxBranches;
}

// ============================================================================
// Main Validation Service
// ============================================================================

/**
 * Perform comprehensive workflow validation
 */
export function validateWorkflow(
  nodes: Node[],
  edges: Edge[]
): ValidationReport {
  const issues: ValidationIssue[] = [];

  // Run all validators
  issues.push(...validateStructure(nodes, edges));
  issues.push(...validateConfiguration(nodes));
  issues.push(...validateDataFlow(nodes, edges));
  issues.push(...validateBestPractices(nodes, edges));
  issues.push(...validateSecurity(nodes));

  // Estimate performance
  const performance = estimatePerformance(nodes, edges);

  // Calculate metrics
  const metrics: ValidationMetric[] = [
    {
      name: 'Estimated Duration',
      value: `${Math.round(performance.estimatedDuration)}ms`,
      status: performance.estimatedDuration < PERFORMANCE_THRESHOLDS.duration.warning ? 'good' : 'warning',
      threshold: PERFORMANCE_THRESHOLDS.duration,
      unit: 'ms',
    },
    {
      name: 'Estimated Memory',
      value: `${(performance.estimatedMemoryUsage / 1024 / 1024).toFixed(1)}MB`,
      status:
        performance.estimatedMemoryUsage < PERFORMANCE_THRESHOLDS.memory.warning
          ? 'good'
          : 'warning',
      threshold: PERFORMANCE_THRESHOLDS.memory,
      unit: 'bytes',
    },
    {
      name: 'Data Bottlenecks',
      value: performance.bottlenecks.length,
      status: performance.bottlenecks.length === 0 ? 'good' : performance.bottlenecks.some((b) => b.impact === 'high') ? 'critical' : 'warning',
    },
    {
      name: 'Parallel Opportunities',
      value: performance.parallelizationOpportunities,
      status: performance.parallelizationOpportunities > 0 ? 'good' : 'warning',
    },
  ];

  // Calculate coverage
  const errorCount = issues.filter((i) => i.severity === 'error').length;
  const warningCount = issues.filter((i) => i.severity === 'warning').length;
  const coveragePercentage = Math.max(
    0,
    100 - (errorCount * 20 + warningCount * 5)
  );

  // Generate recommendations
  const recommendations: string[] = [];
  if (errorCount > 0) {
    recommendations.push(`Fix ${errorCount} critical error${errorCount > 1 ? 's' : ''} before testing`);
  }
  if (performance.bottlenecks.length > 0) {
    recommendations.push(`Optimize ${performance.bottlenecks.length} performance bottleneck${performance.bottlenecks.length > 1 ? 's' : ''}`);
  }
  if (warningCount > 0) {
    recommendations.push(`Address ${warningCount} warning${warningCount > 1 ? 's' : ''} to improve reliability`);
  }
  if (performance.parallelizationOpportunities > 0) {
    recommendations.push(`Consider parallelizing ${performance.parallelizationOpportunities} independent branches`);
  }
  if (recommendations.length === 0) {
    recommendations.push('Workflow is well-structured and ready for testing');
  }

  const overallScore = Math.round(coveragePercentage);

  return {
    overallScore,
    isValid: errorCount === 0,
    issues,
    metrics,
    performance,
    coverage: {
      nodesCovered: nodes.length - issues.filter((i) => i.nodeId).length,
      totalNodes: nodes.length,
      edgesCovered: edges.length - issues.filter((i) => i.edgeId).length,
      totalEdges: edges.length,
      percentage: coveragePercentage,
    },
    recommendations,
    testedAt: Date.now(),
  };
}
