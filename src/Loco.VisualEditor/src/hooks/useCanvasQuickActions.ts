import { useCallback, useRef } from 'react';
import { Node } from 'reactflow';
import { useReactFlow } from 'reactflow';
import { useWorkflowStore } from '@/store/workflowStore';
import { useCollaborationStore } from '@/store/collaborationStore';
import { useToast } from '@/contexts/ToastContext';
import { ActionType } from '@/components/QuickActionsMenu/QuickActionsMenu';

interface UseCanvasQuickActionsOptions {
  nodes: Node[];
  contextMenuNodeId: string | null;
  contextMenuPosition: { x: number; y: number };
  onSelectNode: (nodeId: string | null) => void;
}

/**
 * Custom hook for handling canvas quick action menu items
 * Handles: duplicate, delete, rename, group, properties, info, add-nodes
 * Integrates with collaboration store for real-time updates
 */
export function useCanvasQuickActions({
  nodes,
  contextMenuNodeId,
  contextMenuPosition,
  onSelectNode,
}: UseCanvasQuickActionsOptions) {
  const reactFlowInstance = useReactFlow();
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const { addNode, deleteNode, updateNode } = useWorkflowStore();
  const { isConnected, sendNodeAdded, sendNodeDeleted, sendNodeUpdated } = useCollaborationStore();
  const toast = useToast();

  const handleQuickAction = useCallback(
    (action: ActionType) => {
      const selectedNode = nodes.find((n) => n.id === contextMenuNodeId);

      switch (action) {
        case 'duplicate':
          if (selectedNode) {
            const newNode = {
              ...selectedNode,
              id: `node-${Date.now()}`,
              position: {
                x: selectedNode.position.x + 100,
                y: selectedNode.position.y + 100,
              },
              data: { ...selectedNode.data },
            };
            addNode(newNode);

            // Send to collaboration service
            if (isConnected) {
              sendNodeAdded(newNode);
            }

            toast.success('Node duplicated');
          }
          break;

        case 'delete':
          if (contextMenuNodeId) {
            deleteNode(contextMenuNodeId);

            // Send to collaboration service
            if (isConnected) {
              sendNodeDeleted(contextMenuNodeId);
            }

            toast.info('Node deleted');
          }
          break;

        case 'rename':
          if (contextMenuNodeId && selectedNode) {
            const newName = prompt('Enter new name:', selectedNode.data.label);
            if (newName) {
              updateNode(contextMenuNodeId, { label: newName });

              // Send to collaboration service
              if (isConnected) {
                sendNodeUpdated(contextMenuNodeId, { label: newName });
              }

              toast.success('Node renamed');
            }
          }
          break;

        case 'run':
          toast.info('Running workflow from this node...');
          break;

        case 'group':
          toast.info('Grouping nodes (feature coming soon)');
          break;

        case 'connect':
          toast.info('Connect mode (feature coming soon)');
          break;

        case 'disconnect':
          if (contextMenuNodeId) {
            // Remove all edges connected to this node
            // Note: We need to implement deleteEdge in the store
            toast.info('Disconnected node (feature coming soon)');
          }
          break;

        case 'properties':
          if (contextMenuNodeId) {
            onSelectNode(contextMenuNodeId);
            toast.info('Open property panel');
          }
          break;

        case 'info':
          if (selectedNode) {
            alert(`Node Info:\nID: ${selectedNode.id}\nType: ${selectedNode.type}\nLabel: ${selectedNode.data.label}`);
          }
          break;

        // Add node actions for canvas right-click
        case 'add-trigger':
        case 'add-action':
        case 'add-condition':
        case 'add-transform':
        case 'add-loop':
          const nodeType = action.replace('add-', '');
          const position = reactFlowInstance.project({
            x: contextMenuPosition.x - (reactFlowWrapper.current?.offsetLeft || 0),
            y: contextMenuPosition.y - (reactFlowWrapper.current?.offsetTop || 0),
          });
          const newNode = {
            id: `node-${Date.now()}`,
            type: nodeType,
            position,
            data: {
              label: `New ${nodeType}`,
              config: {},
            },
          };
          addNode(newNode);
          toast.success(`Added ${nodeType} node`);
          break;

        default:
          break;
      }
    },
    [nodes, contextMenuNodeId, contextMenuPosition, addNode, deleteNode, updateNode, onSelectNode, reactFlowInstance, toast, isConnected, sendNodeAdded, sendNodeDeleted, sendNodeUpdated]
  );

  return { handleQuickAction };
}
