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
 *   - "always"                      -> follow whichever way the node went
 *   - anything else                 -> the engine refuses the edge
 * So an *unset* condition is NOT "always" - it is "only on success", and the
 * options below are labeled to match. 'always' is a real case the engine
 * handles by name; it used to work only by falling through an "anything else
 * -> follow it" branch that also made every unevaluatable expression fire.
 *
 * Branch handles are a separate thing: a condition node's true/false outputs
 * are carried as the edge's sourceHandle, not as a condition value.
 */
import { memo, useCallback } from 'react';
import { X, Trash2 } from 'lucide-react';
import { useSelectedEdge } from '@/store/selectors';
import { useWorkflowStore } from '@/store/workflowStore';
import { FormSelect } from '@/components/Form';
import type { SelectOption } from '@/components/Form/FormSelect';

const CONDITION_OPTIONS: SelectOption[] = [
  { value: '', label: 'Only if the previous step succeeded (default)' },
  { value: 'error', label: 'Only if the previous step failed' },
  { value: 'always', label: 'Always, regardless of outcome' },
];

/**
 * Every value the engine understands. Anything else makes it refuse the edge -
 * so a workflow saved by an older build with a custom expression still opens,
 * and the panel shows it as unsupported rather than pretending it routes.
 */
const KNOWN_VALUES = new Set(['', 'success', 'default', 'error', 'always']);

function EdgeConditionPanelComponent() {
  const selectedEdge = useSelectedEdge();
  const { updateEdgeData, deleteEdge, setSelectedEdgeId } = useWorkflowStore();

  const condition = selectedEdge?.data?.condition ?? '';
  // A value from an older build, when this panel offered a free-text
  // expression. The engine refuses those now rather than following them
  // unconditionally, so the panel says so instead of offering to write more.
  const isUnsupported = condition !== '' && !KNOWN_VALUES.has(condition);
  // 'success' and 'default' behave identically to unset - show them as default
  const selectValue = isUnsupported
    ? ''
    : condition === 'success' || condition === 'default'
      ? ''
      : condition;

  const handleConditionChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      if (!selectedEdge) return;
      const value = e.target.value;
      // Default is represented by omitting the field entirely (undefined),
      // matching the engine's own null-check rather than storing an empty string
      updateEdgeData(selectedEdge.id, { condition: value === '' ? undefined : value });
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

        {isUnsupported && (
          <div className="text-xs text-amber-700" role="alert">
            This connection stores <span className="font-mono">{condition}</span>, a custom
            expression. The engine cannot evaluate expressions and now refuses such an edge
            rather than following it regardless of what it says — pick one of the options
            above, or put the comparison in a condition node.
          </div>
        )}
      </div>
    </div>
  );
}

export const EdgeConditionPanel = memo(EdgeConditionPanelComponent);
