/**
 * Auto-Save Hook
 *
 * Provides automatic saving of workflow drafts to localStorage.
 * Saves the workflow every 30 seconds and restores it on page load.
 */

import { useEffect, useRef, useCallback } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';
import { Workflow } from '@/types/workflow';

const AUTO_SAVE_KEY = 'loco_workflow_draft';
const AUTO_SAVE_INTERVAL = 30000; // 30 seconds

export function useAutoSave() {
  const { workflow, exportWorkflow } = useWorkflowStore();
  // ReturnType<typeof setInterval> rather than number: the timer handle is a
  // number in the DOM lib but a Timeout object under Node's types, and this
  // package pulls in both (tests run in Node).
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const lastSavedRef = useRef<string>('');
  const exportWorkflowRef = useRef(exportWorkflow);
  const workflowRef = useRef(workflow);

  // Update refs to always have current values without causing effect recreation
  useEffect(() => {
    exportWorkflowRef.current = exportWorkflow;
  }, [exportWorkflow]);

  useEffect(() => {
    workflowRef.current = workflow;
  }, [workflow]);

  // Save workflow to localStorage - memoized to ensure consistent reference
  const saveDraft = useCallback((workflowData: Workflow) => {
    try {
      const jsonString = JSON.stringify(workflowData);

      // Only save if the workflow has changed
      if (jsonString === lastSavedRef.current) {
        return;
      }

      localStorage.setItem(AUTO_SAVE_KEY, jsonString);
      localStorage.setItem(`${AUTO_SAVE_KEY}_timestamp`, new Date().toISOString());
      lastSavedRef.current = jsonString;

      console.log('Auto-saved workflow draft at', new Date().toLocaleTimeString());
    } catch (error) {
      console.error('Failed to auto-save workflow:', error);
    }
  }, []);

  // Load workflow from localStorage
  const loadDraft = (): Workflow | null => {
    try {
      const draftJson = localStorage.getItem(AUTO_SAVE_KEY);
      if (!draftJson) {
        return null;
      }

      const draft = JSON.parse(draftJson) as Workflow;
      const timestamp = localStorage.getItem(`${AUTO_SAVE_KEY}_timestamp`);

      console.log('Found workflow draft from', timestamp || 'unknown time');
      return draft;
    } catch (error) {
      console.error('Failed to load workflow draft:', error);
      return null;
    }
  };

  // Clear draft from localStorage
  const clearDraft = () => {
    try {
      localStorage.removeItem(AUTO_SAVE_KEY);
      localStorage.removeItem(`${AUTO_SAVE_KEY}_timestamp`);
      lastSavedRef.current = '';
      console.log('Cleared workflow draft');
    } catch (error) {
      console.error('Failed to clear workflow draft:', error);
    }
  };

  // Auto-save interval - stable effect that doesn't depend on workflow/exportWorkflow objects
  // Uses refs to access current values without recreating the interval
  useEffect(() => {
    intervalRef.current = setInterval(() => {
      if (workflowRef.current) {
        const workflowData = exportWorkflowRef.current();
        saveDraft(workflowData);
      }
    }, AUTO_SAVE_INTERVAL);

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }
    };
  }, [saveDraft]); // Only depend on saveDraft callback (which is stable)

  // Save on unmount (page close) - stable effect that uses refs
  useEffect(() => {
    const handleBeforeUnload = () => {
      if (workflowRef.current) {
        const workflowData = exportWorkflowRef.current();
        saveDraft(workflowData);
      }
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [saveDraft]); // Only depend on saveDraft callback (which is stable)

  return {
    loadDraft,
    clearDraft,
    saveDraft: useCallback(() => {
      if (workflowRef.current) {
        saveDraft(exportWorkflowRef.current());
      }
    }, [saveDraft]),
  };
}
