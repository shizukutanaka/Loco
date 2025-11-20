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
import { getAutoLayoutedNodes } from '@/utils/autoLayout';
import { deepClone } from '@/utils/deepClone';
import { deferHistorySnapshot } from '@/utils/deferHistorySnapshot';
import { MAX_HISTORY_SIZE } from '@/utils/constants';

// History state for undo/redo
interface HistoryState {
  nodes: Node[];
  edges: Edge[];
}

interface WorkflowState {
  // Current workflow
  workflow: Workflow | null;

  // React Flow state
  nodes: Node[];
  edges: Edge[];
  viewport: Viewport;

  // Selection state
  selectedNodeId: string | null;

  // History state for undo/redo
  history: HistoryState[];
  historyIndex: number;
  canUndo: boolean;
  canRedo: boolean;

  // Actions
  setWorkflow: (workflow: Workflow) => void;
  updateWorkflowMetadata: (metadata: Partial<Workflow>) => void;

  // History actions
  undo: () => boolean;
  redo: () => boolean;
  pushToHistory: () => void;

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

  // Layout
  autoLayout: (direction?: 'TB' | 'BT' | 'LR' | 'RL') => void;

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
  history: [],
  historyIndex: -1,
  canUndo: false,
  canRedo: false,

  setWorkflow: (workflow) => {
    set({
      workflow,
      nodes: workflowNodesToReactFlowNodes(workflow.nodes),
      edges: workflowEdgesToReactFlowEdges(workflow.edges),
      history: [],
      historyIndex: -1,
      canUndo: false,
      canRedo: false,
    });
  },

  pushToHistory: () => {
    const { nodes, edges, history, historyIndex } = get();

    // Remove any future history if we're not at the end
    const newHistory = history.slice(0, historyIndex + 1);

    // Add current state to history
    newHistory.push({ nodes: deepClone(nodes), edges: deepClone(edges) });

    // Limit history size
    if (newHistory.length > MAX_HISTORY_SIZE) {
      newHistory.shift();
    }

    set({
      history: newHistory,
      historyIndex: newHistory.length - 1,
      canUndo: newHistory.length > 1,
      canRedo: false,
    });
  },

  undo: () => {
    const { history, historyIndex } = get();
    if (historyIndex <= 0) return false;

    const newIndex = historyIndex - 1;
    const state = history[newIndex];

    set({
      nodes: deepClone(state.nodes),
      edges: deepClone(state.edges),
      historyIndex: newIndex,
      canUndo: newIndex > 0,
      canRedo: true,
    });

    return true;
  },

  redo: () => {
    const { history, historyIndex } = get();
    if (historyIndex >= history.length - 1) return false;

    const newIndex = historyIndex + 1;
    const state = history[newIndex];

    set({
      nodes: deepClone(state.nodes),
      edges: deepClone(state.edges),
      historyIndex: newIndex,
      canUndo: true,
      canRedo: newIndex < history.length - 1,
    });

    return true;
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
    // Only push to history for certain change types (not selection or dimensions)
    const shouldPushHistory = changes.some(
      (change) => change.type === 'remove' || change.type === 'position' || change.type === 'add'
    );

    set({
      nodes: applyNodeChanges(changes, get().nodes),
    });

    if (shouldPushHistory) {
      deferHistorySnapshot(() => get().pushToHistory());
    }
  },

  addNode: (node) => {
    set({
      nodes: [...get().nodes, node],
    });
    deferHistorySnapshot(() => get().pushToHistory());
  },

  updateNode: (nodeId, data) => {
    set({
      nodes: get().nodes.map((node) =>
        node.id === nodeId
          ? { ...node, data: { ...node.data, ...data } }
          : node
      ),
    });
    deferHistorySnapshot(() => get().pushToHistory());
  },

  deleteNode: (nodeId) => {
    set({
      nodes: get().nodes.filter((node) => node.id !== nodeId),
      edges: get().edges.filter(
        (edge) => edge.source !== nodeId && edge.target !== nodeId
      ),
      selectedNodeId: get().selectedNodeId === nodeId ? null : get().selectedNodeId,
    });
    deferHistorySnapshot(() => get().pushToHistory());
  },

  onEdgesChange: (changes) => {
    const shouldPushHistory = changes.some(
      (change) => change.type === 'remove' || change.type === 'add'
    );

    set({
      edges: applyEdgeChanges(changes, get().edges),
    });

    if (shouldPushHistory) {
      deferHistorySnapshot(() => get().pushToHistory());
    }
  },

  onConnect: (connection) => {
    set({
      edges: addEdge(connection, get().edges),
    });
    deferHistorySnapshot(() => get().pushToHistory());
  },

  deleteEdge: (edgeId) => {
    set({
      edges: get().edges.filter((edge) => edge.id !== edgeId),
    });
    deferHistorySnapshot(() => get().pushToHistory());
  },

  setSelectedNodeId: (nodeId) => {
    set({ selectedNodeId: nodeId });
  },

  setViewport: (viewport) => {
    set({ viewport });
  },

  autoLayout: (direction = 'TB') => {
    const { nodes, edges } = get();
    if (nodes.length === 0) return;

    const layoutedNodes = getAutoLayoutedNodes(nodes, edges, {
      direction,
      nodeSpacing: 50,
      rankSpacing: 100,
    });

    set({ nodes: layoutedNodes });
    deferHistorySnapshot(() => get().pushToHistory());
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
      history: [],
      historyIndex: -1,
      canUndo: false,
      canRedo: false,
    });
    // Initialize history with loaded state
    deferHistorySnapshot(() => get().pushToHistory());
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
