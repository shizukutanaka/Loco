import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, act, waitFor, fireEvent } from '@testing-library/react';
import { ValidationPanel } from './ValidationPanel';
import { useWorkflowStore } from '@/store/workflowStore';

/**
 * Regression tests for ValidationPanel's hook ordering. Its useMemo/
 * useCallback calls previously sat AFTER `if (!validationReport) return null`,
 * so when the async validation effect delivered the first report the hook
 * count grew and React crashed ("rendered more hooks than during the previous
 * render"). Every test here crosses that null -> report transition, because
 * the report is always produced asynchronously after the first render.
 *
 * Validation runs in an async effect (dynamic import of the analysis engine +
 * analyze), so the report resolves over several real macrotasks. These tests
 * use real timers so testing-library's waitFor polls correctly - a fixed
 * setTimeout(0) flush is not enough for the dynamic import to settle.
 */
describe('ValidationPanel', () => {
  beforeEach(() => {
    useWorkflowStore.setState({
      nodes: [],
      edges: [],
      selectedNodeId: null,
      selectedEdgeId: null,
    });
  });

  it('renders nothing while there is no validation report (empty canvas)', async () => {
    const { container } = render(<ValidationPanel />);
    // Give the effect a chance; with no nodes it never sets a report
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 0));
    });
    expect(container.childElementCount).toBe(0);
  });

  it('auto-opens with errors when the workflow is invalid (hook-order guard)', async () => {
    useWorkflowStore.setState({
      nodes: [
        { id: 'n1', type: 'action', position: { x: 0, y: 0 }, data: { label: 'A' } },
      ],
      // Edge pointing at a node that does not exist -> deterministic
      // "Missing Target Node" error from validateStructure
      edges: [{ id: 'e1', source: 'n1', target: 'ghost', data: {} }],
    });

    render(<ValidationPanel />);

    await waitFor(() => expect(screen.getByText('Validation Issues')).toBeTruthy());
    expect(screen.getByText('Missing Target Node')).toBeTruthy();
  });

  it('collapses to the compact indicator and re-opens from it', async () => {
    useWorkflowStore.setState({
      nodes: [
        { id: 'n1', type: 'action', position: { x: 0, y: 0 }, data: { label: 'A' } },
      ],
      edges: [{ id: 'e1', source: 'n1', target: 'ghost', data: {} }],
    });

    render(<ValidationPanel />);

    const closeButton = await waitFor(() => {
      // The header's X button lives next to the "Validation Issues" label
      const header = screen.getByText('Validation Issues').closest('div')!.parentElement!;
      return header.querySelector('button')!;
    });

    fireEvent.click(closeButton);

    expect(screen.queryByText('Validation Issues')).toBeNull();
    // The count and the word "error" are separate text nodes
    // ({n} error{s}), so match on the span's combined textContent
    const indicator = screen.getByText(
      (_content, element) =>
        element?.tagName === 'SPAN' && /\d+ error/.test(element.textContent || '')
    );
    expect(indicator).toBeTruthy();

    // Clicking the indicator re-opens the full panel
    fireEvent.click(indicator.closest('button')!);
    expect(screen.getByText('Validation Issues')).toBeTruthy();
  });
});
