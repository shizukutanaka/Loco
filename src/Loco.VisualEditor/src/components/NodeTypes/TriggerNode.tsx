import { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { Play } from 'lucide-react';

interface TriggerNodeData {
  label: string;
  integration?: string;
  description?: string;
}

export const TriggerNode = memo(({ data, selected, id }: NodeProps<TriggerNodeData>) => {
  const descriptionId = data.description ? `trigger-desc-${id}` : undefined;
  const ariaLabel = `Trigger node: ${data.label}${data.integration ? ` (${data.integration})` : ''}`;

  return (
    <>
      <div
        role="button"
        tabIndex={0}
        aria-label={ariaLabel}
        aria-describedby={descriptionId}
        aria-selected={selected}
        className={`px-4 py-3 rounded-lg border-2 bg-white shadow-lg min-w-[180px] transition-all focus:outline-none ${
          selected ? 'border-loco-primary ring-2 ring-loco-primary ring-opacity-50' : 'border-green-400'
        } ${selected ? 'focus:ring-4 focus:ring-loco-primary/50' : 'focus:ring-2 focus:ring-green-400/50'}`}
      >
        <div className="flex items-center gap-2 mb-1">
          <div className="w-8 h-8 rounded-full bg-green-100 flex items-center justify-center" aria-hidden="true">
            <Play className="w-4 h-4 text-green-600" />
          </div>
          <div className="flex-1">
            <div className="text-xs text-gray-500 uppercase font-semibold">Trigger</div>
            <div className="font-medium text-sm text-gray-900">{data.label}</div>
          </div>
        </div>
        {data.integration && (
          <div className="text-xs text-gray-600 mt-2 px-2 py-1 bg-gray-50 rounded">
            {data.integration}
          </div>
        )}
        {data.description && (
          <div id={descriptionId} className="sr-only">
            {data.description}
          </div>
        )}
        <Handle
          type="source"
          position={Position.Bottom}
          aria-label="Output connection point"
          className="w-3 h-3 !bg-green-500 border-2 border-white"
        />
      </div>
    </>
  );
});

TriggerNode.displayName = 'TriggerNode';
