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

export type NodeType = 'trigger' | 'action' | 'condition' | 'transform' | 'loop';

export interface NodeData {
  label: string;
  integration?: string;
  config: Record<string, any>;
  description?: string;
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
