import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';

vi.mock('@/api/workflows', () => ({
  getExecutionStatus: vi.fn(),
}));

import { getExecutionStatus } from '@/api/workflows';
import { useExecutionPolling } from './useExecutionPolling';

const running = (id: string) => ({
  success: true as const,
  data: { executionId: id, status: 'running' as const, startedAt: '2026-01-01T00:00:00.000Z' },
});
const completed = (id: string) => ({
  success: true as const,
  data: {
    executionId: id,
    status: 'completed' as const,
    startedAt: '2026-01-01T00:00:00.000Z',
    completedAt: '2026-01-01T00:00:01.000Z',
    output: {},
  },
});

// Flushes the microtask queue (pending promise resolutions) without touching
// fake timers - needed after triggering an async fetch, since
// @testing-library/react's `waitFor` polls via real setTimeout and hangs
// forever under vi.useFakeTimers().
const flushMicrotasks = () => act(async () => {
  await Promise.resolve();
  await Promise.resolve();
});

describe('useExecutionPolling', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('returns null execution when executionId is null', () => {
    const { result } = renderHook(() => useExecutionPolling(null));
    expect(result.current.execution).toBeNull();
    expect(getExecutionStatus).not.toHaveBeenCalled();
  });

  it('fetches immediately when given an executionId', async () => {
    vi.mocked(getExecutionStatus).mockResolvedValue(completed('exec-1'));

    const { result } = renderHook(() => useExecutionPolling('exec-1'));
    await flushMicrotasks();

    expect(getExecutionStatus).toHaveBeenCalledWith('exec-1');
    expect(getExecutionStatus).toHaveBeenCalledTimes(1);
    expect(result.current.execution?.status).toBe('completed');
  });

  it('keeps polling while status is running, on the configured interval', async () => {
    vi.mocked(getExecutionStatus).mockResolvedValue(running('exec-1'));

    renderHook(() => useExecutionPolling('exec-1'));
    await flushMicrotasks();
    expect(getExecutionStatus).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000);
    });
    expect(getExecutionStatus).toHaveBeenCalledTimes(2);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000);
    });
    expect(getExecutionStatus).toHaveBeenCalledTimes(3);
  });

  it('stops polling once the execution reaches a terminal status', async () => {
    vi.mocked(getExecutionStatus).mockResolvedValue(completed('exec-1'));

    renderHook(() => useExecutionPolling('exec-1'));
    await flushMicrotasks();
    expect(getExecutionStatus).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(10_000); // several interval ticks
    });

    // Terminal on the very first fetch: the interval body checks status and must
    // never call fetchExecution again.
    expect(getExecutionStatus).toHaveBeenCalledTimes(1);
  });

  it('ignores a stale response when executionId changes before the request resolves', async () => {
    let resolveFirst!: (value: ReturnType<typeof running>) => void;
    vi.mocked(getExecutionStatus).mockImplementation((id: string) => {
      if (id === 'exec-1') {
        return new Promise((resolve) => {
          resolveFirst = resolve;
        });
      }
      return Promise.resolve(completed('exec-2'));
    });

    const { result, rerender } = renderHook(
      ({ id }: { id: string | null }) => useExecutionPolling(id),
      { initialProps: { id: 'exec-1' as string | null } }
    );
    await flushMicrotasks();

    // Switch executionId before the first request resolves.
    rerender({ id: 'exec-2' });
    await flushMicrotasks();
    expect(result.current.execution?.executionId).toBe('exec-2');

    // Now let the stale exec-1 response resolve - it must not clobber exec-2's state.
    await act(async () => {
      resolveFirst(running('exec-1'));
      await Promise.resolve();
    });

    expect(result.current.execution?.executionId).toBe('exec-2');
    expect(result.current.execution?.status).toBe('completed');
  });

  it('clears execution state when executionId becomes null', async () => {
    vi.mocked(getExecutionStatus).mockResolvedValue(completed('exec-1'));

    const { result, rerender } = renderHook(
      ({ id }: { id: string | null }) => useExecutionPolling(id),
      { initialProps: { id: 'exec-1' as string | null } }
    );
    await flushMicrotasks();
    expect(result.current.execution).not.toBeNull();

    rerender({ id: null });

    expect(result.current.execution).toBeNull();
  });
});
