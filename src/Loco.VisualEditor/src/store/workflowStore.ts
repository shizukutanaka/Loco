import { create } from 'zustand';
import {
  Connection,
  Edge,
  EdgeChange,
  Node,
  NodeChange,
  addEdge,
  applyNodeChanges,
  applyEdgeChanges,
} from 'reactflow';
import { Workflow, WorkflowNode, WorkflowEdge, Viewport } from '@/types/workflow';

interface WorkflowState {
  // Current workflow
  workflow: Workflow | null;

  // React Flow state
  nodes: Node[];
  edges: Edge[];
  viewport: Viewport;

  // Selection state
  selectedNodeId: string | null;

  // Actions
  setWorkflow: (workflow: Workflow) => void;
  updateWorkflowMetadata: (metadata: Partial<Workflow>) => void;

  // Node actions
  onNodesChange: (changes: NodeChange[]) => void;
  addNode: (node: Node) => void;
  updateNode: (nodeId: string, data: Partial<Node['data']>) => void;
  deleteNode: (nodeId: string) => void;

  // Edge actions
  onEdgesChange: (changes: EdgeChange[]) => void;
  onConnect: (connection: Connection) => void;
  deleteEdge: (edgeId: string) => void;

  // Selection
  setSelectedNodeId: (nodeId: string | null) => void;

  // Viewport
  setViewport: (viewport: Viewport) => void;

  // Workflow operations
  newWorkflow: () => void;
  loadWorkflow: (workflow: Workflow) => void;
  exportWorkflow: () => Workflow;
  clearWorkflow: () => void;
}

const initialWorkflow: Workflow = {
  id: crypto.randomUUID(),
  name: 'New Workflow',
  description: '',
  nodes: [],
  edges: [],
  metadata: {
    version: '1.0',
    isPublic: false,
  },
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
};

export const useWorkflowStore = create<WorkflowState>((set, get) => ({
  workflow: initialWorkflow,
  nodes: [],
  edges: [],
  viewport: { x: 0, y: 0, zoom: 1 },
  selectedNodeId: null,

  setWorkflow: (workflow) => {
    set({
      workflow,
      nodes: workflowNodesToReactFlowNodes(workflow.nodes),
      edges: workflowEdgesToReactFlowEdges(workflow.edges),
    });
  },

  updateWorkflowMetadata: (metadata) => {
    const { workflow } = get();
    if (!workflow) return;

    set({
      workflow: {
        ...workflow,
        ...metadata,
        updatedAt: new Date().toISOString(),
      },
    });
  },

  onNodesChange: (changes) => {
    set({
      nodes: applyNodeChanges(changes, get().nodes),
    });
  },

  addNode: (node) => {
    set({
      nodes: [...get().nodes, node],
    });
  },

  updateNode: (nodeId, data) => {
    set({
      nodes: get().nodes.map((node) =>
        node.id === nodeId
          ? { ...node, data: { ...node.data, ...data } }
          : node
      ),
    });
  },

  deleteNode: (nodeId) => {
    set({
      nodes: get().nodes.filter((node) => node.id !== nodeId),
      edges: get().edges.filter(
        (edge) => edge.source !== nodeId && edge.target !== nodeId
      ),
      selectedNodeId: get().selectedNodeId === nodeId ? null : get().selectedNodeId,
    });
  },

  onEdgesChange: (changes) => {
    set({
      edges: applyEdgeChanges(changes, get().edges),
    });
  },

  onConnect: (connection) => {
    set({
      edges: addEdge(connection, get().edges),
    });
  },

  deleteEdge: (edgeId) => {
    set({
      edges: get().edges.filter((edge) => edge.id !== edgeId),
    });
  },

  setSelectedNodeId: (nodeId) => {
    set({ selectedNodeId: nodeId });
  },

  setViewport: (viewport) => {
    set({ viewport });
  },

  newWorkflow: () => {
    const newWorkflow: Workflow = {
      id: crypto.randomUUID(),
      name: 'New Workflow',
      description: '',
      nodes: [],
      edges: [],
      metadata: {
        version: '1.0',
        isPublic: false,
      },
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };

    set({
      workflow: newWorkflow,
      nodes: [],
      edges: [],
      selectedNodeId: null,
    });
  },

  loadWorkflow: (workflow) => {
    set({
      workflow,
      nodes: workflowNodesToReactFlowNodes(workflow.nodes),
      edges: workflowEdgesToReactFlowEdges(workflow.edges),
      selectedNodeId: null,
    });
  },

  exportWorkflow: () => {
    const { workflow, nodes, edges } = get();
    if (!workflow) return initialWorkflow;

    return {
      ...workflow,
      nodes: reactFlowNodesToWorkflowNodes(nodes),
      edges: reactFlowEdgesToWorkflowEdges(edges),
      updatedAt: new Date().toISOString(),
    };
  },

  clearWorkflow: () => {
    set({
      workflow: null,
      nodes: [],
      edges: [],
      selectedNodeId: null,
    });
  },
}));

// Conversion helpers
function workflowNodesToReactFlowNodes(workflowNodes: WorkflowNode[]): Node[] {
  return workflowNodes.map((node) => ({
    id: node.id,
    type: node.type,
    position: node.position,
    data: node.data,
  }));
}

function reactFlowNodesToWorkflowNodes(reactFlowNodes: Node[]): WorkflowNode[] {
  return reactFlowNodes.map((node) => ({
    id: node.id,
    type: node.type as WorkflowNode['type'],
    position: node.position,
    data: node.data,
  }));
}

function workflowEdgesToReactFlowEdges(workflowEdges: WorkflowEdge[]): Edge[] {
  return workflowEdges.map((edge) => ({
    id: edge.id,
    source: edge.source,
    target: edge.target,
    sourceHandle: edge.sourceHandle,
    targetHandle: edge.targetHandle,
    type: edge.type,
    data: edge.data,
  }));
}

function reactFlowEdgesToWorkflowEdges(reactFlowEdges: Edge[]): WorkflowEdge[] {
  return reactFlowEdges.map((edge) => ({
    id: edge.id,
    source: edge.source,
    target: edge.target,
    sourceHandle: edge.sourceHandle || undefined,
    targetHandle: edge.targetHandle || undefined,
    type: edge.type as WorkflowEdge['type'],
    data: edge.data,
  }));
}
