import { useCallback, useRef, useState, memo } from 'react';
import ReactFlow, {
  Background,
  MiniMap,
  NodeTypes,
  ReactFlowProvider,
  OnNodesChange,
  OnEdgesChange,
  OnConnect,
  OnSelectionChangeParams,
  Node,
  Edge,
} from 'reactflow';
import 'reactflow/dist/style.css';

import { useWorkflowStore } from '@/store/workflowStore';
import {
  useNodes,
  useEdges,
  useNodeActions,
} from '@/store/selectors';
import {
  TriggerNode,
  ActionNode,
  ConditionNode,
  TransformNode,
  LoopNode,
} from '@/components/NodeTypes';
import { CanvasControls } from '@/components/CanvasControls/CanvasControls';
import { QuickActionsMenu } from '@/components/QuickActionsMenu/QuickActionsMenu';
import {
  useCanvasZoomControls,
  useCanvasKeyboardShortcuts,
  useCanvasContextMenu,
  useCanvasQuickActions,
} from '@/hooks';

// ============================================================================
// Constants (Memoized - prevent recreation on every render)
// ============================================================================

const NODE_TYPES: NodeTypes = {
  trigger: TriggerNode,
  action: ActionNode,
  condition: ConditionNode,
  transform: TransformNode,
  loop: LoopNode,
};

const NODE_COLORS = {
  trigger: '#86efac',
  action: '#93c5fd',
  condition: '#fde047',
  transform: '#d8b4fe',
  loop: '#fdba74',
  default: '#e5e7eb',
} as const;

// ============================================================================
// Workflow Canvas Component
// ============================================================================

function WorkflowCanvasComponent() {
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const [showMinimap, setShowMinimap] = useState(true);
  const [selectedNodes, setSelectedNodes] = useState<string[]>([]);
  // Use granular selectors for state to minimize re-renders
  // Only subscribe to nodes and edges, not entire store
  const nodes = useNodes();
  const edges = useEdges();

  // Get node actions (addNode, deleteNode, setSelectedNodeId)
  const { addNode, deleteNode, setSelectedNodeId } = useNodeActions();

  // Get canvas event handlers from store
  // (onNodesChange, onEdgesChange, onConnect are callback handlers, not included in granular selectors)
  const { onNodesChange, onEdgesChange, onConnect, setSelectedEdgeId } = useWorkflowStore();

  // Setup canvas control event listeners (zoom in/out, fit view, etc)
  useCanvasZoomControls();

  // Setup canvas context menu
  const contextMenu = useCanvasContextMenu();

  // Setup keyboard shortcuts for deletion and duplication
  useCanvasKeyboardShortcuts({
    selectedNodeIds: selectedNodes,
    onDeleteNodes: (nodeIds) => nodeIds.forEach(deleteNode),
    onDuplicateNode: () => {
      const selectedNode = nodes.find((n) => n.id === selectedNodes[0]);
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
      }
    },
    onClearSelection: () => setSelectedNodes([]),
  });

  // Setup quick actions handler
  const { handleQuickAction } = useCanvasQuickActions({
    nodes,
    contextMenuNodeId: contextMenu.nodeId,
    contextMenuPosition: contextMenu.position,
    onSelectNode: setSelectedNodeId,
  });

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

      const newNode = {
        id: `node-${Date.now()}`,
        type: nodeData.type,
        position,
        data: {
          label: nodeData.label || 'New Node',
          integration: nodeData.integration,
          config: {},
          description: nodeData.description || '',
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

  // Selecting an edge surfaces the EdgeConditionPanel, letting a user set
  // success/error/always routing on that connection - previously there was no
  // UI path to this at all, even though VisualWorkflowEngine's error/success
  // branch routing (ShouldFollowConnection) has always supported it.
  const onEdgeClick = useCallback(
    (_event: React.MouseEvent, edge: Edge) => {
      setSelectedEdgeId(edge.id);
    },
    [setSelectedEdgeId]
  );

  const onNodeContextMenu = useCallback(
    (event: React.MouseEvent, node: Node) => {
      event.preventDefault();
      contextMenu.openContextMenu(event.clientX, event.clientY, node.id || null, node.type || null);
      setSelectedNodeId(node.id || null);
    },
    [contextMenu, setSelectedNodeId]
  );

  const onPaneContextMenu = useCallback(
    (event: React.MouseEvent) => {
      event.preventDefault();
      contextMenu.openCanvasContextMenu(event.clientX, event.clientY);
    },
    [contextMenu]
  );

  const onPaneClick = useCallback(() => {
    setSelectedNodeId(null);
    setSelectedEdgeId(null);
    setSelectedNodes([]);
  }, [setSelectedNodeId, setSelectedEdgeId]);

  // Memoize node color retrieval to prevent recalculation on every render
  const getNodeColor = useCallback(
    (node: Node) => {
      const nodeType = node.type as keyof typeof NODE_COLORS;
      return NODE_COLORS[nodeType] || NODE_COLORS.default;
    },
    []
  );

  const onSelectionChange = useCallback(
    (params: OnSelectionChangeParams) => {
      const selectedNodeIds = params.nodes.map((node) => node.id);
      const selectedEdgeIds = params.edges.map((edge) => edge.id);
      setSelectedNodes(selectedNodeIds);

      // Update single selection for the property / edge-condition panels.
      // Edges can be selected without a mouse (React Flow edges are focusable:
      // Tab to the edge, then Enter/Space) - mirroring that selection into the
      // store here is what makes the EdgeConditionPanel keyboard-reachable.
      if (selectedNodeIds.length === 1) {
        setSelectedNodeId(selectedNodeIds[0]);
      } else if (selectedNodeIds.length === 0 && selectedEdgeIds.length === 1) {
        setSelectedEdgeId(selectedEdgeIds[0]);
      } else if (selectedNodeIds.length === 0 && selectedEdgeIds.length === 0) {
        setSelectedNodeId(null);
        setSelectedEdgeId(null);
      }
    },
    [setSelectedNodeId, setSelectedEdgeId]
  );


  return (
    <div ref={reactFlowWrapper} className="flex-1 h-full relative">

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
        onEdgeClick={onEdgeClick}
        onNodeContextMenu={onNodeContextMenu}
        onPaneClick={onPaneClick}
        onPaneContextMenu={onPaneContextMenu}
        onSelectionChange={onSelectionChange}
        nodeTypes={NODE_TYPES}
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
            nodeColor={getNodeColor}
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
        onClose={contextMenu.closeContextMenu}
        onAction={handleQuickAction}
      />
    </div>
  );
}

export const WorkflowCanvas = memo(WorkflowCanvasComponent);
WorkflowCanvas.displayName = 'WorkflowCanvas';

export function WorkflowCanvasWrapper() {
  return (
    <ReactFlowProvider>
      <WorkflowCanvas />
    </ReactFlowProvider>
  );
}
