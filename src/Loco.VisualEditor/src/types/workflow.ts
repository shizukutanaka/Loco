// Core workflow types
export interface Workflow {
  id: string;
  name: string;
  description?: string;
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
  metadata: WorkflowMetadata;
  createdAt: string;
  updatedAt: string;
}

export interface WorkflowNode {
  id: string;
  type: NodeType;
  position: Position;
  data: NodeData;
}

export interface WorkflowEdge {
  id: string;
  source: string;
  target: string;
  sourceHandle?: string;
  targetHandle?: string;
  type?: 'default' | 'conditional';
  data?: EdgeData;
}

export interface Position {
  x: number;
  y: number;
}

export interface WorkflowMetadata {
  version: string;
  author?: string;
  tags?: string[];
  isPublic: boolean;
}

/**
 * Node types the engine dispatches by TYPE (VisualWorkflowEngine falls back to
 * _nodeHandlers[node.Type] when no `${integration}:${action}` handler matches).
 * 'delay' is one of them - the engine has always implemented it, but the editor
 * had no node type for it, so the feature was unreachable.
 */
export type NodeType = 'trigger' | 'action' | 'condition' | 'transform' | 'loop' | 'delay';

export interface NodeData {
  label: string;
  integration?: string;
  config: Record<string, any>;
  description?: string;
  /**
   * ID of the stored connection supplying this node's credentials.
   *
   * A reference, never the secret itself - so an exported workflow JSON is
   * safe to share and the credential can be rotated without touching the
   * workflow. The server resolves it and initializes the connector at
   * execution time (see docs/agent-instructions/INSTRUCTIONS_OPUS.md, O-6).
   *
   * Never put credential values in `config`.
   */
  credentialId?: string;
}

export interface EdgeData {
  condition?: string;
  label?: string;
}

// Integration types
export interface Integration {
  id: string;
  name: string;
  category: IntegrationCategory;
  icon: string;
  description: string;
  actions: IntegrationAction[];
  triggers?: IntegrationTrigger[];
}

export type IntegrationCategory =
  | 'communication'
  | 'database'
  | 'cloud'
  | 'ai'
  | 'web'
  | 'file'
  | 'transform';

export interface IntegrationAction {
  id: string;
  name: string;
  description: string;
  parameters: ActionParameter[];
}

export interface IntegrationTrigger {
  id: string;
  name: string;
  description: string;
  parameters: ActionParameter[];
}

export interface ActionParameter {
  name: string;
  type: ParameterType;
  required: boolean;
  description: string;
  defaultValue?: any;
  options?: ParameterOption[];
}

export type ParameterType =
  | 'string'
  | 'number'
  | 'boolean'
  | 'select'
  | 'multiselect'
  | 'json'
  | 'code';

export interface ParameterOption {
  label: string;
  value: string | number;
}

// Viewport state
export interface Viewport {
  x: number;
  y: number;
  zoom: number;
}
