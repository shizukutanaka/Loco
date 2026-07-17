/**
 * EdgeConditionPanel
 *
 * Lets a user set a connection's routing condition once the connection is
 * selected on the canvas (click, or Tab + Enter via keyboard).
 *
 * The engine's semantics (VisualWorkflowEngine.ShouldFollowConnection,
 * Core/Workflows/VisualWorkflowEngine.cs) are:
 *   - null / "default" / "success"  -> follow only if the source node succeeded
 *   - "error"                       -> follow only if the source node failed
 *   - any other non-empty string    -> always follow (unconditional)
 * So an *unset* condition is NOT "always" - it is "only on success". The
 * options below are labeled to match that real behavior, and "always" is
 * stored as the literal string 'always' (which the engine treats as
 * unconditional via its "anything else" branch).
 */
import { memo, useCallback, useEffect, useState } from 'react';
import { X, Trash2 } from 'lucide-react';
import { useSelectedEdge } from '@/store/selectors';
import { useWorkflowStore } from '@/store/workflowStore';
import { FormSelect, FormInput } from '@/components/Form';
import type { SelectOption } from '@/components/Form/FormSelect';

const CONDITION_OPTIONS: SelectOption[] = [
  { value: '', label: 'Only if the previous step succeeded (default)' },
  { value: 'error', label: 'Only if the previous step failed' },
  { value: 'always', label: 'Always, regardless of outcome' },
  { value: 'custom', label: 'Custom expression…' },
];

/** Values with a dedicated dropdown entry; anything else is a custom expression. */
const KNOWN_VALUES = new Set(['', 'success', 'default', 'error', 'always']);

function EdgeConditionPanelComponent() {
  const selectedEdge = useSelectedEdge();
  const { updateEdgeData, deleteEdge, setSelectedEdgeId } = useWorkflowStore();

  // "Custom expression…" has to be sticky UI state: right after the user picks
  // it the stored condition is still empty, which is indistinguishable from the
  // default - deriving the select value from data alone made the dropdown snap
  // straight back and the input never appeared.
  const [customMode, setCustomMode] = useState(false);
  const selectedEdgeId = selectedEdge?.id;
  useEffect(() => {
    setCustomMode(false);
  }, [selectedEdgeId]);

  const condition = selectedEdge?.data?.condition ?? '';
  const isCustomValue = condition !== '' && !KNOWN_VALUES.has(condition);
  const isCustom = customMode || isCustomValue;
  // 'success' and 'default' behave identically to unset - show them as default
  const selectValue = isCustom
    ? 'custom'
    : condition === 'success' || condition === 'default'
      ? ''
      : condition;

  const handleConditionChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      if (!selectedEdge) return;
      const value = e.target.value;
      if (value === 'custom') {
        // Show the expression input; keep whatever is stored until they type
        setCustomMode(true);
        return;
      }
      setCustomMode(false);
      // Default is represented by omitting the field entirely (undefined),
      // matching the engine's own null-check rather than storing an empty string
      updateEdgeData(selectedEdge.id, { condition: value === '' ? undefined : value });
    },
    [selectedEdge, updateEdgeData]
  );

  const handleCustomConditionChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      if (!selectedEdge) return;
      const text = e.target.value;
      // An empty expression must not be stored as '' - the engine's "anything
      // else" branch would silently treat it as "always follow"
      updateEdgeData(selectedEdge.id, { condition: text === '' ? undefined : text });
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
            value={isCustomValue ? condition : ''}
            onChange={handleCustomConditionChange}
            placeholder="e.g. output.status === 200"
            helpText="Expression evaluation is not yet implemented in the engine - today any custom value behaves like 'Always'. Leaving this empty falls back to the default (only on success)."
          />
        )}
      </div>
    </div>
  );
}

export const EdgeConditionPanel = memo(EdgeConditionPanelComponent);
