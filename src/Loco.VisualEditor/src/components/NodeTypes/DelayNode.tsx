import { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { Clock } from 'lucide-react';

/**
 * The renderer for `delay`, which had none.
 *
 * `delay` is a NodeType (types/workflow.ts), the palette offers it, the
 * PropertyPanel edits its `seconds`, the simulator honours it and the engine
 * has always implemented it - but it was absent from the canvas' nodeTypes
 * map, so React Flow fell back to its generic default node and logged an
 * error. A dropped Delay was a plain grey box among five styled ones.
 *
 * `seconds` is shown on the face because a delay whose duration is invisible
 * is indistinguishable from any other delay, and it is the only thing about
 * this node worth reading at a glance.
 */
interface DelayNodeData {
  label: string;
  description?: string;
  config?: { seconds?: number | string };
}

export const DelayNode = memo(({ data, selected, id }: NodeProps<DelayNodeData>) => {
  const descriptionId = data.description ? `delay-desc-${id}` : undefined;
  const seconds = Number(data.config?.seconds ?? 0);
  const duration = Number.isFinite(seconds) && seconds > 0 ? `${seconds}s` : 'not set';
  const ariaLabel = `Delay node: ${data.label}, waiting ${duration}`;

  return (
    <>
      <div
        role="button"
        tabIndex={0}
        aria-label={ariaLabel}
        aria-describedby={descriptionId}
        aria-selected={selected}
        className={`px-4 py-3 rounded-lg border-2 bg-white shadow-lg min-w-[180px] transition-all focus:outline-none ${
          selected ? 'border-loco-primary ring-2 ring-loco-primary ring-opacity-50' : 'border-slate-400'
        } ${selected ? 'focus:ring-4 focus:ring-loco-primary/50' : 'focus:ring-2 focus:ring-slate-400/50'}`}
      >
        <Handle
          type="target"
          position={Position.Top}
          aria-label="Input connection point"
          className="w-3 h-3 !bg-slate-500 border-2 border-white"
        />
        <div className="flex items-center gap-2 mb-1">
          <div className="w-8 h-8 rounded-full bg-slate-100 flex items-center justify-center" aria-hidden="true">
            <Clock className="w-4 h-4 text-slate-600" />
          </div>
          <div className="flex-1">
            <div className="text-xs text-gray-500 uppercase font-semibold">Delay</div>
            <div className="font-medium text-sm text-gray-900">{data.label}</div>
          </div>
        </div>
        <div className="text-xs text-gray-600 mt-2 px-2 py-1 bg-gray-50 rounded">
          Wait {duration}
        </div>
        {data.description && (
          <div id={descriptionId} className="sr-only">
            {data.description}
          </div>
        )}
        <Handle
          type="source"
          position={Position.Bottom}
          aria-label="Output connection point"
          className="w-3 h-3 !bg-slate-500 border-2 border-white"
        />
      </div>
    </>
  );
});

DelayNode.displayName = 'DelayNode';
