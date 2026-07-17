import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { EdgeConditionPanel } from './EdgeConditionPanel';
import { useWorkflowStore } from '@/store/workflowStore';

/**
 * Component tests for EdgeConditionPanel. These guard two bugs found by
 * adversarial self-review of the first version:
 *  1. Choosing "Custom expression…" snapped straight back to the default
 *     option and the expression input never appeared (the select value was
 *     derived purely from stored data, and "custom just chosen" stores '').
 *  2. The unset condition was labeled "Always", but the engine
 *     (ShouldFollowConnection) treats null/'default'/'success' as
 *     "only on success" - true unconditional routing needs a distinct value.
 */
describe('EdgeConditionPanel', () => {
  beforeEach(() => {
    useWorkflowStore.setState({
      nodes: [
        { id: 'n1', type: 'action', position: { x: 0, y: 0 }, data: { label: 'A' } },
        { id: 'n2', type: 'action', position: { x: 0, y: 0 }, data: { label: 'B' } },
      ],
      edges: [{ id: 'e1', source: 'n1', target: 'n2', data: {} }],
      selectedNodeId: null,
      selectedEdgeId: null,
    });
  });

  const selectEdge = (id = 'e1') => {
    act(() => {
      useWorkflowStore.getState().setSelectedEdgeId(id);
    });
  };

  const getCondition = (id = 'e1') =>
    useWorkflowStore.getState().edges.find((e) => e.id === id)?.data?.condition;

  const getSelect = () =>
    screen.getByLabelText('Run this connection') as HTMLSelectElement;

  const queryCustomInput = () =>
    screen.queryByLabelText('Custom condition expression') as HTMLInputElement | null;

  it('renders nothing when no edge is selected', () => {
    const { container } = render(<EdgeConditionPanel />);
    expect(container.childElementCount).toBe(0);
  });

  it('shows the default option for an edge with no condition', () => {
    selectEdge();
    render(<EdgeConditionPanel />);

    expect(getSelect().value).toBe('');
  });

  it('shows the default option for a stored "success" condition (same engine behavior)', () => {
    useWorkflowStore.setState({
      edges: [{ id: 'e1', source: 'n1', target: 'n2', data: { condition: 'success' } }],
    });
    selectEdge();
    render(<EdgeConditionPanel />);

    expect(getSelect().value).toBe('');
  });

  it('stores the literal "always" for unconditional routing', () => {
    selectEdge();
    render(<EdgeConditionPanel />);

    fireEvent.change(getSelect(), { target: { value: 'always' } });

    expect(getCondition()).toBe('always');
  });

  it('stores undefined (not empty string) when switching back to the default', () => {
    useWorkflowStore.setState({
      edges: [{ id: 'e1', source: 'n1', target: 'n2', data: { condition: 'error' } }],
    });
    selectEdge();
    render(<EdgeConditionPanel />);

    fireEvent.change(getSelect(), { target: { value: '' } });

    expect(getCondition()).toBeUndefined();
  });

  it('keeps "Custom expression…" selected and shows the input when chosen', () => {
    selectEdge();
    render(<EdgeConditionPanel />);

    fireEvent.change(getSelect(), { target: { value: 'custom' } });

    expect(getSelect().value).toBe('custom');
    expect(queryCustomInput()).not.toBeNull();
    // Nothing typed yet - the stored condition must not have been clobbered
    expect(getCondition()).toBeUndefined();
  });

  it('stores a typed custom expression, and clearing it falls back to undefined', () => {
    selectEdge();
    render(<EdgeConditionPanel />);

    fireEvent.change(getSelect(), { target: { value: 'custom' } });
    const input = queryCustomInput()!;

    fireEvent.change(input, { target: { value: 'output.status === 200' } });
    expect(getCondition()).toBe('output.status === 200');

    fireEvent.change(input, { target: { value: '' } });
    // '' would hit the engine's "anything else" branch and mean "always" -
    // it must be stored as undefined instead
    expect(getCondition()).toBeUndefined();
  });

  it('shows an existing custom value as "custom" with the expression in the input', () => {
    useWorkflowStore.setState({
      edges: [
        { id: 'e1', source: 'n1', target: 'n2', data: { condition: 'output.ok' } },
      ],
    });
    selectEdge();
    render(<EdgeConditionPanel />);

    expect(getSelect().value).toBe('custom');
    expect(queryCustomInput()!.value).toBe('output.ok');
  });

  it('resets custom mode when a different edge is selected', () => {
    useWorkflowStore.setState({
      edges: [
        { id: 'e1', source: 'n1', target: 'n2', data: {} },
        { id: 'e2', source: 'n2', target: 'n1', data: { condition: 'error' } },
      ],
    });
    selectEdge('e1');
    render(<EdgeConditionPanel />);

    fireEvent.change(getSelect(), { target: { value: 'custom' } });
    expect(queryCustomInput()).not.toBeNull();

    selectEdge('e2');
    // The new edge has a known value - the custom input must be gone and the
    // select must show that value, not a stale custom mode
    expect(getSelect().value).toBe('error');
    expect(queryCustomInput()).toBeNull();
  });
});
