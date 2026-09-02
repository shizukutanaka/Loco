/**
 * Workflow Simulation Engine
 *
 * Simulates workflow execution without actual integration calls:
 * - Step-by-step execution simulation
 * - Mock data flow through nodes
 * - Error injection and recovery testing
 * - Performance measurement
 * - Execution trace recording
 */

import { Node, Edge } from 'reactflow';
import { compare } from './conditionSemantics';

// ============================================================================
// Types
// ============================================================================

export interface SimulationData {
  // Mock payloads are produced here and rendered, never computed on, so callers
  // narrowing at the point of use is the correct contract.
  [key: string]: unknown;
}

export type ExecutionResult = 'success' | 'error' | 'skipped' | 'running';

export interface ExecutionStep {
  nodeId: string;
  nodeName: string;
  nodeType: string;
  status: ExecutionResult;
  startTime: number;
  endTime: number;
  duration: number;
  inputData: SimulationData;
  outputData: SimulationData;
  error?: string;
  errorNode?: string;
}

export interface SimulationConfig {
  injectErrors?: boolean;
  errorRate?: number; // 0-1
  mockDelay?: boolean;
  delayMultiplier?: number;
  breakOnError?: boolean;
  recordTrace?: boolean;
  timeLimit?: number; // milliseconds
}

export interface SimulationResult {
  success: boolean;
  totalDuration: number;
  stepsExecuted: ExecutionStep[];
  finalData: SimulationData;
  errors: Array<{ step: number; message: string; nodeId: string }>;
  coverage: {
    nodesExecuted: number;
    totalNodes: number;
    pathsTaken: number;
  };
  warnings: string[];
}

// ============================================================================
// Mock Data Generators
// ============================================================================

/**
 * Generate mock data for different data types
 */
function generateMockData(
  nodeType: string,
  config: Record<string, unknown> = {}
): SimulationData {
  switch (nodeType) {
    case 'trigger':
      return {
        timestamp: Date.now(),
        payload: { id: '123', name: 'Test Event', data: { test: true } },
        source: 'webhook',
      };

    case 'action':
      return {
        success: true,
        result: {
          id: 'result-' + Math.random().toString(36).substr(2, 9),
          status: 'completed',
          data: { processed: true },
        },
        statusCode: 200,
        duration: Math.random() * 1000,
        headers: { 'content-type': 'application/json' },
      };

    case 'condition':
      // Replaced in simulateNodeExecution with the real verdict. This used to
      // be `matched: Math.random() > 0.5` and reported a `config.expression`
      // that nothing writes.
      return { matched: false, evaluated: false };

    case 'transform':
      return {
        output: {
          transformed: true,
          timestamp: Date.now(),
          version: '1.0',
        },
        error: null,
      };

    case 'loop':
      return {
        current: { id: 1, value: 'item-1' },
        index: 0,
        total: 5,
        completed: false,
      };

    case 'delay':
      return {
        waited: true,
        // Delay nodes store `seconds`, so report the wait in the same unit the
        // user configured rather than a millisecond key nobody writes.
        duration: (Number(config.seconds) || 1) * 1000,
      };

    default:
      return { output: 'test data' };
  }
}

/**
 * Merge data from multiple sources
 */
function mergeData(base: SimulationData, ...updates: SimulationData[]): SimulationData {
  let result = { ...base };
  updates.forEach((update) => {
    result = { ...result, ...update };
  });
  return result;
}

// ============================================================================
// Execution Simulation
// ============================================================================

/**
 * Simulate a single node execution
 */
export function simulateNodeExecution(
  node: Node,
  inputData: SimulationData,
  config: SimulationConfig = {},
  nodeOutputs: Record<string, SimulationData> = {}
): ExecutionStep {
  const startTime = performance.now();
  const nodeType = node.type ?? 'unknown';
  let status: ExecutionResult = 'success';
  let error: string | undefined;
  let outputData = generateMockData(nodeType, node.data.config);
  if (nodeType === 'condition') {
    // The one node whose output is not mock data. Its verdict is what the
    // branch selection below reads, so it is evaluated once, here, from the
    // same left/operation/right the engine reads.
    const config = node.data.config ?? {};
    try {
      outputData = {
        matched: evaluateCondition(node, inputData, nodeOutputs),
        evaluated: true,
        left: config.left,
        operation: config.operation ?? 'equals',
        right: config.right,
      };
    } catch (e) {
      // An ordering comparison on non-numbers fails the node in the engine, so
      // the simulation has to fail it too. Reporting `false` and carrying on
      // down the false branch is the simulator promising a green run for a
      // workflow that will die.
      status = 'error';
      error = e instanceof Error ? e.message : String(e);
      outputData = { error };
    }
  }

  // Simulate errors based on configuration
  if (config.injectErrors && Math.random() < (config.errorRate || 0.1)) {
    status = 'error';
    const errorMessages = [
      'Integration timeout',
      'Invalid configuration',
      'Authentication failed',
      'Network error',
      'Rate limit exceeded',
    ];
    error = errorMessages[Math.floor(Math.random() * errorMessages.length)];
    outputData = { error };
  }

  // Simulate delay
  let duration = Math.random() * 100; // Base 0-100ms simulation
  // Delay nodes store `seconds`; `duration` was never written, so simulated
  // delays were always the random baseline regardless of configuration.
  const delaySeconds = Number(node.data.config?.seconds ?? 0);
  if (config.mockDelay && delaySeconds > 0) {
    duration = (delaySeconds * 1000) / (config.delayMultiplier || 10);
  }

  const endTime = startTime + duration;

  return {
    nodeId: node.id,
    nodeName: node.data.label,
    nodeType,
    status,
    startTime,
    endTime,
    duration,
    inputData: { ...inputData },
    outputData,
    error,
  };
}

/**
 * Find next nodes after current execution
 */
export function findNextNodes(
  currentNodeId: string,
  edges: Edge[],
  nodes: Node[],
  verdict?: boolean
): Node[] {
  // Mirrors VisualWorkflowEngine.ShouldFollowConnection: an edge leaving a
  // "true" or "false" handle is followed only when the verdict matches; an
  // edge with no handle is the default output and is always followed.
  const nextEdges = edges.filter((e) => {
    if (e.source !== currentNodeId) return false;
    if (e.sourceHandle === 'true' || e.sourceHandle === 'false') {
      return verdict !== undefined && verdict === (e.sourceHandle === 'true');
    }
    return true;
  });
  return nextEdges
    .map((e) => nodes.find((n) => n.id === e.target))
    .filter((n) => n !== undefined) as Node[];
}

/**
 * Resolve a {{reference}} the way WorkflowVariableResolver does on the server.
 *
 * A value that is exactly one reference keeps the referenced value's type
 * (a number stays a number, so greater_than compares numbers); a reference
 * inside longer text is substituted as text; an unresolvable reference is
 * null - not its own braces, which would be non-empty text and make
 * `contains ''` true.
 *
 * Lookup order matches the engine: a node's own output by node id, then the
 * merged simulation data (the engine's workflow variables), then `previous`.
 */
export function resolveReference(
  value: unknown,
  data: SimulationData,
  nodeOutputs: Record<string, SimulationData>
): unknown {
  if (typeof value !== 'string') return value;

  const lookup = (path: string): unknown => {
    const [head, ...rest] = path.trim().split('.');
    let current: unknown;
    let fromNodeResult = false;

    // Workflow variables first, then node results, then `previous`. The engine
    // is the authority on this order and resolves variables first; this used to
    // check node outputs first, so a variable and a node sharing a name
    // resolved to opposite things in the two implementations.
    if (head in data) {
      current = data[head];
    } else if (head in nodeOutputs) {
      current = nodeOutputs[head];
      fromNodeResult = true;
    } else if (head === 'previous') {
      const ids = Object.keys(nodeOutputs);
      if (!ids.length) return null;
      current = nodeOutputs[ids[ids.length - 1]];
      fromNodeResult = true;
    } else {
      return null;
    }

    // Within a node result an optional `data` segment is skipped, so
    // {{n1.data.status}} and {{n1.status}} name the same thing. The engine
    // has always allowed both; the simulator returned null for the first.
    const segments =
      fromNodeResult && rest[0] === 'data' && rest.length > 1 ? rest.slice(1) : rest;

    for (const part of segments) {
      if (current && typeof current === 'object' && part in (current as object)) {
        current = (current as Record<string, unknown>)[part];
      } else {
        return null;
      }
    }
    return current ?? null;
  };

  // Exactly one reference and nothing else keeps the referenced value's type.
  // The test is that the FIRST closing brace is the last thing in the string:
  // a greedy /^{{(.+)}}$/ also matched "{{user}} said {{message}}", read it as
  // the single path "user}} said {{message", and resolved the whole parameter
  // to null. "prefix {{user}} said {{message}}" was unaffected, which is how
  // it survived - the failing shape is exactly "{{first}} {{last}}".
  const trimmed = value;
  if (
    trimmed.startsWith('{{') &&
    trimmed.endsWith('}}') &&
    trimmed.indexOf('}}') === trimmed.length - 2
  ) {
    return lookup(trimmed.slice(2, -2));
  }

  return value.replace(/\{\{(.+?)\}\}/g, (_, path) => {
    const resolved = lookup(path);
    return resolved == null ? '' : String(resolved);
  });
}

/**
 * The condition node's verdict, computed the way the engine computes it.
 *
 * This was `Math.random() > 0.5` with both parameters unused. The engine's
 * handler (VisualWorkflowEngine.RegisterDefaultHandlers) reads left, right
 * and operation, defaults operation to "equals", and falls through to false
 * for an operation it does not know - so does this.
 */
export function evaluateCondition(
  conditionNode: Node,
  data: SimulationData,
  nodeOutputs: Record<string, SimulationData> = {}
): boolean {
  const config = conditionNode.data.config ?? {};

  return compare(
    resolveReference(config.left, data, nodeOutputs),
    String(config.operation ?? 'equals'),
    resolveReference(config.right, data, nodeOutputs),
    conditionNode.data.label ?? ''
  );
}

// ============================================================================
// Main Simulation Engine
// ============================================================================

/**
 * Run full workflow simulation
 */
export function simulateWorkflow(
  nodes: Node[],
  edges: Edge[],
  config: SimulationConfig = {}
): SimulationResult {
  const startTime = performance.now();
  const steps: ExecutionStep[] = [];
  const errors: Array<{ step: number; message: string; nodeId: string }> = [];
  const visitedNodes = new Set<string>();
  const warnings: string[] = [];

  // Find trigger node (workflow start)
  const triggerNode = nodes.find((n) => n.type === 'trigger');
  if (!triggerNode) {
    return {
      success: false,
      totalDuration: 0,
      stepsExecuted: [],
      finalData: {},
      errors: [{ step: 0, message: 'No trigger node found', nodeId: '' }],
      coverage: { nodesExecuted: 0, totalNodes: nodes.length, pathsTaken: 0 },
      warnings: ['Workflow has no trigger node'],
    };
  }

  // Execute workflow step by step
  let currentNodes = [triggerNode];
  let currentData: SimulationData = {};
  // Each node's own output by id, so a condition can read {{nodeId.field}}
  // the way the engine reads NodeResults.
  const nodeOutputs: Record<string, SimulationData> = {};
  let pathsTaken = 0;
  let stepCount = 0;
  const maxSteps = Math.min(nodes.length * 5, 100); // Prevent infinite loops

  while (currentNodes.length > 0 && stepCount < maxSteps) {
    const nextNodes: Node[] = [];

    for (const node of currentNodes) {
      if (visitedNodes.has(node.id) && node.type !== 'loop') {
        // Skip already visited nodes (except loops which can repeat)
        continue;
      }

      visitedNodes.add(node.id);

      // Simulate execution
      const step = simulateNodeExecution(node, currentData, config, nodeOutputs);
      steps.push(step);
      nodeOutputs[node.id] = step.outputData;
      stepCount++;

      // Record error
      if (step.status === 'error') {
        errors.push({
          step: steps.length - 1,
          message: step.error || 'Unknown error',
          nodeId: node.id,
        });

        if (config.breakOnError) {
          // Check if there's an error handler
          const errorPath = edges.find(
            (e) => e.source === node.id && e.data?.condition === 'error'
          );
          if (!errorPath) {
            // No error handler - execution stops
            warnings.push(
              `Error in "${node.data.label}" with no error handler`
            );
            continue;
          }
        }
      }

      // Merge output data
      currentData = mergeData(currentData, step.outputData);

      // Find next nodes to execute
      if (node.type === 'condition') {
        // The verdict was computed in simulateNodeExecution; the edges whose
        // handle matches it are followed. This used to call evaluateCondition,
        // discard the answer, and pick one outgoing edge at random - so the
        // tester's step count changed from run to run for the same workflow.
        const verdict = step.outputData.matched === true;
        const branches = findNextNodes(node.id, edges, nodes, verdict);

        if (branches.length > 0) {
          nextNodes.push(...branches);
          pathsTaken++;
        }
      } else {
        // For non-condition nodes, execute all outgoing branches
        const next = findNextNodes(node.id, edges, nodes);
        nextNodes.push(...next);
        if (next.length > 1) {
          pathsTaken++; // Count parallel branches
        }
      }
    }

    currentNodes = nextNodes;
  }

  const endTime = performance.now();
  const totalDuration = endTime - startTime;

  // Check for time limit exceeded
  if (stepCount >= maxSteps) {
    warnings.push('Simulation reached maximum step limit - possible infinite loop');
  }

  return {
    success: errors.length === 0,
    totalDuration,
    stepsExecuted: steps,
    finalData: currentData,
    errors,
    coverage: {
      nodesExecuted: visitedNodes.size,
      totalNodes: nodes.length,
      pathsTaken,
    },
    warnings,
  };
}

/**
 * Simulate specific error scenario
 */
export function simulateErrorScenario(
  nodes: Node[],
  edges: Edge[],
  errorNodeId: string
): SimulationResult {
  const steps: ExecutionStep[] = [];
  const errors: Array<{ step: number; message: string; nodeId: string }> = [];

  // Find path to error node
  const errorNode = nodes.find((n) => n.id === errorNodeId);
  if (!errorNode) {
    return {
      success: false,
      totalDuration: 0,
      stepsExecuted: [],
      finalData: {},
      errors: [{ step: 0, message: 'Error node not found', nodeId: errorNodeId }],
      coverage: { nodesExecuted: 0, totalNodes: nodes.length, pathsTaken: 0 },
      warnings: [],
    };
  }

  // Trace path backwards to trigger
  const path = tracePathToTrigger(errorNodeId, edges, nodes);
  if (path.length === 0) {
    return {
      success: false,
      totalDuration: 0,
      stepsExecuted: [],
      finalData: {},
      errors: [{ step: 0, message: 'Cannot trace path to trigger', nodeId: errorNodeId }],
      coverage: { nodesExecuted: 0, totalNodes: nodes.length, pathsTaken: 0 },
      warnings: [],
    };
  }

  let currentData: SimulationData = {};

  // Execute path up to error node
  for (let i = 0; i < path.length; i++) {
    const nodeId = path[i];
    const node = nodes.find((n) => n.id === nodeId);
    if (!node) continue;

    const step = simulateNodeExecution(
      node,
      currentData,
      { injectErrors: nodeId === errorNodeId, errorRate: 1 }
    );
    steps.push(step);

    if (step.status === 'error') {
      errors.push({
        step: steps.length - 1,
        message: step.error || 'Simulated error',
        nodeId,
      });
    }

    currentData = mergeData(currentData, step.outputData);
  }

  // Check for error handlers
  const errorHandlers = edges.filter(
    (e) => e.source === errorNodeId && e.data?.condition === 'error'
  );

  return {
    success: false,
    totalDuration: steps.reduce((sum, s) => sum + s.duration, 0),
    stepsExecuted: steps,
    finalData: currentData,
    errors,
    coverage: {
      nodesExecuted: steps.length,
      totalNodes: nodes.length,
      pathsTaken: 0,
    },
    warnings: errorHandlers.length === 0 ? ['No error handler for this node'] : [],
  };
}

/**
 * Trace path from node back to trigger
 */
export function tracePathToTrigger(
  nodeId: string,
  edges: Edge[],
  nodes: Node[]
): string[] {
  const path: string[] = [nodeId];
  let currentId = nodeId;

  while (true) {
    const incomingEdges = edges.filter((e) => e.target === currentId);
    if (incomingEdges.length === 0) break;

    const edge = incomingEdges[0];
    const sourceNode = nodes.find((n) => n.id === edge.source);

    if (!sourceNode) break;
    path.unshift(edge.source);

    if (sourceNode.type === 'trigger') {
      break;
    }

    currentId = edge.source;

    if (path.length > nodes.length) {
      // Prevent infinite loops
      break;
    }
  }

  return path;
}

/**
 * Analyze execution trace for bottlenecks
 */
export function analyzeExecutionTrace(
  steps: ExecutionStep[]
): Array<{ nodeId: string; nodeName: string; duration: number; percentage: number }> {
  const totalDuration = steps.reduce((sum, s) => sum + s.duration, 0);

  const nodeStats = new Map<
    string,
    { nodeName: string; totalDuration: number; count: number }
  >();

  steps.forEach((step) => {
    const existing = nodeStats.get(step.nodeId);
    if (existing) {
      existing.totalDuration += step.duration;
      existing.count++;
    } else {
      nodeStats.set(step.nodeId, {
        nodeName: step.nodeName,
        totalDuration: step.duration,
        count: 1,
      });
    }
  });

  return Array.from(nodeStats.entries())
    .map(([nodeId, stats]) => ({
      nodeId,
      nodeName: stats.nodeName,
      duration: stats.totalDuration,
      percentage: totalDuration > 0 ? (stats.totalDuration / totalDuration) * 100 : 0,
    }))
    .sort((a, b) => b.duration - a.duration);
}

/**
 * Generate execution summary report
 */
export function generateExecutionReport(
  result: SimulationResult
): string {
  const lines: string[] = [];

  lines.push('=== WORKFLOW SIMULATION REPORT ===\n');
  lines.push(`Status: ${result.success ? '✓ SUCCESS' : '✗ FAILED'}`);
  lines.push(`Total Duration: ${Math.round(result.totalDuration)}ms`);
  lines.push(`Steps Executed: ${result.stepsExecuted.length}`);
  lines.push(`Coverage: ${result.coverage.nodesExecuted}/${result.coverage.totalNodes} nodes\n`);

  if (result.errors.length > 0) {
    lines.push('ERRORS:');
    result.errors.forEach((err) => {
      lines.push(`  [Step ${err.step}] ${err.message} (Node: ${err.nodeId})`);
    });
    lines.push('');
  }

  if (result.warnings.length > 0) {
    lines.push('WARNINGS:');
    result.warnings.forEach((warning) => {
      lines.push(`  ⚠ ${warning}`);
    });
    lines.push('');
  }

  const bottlenecks = analyzeExecutionTrace(result.stepsExecuted);
  if (bottlenecks.length > 0 && bottlenecks[0].percentage > 20) {
    lines.push('BOTTLENECKS:');
    bottlenecks.slice(0, 3).forEach((bn) => {
      lines.push(
        `  ${bn.nodeName}: ${Math.round(bn.duration)}ms (${bn.percentage.toFixed(1)}%)`
      );
    });
  }

  return lines.join('\n');
}
