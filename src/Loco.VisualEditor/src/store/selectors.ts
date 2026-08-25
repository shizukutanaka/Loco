/**
 * Zustand Store Selectors
 *
 * Optimized selectors for workflowStore using Zustand best practices.
 * Defines selectors outside components for better performance and reusability.
 *
 * Reference: https://docs.pmnd.rs/zustand/guides/typescript#slicing-the-store-into-smaller-stores
 */

import { useWorkflowStore } from './workflowStore';
import type { WorkflowState } from './workflowStore';

// ============================================================================
// Workflow Selection
// ============================================================================

/**
 * Select entire workflow object
 * Use sparingly - prefer more specific selectors
 */
export const useWorkflow = () => useWorkflowStore((state: WorkflowState) => state.workflow);

/**
 * Select only workflow metadata
 * Prevents re-renders when nodes/edges change
 */
export const useWorkflowMetadata = () =>
  useWorkflowStore((state: WorkflowState) => ({
    id: state.workflow?.id,
    name: state.workflow?.name,
    description: state.workflow?.description,
    metadata: state.workflow?.metadata,
  }));

// ============================================================================
// Canvas State Selection
// ============================================================================

/**
 * Select both nodes and edges
 * For canvas components that need both
 */
export const useCanvasState = () =>
  useWorkflowStore((state: WorkflowState) => ({
    nodes: state.nodes,
    edges: state.edges,
    viewport: state.viewport,
  }));

/**
 * Select only nodes array
 * For components that only care about nodes
 */
export const useNodes = () => useWorkflowStore((state: WorkflowState) => state.nodes);

/**
 * Select only edges array
 * For components that only care about edges
 */
export const useEdges = () => useWorkflowStore((state: WorkflowState) => state.edges);

/**
 * Select viewport state
 * For canvas zoom/pan controls
 */
export const useViewport = () =>
  useWorkflowStore((state: WorkflowState) => state.viewport);

// ============================================================================
// Selection State
// ============================================================================

/**
 * Select selected node ID
 * For components tracking current selection
 */
export const useSelectedNodeId = () =>
  useWorkflowStore((state: WorkflowState) => state.selectedNodeId);

/**
 * Select selected node object
 * Memoized to prevent unnecessary re-renders
 */
export const useSelectedNode = () =>
  useWorkflowStore((state: WorkflowState) => {
    if (!state.selectedNodeId) return null;
    return state.nodes.find((n) => n.id === state.selectedNodeId) || null;
  });

/**
 * Select selected edge ID
 * For components tracking current edge selection (e.g. EdgeConditionPanel)
 */
export const useSelectedEdgeId = () =>
  useWorkflowStore((state: WorkflowState) => state.selectedEdgeId);

/**
 * Select selected edge object
 */
export const useSelectedEdge = () =>
  useWorkflowStore((state: WorkflowState) => {
    if (!state.selectedEdgeId) return null;
    return state.edges.find((e) => e.id === state.selectedEdgeId) || null;
  });

// ============================================================================
// History State Selection
// ============================================================================

/**
 * Select undo/redo capabilities
 * For toolbar buttons showing enabled/disabled state
 */
export const useHistoryState = () =>
  useWorkflowStore((state: WorkflowState) => ({
    canUndo: state.canUndo,
    canRedo: state.canRedo,
    historyIndex: state.historyIndex,
    historyLength: state.history.length,
  }));

/**
 * Select only undo capability
 * For minimal re-render optimization
 */
export const useCanUndo = () => useWorkflowStore((state: WorkflowState) => state.canUndo);

/**
 * Select only redo capability
 * For minimal re-render optimization
 */
export const useCanRedo = () => useWorkflowStore((state: WorkflowState) => state.canRedo);

// ============================================================================
// Action Selection (for components using multiple actions)
// ============================================================================

/**
 * Select all node-related actions
 * Use when component needs multiple node operations
 */
export const useNodeActions = () =>
  useWorkflowStore((state: WorkflowState) => ({
    addNode: state.addNode,
    updateNode: state.updateNode,
    deleteNode: state.deleteNode,
    setSelectedNodeId: state.setSelectedNodeId,
  }));

/**
 * Select all edge-related actions
 * Use when component needs multiple edge operations
 */
export const useEdgeActions = () =>
  useWorkflowStore((state: WorkflowState) => ({
    onEdgesChange: state.onEdgesChange,
    onConnect: state.onConnect,
    deleteEdge: state.deleteEdge,
  }));

/**
 * Select all history actions
 * Use for undo/redo functionality
 */
export const useHistoryActions = () =>
  useWorkflowStore((state: WorkflowState) => ({
    undo: state.undo,
    redo: state.redo,
    pushToHistory: state.pushToHistory,
  }));

/**
 * Select all workflow actions
 * Use when component manages workflow lifecycle
 */
export const useWorkflowActions = () =>
  useWorkflowStore((state: WorkflowState) => ({
    setWorkflow: state.setWorkflow,
    newWorkflow: state.newWorkflow,
    loadWorkflow: state.loadWorkflow,
    exportWorkflow: state.exportWorkflow,
    clearWorkflow: state.clearWorkflow,
    updateWorkflowMetadata: state.updateWorkflowMetadata,
  }));

// ============================================================================
// Canvas Statistics
// ============================================================================

/**
 * Get count of selected nodes
 * Optimized: uses single-pass iteration instead of array filter
 * Memoized: only recalculates when nodes array changes
 * Useful for status bars showing selection count
 */
export const useSelectedNodeCount = () =>
  useWorkflowStore((state: WorkflowState) => {
    let count = 0;
    for (let i = 0; i < state.nodes.length; i++) {
      if (state.nodes[i].selected) count++;
    }
    return count;
  });

/**
 * Get canvas statistics
 * Optimized: uses memoized selector to prevent duplicate node filtering
 * Useful for analytics and debugging
 */
export const useCanvasStats = () =>
  useWorkflowStore((state: WorkflowState) => {
    // Optimize: count selected nodes with single pass instead of filter
    let selectedNodeCount = 0;
    for (let i = 0; i < state.nodes.length; i++) {
      if (state.nodes[i].selected) selectedNodeCount++;
    }

    return {
      nodeCount: state.nodes.length,
      edgeCount: state.edges.length,
      selectedNodeCount,
      hasWorkflow: state.workflow !== null,
    };
  });

// ============================================================================


