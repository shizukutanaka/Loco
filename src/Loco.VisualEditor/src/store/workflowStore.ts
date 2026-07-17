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
import { Workflow, WorkflowNode, WorkflowEdge, EdgeData, Viewport } from '@/types/workflow';
import { getAutoLayoutedNodes } from '@/utils/autoLayout';
import { createOptimizedHistorySnapshot } from '@/utils/structuralSharing';
import { deferHistorySnapshot } from '@/utils/deferHistorySnapshot';
import { MAX_HISTORY_SIZE } from '@/utils/constants';

// History state for undo/redo
interface HistoryState {
  nodes: Node[];
  edges: Edge[];
}

export interface WorkflowState {
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
  updateEdgeData: (edgeId: string, data: Partial<EdgeData>) => void;

  // Selection
  selectedEdgeId: string | null;
  setSelectedNodeId: (nodeId: string | null) => void;
  setSelectedEdgeId: (edgeId: string | null) => void;

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
  selectedEdgeId: null,
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

    // Get previous state for structural sharing comparison
    const previousState = historyIndex >= 0 ? history[historyIndex] : undefined;

    // Add current state to history with optimized cloning
    // Only creates new array references if content has actually changed
    const snapshot = createOptimizedHistorySnapshot(
      nodes,
      edges,
      previousState?.nodes,
      previousState?.edges
    );
    newHistory.push(snapshot);

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

    // No need to deep clone - history snapshots are already immutable
    // Reusing references from history is safe due to structural sharing
    set({
      nodes: state.nodes,
      edges: state.edges,
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

    // No need to deep clone - history snapshots are already immutable
    // Reusing references from history is safe due to structural sharing
    set({
      nodes: state.nodes,
      edges: state.edges,
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
    const remainingEdges = get().edges.filter(
      (edge) => edge.source !== nodeId && edge.target !== nodeId
    );
    const selectedEdgeId = get().selectedEdgeId;
    set({
      nodes: get().nodes.filter((node) => node.id !== nodeId),
      edges: remainingEdges,
      selectedNodeId: get().selectedNodeId === nodeId ? null : get().selectedNodeId,
      // The cascade above can also remove the currently selected edge
      selectedEdgeId:
        selectedEdgeId && !remainingEdges.some((edge) => edge.id === selectedEdgeId)
          ? null
          : selectedEdgeId,
    });
    deferHistorySnapshot(() => get().pushToHistory());
  },

  onEdgesChange: (changes) => {
    const shouldPushHistory = changes.some(
      (change) => change.type === 'remove' || change.type === 'add'
    );

    const newEdges = applyEdgeChanges(changes, get().edges);
    const selectedEdgeId = get().selectedEdgeId;
    set({
      edges: newEdges,
      // React Flow can remove edges through here (e.g. Delete key), bypassing
      // deleteEdge - don't leave the selection pointing at a removed edge
      selectedEdgeId:
        selectedEdgeId && !newEdges.some((edge) => edge.id === selectedEdgeId)
          ? null
          : selectedEdgeId,
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
      selectedEdgeId: get().selectedEdgeId === edgeId ? null : get().selectedEdgeId,
    });
    deferHistorySnapshot(() => get().pushToHistory());
  },

  // Sets an edge's condition ("success" | "error" | a custom expression | undefined
  // for "always"), matching VisualWorkflowEngine.ShouldFollowConnection's
  // interpretation (Core/Workflows/VisualWorkflowEngine.cs). Previously there was no
  // UI path to set this at all - the engine's error/success branch routing was
  // reachable only by hand-editing exported JSON.
  updateEdgeData: (edgeId, data) => {
    set({
      edges: get().edges.map((edge) =>
        edge.id === edgeId
          ? { ...edge, data: { ...(edge.data as EdgeData | undefined), ...data } }
          : edge
      ),
    });
    deferHistorySnapshot(() => get().pushToHistory());
  },

  setSelectedNodeId: (nodeId) => {
    set({ selectedNodeId: nodeId, selectedEdgeId: nodeId ? null : get().selectedEdgeId });
  },

  setSelectedEdgeId: (edgeId) => {
    set({ selectedEdgeId: edgeId, selectedNodeId: edgeId ? null : get().selectedNodeId });
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
      selectedEdgeId: null,
    });
  },

  loadWorkflow: (workflow) => {
    set({
      workflow,
      nodes: workflowNodesToReactFlowNodes(workflow.nodes),
      edges: workflowEdgesToReactFlowEdges(workflow.edges),
      selectedNodeId: null,
      selectedEdgeId: null,
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
      selectedEdgeId: null,
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
