import { useState, useEffect, useCallback } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';
import {
  validateLabel as validateLabelFn,
  validateCondition as validateConditionFn,
  validateCode as validateCodeFn,
} from '@/utils/nodeValidation';

export interface NodeConfig {
  condition?: string;
  code?: string;
  action?: string;
  parameters?: Record<string, string | number>;
  [key: string]: unknown;
}

export interface NodeData {
  label: string;
  integration?: string;
  config: NodeConfig;
  description?: string;
  [key: string]: unknown;
}

export interface ValidationError {
  label?: string;
  condition?: string;
  code?: string;
  action?: string;
  parameters?: Record<string, string>;
  // Condition nodes are configured as left/operation/right, matching the
  // comparison the engine's built-in condition handler actually performs.
  left?: string;
  right?: string;
  // Transform nodes of type "json" carry a JSON literal the engine parses.
  json?: string;
  // Loop nodes carry a JSON array the engine iterates.
  items?: string;
  // Trigger nodes carry a cron expression the scheduler registers.
  cron?: string;
}

type ConfigValue = string | number | Record<string, unknown> | undefined;

/**
 * Custom hook for managing property panel form state and validation
 * Handles: localData state, label changes, config changes with validation
 */
export function usePropertyPanelFormState(selectedNodeId: string | null) {
  const { nodes, updateNode } = useWorkflowStore();

  const selectedNode = nodes.find((n) => n.id === selectedNodeId);

  const [localData, setLocalData] = useState<NodeData>({
    label: '',
    config: {},
  });

  const [errors, setErrors] = useState<ValidationError>({});

  // Sync local data with selected node
  useEffect(() => {
    if (selectedNode) {
      setLocalData(selectedNode.data);
      setErrors({}); // Clear errors when switching nodes
    }
  }, [selectedNode]);

  // Validation functions
  // Using consolidated validation from nodeValidation.ts
  const validateLabel = useCallback((label: string): string | undefined => {
    const error = validateLabelFn(label);
    return error ?? undefined;
  }, []);

  const validateCondition = useCallback((condition: string): string | undefined => {
    const error = validateConditionFn(condition);
    return error ?? undefined;
  }, []);

  const validateCode = useCallback((code: string): string | undefined => {
    const error = validateCodeFn(code);
    return error ?? undefined;
  }, []);

  const handleLabelChange = useCallback(
    (label: string) => {
      setLocalData((prev) => ({ ...prev, label }));
      const error = validateLabel(label);
      setErrors((prev) => ({ ...prev, label: error }));

      // Only update if valid - use selectedNodeId instead of object reference
      if (!error && selectedNodeId) {
        updateNode(selectedNodeId, { label });
      }
    },
    [selectedNodeId, validateLabel, updateNode]
  );

  const handleConfigChange = useCallback(
    (key: string, value: ConfigValue) => {
      // Validate based on field type
      let error: string | undefined;
      if (key === 'condition') {
        error = validateCondition(String(value || ''));
      } else if (key === 'code') {
        error = validateCode(String(value || ''));
      } else if (key === 'cron') {
        // WorkflowSchedulerService logs and SKIPS a workflow whose cron
        // expression will not parse, so an invalid one means the workflow
        // silently never runs. Catch the shape here instead.
        const raw = String(value ?? '').trim();
        if (raw !== '') {
          const fields = raw.split(/\s+/);
          if (fields.length !== 5) {
            error = 'Expected 5 fields: minute hour day month day-of-week';
          } else if (!fields.every((f) => /^[*\d/,-]+$/.test(f))) {
            error = 'Only digits and * , - / are allowed in each field';
          }
        }
      } else if (key === 'json' || key === 'items') {
        // The engine deserializes this literal at run time, so malformed JSON
        // would fail mid-execution. Catch it while the user is still editing.
        const raw = String(value ?? '').trim();
        if (raw !== '') {
          try {
            JSON.parse(raw);
          } catch {
            error = 'Not valid JSON';
          }
        }
      }

      // Use functional setState to access current state without stale closures
      setLocalData((prev) => {
        const updated = {
          ...prev,
          config: { ...prev.config, [key]: value },
        };

        // Only update store if valid - use selectedNodeId (string) instead of object reference
        if (!error && selectedNodeId) {
          updateNode(selectedNodeId, { config: updated.config });
        }

        return updated;
      });

      setErrors((prev) => ({ ...prev, [key]: error }));
    },
    // Only depend on stable values: selectedNodeId, validateCondition, validateCode, updateNode
    // Removed: selectedNode (object reference), localData.config (stale/unused)
    [selectedNodeId, validateCondition, validateCode, updateNode]
  );

  return {
    localData,
    errors,
    handleLabelChange,
    handleConfigChange,
  };
}
