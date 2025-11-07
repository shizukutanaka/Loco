/**
 * Auto-Save Hook
 *
 * Provides automatic saving of workflow drafts to localStorage.
 * Saves the workflow every 30 seconds and restores it on page load.
 */

import { useEffect, useRef } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';
import { Workflow } from '@/types/workflow';

const AUTO_SAVE_KEY = 'loco_workflow_draft';
const AUTO_SAVE_INTERVAL = 30000; // 30 seconds

export function useAutoSave() {
  const { workflow, exportWorkflow } = useWorkflowStore();
  const intervalRef = useRef<number | null>(null);
  const lastSavedRef = useRef<string>('');

  // Save workflow to localStorage
  const saveDraft = (workflowData: Workflow) => {
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
  };

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

  // Auto-save interval
  useEffect(() => {
    intervalRef.current = setInterval(() => {
      if (workflow) {
        const workflowData = exportWorkflow();
        saveDraft(workflowData);
      }
    }, AUTO_SAVE_INTERVAL);

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }
    };
  }, [workflow, exportWorkflow]);

  // Save on unmount (page close)
  useEffect(() => {
    const handleBeforeUnload = () => {
      if (workflow) {
        const workflowData = exportWorkflow();
        saveDraft(workflowData);
      }
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [workflow, exportWorkflow]);

  return {
    loadDraft,
    clearDraft,
    saveDraft: () => {
      if (workflow) {
        saveDraft(exportWorkflow());
      }
    },
  };
}
