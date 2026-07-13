/**
 * EdgeConditionPanel
 *
 * Lets a user set a connection's routing condition (always / on success / on
 * error / custom expression) once the connection is selected on the canvas.
 *
 * VisualWorkflowEngine.ShouldFollowConnection (Core/Workflows/VisualWorkflowEngine.cs)
 * has always interpreted WorkflowConnection.Condition as "default"/null/"success"
 * (follow only if the source node succeeded), "error" (follow only if it failed),
 * or anything else (always follow) - but there was no UI path to set this at all,
 * so error-handling branches were reachable only by hand-editing exported JSON.
 */
import { memo, useCallback } from 'react';
import { X, Trash2 } from 'lucide-react';
import { useSelectedEdge } from '@/store/selectors';
import { useWorkflowStore } from '@/store/workflowStore';
import { FormSelect, FormInput } from '@/components/Form';
import type { SelectOption } from '@/components/Form/FormSelect';

const CONDITION_OPTIONS: SelectOption[] = [
  { value: '', label: 'Always' },
  { value: 'success', label: 'Only if the previous step succeeded' },
  { value: 'error', label: 'Only if the previous step failed' },
  { value: 'custom', label: 'Custom expression…' },
];

const KNOWN_VALUES = new Set(['', 'success', 'error']);

function EdgeConditionPanelComponent() {
  const selectedEdge = useSelectedEdge();
  const { updateEdgeData, deleteEdge, setSelectedEdgeId } = useWorkflowStore();

  const condition = selectedEdge?.data?.condition ?? '';
  const isCustom = condition !== '' && !KNOWN_VALUES.has(condition);
  const selectValue = isCustom ? 'custom' : condition;

  const handleConditionChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      if (!selectedEdge) return;
      const value = e.target.value;
      // "Always" is represented by omitting the field entirely (undefined),
      // matching the engine's own null-check rather than storing an empty string.
      updateEdgeData(selectedEdge.id, { condition: value === '' ? undefined : value === 'custom' ? '' : value });
    },
    [selectedEdge, updateEdgeData]
  );

  const handleCustomConditionChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      if (!selectedEdge) return;
      updateEdgeData(selectedEdge.id, { condition: e.target.value });
    },
    [selectedEdge, updateEdgeData]
  );

  const handleDelete = useCallback(() => {
    if (!selectedEdge) return;
    deleteEdge(selectedEdge.id);
  }, [selectedEdge, deleteEdge]);

  const handleClose = useCallback(() => {
    setSelectedEdgeId(null);
  }, [setSelectedEdgeId]);

  if (!selectedEdge) return null;

  return (
    <div className="w-96 bg-white border-l border-gray-200 flex flex-col h-full">
      <div className="p-4 border-b border-gray-200 flex items-center justify-between">
        <h2 className="text-lg font-semibold text-gray-900">Connection</h2>
        <div className="flex gap-2">
          <button
            onClick={handleDelete}
            className="p-2 text-red-600 hover:bg-red-50 rounded-lg transition-colors"
            title="Delete connection"
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

      <div className="flex-1 overflow-y-auto p-4 flex flex-col gap-4">
        <p className="text-sm text-gray-500">
          From <span className="font-mono text-gray-700">{selectedEdge.source}</span> to{' '}
          <span className="font-mono text-gray-700">{selectedEdge.target}</span>
        </p>

        <FormSelect
          id="edge-condition"
          label="Run this connection"
          value={selectValue}
          onChange={handleConditionChange}
          options={CONDITION_OPTIONS}
          showEmpty={false}
          helpText="Controls whether the workflow follows this connection based on the previous step's outcome."
        />

        {isCustom && (
          <FormInput
            id="edge-condition-custom"
            label="Custom condition expression"
            value={condition}
            onChange={handleCustomConditionChange}
            placeholder="e.g. output.status === 200"
            helpText="Any value other than 'success' or 'error' always follows this connection today; custom expression evaluation is not yet implemented in the engine."
          />
        )}
      </div>
    </div>
  );
}

export const EdgeConditionPanel = memo(EdgeConditionPanelComponent);
