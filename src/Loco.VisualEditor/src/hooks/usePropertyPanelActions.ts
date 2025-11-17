import { useCallback } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';

/**
 * Custom hook for managing property panel actions
 * Handles: delete node, close/deselect node
 */
export function usePropertyPanelActions(selectedNodeId: string | null) {
  const { deleteNode, setSelectedNodeId } = useWorkflowStore();

  const handleDelete = useCallback(() => {
    if (selectedNodeId) {
      deleteNode(selectedNodeId);
    }
  }, [selectedNodeId, deleteNode]);

  const handleClose = useCallback(() => {
    setSelectedNodeId(null);
  }, [setSelectedNodeId]);

  return {
    handleDelete,
    handleClose,
  };
}
