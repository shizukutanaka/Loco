import { memo, useMemo, useCallback } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';
import { getIntegrationById } from '@/data/integrations';
import { X, Trash2 } from 'lucide-react';
import { FormInput, FormTextarea, FormSelect } from '@/components/Form';
import {
  usePropertyPanelFormState,
  usePropertyPanelActions,
} from '@/hooks';

// ============================================================================
// Property Panel Component
// ============================================================================

function PropertyPanelComponent() {
  const { nodes, selectedNodeId } = useWorkflowStore();

  // Use custom hooks
  const { localData, errors, handleLabelChange, handleConfigChange } =
    usePropertyPanelFormState(selectedNodeId);
  const { handleDelete, handleClose } = usePropertyPanelActions(selectedNodeId);

  // Memoize selected node lookup to preserve referential equality
  const memoizedSelectedNode = useMemo(
    () => nodes.find((n) => n.id === selectedNodeId),
    [nodes, selectedNodeId]
  );

  // Memoize event handler for action selection to prevent FormSelect re-renders
  const handleActionChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => handleConfigChange('action', e.target.value),
    [handleConfigChange]
  );

  // Memoize label change handler to preserve referential equality
  const handleLabelInput = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => handleLabelChange(e.target.value),
    [handleLabelChange]
  );

  if (!memoizedSelectedNode) {
    return (
      <div className="w-96 bg-white border-l border-gray-200 p-6 flex items-center justify-center text-gray-500">
        <div className="text-center">
          <div className="text-4xl mb-2">👈</div>
          <div className="text-sm">Select a node to configure</div>
        </div>
      </div>
    );
  }

  // Update integration memoization after guard to ensure memoizedSelectedNode exists
  const memoizedIntegration = useMemo(
    () => getIntegrationById(memoizedSelectedNode.data.integration),
    [memoizedSelectedNode.data.integration]
  );

  // Use memoizedIntegration instead of integration
  const finalIntegration = memoizedIntegration;

  // Memoize action options to prevent unnecessary FormSelect re-renders
  const actionOptions = useMemo(
    () => finalIntegration?.actions?.map((action) => ({
      value: action.id,
      label: action.name,
    })) || [],
    [finalIntegration?.actions]
  );

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
            onClick={handleClose}
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
              memoizedSelectedNode.type === 'trigger'
                ? 'bg-green-100 text-green-700'
                : memoizedSelectedNode.type === 'action'
                ? 'bg-blue-100 text-blue-700'
                : memoizedSelectedNode.type === 'condition'
                ? 'bg-yellow-100 text-yellow-700'
                : memoizedSelectedNode.type === 'transform'
                ? 'bg-purple-100 text-purple-700'
                : 'bg-orange-100 text-orange-700'
            }`}
          >
            {memoizedSelectedNode.type}
          </span>
        </div>

        {/* Node Label */}
        <FormInput
          id="node-label"
          type="text"
          label="Node Label"
          value={localData.label || ''}
          onChange={handleLabelInput}
          placeholder="Enter node label"
          error={errors.label}
          helpText={`${localData.label?.length || 0}/100 characters`}
        />

        {/* Integration Info */}
        {finalIntegration && (
          <div className="mb-4 p-3 bg-gray-50 rounded-lg">
            <div className="flex items-center gap-2 mb-2">
              <span className="text-2xl">{finalIntegration.icon}</span>
              <div>
                <div className="font-medium text-sm">{finalIntegration.name}</div>
                <div className="text-xs text-gray-600">{finalIntegration.category}</div>
              </div>
            </div>
            <div className="text-xs text-gray-600">{finalIntegration.description}</div>
          </div>
        )}

        {/* Configuration Fields */}
        <div className="space-y-4">
          <h3 className="text-sm font-semibold text-gray-700">Configuration</h3>

          {memoizedSelectedNode.type === 'condition' && (
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

          {memoizedSelectedNode.type === 'transform' && (
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

          {finalIntegration && actionOptions.length > 0 && (
            <>
              {/* Action Selection */}
              <FormSelect
                id="action-selection"
                label="Action"
                value={localData.config?.action || ''}
                onChange={(e) => handleActionChange(e)}
                options={actionOptions}
                placeholder="Select an action"
              />

              {/* Action Parameters */}
              {localData.config?.action && (
                <>
                  {finalIntegration.actions
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
              <span className="font-medium">Node ID:</span> {memoizedSelectedNode.id}
            </div>
            <div>
              <span className="font-medium">Position:</span> ({Math.round(memoizedSelectedNode.position.x)},{' '}
              {Math.round(memoizedSelectedNode.position.y)})
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export const PropertyPanel = memo(PropertyPanelComponent);
PropertyPanel.displayName = 'PropertyPanel';
