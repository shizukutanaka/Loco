import { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { Play } from 'lucide-react';

interface TriggerNodeData {
  label: string;
  integration?: string;
  description?: string;
}

export const TriggerNode = memo(({ data, selected }: NodeProps<TriggerNodeData>) => {
  return (
    <div
      className={`px-4 py-3 rounded-lg border-2 bg-white shadow-lg min-w-[180px] ${
        selected ? 'border-loco-primary ring-2 ring-loco-primary ring-opacity-50' : 'border-green-400'
      }`}
    >
      <div className="flex items-center gap-2 mb-1">
        <div className="w-8 h-8 rounded-full bg-green-100 flex items-center justify-center">
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
      <Handle
        type="source"
        position={Position.Bottom}
        className="w-3 h-3 !bg-green-500 border-2 border-white"
      />
    </div>
  );
});

TriggerNode.displayName = 'TriggerNode';
