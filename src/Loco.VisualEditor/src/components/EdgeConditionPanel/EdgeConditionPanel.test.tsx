import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { EdgeConditionPanel } from './EdgeConditionPanel';
import { useWorkflowStore } from '@/store/workflowStore';

/**
 * Component tests for EdgeConditionPanel.
 *
 * The panel's job is to offer exactly what the engine can act on. It used to
 * offer more: a free-text "Custom expression…" that the engine had no evaluator
 * for and followed unconditionally, so a typed expression fired on every run
 * whatever it said. The engine refuses those edges now, the option is gone, and
 * these pin both halves - the reduced option list, and what the panel does with
 * an expression a previous build already saved.
 *
 * Also pinned, from an earlier review: an unset condition is NOT "always". The
 * engine treats null/'default'/'success' as "only on success", so unconditional
 * routing needs the distinct 'always' value.
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

  /** Put a condition on the edge, then select it - the panel reads from the store. */
  const selectEdgeWithCondition = (condition: string, id = 'e1') => {
    act(() => {
      useWorkflowStore.setState({
        edges: useWorkflowStore
          .getState()
          .edges.map((e) => (e.id === id ? { ...e, data: { ...e.data, condition } } : e)),
      });
      useWorkflowStore.getState().setSelectedEdgeId(id);
    });
  };

  const getCondition = (id = 'e1') =>
    useWorkflowStore.getState().edges.find((e) => e.id === id)?.data?.condition;

  const getSelect = () =>
    screen.getByLabelText('Run this connection') as HTMLSelectElement;

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

  it('offers only the conditions the engine can evaluate', () => {
    // The dropdown used to include "Custom expression…". The engine has no
    // expression evaluator: it followed any unrecognised value unconditionally,
    // so a typed expression fired on every run whatever it said. The engine now
    // refuses such an edge, and the option that produced them is gone.
    selectEdge();
    render(<EdgeConditionPanel />);

    const values = Array.from(getSelect().options).map((o) => o.value);
    expect(values).toEqual(['', 'error', 'always']);
    expect(values).not.toContain('custom');
  });

  it('flags a custom expression saved by an older build instead of hiding it', () => {
    // Such an edge still opens - the workflow is not corrupt - but the panel
    // has to say the engine will refuse it rather than show a blank default.
    selectEdgeWithCondition('output.status === 200');
    render(<EdgeConditionPanel />);

    const warning = screen.getByRole('alert');
    expect(warning.textContent).toContain('output.status === 200');
    expect(warning.textContent).toMatch(/refuses/i);
  });

  it('leaves the stored expression alone until the user picks a supported option', () => {
    selectEdgeWithCondition('output.status === 200');
    render(<EdgeConditionPanel />);

    expect(getCondition()).toBe('output.status === 200');

    fireEvent.change(getSelect(), { target: { value: 'error' } });

    expect(getCondition()).toBe('error');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('shows no warning for an edge the engine understands', () => {
    selectEdgeWithCondition('always');
    render(<EdgeConditionPanel />);

    expect(screen.queryByRole('alert')).toBeNull();
    expect(getSelect().value).toBe('always');
  });
});
