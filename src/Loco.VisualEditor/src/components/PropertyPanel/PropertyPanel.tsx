import { useWorkflowStore } from '@/store/workflowStore';
import { getIntegrationById } from '@/data/integrations';
import { X, Trash2 } from 'lucide-react';
import { useState, useEffect } from 'react';

export function PropertyPanel() {
  const { nodes, selectedNodeId, updateNode, deleteNode, setSelectedNodeId } =
    useWorkflowStore();

  const selectedNode = nodes.find((n) => n.id === selectedNodeId);
  const [localData, setLocalData] = useState<any>({});

  useEffect(() => {
    if (selectedNode) {
      setLocalData(selectedNode.data);
    }
  }, [selectedNode]);

  if (!selectedNode) {
    return (
      <div className="w-96 bg-white border-l border-gray-200 p-6 flex items-center justify-center text-gray-500">
        <div className="text-center">
          <div className="text-4xl mb-2">👈</div>
          <div className="text-sm">Select a node to configure</div>
        </div>
      </div>
    );
  }

  const integration = selectedNode.data.integration
    ? getIntegrationById(selectedNode.data.integration)
    : null;

  const handleLabelChange = (label: string) => {
    setLocalData({ ...localData, label });
    updateNode(selectedNode.id, { label });
  };

  const handleConfigChange = (key: string, value: any) => {
    const newConfig = { ...localData.config, [key]: value };
    setLocalData({ ...localData, config: newConfig });
    updateNode(selectedNode.id, { config: newConfig });
  };

  const handleDelete = () => {
    deleteNode(selectedNode.id);
  };

  return (
    <div className="w-96 bg-white border-l border-gray-200 flex flex-col h-full">
      <div className="p-4 border-b border-gray-200 flex items-center justify-between">
        <h2 className="text-lg font-semibold text-gray-900">Node Properties</h2>
        <div className="flex gap-2">
          <button
            onClick={handleDelete}
            className="p-2 text-red-600 hover:bg-red-50 rounded-lg transition-colors"
            title="Delete node"
          >
            <Trash2 className="w-4 h-4" />
          </button>
          <button
            onClick={() => setSelectedNodeId(null)}
            className="p-2 text-gray-500 hover:bg-gray-100 rounded-lg transition-colors"
            title="Close"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-4">
        {/* Node Type Badge */}
        <div className="mb-4">
          <span
            className={`inline-block px-3 py-1 rounded-full text-xs font-semibold uppercase ${
              selectedNode.type === 'trigger'
                ? 'bg-green-100 text-green-700'
                : selectedNode.type === 'action'
                ? 'bg-blue-100 text-blue-700'
                : selectedNode.type === 'condition'
                ? 'bg-yellow-100 text-yellow-700'
                : selectedNode.type === 'transform'
                ? 'bg-purple-100 text-purple-700'
                : 'bg-orange-100 text-orange-700'
            }`}
          >
            {selectedNode.type}
          </span>
        </div>

        {/* Node Label */}
        <div className="mb-4">
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Node Label
          </label>
          <input
            type="text"
            value={localData.label || ''}
            onChange={(e) => handleLabelChange(e.target.value)}
            className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
            placeholder="Enter node label"
          />
        </div>

        {/* Integration Info */}
        {integration && (
          <div className="mb-4 p-3 bg-gray-50 rounded-lg">
            <div className="flex items-center gap-2 mb-2">
              <span className="text-2xl">{integration.icon}</span>
              <div>
                <div className="font-medium text-sm">{integration.name}</div>
                <div className="text-xs text-gray-600">{integration.category}</div>
              </div>
            </div>
            <div className="text-xs text-gray-600">{integration.description}</div>
          </div>
        )}

        {/* Configuration Fields */}
        <div className="space-y-4">
          <h3 className="text-sm font-semibold text-gray-700">Configuration</h3>

          {selectedNode.type === 'condition' && (
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Condition Expression
              </label>
              <textarea
                value={localData.config?.condition || ''}
                onChange={(e) => handleConfigChange('condition', e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent font-mono text-sm"
                rows={3}
                placeholder="e.g., item.price > 100"
              />
            </div>
          )}

          {selectedNode.type === 'transform' && (
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Transform Code (C#)
              </label>
              <textarea
                value={localData.config?.code || ''}
                onChange={(e) => handleConfigChange('code', e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent font-mono text-sm"
                rows={10}
                placeholder="return items.Select(item => new { ... }).ToList();"
              />
            </div>
          )}

          {integration && integration.actions && integration.actions.length > 0 && (
            <>
              {/* Action Selection */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Action
                </label>
                <select
                  value={localData.config?.action || ''}
                  onChange={(e) => handleConfigChange('action', e.target.value)}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
                >
                  <option value="">Select an action</option>
                  {integration.actions.map((action) => (
                    <option key={action.id} value={action.id}>
                      {action.name}
                    </option>
                  ))}
                </select>
              </div>

              {/* Action Parameters */}
              {localData.config?.action && (
                <>
                  {integration.actions
                    .find((a) => a.id === localData.config.action)
                    ?.parameters.map((param) => (
                      <div key={param.name}>
                        <label className="block text-sm font-medium text-gray-700 mb-2">
                          {param.name}
                          {param.required && (
                            <span className="text-red-500 ml-1">*</span>
                          )}
                        </label>
                        {param.type === 'select' ? (
                          <select
                            value={localData.config?.parameters?.[param.name] || ''}
                            onChange={(e) =>
                              handleConfigChange('parameters', {
                                ...localData.config?.parameters,
                                [param.name]: e.target.value,
                              })
                            }
                            className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
                          >
                            <option value="">Select...</option>
                            {param.options?.map((opt) => (
                              <option key={opt.value} value={opt.value}>
                                {opt.label}
                              </option>
                            ))}
                          </select>
                        ) : param.type === 'json' || param.type === 'code' ? (
                          <textarea
                            value={localData.config?.parameters?.[param.name] || ''}
                            onChange={(e) =>
                              handleConfigChange('parameters', {
                                ...localData.config?.parameters,
                                [param.name]: e.target.value,
                              })
                            }
                            className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent font-mono text-sm"
                            rows={4}
                            placeholder={param.description}
                          />
                        ) : (
                          <input
                            type={param.type === 'number' ? 'number' : 'text'}
                            value={localData.config?.parameters?.[param.name] || ''}
                            onChange={(e) =>
                              handleConfigChange('parameters', {
                                ...localData.config?.parameters,
                                [param.name]: e.target.value,
                              })
                            }
                            className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
                            placeholder={param.description}
                          />
                        )}
                        <p className="text-xs text-gray-500 mt-1">{param.description}</p>
                      </div>
                    ))}
                </>
              )}
            </>
          )}
        </div>

        {/* Node Info */}
        <div className="mt-6 pt-4 border-t border-gray-200">
          <div className="text-xs text-gray-500">
            <div className="mb-1">
              <span className="font-medium">Node ID:</span> {selectedNode.id}
            </div>
            <div>
              <span className="font-medium">Position:</span> ({Math.round(selectedNode.position.x)},{' '}
              {Math.round(selectedNode.position.y)})
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
