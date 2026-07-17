import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import { PropertyPanel } from './PropertyPanel';
import { useWorkflowStore } from '@/store/workflowStore';

/**
 * Regression tests for PropertyPanel's hook ordering. Its useMemo calls
 * previously sat AFTER the `if (!selectedNode) return ...` early return, so
 * the hook count changed whenever a node was (de)selected and React crashed
 * with "rendered more hooks than during the previous render". The transition
 * tests below re-render across that exact boundary and would fail if the
 * hooks ever move back below the guard.
 */
describe('PropertyPanel', () => {
  beforeEach(() => {
    useWorkflowStore.setState({
      nodes: [
        {
          id: 'n1',
          type: 'action',
          position: { x: 10, y: 20 },
          data: { label: 'My Node', config: {} },
        },
      ],
      edges: [],
      selectedNodeId: null,
      selectedEdgeId: null,
    });
  });

  it('shows the placeholder when no node is selected', () => {
    render(<PropertyPanel />);
    expect(screen.getByText('Select a node to configure')).toBeTruthy();
  });

  it('survives the none-selected -> selected transition (hook-order guard)', () => {
    render(<PropertyPanel />);

    act(() => {
      useWorkflowStore.getState().setSelectedNodeId('n1');
    });

    expect(screen.getByText('Node Properties')).toBeTruthy();
    expect((screen.getByLabelText('Node Label') as HTMLInputElement).value).toBe('My Node');
    expect(screen.getByText('n1')).toBeTruthy();
  });

  it('survives the selected -> none-selected transition back to the placeholder', () => {
    useWorkflowStore.setState({ selectedNodeId: 'n1' });
    render(<PropertyPanel />);
    expect(screen.getByText('Node Properties')).toBeTruthy();

    act(() => {
      useWorkflowStore.getState().setSelectedNodeId(null);
    });

    expect(screen.getByText('Select a node to configure')).toBeTruthy();
  });

  it('switches between two selected nodes without stale form state', () => {
    useWorkflowStore.setState({
      nodes: [
        { id: 'n1', type: 'action', position: { x: 0, y: 0 }, data: { label: 'First', config: {} } },
        { id: 'n2', type: 'trigger', position: { x: 0, y: 0 }, data: { label: 'Second', config: {} } },
      ],
      selectedNodeId: 'n1',
    });
    render(<PropertyPanel />);
    expect((screen.getByLabelText('Node Label') as HTMLInputElement).value).toBe('First');

    act(() => {
      useWorkflowStore.getState().setSelectedNodeId('n2');
    });

    expect((screen.getByLabelText('Node Label') as HTMLInputElement).value).toBe('Second');
    expect(screen.getByText('trigger')).toBeTruthy();
  });
});
