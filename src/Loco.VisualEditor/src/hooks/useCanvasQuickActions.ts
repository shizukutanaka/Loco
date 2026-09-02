import { useCallback, useRef, useEffect } from 'react';
import { Node, Edge } from 'reactflow';
import { useReactFlow } from 'reactflow';
import { useWorkflowStore } from '@/store/workflowStore';
import { useToast } from '@/contexts/ToastContext';
import { ActionType } from '@/components/QuickActionsMenu/QuickActionsMenu';

interface UseCanvasQuickActionsOptions {
  nodes: Node[];
  edges: Edge[];
  contextMenuNodeId: string | null;
  contextMenuPosition: { x: number; y: number };
  onSelectNode: (nodeId: string | null) => void;
}

/**
 * Custom hook for handling canvas quick action menu items
 * Handles: duplicate, delete, rename, group, properties, info, add-nodes
 */
export function useCanvasQuickActions({
  nodes,
  edges,
  contextMenuNodeId,
  contextMenuPosition,
  onSelectNode,
}: UseCanvasQuickActionsOptions) {
  const reactFlowInstance = useReactFlow();
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const { addNode, deleteNode, updateNode, deleteEdge } = useWorkflowStore();
  const toast = useToast();

  // Store nodes in ref to avoid callback recreation when nodes change
  const nodesRef = useRef<Node[]>(nodes);
  const edgesRef = useRef<Edge[]>(edges);

  // Update refs with current graph without recreating the callback
  useEffect(() => {
    nodesRef.current = nodes;
  }, [nodes]);

  useEffect(() => {
    edgesRef.current = edges;
  }, [edges]);

  const handleQuickAction = useCallback(
    (action: ActionType) => {
      const selectedNode = nodesRef.current.find((n) => n.id === contextMenuNodeId);

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


            toast.success('Node duplicated');
          }
          break;

        case 'delete':
          if (contextMenuNodeId) {
            deleteNode(contextMenuNodeId);


            toast.info('Node deleted');
          }
          break;

        case 'rename':
          if (contextMenuNodeId && selectedNode) {
            const newName = prompt('Enter new name:', selectedNode.data.label);
            if (newName) {
              updateNode(contextMenuNodeId, { label: newName });


              toast.success('Node renamed');
            }
          }
          break;

        case 'disconnect':
          if (contextMenuNodeId) {
            // This said "feature coming soon" and did nothing. The store has
            // had deleteEdge the whole time, so the note claiming it was
            // missing was simply stale.
            const attached = edgesRef.current.filter(
              (e) => e.source === contextMenuNodeId || e.target === contextMenuNodeId
            );

            attached.forEach((e) => deleteEdge(e.id));

            toast.success(
              attached.length === 1
                ? 'Removed 1 connection'
                : `Removed ${attached.length} connections`
            );
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
        case 'add-delay': {
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
        }

        default:
          break;
      }
    },
    // Only include dependencies that actually change.
    // Removed: nodes - now using ref to avoid callback recreation when nodes change
    // Store methods (addNode, deleteNode, updateNode, sendNodeAdded, etc.) are stable in Zustand and don't need to be included
    // reactFlowInstance and toast are also stable context/hook values
    [contextMenuNodeId, contextMenuPosition, onSelectNode]
  );

  return { handleQuickAction };
}
