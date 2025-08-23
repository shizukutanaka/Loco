import React, { memo, useCallback, useMemo, useState } from 'react';
import dynamic from 'next/dynamic';
import { 
  Plus,
  Save,
  Play,
  Download,
  Upload,
  Settings,
  Trash2,
  Copy
} from 'lucide-react';

// Lazy load React Flow for better initial load
const ReactFlow = dynamic(
  () => import('react-flow-renderer'),
  { 
    ssr: false,
    loading: () => <div className="h-full bg-gray-800 animate-pulse rounded-lg" />
  }
);

// Import node types dynamically
const TriggerNode = dynamic(() => import('./nodes/TriggerNode'), { ssr: false });
const ActionNode = dynamic(() => import('./nodes/ActionNode'), { ssr: false });
const ConditionNode = dynamic(() => import('./nodes/ConditionNode'), { ssr: false });

// Memoized toolbar button
const ToolbarButton = memo(({ icon: Icon, label, onClick, variant = 'default' }) => {
  const variants = {
    default: 'bg-gray-700 hover:bg-gray-600 text-white',
    primary: 'bg-blue-600 hover:bg-blue-500 text-white',
    success: 'bg-green-600 hover:bg-green-500 text-white',
    danger: 'bg-red-600 hover:bg-red-500 text-white'
  };

  return (
    <button
      onClick={onClick}
      className={`px-3 py-2 rounded-lg transition-all flex items-center space-x-2 ${variants[variant]}`}
      title={label}
    >
      <Icon className="h-4 w-4" />
      <span className="hidden sm:inline text-sm">{label}</span>
    </button>
  );
});

ToolbarButton.displayName = 'ToolbarButton';

// Optimized Flow Builder component
const FlowBuilder = memo(() => {
  const [nodes, setNodes] = useState([]);
  const [edges, setEdges] = useState([]);
  const [selectedNode, setSelectedNode] = useState(null);

  // Memoize node types
  const nodeTypes = useMemo(() => ({
    trigger: TriggerNode,
    action: ActionNode,
    condition: ConditionNode
  }), []);

  // Memoize default edge options
  const defaultEdgeOptions = useMemo(() => ({
    animated: true,
    style: { stroke: '#4B5563', strokeWidth: 2 }
  }), []);

  // Use callbacks for all event handlers
  const onNodesChange = useCallback((changes) => {
    setNodes((nds) => applyNodeChanges(changes, nds));
  }, []);

  const onEdgesChange = useCallback((changes) => {
    setEdges((eds) => applyEdgeChanges(changes, eds));
  }, []);

  const onConnect = useCallback((params) => {
    setEdges((eds) => addEdge({ ...params, animated: true }, eds));
  }, []);

  const onNodeClick = useCallback((event, node) => {
    setSelectedNode(node);
  }, []);

  const addNode = useCallback((type) => {
    const newNode = {
      id: `${type}_${Date.now()}`,
      type,
      position: { 
        x: Math.random() * 400 + 100, 
        y: Math.random() * 300 + 100 
      },
      data: { 
        label: `New ${type}`,
        config: {}
      }
    };
    setNodes((nds) => [...nds, newNode]);
  }, []);

  const deleteSelectedNode = useCallback(() => {
    if (selectedNode) {
      setNodes((nds) => nds.filter(n => n.id !== selectedNode.id));
      setEdges((eds) => eds.filter(e => 
        e.source !== selectedNode.id && e.target !== selectedNode.id
      ));
      setSelectedNode(null);
    }
  }, [selectedNode]);

  const saveFlow = useCallback(() => {
    const flow = { nodes, edges };
    const json = JSON.stringify(flow, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `flow_${Date.now()}.json`;
    link.click();
    URL.revokeObjectURL(url);
  }, [nodes, edges]);

  const loadFlow = useCallback((event) => {
    const file = event.target.files[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = (e) => {
        try {
          const flow = JSON.parse(e.target.result);
          setNodes(flow.nodes || []);
          setEdges(flow.edges || []);
        } catch (error) {
          console.error('Failed to load flow:', error);
        }
      };
      reader.readAsText(file);
    }
  }, []);

  const executeFlow = useCallback(() => {
    console.log('Executing flow with nodes:', nodes, 'and edges:', edges);
    // Add execution logic here
  }, [nodes, edges]);

  // Memoize component sections
  const toolbar = useMemo(() => (
    <div className="absolute top-4 left-4 z-10 bg-gray-800 rounded-lg p-2 shadow-lg border border-gray-700">
      <div className="flex space-x-2">
        <ToolbarButton 
          icon={Plus} 
          label="Trigger" 
          onClick={() => addNode('trigger')}
        />
        <ToolbarButton 
          icon={Plus} 
          label="Action" 
          onClick={() => addNode('action')}
          variant="primary"
        />
        <ToolbarButton 
          icon={Plus} 
          label="Condition" 
          onClick={() => addNode('condition')}
        />
      </div>
    </div>
  ), [addNode]);

  const actionBar = useMemo(() => (
    <div className="absolute top-4 right-4 z-10 bg-gray-800 rounded-lg p-2 shadow-lg border border-gray-700">
      <div className="flex space-x-2">
        <ToolbarButton 
          icon={Save} 
          label="Save" 
          onClick={saveFlow}
        />
        <label className="px-3 py-2 rounded-lg bg-gray-700 hover:bg-gray-600 text-white transition-all flex items-center space-x-2 cursor-pointer">
          <Upload className="h-4 w-4" />
          <span className="hidden sm:inline text-sm">Load</span>
          <input 
            type="file" 
            accept=".json"
            onChange={loadFlow}
            className="hidden"
          />
        </label>
        <ToolbarButton 
          icon={Play} 
          label="Run" 
          onClick={executeFlow}
          variant="success"
        />
        {selectedNode && (
          <ToolbarButton 
            icon={Trash2} 
            label="Delete" 
            onClick={deleteSelectedNode}
            variant="danger"
          />
        )}
      </div>
    </div>
  ), [saveFlow, loadFlow, executeFlow, selectedNode, deleteSelectedNode]);

  return (
    <div className="h-full bg-gray-900 relative">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onConnect={onConnect}
        onNodeClick={onNodeClick}
        nodeTypes={nodeTypes}
        defaultEdgeOptions={defaultEdgeOptions}
        fitView
        className="bg-gray-900"
      >
        {toolbar}
        {actionBar}
        
        {/* Node Properties Panel */}
        {selectedNode && (
          <NodePropertiesPanel 
            node={selectedNode}
            onUpdate={(updates) => {
              setNodes((nds) => 
                nds.map(n => 
                  n.id === selectedNode.id 
                    ? { ...n, data: { ...n.data, ...updates } }
                    : n
                )
              );
            }}
            onClose={() => setSelectedNode(null)}
          />
        )}
      </ReactFlow>
    </div>
  );
});

FlowBuilder.displayName = 'FlowBuilder';

// Memoized properties panel
const NodePropertiesPanel = memo(({ node, onUpdate, onClose }) => {
  const [localData, setLocalData] = useState(node.data);

  const handleChange = useCallback((key, value) => {
    const updates = { ...localData, [key]: value };
    setLocalData(updates);
    onUpdate(updates);
  }, [localData, onUpdate]);

  return (
    <div className="absolute bottom-4 right-4 z-10 bg-gray-800 rounded-lg p-4 shadow-lg border border-gray-700 w-80">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-white font-semibold flex items-center">
          <Settings className="h-4 w-4 mr-2" />
          Node Properties
        </h3>
        <button 
          onClick={onClose}
          className="text-gray-400 hover:text-white transition-colors"
        >
          ×
        </button>
      </div>
      
      <div className="space-y-3">
        <div>
          <label className="block text-sm text-gray-400 mb-1">Label</label>
          <input
            type="text"
            value={localData.label || ''}
            onChange={(e) => handleChange('label', e.target.value)}
            className="w-full px-3 py-2 bg-gray-700 text-white rounded-lg border border-gray-600 focus:border-blue-500 focus:outline-none"
          />
        </div>
        
        <div>
          <label className="block text-sm text-gray-400 mb-1">Type</label>
          <input
            type="text"
            value={node.type}
            disabled
            className="w-full px-3 py-2 bg-gray-700 text-gray-400 rounded-lg border border-gray-600"
          />
        </div>
        
        <div>
          <label className="block text-sm text-gray-400 mb-1">ID</label>
          <input
            type="text"
            value={node.id}
            disabled
            className="w-full px-3 py-2 bg-gray-700 text-gray-400 rounded-lg border border-gray-600 font-mono text-xs"
          />
        </div>
      </div>
    </div>
  );
});

NodePropertiesPanel.displayName = 'NodePropertiesPanel';

// Helper functions (should be imported from react-flow-renderer)
const applyNodeChanges = (changes, nodes) => {
  // Simplified implementation - should use react-flow-renderer's helper
  return nodes;
};

const applyEdgeChanges = (changes, edges) => {
  // Simplified implementation - should use react-flow-renderer's helper
  return edges;
};

const addEdge = (params, edges) => {
  // Simplified implementation - should use react-flow-renderer's helper
  return [...edges, { ...params, id: `e${params.source}-${params.target}` }];
};

export default FlowBuilder;
