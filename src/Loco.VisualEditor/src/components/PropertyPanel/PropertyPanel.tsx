import { useWorkflowStore } from '@/store/workflowStore';
import { getIntegrationById } from '@/data/integrations';
import { X, Trash2 } from 'lucide-react';
import { useState, useEffect } from 'react';
import { FormInput, FormTextarea, FormSelect } from '@/components/Form';

// ============================================================================
// Types
// ============================================================================

interface NodeConfig {
  condition?: string;
  code?: string;
  action?: string;
  parameters?: Record<string, string | number>;
  [key: string]: unknown;
}

interface NodeData {
  label: string;
  integration?: string;
  config: NodeConfig;
  description?: string;
  [key: string]: unknown;
}

type ConfigValue = string | number | Record<string, unknown> | undefined;

interface ValidationError {
  label?: string;
  condition?: string;
  code?: string;
  action?: string;
  parameters?: Record<string, string>;
}

// ============================================================================
// Validation Functions
// ============================================================================

const validateLabel = (label: string): string | undefined => {
  if (!label.trim()) {
    return 'Node label is required';
  }
  if (label.length > 100) {
    return 'Label must be less than 100 characters';
  }
  return undefined;
};

const validateCondition = (condition: string): string | undefined => {
  if (!condition.trim()) {
    return 'Condition expression is required';
  }
  return undefined;
};

const validateCode = (code: string): string | undefined => {
  if (!code.trim()) {
    return 'Transform code is required';
  }
  return undefined;
};

// ============================================================================
// Property Panel Component
// ============================================================================

export function PropertyPanel() {
  const { nodes, selectedNodeId, updateNode, deleteNode, setSelectedNodeId } =
    useWorkflowStore();

  const selectedNode = nodes.find((n) => n.id === selectedNodeId);
  const [localData, setLocalData] = useState<NodeData>({
    label: '',
    config: {},
  });
  const [errors, setErrors] = useState<ValidationError>({});

  useEffect(() => {
    if (selectedNode) {
      setLocalData(selectedNode.data);
      setErrors({}); // Clear errors when switching nodes
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
    const error = validateLabel(label);
    setErrors({ ...errors, label: error });
    if (!error) {
      updateNode(selectedNode.id, { label });
    }
  };

  const handleConfigChange = (key: string, value: ConfigValue) => {
    const newConfig = { ...localData.config, [key]: value };
    setLocalData({ ...localData, config: newConfig });

    // Validate based on field type
    let error: string | undefined;
    if (key === 'condition') {
      error = validateCondition(String(value || ''));
    } else if (key === 'code') {
      error = validateCode(String(value || ''));
    }

    setErrors({ ...errors, [key]: error });

    // Only update if valid
    if (!error) {
      updateNode(selectedNode.id, { config: newConfig });
    }
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
        <FormInput
          id="node-label"
          type="text"
          label="Node Label"
          value={localData.label || ''}
          onChange={(e) => handleLabelChange(e.target.value)}
          placeholder="Enter node label"
          error={errors.label}
          helpText={`${localData.label?.length || 0}/100 characters`}
        />

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
            <FormTextarea
              id="condition-expression"
              label="Condition Expression"
              value={localData.config?.condition || ''}
              onChange={(e) => handleConfigChange('condition', e.target.value)}
              placeholder="e.g., item.price > 100"
              error={errors.condition}
              rows={3}
              isCode={true}
              helpText="Write a condition that evaluates to true or false"
            />
          )}

          {selectedNode.type === 'transform' && (
            <FormTextarea
              id="transform-code"
              label="Transform Code (C#)"
              value={localData.config?.code || ''}
              onChange={(e) => handleConfigChange('code', e.target.value)}
              placeholder="return items.Select(item => new { ... }).ToList();"
              error={errors.code}
              rows={10}
              isCode={true}
              helpText="Write C# code to transform the input data"
            />
          )}

          {integration && integration.actions && integration.actions.length > 0 && (
            <>
              {/* Action Selection */}
              <FormSelect
                id="action-selection"
                label="Action"
                value={localData.config?.action || ''}
                onChange={(e) => handleConfigChange('action', e.target.value)}
                options={integration.actions.map((action) => ({
                  value: action.id,
                  label: action.name,
                }))}
                placeholder="Select an action"
              />

              {/* Action Parameters */}
              {localData.config?.action && (
                <>
                  {integration.actions
                    .find((a) => a.id === localData.config.action)
                    ?.parameters.map((param) => (
                      <div key={param.name}>
                        {param.type === 'select' ? (
                          <FormSelect
                            id={`param-${param.name}`}
                            label={param.name}
                            value={localData.config?.parameters?.[param.name] || ''}
                            onChange={(e) =>
                              handleConfigChange('parameters', {
                                ...localData.config?.parameters,
                                [param.name]: e.target.value,
                              })
                            }
                            options={param.options?.map((opt) => ({
                              value: opt.value,
                              label: opt.label,
                            })) || []}
                            placeholder="Select..."
                            helpText={param.description}
                            required={param.required}
                          />
                        ) : param.type === 'json' || param.type === 'code' ? (
                          <FormTextarea
                            id={`param-${param.name}`}
                            label={param.name}
                            value={String(localData.config?.parameters?.[param.name] || '')}
                            onChange={(e) =>
                              handleConfigChange('parameters', {
                                ...localData.config?.parameters,
                                [param.name]: e.target.value,
                              })
                            }
                            placeholder={param.description}
                            rows={4}
                            isCode={true}
                            helpText={param.description}
                          />
                        ) : (
                          <FormInput
                            id={`param-${param.name}`}
                            type={param.type === 'number' ? 'number' : 'text'}
                            label={param.name}
                            value={localData.config?.parameters?.[param.name] || ''}
                            onChange={(e) =>
                              handleConfigChange('parameters', {
                                ...localData.config?.parameters,
                                [param.name]: e.target.value,
                              })
                            }
                            placeholder={param.description}
                            helpText={param.description}
                            required={param.required}
                          />
                        )}
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
