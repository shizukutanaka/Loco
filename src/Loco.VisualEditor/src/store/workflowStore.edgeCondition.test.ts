import { describe, it, expect, beforeEach } from 'vitest';
import { useWorkflowStore } from './workflowStore';

/**
 * Tests for edge selection + condition editing (EdgeConditionPanel's backing
 * store logic). There was previously no UI path to set a connection's
 * success/error routing at all - VisualWorkflowEngine.ShouldFollowConnection
 * has always supported it, but only hand-edited exported JSON could reach it.
 */
describe('workflowStore edge selection and condition', () => {
  beforeEach(() => {
    useWorkflowStore.setState({
      nodes: [
        { id: 'n1', type: 'action', position: { x: 0, y: 0 }, data: { label: 'A' } },
        { id: 'n2', type: 'action', position: { x: 0, y: 0 }, data: { label: 'B' } },
      ],
      edges: [{ id: 'e1', source: 'n1', target: 'n2', data: {} }],
      selectedNodeId: null,
      selectedEdgeId: null,
      history: [],
      historyIndex: -1,
      canUndo: false,
      canRedo: false,
    });
  });

  it('setSelectedEdgeId selects an edge and clears node selection', () => {
    useWorkflowStore.getState().setSelectedNodeId('n1');
    useWorkflowStore.getState().setSelectedEdgeId('e1');

    const state = useWorkflowStore.getState();
    expect(state.selectedEdgeId).toBe('e1');
    expect(state.selectedNodeId).toBeNull();
  });

  it('setSelectedNodeId selects a node and clears edge selection', () => {
    useWorkflowStore.getState().setSelectedEdgeId('e1');
    useWorkflowStore.getState().setSelectedNodeId('n1');

    const state = useWorkflowStore.getState();
    expect(state.selectedNodeId).toBe('n1');
    expect(state.selectedEdgeId).toBeNull();
  });

  it('updateEdgeData merges into the existing edge data without replacing it', () => {
    useWorkflowStore.setState({
      edges: [{ id: 'e1', source: 'n1', target: 'n2', data: { label: 'keep me' } }],
    });

    useWorkflowStore.getState().updateEdgeData('e1', { condition: 'error' });

    const edge = useWorkflowStore.getState().edges.find((e) => e.id === 'e1');
    expect(edge?.data).toEqual({ label: 'keep me', condition: 'error' });
  });

  it('updateEdgeData only affects the targeted edge', () => {
    useWorkflowStore.setState({
      edges: [
        { id: 'e1', source: 'n1', target: 'n2', data: {} },
        { id: 'e2', source: 'n2', target: 'n1', data: {} },
      ],
    });

    useWorkflowStore.getState().updateEdgeData('e1', { condition: 'success' });

    const edges = useWorkflowStore.getState().edges;
    expect(edges.find((e) => e.id === 'e1')?.data?.condition).toBe('success');
    expect(edges.find((e) => e.id === 'e2')?.data?.condition).toBeUndefined();
  });

  it('deleteEdge clears selectedEdgeId when the deleted edge was selected', () => {
    useWorkflowStore.getState().setSelectedEdgeId('e1');
    useWorkflowStore.getState().deleteEdge('e1');

    const state = useWorkflowStore.getState();
    expect(state.selectedEdgeId).toBeNull();
    expect(state.edges).toHaveLength(0);
  });

  it('deleteEdge leaves selectedEdgeId alone when a different edge is deleted', () => {
    useWorkflowStore.setState({
      edges: [
        { id: 'e1', source: 'n1', target: 'n2', data: {} },
        { id: 'e2', source: 'n2', target: 'n1', data: {} },
      ],
    });
    useWorkflowStore.getState().setSelectedEdgeId('e1');

    useWorkflowStore.getState().deleteEdge('e2');

    expect(useWorkflowStore.getState().selectedEdgeId).toBe('e1');
  });

  it('deleteNode cascade clears selectedEdgeId when it removes the selected edge', () => {
    useWorkflowStore.getState().setSelectedEdgeId('e1');

    // e1 connects n1 -> n2; deleting n1 cascade-deletes e1
    useWorkflowStore.getState().deleteNode('n1');

    const state = useWorkflowStore.getState();
    expect(state.edges).toHaveLength(0);
    expect(state.selectedEdgeId).toBeNull();
  });

  it('deleteNode keeps selectedEdgeId when the selected edge is unaffected', () => {
    useWorkflowStore.setState({
      nodes: [
        { id: 'n1', type: 'action', position: { x: 0, y: 0 }, data: { label: 'A' } },
        { id: 'n2', type: 'action', position: { x: 0, y: 0 }, data: { label: 'B' } },
        { id: 'n3', type: 'action', position: { x: 0, y: 0 }, data: { label: 'C' } },
      ],
      edges: [
        { id: 'e1', source: 'n1', target: 'n2', data: {} },
        { id: 'e2', source: 'n2', target: 'n3', data: {} },
      ],
    });
    useWorkflowStore.getState().setSelectedEdgeId('e2');

    useWorkflowStore.getState().deleteNode('n1');

    const state = useWorkflowStore.getState();
    expect(state.edges.map((e) => e.id)).toEqual(['e2']);
    expect(state.selectedEdgeId).toBe('e2');
  });

  it('onEdgesChange remove clears selectedEdgeId when it removes the selected edge', () => {
    useWorkflowStore.getState().setSelectedEdgeId('e1');

    // React Flow removes edges through onEdgesChange (e.g. Delete key),
    // bypassing deleteEdge entirely
    useWorkflowStore.getState().onEdgesChange([{ type: 'remove', id: 'e1' }]);

    const state = useWorkflowStore.getState();
    expect(state.edges).toHaveLength(0);
    expect(state.selectedEdgeId).toBeNull();
  });

  it('onEdgesChange keeps selectedEdgeId when a different edge is removed', () => {
    useWorkflowStore.setState({
      edges: [
        { id: 'e1', source: 'n1', target: 'n2', data: {} },
        { id: 'e2', source: 'n2', target: 'n1', data: {} },
      ],
    });
    useWorkflowStore.getState().setSelectedEdgeId('e1');

    useWorkflowStore.getState().onEdgesChange([{ type: 'remove', id: 'e2' }]);

    expect(useWorkflowStore.getState().selectedEdgeId).toBe('e1');
  });
});
