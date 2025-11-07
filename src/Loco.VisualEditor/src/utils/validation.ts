import { z } from 'zod';
import { Workflow, WorkflowNode, WorkflowEdge } from '@/types/workflow';
import { getIntegrationById } from '@/data/integrations';

// Validation result types
export interface ValidationResult {
  isValid: boolean;
  errors: ValidationError[];
  warnings: ValidationWarning[];
}

export interface ValidationError {
  type: 'error';
  nodeId?: string;
  edgeId?: string;
  field?: string;
  message: string;
}

export interface ValidationWarning {
  type: 'warning';
  nodeId?: string;
  message: string;
}

// Zod schemas for workflow validation
const positionSchema = z.object({
  x: z.number(),
  y: z.number(),
});

const nodeDataSchema = z.object({
  label: z.string().min(1, 'Node label is required'),
  integration: z.string().optional(),
  config: z.record(z.any()),
  description: z.string().optional(),
});

const workflowNodeSchema = z.object({
  id: z.string().min(1, 'Node ID is required'),
  type: z.enum(['trigger', 'action', 'condition', 'transform', 'loop']),
  position: positionSchema,
  data: nodeDataSchema,
});

const workflowEdgeSchema = z.object({
  id: z.string().min(1, 'Edge ID is required'),
  source: z.string().min(1, 'Edge source is required'),
  target: z.string().min(1, 'Edge target is required'),
  sourceHandle: z.string().optional(),
  targetHandle: z.string().optional(),
  type: z.enum(['default', 'conditional']).optional(),
  data: z.any().optional(),
});

const workflowMetadataSchema = z.object({
  version: z.string(),
  author: z.string().optional(),
  tags: z.array(z.string()).optional(),
  isPublic: z.boolean(),
});

const workflowSchema = z.object({
  id: z.string().min(1, 'Workflow ID is required'),
  name: z.string().min(1, 'Workflow name is required'),
  description: z.string().optional(),
  nodes: z.array(workflowNodeSchema),
  edges: z.array(workflowEdgeSchema),
  metadata: workflowMetadataSchema,
  createdAt: z.string(),
  updatedAt: z.string(),
});

/**
 * Validate workflow schema using Zod
 */
export function validateWorkflowSchema(workflow: Workflow): ValidationResult {
  const errors: ValidationError[] = [];
  const warnings: ValidationWarning[] = [];

  try {
    workflowSchema.parse(workflow);
  } catch (error) {
    if (error instanceof z.ZodError) {
      error.errors.forEach((err) => {
        errors.push({
          type: 'error',
          message: `${err.path.join('.')}: ${err.message}`,
        });
      });
    }
  }

  return {
    isValid: errors.length === 0,
    errors,
    warnings,
  };
}

/**
 * Validate workflow connections (edges)
 */
export function validateConnections(
  nodes: WorkflowNode[],
  edges: WorkflowEdge[]
): ValidationResult {
  const errors: ValidationError[] = [];
  const warnings: ValidationWarning[] = [];
  const nodeIds = new Set(nodes.map((n) => n.id));

  // Check for invalid edge references
  edges.forEach((edge) => {
    if (!nodeIds.has(edge.source)) {
      errors.push({
        type: 'error',
        edgeId: edge.id,
        message: `Edge references non-existent source node: ${edge.source}`,
      });
    }
    if (!nodeIds.has(edge.target)) {
      errors.push({
        type: 'error',
        edgeId: edge.id,
        message: `Edge references non-existent target node: ${edge.target}`,
      });
    }
  });

  // Check for orphaned nodes (except triggers)
  const connectedNodes = new Set<string>();
  edges.forEach((edge) => {
    connectedNodes.add(edge.source);
    connectedNodes.add(edge.target);
  });

  nodes.forEach((node) => {
    if (node.type !== 'trigger' && !connectedNodes.has(node.id)) {
      warnings.push({
        type: 'warning',
        nodeId: node.id,
        message: `Node "${node.data.label}" is not connected to any other nodes`,
      });
    }
  });

  // Check for cycles in workflow (warning only)
  const hasCycle = detectCycle(nodes, edges);
  if (hasCycle) {
    warnings.push({
      type: 'warning',
      message: 'Workflow contains a cycle, which may cause infinite loops',
    });
  }

  return {
    isValid: errors.length === 0,
    errors,
    warnings,
  };
}

/**
 * Validate node configuration
 */
export function validateNodeConfiguration(node: WorkflowNode): ValidationResult {
  const errors: ValidationError[] = [];
  const warnings: ValidationWarning[] = [];

  // Check if integration exists
  if (node.data.integration) {
    const integration = getIntegrationById(node.data.integration);
    if (!integration) {
      errors.push({
        type: 'error',
        nodeId: node.id,
        message: `Integration "${node.data.integration}" not found`,
      });
      return { isValid: false, errors, warnings };
    }

    // Check if action is selected
    const selectedAction = node.data.config?.action;
    if (integration.actions && integration.actions.length > 0) {
      if (!selectedAction) {
        errors.push({
          type: 'error',
          nodeId: node.id,
          field: 'action',
          message: 'Action is required',
        });
      } else {
        const action = integration.actions.find((a) => a.id === selectedAction);
        if (!action) {
          errors.push({
            type: 'error',
            nodeId: node.id,
            field: 'action',
            message: `Action "${selectedAction}" not found`,
          });
        } else {
          // Validate required parameters
          action.parameters.forEach((param) => {
            if (param.required) {
              const value = node.data.config?.parameters?.[param.name];
              if (!value || value === '') {
                errors.push({
                  type: 'error',
                  nodeId: node.id,
                  field: param.name,
                  message: `Required parameter "${param.name}" is missing`,
                });
              }
            }
          });
        }
      }
    }
  }

  // Validate condition nodes
  if (node.type === 'condition') {
    const condition = node.data.config?.condition;
    if (!condition || condition.trim() === '') {
      errors.push({
        type: 'error',
        nodeId: node.id,
        field: 'condition',
        message: 'Condition expression is required',
      });
    }
  }

  // Validate transform nodes
  if (node.type === 'transform') {
    const code = node.data.config?.code;
    if (!code || code.trim() === '') {
      errors.push({
        type: 'error',
        nodeId: node.id,
        field: 'code',
        message: 'Transform code is required',
      });
    }
  }

  return {
    isValid: errors.length === 0,
    errors,
    warnings,
  };
}

/**
 * Validate entire workflow
 */
export function validateWorkflow(workflow: Workflow): ValidationResult {
  const allErrors: ValidationError[] = [];
  const allWarnings: ValidationWarning[] = [];

  // Schema validation
  const schemaResult = validateWorkflowSchema(workflow);
  allErrors.push(...schemaResult.errors);
  allWarnings.push(...schemaResult.warnings);

  // Connection validation
  const connectionResult = validateConnections(workflow.nodes, workflow.edges);
  allErrors.push(...connectionResult.errors);
  allWarnings.push(...connectionResult.warnings);

  // Node configuration validation
  workflow.nodes.forEach((node) => {
    const nodeResult = validateNodeConfiguration(node);
    allErrors.push(...nodeResult.errors);
    allWarnings.push(...nodeResult.warnings);
  });

  // Check for at least one trigger
  const triggerCount = workflow.nodes.filter((n) => n.type === 'trigger').length;
  if (triggerCount === 0) {
    allErrors.push({
      type: 'error',
      message: 'Workflow must have at least one trigger node',
    });
  }

  // Check for at least one action
  const actionCount = workflow.nodes.filter(
    (n) => n.type === 'action' || n.type === 'transform'
  ).length;
  if (actionCount === 0) {
    allWarnings.push({
      type: 'warning',
      message: 'Workflow has no action nodes',
    });
  }

  return {
    isValid: allErrors.length === 0,
    errors: allErrors,
    warnings: allWarnings,
  };
}

/**
 * Detect cycles in workflow graph
 */
function detectCycle(nodes: WorkflowNode[], edges: WorkflowEdge[]): boolean {
  const graph = new Map<string, string[]>();
  const visited = new Set<string>();
  const recursionStack = new Set<string>();

  // Build adjacency list
  nodes.forEach((node) => graph.set(node.id, []));
  edges.forEach((edge) => {
    const neighbors = graph.get(edge.source) || [];
    neighbors.push(edge.target);
    graph.set(edge.source, neighbors);
  });

  // DFS to detect cycle
  function hasCycleDFS(nodeId: string): boolean {
    visited.add(nodeId);
    recursionStack.add(nodeId);

    const neighbors = graph.get(nodeId) || [];
    for (const neighbor of neighbors) {
      if (!visited.has(neighbor)) {
        if (hasCycleDFS(neighbor)) {
          return true;
        }
      } else if (recursionStack.has(neighbor)) {
        return true;
      }
    }

    recursionStack.delete(nodeId);
    return false;
  }

  // Check each node
  for (const node of nodes) {
    if (!visited.has(node.id)) {
      if (hasCycleDFS(node.id)) {
        return true;
      }
    }
  }

  return false;
}

/**
 * Get validation error message for display
 */
export function formatValidationError(error: ValidationError): string {
  let message = error.message;
  if (error.nodeId) {
    message = `Node ${error.nodeId}: ${message}`;
  }
  if (error.edgeId) {
    message = `Edge ${error.edgeId}: ${message}`;
  }
  if (error.field) {
    message = `${error.field}: ${message}`;
  }
  return message;
}

/**
 * Get validation warning message for display
 */
export function formatValidationWarning(warning: ValidationWarning): string {
  let message = warning.message;
  if (warning.nodeId) {
    message = `Node ${warning.nodeId}: ${message}`;
  }
  return message;
}
