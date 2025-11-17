import { useState, useEffect, useCallback } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';

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
  const validateLabel = useCallback((label: string): string | undefined => {
    if (!label.trim()) {
      return 'Node label is required';
    }
    if (label.length > 100) {
      return 'Label must be less than 100 characters';
    }
    return undefined;
  }, []);

  const validateCondition = useCallback((condition: string): string | undefined => {
    if (!condition.trim()) {
      return 'Condition expression is required';
    }
    return undefined;
  }, []);

  const validateCode = useCallback((code: string): string | undefined => {
    if (!code.trim()) {
      return 'Transform code is required';
    }
    return undefined;
  }, []);

  const handleLabelChange = useCallback(
    (label: string) => {
      setLocalData((prev) => ({ ...prev, label }));
      const error = validateLabel(label);
      setErrors((prev) => ({ ...prev, label: error }));

      // Only update if valid
      if (!error && selectedNode) {
        updateNode(selectedNode.id, { label });
      }
    },
    [selectedNode, validateLabel, updateNode]
  );

  const handleConfigChange = useCallback(
    (key: string, value: ConfigValue) => {
      setLocalData((prev) => ({
        ...prev,
        config: { ...prev.config, [key]: value },
      }));

      // Validate based on field type
      let error: string | undefined;
      if (key === 'condition') {
        error = validateCondition(String(value || ''));
      } else if (key === 'code') {
        error = validateCode(String(value || ''));
      }

      setErrors((prev) => ({ ...prev, [key]: error }));

      // Only update if valid
      if (!error && selectedNode) {
        const newConfig = { ...localData.config, [key]: value };
        updateNode(selectedNode.id, { config: newConfig });
      }
    },
    [selectedNode, localData.config, validateCondition, validateCode, updateNode]
  );

  return {
    localData,
    errors,
    handleLabelChange,
    handleConfigChange,
  };
}
