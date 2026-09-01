/**
 * Data Flow Analysis Engine
 *
 * Analyzes data flow through workflow nodes to detect:
 * - Parameter type mismatches
 * - Missing variable references
 * - Incompatible node connections
 * - Data loss scenarios
 * - Performance bottlenecks in data processing
 */

import { Node, Edge } from 'reactflow';
import { integrations } from '@/data/integrations';

// ============================================================================
// Types
// ============================================================================

export interface DataType {
  name: string;
  baseType: 'string' | 'number' | 'boolean' | 'object' | 'array' | 'any';
  schema?: Record<string, DataType>;
  nullable?: boolean;
}

export interface NodeOutputSchema {
  [key: string]: DataType;
}

export interface ParameterDefinition {
  name: string;
  type: DataType;
  required: boolean;
  description?: string;
}

export interface DataFlowIssue {
  type: 'type_mismatch' | 'missing_output' | 'incompatible_connection' | 'unused_output' | 'data_loss';
  severity: 'error' | 'warning';
  sourceNodeId: string;
  targetNodeId?: string;
  message: string;
  field?: string;
}

export interface DataFlowAnalysis {
  issues: DataFlowIssue[];
  nodeOutputSchemas: Map<string, NodeOutputSchema>;
  dataFlowPaths: Array<{ path: string[]; dataType: DataType }>;
  isValidDataFlow: boolean;
}

// ============================================================================
// Node Type Output Schemas
// ============================================================================

// Define what data each node type outputs
const NODE_OUTPUT_SCHEMAS: Record<string, NodeOutputSchema> = {
  trigger: {
    timestamp: { name: 'timestamp', baseType: 'number' },
    payload: { name: 'payload', baseType: 'any' },
  },
  action: {
    success: { name: 'success', baseType: 'boolean' },
    result: { name: 'result', baseType: 'any' },
    statusCode: { name: 'statusCode', baseType: 'number' },
    duration: { name: 'duration', baseType: 'number' },
  },
  condition: {
    matched: { name: 'matched', baseType: 'boolean' },
  },
  transform: {
    output: { name: 'output', baseType: 'any' },
    error: { name: 'error', baseType: 'string', nullable: true },
  },
  loop: {
    current: { name: 'current', baseType: 'any' },
    index: { name: 'index', baseType: 'number' },
    completed: { name: 'completed', baseType: 'boolean' },
  },
};

/**
 * The required/optional parameters of one connector action.
 *
 * Derived from `data/integrations.ts`, which is where a connector's parameters
 * are declared, where the PropertyPanel builds its input fields from, and what
 * `scripts/check-structure.py` verifies the C# connectors actually read. This
 * used to be a hand-written table with three entries - http, database, email -
 * and it was wrong in both directions:
 *
 *   It covered 3 integrations out of the whole catalogue, so the
 *   required-parameter check was simply silent for every other connector.
 *
 *   Its http entry required `method`, which no http action takes. HttpConnector
 *   models one action per verb (get/post/put/patch/delete), so the parameter
 *   does not exist, the panel offers no field for it, and a correctly
 *   configured HTTP node reported "Missing Required Parameter: method"
 *   permanently, with nothing a user could do to clear it.
 *
 * Requirements also differ per ACTION, not per integration - Slack's
 * sendMessage and its uploadFile need different things - which a table keyed
 * by integration alone cannot express.
 */
export function getActionParameters(
  integrationId: string | undefined,
  actionId: string | undefined
): ParameterDefinition[] {
  if (!integrationId) return [];

  const integration = integrations.find((i) => i.id === integrationId);
  if (!integration) return [];

  // With no action chosen there is nothing to require yet; validation reports
  // the missing action itself, and guessing at one action's parameters would
  // produce errors for fields the user has not been asked for.
  const action = integration.actions?.find((a) => a.id === actionId);
  if (!action) return [];

  return action.parameters.map((p) => ({
    name: p.name,
    type: { name: p.name, baseType: PARAMETER_BASE_TYPES[p.type] ?? 'any' },
    required: p.required,
    description: p.description,
  }));
}

/** Maps a declared parameter type onto the analyzer's coarser base types. */
const PARAMETER_BASE_TYPES: Record<string, DataType['baseType']> = {
  string: 'string',
  number: 'number',
  boolean: 'boolean',
  json: 'object',
  select: 'string',
  array: 'array',
};

// ============================================================================
// Type Compatibility Checking
// ============================================================================

/**
 * Check if source type is compatible with target type
 */
export function isTypeCompatible(sourceType: DataType, targetType: DataType): boolean {
  // 'any' type is compatible with everything
  if (sourceType.baseType === 'any' || targetType.baseType === 'any') {
    return true;
  }

  // Exact match
  if (sourceType.baseType === targetType.baseType) {
    return true;
  }

  // Number and string can be coerced
  if (
    (sourceType.baseType === 'number' && targetType.baseType === 'string') ||
    (sourceType.baseType === 'string' && targetType.baseType === 'number')
  ) {
    return true;
  }

  // Object and array coercion
  if (sourceType.baseType === 'object' && targetType.baseType === 'array') {
    return false; // Generally incompatible
  }

  return false;
}

/**
 * Get detailed type description
 */
export function getTypeDescription(dataType: DataType): string {
  const nullable = dataType.nullable ? '?' : '';
  return `${dataType.baseType}${nullable}`;
}

// ============================================================================
// Data Flow Path Analysis
// ============================================================================

/**
 * Trace data flow paths through workflow
 */
export function traceDataFlowPaths(
  nodes: Node[],
  edges: Edge[],
  outputSchemas: Map<string, NodeOutputSchema>
): Array<{ path: string[]; dataType: DataType }> {
  const paths: Array<{ path: string[]; dataType: DataType }> = [];

  // Find all trigger nodes (start points)
  const triggerNodes = nodes.filter((n) => n.type === 'trigger');

  if (triggerNodes.length === 0) {
    return paths;
  }

  // For each trigger, trace all possible paths
  triggerNodes.forEach((trigger) => {
    const triggerOutputs = outputSchemas.get(trigger.id) || NODE_OUTPUT_SCHEMAS.trigger;

    Object.entries(triggerOutputs).forEach(([_outputKey, outputType]) => {
      // BFS to find all paths this data can follow
      const queue: { nodeId: string; visited: Set<string>; path: string[] }[] = [
        {
          nodeId: trigger.id,
          visited: new Set([trigger.id]),
          path: [trigger.id],
        },
      ];

      while (queue.length > 0) {
        const { nodeId, visited, path } = queue.shift()!;

        // Find outgoing edges
        const outgoingEdges = edges.filter((e: Edge) => e.source === nodeId);

        if (outgoingEdges.length === 0) {
          // End of path
          paths.push({
            path,
            dataType: outputType,
          });
        } else {
          outgoingEdges.forEach((edge: Edge) => {
            if (!visited.has(edge.target)) {
              const newVisited = new Set(visited);
              newVisited.add(edge.target);
              queue.push({
                nodeId: edge.target,
                visited: newVisited,
                path: [...path, edge.target],
              });
            }
          });
        }
      }
    });
  });

  return paths;
}

// ============================================================================
// Main Analysis Engine
// ============================================================================

/**
 * Analyze entire workflow data flow
 */
export function analyzeDataFlow(
  nodes: Node[],
  edges: Edge[]
): DataFlowAnalysis {
  const issues: DataFlowIssue[] = [];
  const nodeOutputSchemas = new Map<string, NodeOutputSchema>();

  // Step 1: Determine output schema for each node
  nodes.forEach((node) => {
    const nodeType = node.type ?? 'unknown';

    if (NODE_OUTPUT_SCHEMAS[nodeType]) {
      nodeOutputSchemas.set(node.id, { ...NODE_OUTPUT_SCHEMAS[nodeType] });
    } else {
      // Default: unknown output
      nodeOutputSchemas.set(node.id, {
        output: { name: 'output', baseType: 'any' },
      });
    }
  });

  // Step 2: Check edge connections for type compatibility
  edges.forEach((edge: Edge) => {
    const sourceNode = nodes.find((n) => n.id === edge.source);
    const targetNode = nodes.find((n) => n.id === edge.target);

    if (!sourceNode || !targetNode) {
      return;
    }

    const sourceOutputs = nodeOutputSchemas.get(edge.source) || {};

    // The editor writes data.integration and config.action; config.actionType
    // is never written, so an earlier version of this lookup always missed.
    const expectedParams = getActionParameters(
      targetNode.data.integration,
      targetNode.data.config?.action
    );

    // Check if target node has any required parameters
    if (expectedParams.length > 0 && targetNode.data.config?.parameters) {
      // Verify at least some outputs match expected parameters
      const sourceOutputKeys = Object.keys(sourceOutputs);
      const parameterNames = expectedParams.map((p: ParameterDefinition) => p.name);

      const hasCompatibleOutput = sourceOutputKeys.some((key) =>
        parameterNames.includes(key)
      );

      if (!hasCompatibleOutput && sourceOutputKeys.length > 0) {
        issues.push({
          type: 'incompatible_connection',
          severity: 'warning',
          sourceNodeId: edge.source,
          targetNodeId: edge.target,
          message: `Output from "${sourceNode.data.label}" may not contain required parameters for "${targetNode.data.label}"`,
        });
      }
    }
  });

  // Step 3: Check for unused outputs
  nodes.forEach((node) => {
    const nodeId = node.id;
    const outputs = nodeOutputSchemas.get(nodeId) || {};

    Object.keys(outputs).forEach((_outputKey) => {
      // In practice, we'd check if this output is used by any downstream node
      // For now, we'll skip unused output detection as data fields aren't always explicitly set
    });
  });

  // Step 4: Detect missing critical outputs
  nodes.forEach((node) => {
    const nodeType = node.type;
    const isAction = nodeType === 'action';

    if (isAction && !nodeOutputSchemas.has(node.id)) {
      issues.push({
        type: 'missing_output',
        severity: 'error',
        sourceNodeId: node.id,
        message: `Cannot determine output schema for action "${node.data.label}". Integration may be misconfigured.`,
      });
    }
  });

  // Step 5: Check for orphaned nodes
  const nodesWithIncoming = new Set(edges.map((e: Edge) => e.target));

  nodes.forEach((node) => {
    if (
      node.type !== 'trigger' &&
      !nodesWithIncoming.has(node.id)
    ) {
      issues.push({
        type: 'data_loss',
        severity: 'warning',
        sourceNodeId: node.id,
        message: `Node "${node.data.label}" has no incoming connections and will never receive data`,
      });
    }
  });

  // Step 6: Trace complete data flow paths
  const dataFlowPaths = traceDataFlowPaths(nodes, edges, nodeOutputSchemas);

  return {
    issues,
    nodeOutputSchemas,
    dataFlowPaths,
    isValidDataFlow: issues.filter((i) => i.severity === 'error').length === 0,
  };
}

/**
 * Get compatibility report between two nodes
 */
export function getCompatibilityReport(
  _sourceNode: Node,
  _targetNode: Node,
  sourceOutputs: NodeOutputSchema,
  targetInputs: Record<string, ParameterDefinition>
): {
  compatible: boolean;
  matches: Array<{ output: string; input: string }>;
  mismatches: Array<{ output?: string; input?: string; reason: string }>;
} {
  const matches: Array<{ output: string; input: string }> = [];
  const mismatches: Array<{ output?: string; input?: string; reason: string }> = [];

  // Try to match source outputs to target inputs
  for (const [outputName, outputType] of Object.entries(sourceOutputs)) {
    let foundMatch = false;

    for (const [inputName, inputDef] of Object.entries(targetInputs)) {
      if (isTypeCompatible(outputType, inputDef.type)) {
        matches.push({ output: outputName, input: inputName });
        foundMatch = true;
        break;
      }
    }

    if (!foundMatch && outputType.baseType !== 'any') {
      mismatches.push({
        output: outputName,
        reason: `Type "${getTypeDescription(outputType)}" not compatible with any target input`,
      });
    }
  }

  // Check for required inputs without matches
  for (const [inputName, inputDef] of Object.entries(targetInputs)) {
    if (inputDef.required) {
      const hasMatch = matches.some((m) => m.input === inputName);
      if (!hasMatch) {
        mismatches.push({
          input: inputName,
          reason: `Required parameter has no compatible output from source node`,
        });
      }
    }
  }

  return {
    compatible: mismatches.filter((m) => m.reason.includes('Required')).length === 0,
    matches,
    mismatches,
  };
}

/**
 * Estimate data volume through workflow path
 */
export function estimateDataVolume(
  nodes: Node[],
  _edges: Edge[],
  nodeOutputSchemas: Map<string, NodeOutputSchema>
): Map<string, number> {
  const dataVolumes = new Map<string, number>();

  // Estimate based on node type and output schema
  nodes.forEach((node) => {
    const outputs = nodeOutputSchemas.get(node.id) || {};
    let estimatedSize = 0;

    Object.values(outputs).forEach((outputType) => {
      // Rough estimation: 100 bytes for primitive, 1KB for object, 10KB for array
      switch (outputType.baseType) {
        case 'string':
          estimatedSize += 100;
          break;
        case 'number':
          estimatedSize += 8;
          break;
        case 'boolean':
          estimatedSize += 1;
          break;
        case 'object':
          estimatedSize += 1024;
          break;
        case 'array':
          estimatedSize += 10240;
          break;
        case 'any':
          estimatedSize += 512;
          break;
      }
    });

    dataVolumes.set(node.id, estimatedSize);
  });

  return dataVolumes;
}
