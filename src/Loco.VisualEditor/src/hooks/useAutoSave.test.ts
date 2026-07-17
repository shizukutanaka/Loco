import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useAutoSave } from './useAutoSave';
import { useWorkflowStore } from '@/store/workflowStore';
import type { Workflow } from '@/types/workflow';

const AUTO_SAVE_KEY = 'loco_workflow_draft';

const testWorkflow: Workflow = {
  id: 'wf-1',
  name: 'Draft Workflow',
  description: '',
  nodes: [],
  edges: [],
  metadata: { version: '1.0', isPublic: false },
  createdAt: '2026-01-01T00:00:00.000Z',
  updatedAt: '2026-01-01T00:00:00.000Z',
};

describe('useAutoSave', () => {
  beforeEach(() => {
    localStorage.clear();
    useWorkflowStore.setState({
      workflow: testWorkflow,
      nodes: [],
      edges: [],
    });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('saveDraft writes the exported workflow and a timestamp to localStorage', () => {
    const { result, unmount } = renderHook(() => useAutoSave());

    result.current.saveDraft();

    const stored = localStorage.getItem(AUTO_SAVE_KEY);
    expect(stored).not.toBeNull();
    expect(JSON.parse(stored!).id).toBe('wf-1');
    expect(localStorage.getItem(`${AUTO_SAVE_KEY}_timestamp`)).not.toBeNull();
    unmount();
  });

  it('skips the write when the workflow has not changed since the last save', () => {
    // Freeze time so exportWorkflow's updatedAt stamp is identical both times
    vi.useFakeTimers();
    const { result, unmount } = renderHook(() => useAutoSave());
    const setItem = vi.spyOn(Storage.prototype, 'setItem');

    result.current.saveDraft();
    const callsAfterFirst = setItem.mock.calls.length;
    expect(callsAfterFirst).toBeGreaterThan(0);

    result.current.saveDraft();
    expect(setItem.mock.calls.length).toBe(callsAfterFirst);

    setItem.mockRestore();
    unmount();
  });

  it('loadDraft round-trips the saved draft', () => {
    const { result, unmount } = renderHook(() => useAutoSave());

    result.current.saveDraft();
    const draft = result.current.loadDraft();

    expect(draft?.id).toBe('wf-1');
    expect(draft?.name).toBe('Draft Workflow');
    unmount();
  });

  it('loadDraft returns null when nothing is stored', () => {
    const { result, unmount } = renderHook(() => useAutoSave());
    expect(result.current.loadDraft()).toBeNull();
    unmount();
  });

  it('loadDraft returns null (not a throw) for corrupted JSON', () => {
    localStorage.setItem(AUTO_SAVE_KEY, '{not valid json');
    const { result, unmount } = renderHook(() => useAutoSave());
    expect(result.current.loadDraft()).toBeNull();
    unmount();
  });

  it('clearDraft removes both the draft and its timestamp', () => {
    const { result, unmount } = renderHook(() => useAutoSave());

    result.current.saveDraft();
    result.current.clearDraft();

    expect(localStorage.getItem(AUTO_SAVE_KEY)).toBeNull();
    expect(localStorage.getItem(`${AUTO_SAVE_KEY}_timestamp`)).toBeNull();
    unmount();
  });

  it('auto-saves on the 30-second interval', () => {
    vi.useFakeTimers();
    const { unmount } = renderHook(() => useAutoSave());

    expect(localStorage.getItem(AUTO_SAVE_KEY)).toBeNull();

    vi.advanceTimersByTime(30000);

    const stored = localStorage.getItem(AUTO_SAVE_KEY);
    expect(stored).not.toBeNull();
    expect(JSON.parse(stored!).id).toBe('wf-1');
    unmount();
  });

  it('saves on beforeunload (page close)', () => {
    const { unmount } = renderHook(() => useAutoSave());

    expect(localStorage.getItem(AUTO_SAVE_KEY)).toBeNull();
    window.dispatchEvent(new Event('beforeunload'));

    expect(localStorage.getItem(AUTO_SAVE_KEY)).not.toBeNull();
    unmount();
  });
});
