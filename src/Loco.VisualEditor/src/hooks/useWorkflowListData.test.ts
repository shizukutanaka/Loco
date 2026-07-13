import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import type { ReactNode } from 'react';
import { createElement } from 'react';

vi.mock('@/api/workflows', () => ({
  listWorkflows: vi.fn(),
}));

import { listWorkflows } from '@/api/workflows';
import { useWorkflowListData } from './useWorkflowListData';
import { ToastProvider } from '@/contexts/ToastContext';

// useToast() throws outside a ToastProvider.
const wrapper = ({ children }: { children: ReactNode }) => createElement(ToastProvider, null, children);

const flushMicrotasks = () => act(async () => {
  await Promise.resolve();
  await Promise.resolve();
});

const makeWorkflow = (overrides: Partial<Record<string, unknown>> = {}) => ({
  id: 'wf-1',
  name: 'Alpha',
  description: undefined,
  nodes: [],
  edges: [],
  metadata: { version: '1.0.0', isPublic: false },
  createdAt: '2026-01-01T00:00:00.000Z',
  updatedAt: '2026-01-01T00:00:00.000Z',
  ...overrides,
});

describe('useWorkflowListData', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('does not fetch when isOpen is false', async () => {
    renderHook(() => useWorkflowListData({ isOpen: false, sortBy: 'updated' }), { wrapper });
    await flushMicrotasks();

    expect(listWorkflows).not.toHaveBeenCalled();
  });

  it('fetches and maps the envelope response into WorkflowListItem[]', async () => {
    vi.mocked(listWorkflows).mockResolvedValue({
      success: true,
      data: {
        workflows: [
          makeWorkflow({
            id: 'wf-1',
            name: 'Alpha',
            nodes: [{ id: 'n1' }],
            edges: [],
            metadata: { version: '1.0.0', isPublic: false, tags: ['prod'] },
          }),
        ],
        total: 1,
        page: 1,
        pageSize: 20,
      },
    });

    const { result } = renderHook(() => useWorkflowListData({ isOpen: true, sortBy: 'updated' }), { wrapper });
    await flushMicrotasks();

    expect(result.current.workflows).toHaveLength(1);
    expect(result.current.workflows[0]).toMatchObject({
      id: 'wf-1',
      name: 'Alpha',
      nodeCount: 1,
      edgeCount: 0,
    });
    expect(result.current.allTags).toEqual(['prod']);
    expect(result.current.isLoading).toBe(false);
  });

  it('does nothing (no crash, no state change) on a failure envelope', async () => {
    vi.mocked(listWorkflows).mockResolvedValue({
      success: false,
      error: { code: 'INTERNAL_ERROR', message: 'boom' },
    });

    const { result } = renderHook(() => useWorkflowListData({ isOpen: true, sortBy: 'updated' }), { wrapper });
    await flushMicrotasks();

    expect(result.current.workflows).toEqual([]);
    expect(result.current.isLoading).toBe(false);
  });

  it('sorts by name ascending when sortBy is "name"', async () => {
    vi.mocked(listWorkflows).mockResolvedValue({
      success: true,
      data: {
        workflows: [
          makeWorkflow({ id: 'wf-b', name: 'Bravo' }),
          makeWorkflow({ id: 'wf-a', name: 'Alpha' }),
        ],
        total: 2,
        page: 1,
        pageSize: 20,
      },
    });

    const { result } = renderHook(() => useWorkflowListData({ isOpen: true, sortBy: 'name' }), { wrapper });
    await flushMicrotasks();

    expect(result.current.workflows.map((w) => w.name)).toEqual(['Alpha', 'Bravo']);
  });

  it('sorts by createdAt descending when sortBy is "created"', async () => {
    vi.mocked(listWorkflows).mockResolvedValue({
      success: true,
      data: {
        workflows: [
          makeWorkflow({ id: 'wf-old', name: 'Old', createdAt: '2025-01-01T00:00:00.000Z' }),
          makeWorkflow({ id: 'wf-new', name: 'New', createdAt: '2026-01-01T00:00:00.000Z' }),
        ],
        total: 2,
        page: 1,
        pageSize: 20,
      },
    });

    const { result } = renderHook(() => useWorkflowListData({ isOpen: true, sortBy: 'created' }), { wrapper });
    await flushMicrotasks();

    expect(result.current.workflows.map((w) => w.id)).toEqual(['wf-new', 'wf-old']);
  });

  it('deduplicates tags across workflows', async () => {
    vi.mocked(listWorkflows).mockResolvedValue({
      success: true,
      data: {
        workflows: [
          makeWorkflow({ id: 'wf-1', metadata: { version: '1', isPublic: false, tags: ['prod', 'critical'] } }),
          makeWorkflow({ id: 'wf-2', metadata: { version: '1', isPublic: false, tags: ['prod'] } }),
        ],
        total: 2,
        page: 1,
        pageSize: 20,
      },
    });

    const { result } = renderHook(() => useWorkflowListData({ isOpen: true, sortBy: 'updated' }), { wrapper });
    await flushMicrotasks();

    expect(result.current.allTags).toEqual(['critical', 'prod']);
  });
});
