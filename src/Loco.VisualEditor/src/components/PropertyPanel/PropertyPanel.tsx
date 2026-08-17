import { memo, useCallback, useMemo } from 'react';
import { useSelectedNode, useSelectedNodeId } from '@/store/selectors';
import { getIntegrationById } from '@/data/integrations';
import { X, Trash2 } from 'lucide-react';
import { FormInput, FormTextarea, FormSelect } from '@/components/Form';
import {
  usePropertyPanelFormState,
  usePropertyPanelActions,
} from '@/hooks';

// ============================================================================
// Constants
// ============================================================================

/**
 * Transform modes the built-in handler supports. "json" deserializes the `json`
 * parameter; anything else returns the `input` parameter unchanged.
 */
const TRANSFORM_TYPES = [
  { value: 'json', label: 'JSON literal' },
  { value: 'passthrough', label: 'Passthrough (input unchanged)' },
];

/**
 * The comparisons the engine's built-in condition handler implements. Keep in
 * sync with the `operation switch` in
 * VisualWorkflowEngine.RegisterDefaultHandlers - an operation not listed there
 * falls through to `_ => false`.
 */
const CONDITION_OPERATIONS = [
  { value: 'equals', label: 'equals' },
  { value: 'not_equals', label: 'does not equal' },
  { value: 'greater_than', label: 'is greater than' },
  { value: 'less_than', label: 'is less than' },
  { value: 'contains', label: 'contains' },
];

// ============================================================================
// Property Panel Component
// ============================================================================

function PropertyPanelComponent() {
  // Use granular selectors to only subscribe to selected node/ID, not entire nodes array
  // This prevents re-renders when unselected nodes change
  const selectedNode = useSelectedNode();
  const selectedNodeId = useSelectedNodeId();

  // Use custom hooks
  const { localData, errors, handleLabelChange, handleConfigChange } =
    usePropertyPanelFormState(selectedNodeId);
  const { handleDelete, handleClose } = usePropertyPanelActions(selectedNodeId);

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

  // These useMemo calls previously sat AFTER the `if (!selectedNode) return`
  // early return, so the hook count changed when a node was (de)selected,
  // triggering React's "rendered more hooks than during the previous render"
  // crash. They now run unconditionally with optional chaining and the render
  // is gated below.
  const finalIntegration = useMemo(
    () => (selectedNode ? getIntegrationById(selectedNode.data.integration) : undefined),
    [selectedNode]
  );

  // Memoize action options to prevent unnecessary FormSelect re-renders
  const actionOptions = useMemo(
    () => finalIntegration?.actions?.map((action) => ({
      value: action.id,
      label: action.name,
    })) || [],
    [finalIntegration?.actions]
  );

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

          {/*
            The engine's built-in condition handler compares three parameters -
            left, operation, right (VisualWorkflowEngine.RegisterDefaultHandlers).
            It has no expression parser. This panel used to write a single
            free-text `condition` string, which the engine never read, so every
            condition node evaluated Equals(null, null) - i.e. TRUE, always, and
            silently. These inputs write the parameters the engine actually
            evaluates.
          */}
          {selectedNode.type === 'condition' && (
            <>
              <FormInput
                id="condition-left"
                label="Left Value"
                value={String(localData.config?.left ?? '')}
                onChange={(e) => handleConfigChange('left', e.target.value)}
                placeholder="e.g., {{price}}"
                error={errors.left}
                helpText="Value to compare. Supports {{variable}} references."
              />
              <FormSelect
                id="condition-operation"
                label="Operation"
                value={String(localData.config?.operation ?? 'equals')}
                onChange={(e) => handleConfigChange('operation', e.target.value)}
                options={CONDITION_OPERATIONS}
              />
              <FormInput
                id="condition-right"
                label="Right Value"
                value={String(localData.config?.right ?? '')}
                onChange={(e) => handleConfigChange('right', e.target.value)}
                placeholder="e.g., 100"
                error={errors.right}
                helpText="Value to compare against."
              />
            </>
          )}

          {/*
            The built-in transform handler reads `type` and, for type "json",
            `json` - it does NOT compile or run C#. This panel previously offered
            a "Transform Code (C#)" editor writing config.code, which the engine
            never read: the field did nothing and advertised a capability that
            does not exist. These inputs expose what the handler actually does.
          */}
          {selectedNode.type === 'transform' && (
            <>
              <FormSelect
                id="transform-type"
                label="Transform Type"
                value={String(localData.config?.type ?? 'json')}
                onChange={(e) => handleConfigChange('type', e.target.value)}
                options={TRANSFORM_TYPES}
                helpText="JSON parses the literal below; Passthrough forwards the input unchanged."
              />
              {String(localData.config?.type ?? 'json') === 'json' && (
                <FormTextarea
                  id="transform-json"
                  label="JSON"
                  value={String(localData.config?.json ?? '')}
                  onChange={(e) => handleConfigChange('json', e.target.value)}
                  placeholder='{ "key": "value" }'
                  error={errors.json}
                  rows={8}
                  isCode={true}
                  helpText="Parsed and emitted as this node's output."
                />
              )}
            </>
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

export const PropertyPanel = memo(PropertyPanelComponent);
PropertyPanel.displayName = 'PropertyPanel';
