import { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { Code2 } from 'lucide-react';

interface TransformNodeData {
  label: string;
  description?: string;
}

export const TransformNode = memo(({ data, selected, id }: NodeProps<TransformNodeData>) => {
  const descriptionId = data.description ? `transform-desc-${id}` : undefined;
  const ariaLabel = `Transform node: ${data.label}`;

  return (
    <>
      <div
        role="button"
        tabIndex={0}
        aria-label={ariaLabel}
        aria-describedby={descriptionId}
        aria-selected={selected}
        className={`px-4 py-3 rounded-lg border-2 bg-white shadow-lg min-w-[180px] transition-all focus:outline-none ${
          selected ? 'border-loco-primary ring-2 ring-loco-primary ring-opacity-50' : 'border-purple-400'
        } ${selected ? 'focus:ring-4 focus:ring-loco-primary/50' : 'focus:ring-2 focus:ring-purple-400/50'}`}
      >
        <Handle
          type="target"
          position={Position.Top}
          aria-label="Input connection point"
          className="w-3 h-3 !bg-purple-500 border-2 border-white"
        />
        <div className="flex items-center gap-2 mb-1">
          <div className="w-8 h-8 rounded-full bg-purple-100 flex items-center justify-center" aria-hidden="true">
            <Code2 className="w-4 h-4 text-purple-600" />
          </div>
          <div className="flex-1">
            <div className="text-xs text-gray-500 uppercase font-semibold">Transform</div>
            <div className="font-medium text-sm text-gray-900">{data.label}</div>
          </div>
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
          className="w-3 h-3 !bg-purple-500 border-2 border-white"
        />
      </div>
    </>
  );
});

TransformNode.displayName = 'TransformNode';
