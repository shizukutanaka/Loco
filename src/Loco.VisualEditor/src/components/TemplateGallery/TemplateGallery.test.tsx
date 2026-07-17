import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { TemplateGallery } from './TemplateGallery';
import { useWorkflowStore } from '@/store/workflowStore';

/**
 * Regression tests for TemplateGallery's hook ordering. Its useMemo/
 * useCallback calls previously sat AFTER `if (!isOpen) return null`, so
 * opening or closing the gallery changed the hook count and crashed React
 * ("rendered more hooks than during the previous render"). The open/close
 * cycle test below crosses that boundary in both directions.
 */
describe('TemplateGallery', () => {
  beforeEach(() => {
    useWorkflowStore.setState({
      workflow: null,
      nodes: [],
      edges: [],
      selectedNodeId: null,
      selectedEdgeId: null,
    });
  });

  it('renders nothing when closed', () => {
    const { container } = render(<TemplateGallery isOpen={false} onClose={() => {}} />);
    expect(container.childElementCount).toBe(0);
  });

  it('survives a full closed -> open -> closed cycle (hook-order guard)', () => {
    const { container, rerender } = render(
      <TemplateGallery isOpen={false} onClose={() => {}} />
    );

    rerender(<TemplateGallery isOpen={true} onClose={() => {}} />);
    expect(screen.getByText('Workflow Templates')).toBeTruthy();
    expect(screen.getByText('Slack Notification')).toBeTruthy();

    rerender(<TemplateGallery isOpen={false} onClose={() => {}} />);
    expect(container.childElementCount).toBe(0);
  });

  it('filters templates by search query', () => {
    render(<TemplateGallery isOpen={true} onClose={() => {}} />);

    fireEvent.change(screen.getByPlaceholderText(/search/i), {
      target: { value: 'zzz-no-such-template' },
    });

    expect(screen.getByText('No templates found')).toBeTruthy();
    expect(screen.queryByText('Slack Notification')).toBeNull();
  });

  it('loads the chosen template into the store and closes', () => {
    const onClose = vi.fn();
    render(<TemplateGallery isOpen={true} onClose={onClose} />);

    fireEvent.click(screen.getByText('Slack Notification'));

    expect(onClose).toHaveBeenCalledTimes(1);
    const workflow = useWorkflowStore.getState().workflow;
    expect(workflow?.name).toBe('Slack Notification (Copy)');
    expect(useWorkflowStore.getState().nodes.length).toBeGreaterThan(0);
  });
});
