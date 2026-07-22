import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { deferHistorySnapshot, createDeferredHistorySnapshot } from './deferHistorySnapshot';

describe('deferHistorySnapshot', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it('defers the callback to a later tick rather than running it synchronously', () => {
    const cb = vi.fn();
    deferHistorySnapshot(cb);
    expect(cb).not.toHaveBeenCalled();

    vi.runAllTimers();
    expect(cb).toHaveBeenCalledOnce();
  });

  it('createDeferredHistorySnapshot defers a call to getState().pushToHistory()', () => {
    const pushToHistory = vi.fn();
    const getState = vi.fn(() => ({ pushToHistory }));

    const deferred = createDeferredHistorySnapshot(getState);
    deferred();

    // getState is only read when the deferred tick runs
    expect(pushToHistory).not.toHaveBeenCalled();
    vi.runAllTimers();
    expect(getState).toHaveBeenCalledOnce();
    expect(pushToHistory).toHaveBeenCalledOnce();
  });
});
