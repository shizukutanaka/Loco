import { useCallback, useRef, useState, useEffect } from 'react';
import ReactFlow, {
  Background,
  MiniMap,
  NodeTypes,
  ReactFlowProvider,
  OnNodesChange,
  OnEdgesChange,
  OnConnect,
  OnSelectionChangeParams,
  useReactFlow,
  Node,
} from 'reactflow';
import 'reactflow/dist/style.css';

import { useWorkflowStore } from '@/store/workflowStore';
import {
  TriggerNode,
  ActionNode,
  ConditionNode,
  TransformNode,
  LoopNode,
} from '@/components/NodeTypes';
import { getIntegrationById } from '@/data/integrations';
import { CanvasControls } from '@/components/CanvasControls/CanvasControls';
import { QuickActionsMenu, ActionType } from '@/components/QuickActionsMenu/QuickActionsMenu';
import { CollaborationOverlay } from '@/components/CollaborationOverlay/CollaborationOverlay';
import { useToast } from '@/contexts/ToastContext';
import { useCollaborationStore } from '@/store/collaborationStore';

const nodeTypes: NodeTypes = {
  trigger: TriggerNode,
  action: ActionNode,
  condition: ConditionNode,
  transform: TransformNode,
  loop: LoopNode,
};

export function WorkflowCanvas() {
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const reactFlowInstance = useReactFlow();
  const [showMinimap, setShowMinimap] = useState(true);
  const [selectedNodes, setSelectedNodes] = useState<string[]>([]);
  const [contextMenu, setContextMenu] = useState<{
    isOpen: boolean;
    position: { x: number; y: number };
    nodeId: string | null;
    nodeType: string | null;
  }>({
    isOpen: false,
    position: { x: 0, y: 0 },
    nodeId: null,
    nodeType: null,
  });
  const toast = useToast();
  const {
    nodes,
    edges,
    onNodesChange,
    onEdgesChange,
    onConnect,
    addNode,
    setSelectedNodeId,
    deleteNode,
    updateNode,
  } = useWorkflowStore();

  const {
    isConnected,
    updateSelection,
    sendNodeAdded,
    sendNodeDeleted,
    sendNodeUpdated,
  } = useCollaborationStore();

  // Listen for canvas control events from keyboard shortcuts
  useEffect(() => {
    const handleZoomIn = () => reactFlowInstance.zoomIn({ duration: 200 });
    const handleZoomOut = () => reactFlowInstance.zoomOut({ duration: 200 });
    const handleResetZoom = () => reactFlowInstance.setViewport({ x: 0, y: 0, zoom: 1 }, { duration: 400 });
    const handleFitView = () => reactFlowInstance.fitView({ padding: 0.2, duration: 400 });

    window.addEventListener('canvas:zoom-in', handleZoomIn);
    window.addEventListener('canvas:zoom-out', handleZoomOut);
    window.addEventListener('canvas:reset-zoom', handleResetZoom);
    window.addEventListener('canvas:fit-view', handleFitView);

    return () => {
      window.removeEventListener('canvas:zoom-in', handleZoomIn);
      window.removeEventListener('canvas:zoom-out', handleZoomOut);
      window.removeEventListener('canvas:reset-zoom', handleResetZoom);
      window.removeEventListener('canvas:fit-view', handleFitView);
    };
  }, [reactFlowInstance]);

  const onDragOver = useCallback((event: React.DragEvent) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }, []);

  const onDrop = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault();

      const reactFlowBounds = reactFlowWrapper.current?.getBoundingClientRect();
      if (!reactFlowBounds) return;

      const data = event.dataTransfer.getData('application/reactflow');
      if (!data) return;

      const nodeData = JSON.parse(data);
      const position = {
        x: event.clientX - reactFlowBounds.left - 90,
        y: event.clientY - reactFlowBounds.top - 40,
      };

      const integration = nodeData.integration
        ? getIntegrationById(nodeData.integration)
        : null;

      const newNode = {
        id: `node-${Date.now()}`,
        type: nodeData.type,
        position,
        data: {
          label: nodeData.label || integration?.name || 'New Node',
          integration: nodeData.integration,
          config: {},
          description: integration?.description || '',
        },
      };

      addNode(newNode);
    },
    [addNode]
  );

  const onNodeClick = useCallback(
    (_event: React.MouseEvent, node: Node) => {
      setSelectedNodeId(node.id);
    },
    [setSelectedNodeId]
  );

  const onNodeContextMenu = useCallback(
    (event: React.MouseEvent, node: Node) => {
      event.preventDefault();
      setContextMenu({
        isOpen: true,
        position: { x: event.clientX, y: event.clientY },
        nodeId: node.id || null,
        nodeType: node.type || null,
      });
      setSelectedNodeId(node.id || null);
    },
    [setSelectedNodeId]
  );

  const onPaneContextMenu = useCallback((event: React.MouseEvent) => {
    event.preventDefault();
    const reactFlowBounds = reactFlowWrapper.current?.getBoundingClientRect();
    if (reactFlowBounds) {
      setContextMenu({
        isOpen: true,
        position: { x: event.clientX, y: event.clientY },
        nodeId: null,
        nodeType: null,
      });
    }
  }, []);

  const onPaneClick = useCallback(() => {
    setSelectedNodeId(null);
    setSelectedNodes([]);
  }, [setSelectedNodeId]);

  const onSelectionChange = useCallback(
    (params: OnSelectionChangeParams) => {
      const selectedNodeIds = params.nodes.map((node) => node.id);
      setSelectedNodes(selectedNodeIds);

      // Update single selection for property panel
      if (selectedNodeIds.length === 1) {
        setSelectedNodeId(selectedNodeIds[0]);
      } else if (selectedNodeIds.length === 0) {
        setSelectedNodeId(null);
      }

      // Send selection to collaboration service
      if (isConnected) {
        updateSelection(selectedNodeIds);
      }
    },
    [setSelectedNodeId, isConnected, updateSelection]
  );

  // Listen for delete key to delete selected nodes
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ignore if typing in input/textarea
      const target = e.target as HTMLElement;
      if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
        return;
      }

      const isMac = navigator.platform.toUpperCase().indexOf('MAC') >= 0;
      const ctrlKey = isMac ? e.metaKey : e.ctrlKey;

      // Delete or Backspace key
      if ((e.key === 'Delete' || e.key === 'Backspace') && selectedNodes.length > 0) {
        e.preventDefault();
        selectedNodes.forEach((nodeId) => deleteNode(nodeId));
        setSelectedNodes([]);
      }

      // Duplicate with Ctrl+D
      if (ctrlKey && e.key === 'd' && contextMenu.nodeId) {
        e.preventDefault();
        handleQuickAction('duplicate');
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [selectedNodes, deleteNode, contextMenu.nodeId]);

  const handleQuickAction = useCallback(
    (action: ActionType) => {
      const selectedNode = nodes.find((n) => n.id === contextMenu.nodeId);

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
          if (contextMenu.nodeId) {
            deleteNode(contextMenu.nodeId);

            // Send to collaboration service
            if (isConnected) {
              sendNodeDeleted(contextMenu.nodeId);
            }

            toast.info('Node deleted');
          }
          break;

        case 'rename':
          if (contextMenu.nodeId && selectedNode) {
            const newName = prompt('Enter new name:', selectedNode.data.label);
            if (newName) {
              updateNode(contextMenu.nodeId, { label: newName });

              // Send to collaboration service
              if (isConnected) {
                sendNodeUpdated(contextMenu.nodeId, { label: newName });
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
          if (contextMenu.nodeId) {
            // Remove all edges connected to this node
            // Note: We need to implement deleteEdge in the store
            toast.info('Disconnected node (feature coming soon)');
          }
          break;

        case 'properties':
          if (contextMenu.nodeId) {
            setSelectedNodeId(contextMenu.nodeId);
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
            x: contextMenu.position.x - (reactFlowWrapper.current?.offsetLeft || 0),
            y: contextMenu.position.y - (reactFlowWrapper.current?.offsetTop || 0),
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
    [nodes, edges, contextMenu, addNode, deleteNode, updateNode, setSelectedNodeId, reactFlowInstance, toast, isConnected, sendNodeAdded, sendNodeDeleted, sendNodeUpdated]
  );

  return (
    <div ref={reactFlowWrapper} className="flex-1 h-full relative">
      {/* Collaboration overlay for real-time cursors and presence */}
      <CollaborationOverlay />

      {selectedNodes.length > 1 && (
        <div className="absolute top-4 left-1/2 transform -translate-x-1/2 z-10 bg-white rounded-lg shadow-lg border border-gray-200 px-4 py-2 flex items-center gap-3">
          <span className="text-sm font-medium text-gray-700">
            {selectedNodes.length} nodes selected
          </span>
          <button
            onClick={() => {
              selectedNodes.forEach((nodeId) => deleteNode(nodeId));
              setSelectedNodes([]);
            }}
            className="flex items-center gap-1 px-3 py-1 text-sm text-red-600 hover:bg-red-50 rounded transition-colors"
          >
            Delete All
          </button>
        </div>
      )}
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange as OnNodesChange}
        onEdgesChange={onEdgesChange as OnEdgesChange}
        onConnect={onConnect as OnConnect}
        onDrop={onDrop}
        onDragOver={onDragOver}
        onNodeClick={onNodeClick}
        onNodeContextMenu={onNodeContextMenu}
        onPaneClick={onPaneClick}
        onPaneContextMenu={onPaneContextMenu}
        onSelectionChange={onSelectionChange}
        nodeTypes={nodeTypes}
        fitView
        snapToGrid
        snapGrid={[15, 15]}
        multiSelectionKeyCode="Shift"
        selectionOnDrag={true}
        panOnDrag={[1, 2]}
        defaultEdgeOptions={{
          type: 'smoothstep',
          animated: true,
        }}
      >
        <Background color="#e5e7eb" gap={16} />
        {showMinimap && (
          <MiniMap
            nodeColor={(node) => {
              switch (node.type) {
                case 'trigger':
                  return '#86efac';
                case 'action':
                  return '#93c5fd';
                case 'condition':
                  return '#fde047';
                case 'transform':
                  return '#d8b4fe';
                case 'loop':
                  return '#fdba74';
                default:
                  return '#e5e7eb';
              }
            }}
            className="!bg-white !border-2 !border-gray-200 !cursor-pointer"
            zoomable
            pannable
          />
        )}
        <CanvasControls
          showMinimap={showMinimap}
          onToggleMinimap={() => setShowMinimap(!showMinimap)}
        />
      </ReactFlow>
      <QuickActionsMenu
        isOpen={contextMenu.isOpen}
        position={contextMenu.position}
        nodeId={contextMenu.nodeId}
        nodeType={contextMenu.nodeType}
        onClose={() => setContextMenu({ ...contextMenu, isOpen: false })}
        onAction={handleQuickAction}
      />
    </div>
  );
}

export function WorkflowCanvasWrapper() {
  return (
    <ReactFlowProvider>
      <WorkflowCanvas />
    </ReactFlowProvider>
  );
}
