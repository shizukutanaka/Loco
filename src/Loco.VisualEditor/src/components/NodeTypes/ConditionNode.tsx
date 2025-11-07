import { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { GitBranch } from 'lucide-react';

interface ConditionNodeData {
  label: string;
  condition?: string;
  description?: string;
}

export const ConditionNode = memo(({ data, selected }: NodeProps<ConditionNodeData>) => {
  return (
    <div
      className={`px-4 py-3 rounded-lg border-2 bg-white shadow-lg min-w-[180px] ${
        selected ? 'border-loco-primary ring-2 ring-loco-primary ring-opacity-50' : 'border-yellow-400'
      }`}
    >
      <Handle
        type="target"
        position={Position.Top}
        className="w-3 h-3 !bg-yellow-500 border-2 border-white"
      />
      <div className="flex items-center gap-2 mb-1">
        <div className="w-8 h-8 rounded-full bg-yellow-100 flex items-center justify-center">
          <GitBranch className="w-4 h-4 text-yellow-600" />
        </div>
        <div className="flex-1">
          <div className="text-xs text-gray-500 uppercase font-semibold">Condition</div>
          <div className="font-medium text-sm text-gray-900">{data.label}</div>
        </div>
      </div>
      {data.condition && (
        <div className="text-xs text-gray-600 mt-2 px-2 py-1 bg-gray-50 rounded font-mono">
          {data.condition}
        </div>
      )}
      <Handle
        type="source"
        position={Position.Bottom}
        id="true"
        className="w-3 h-3 !bg-green-500 border-2 border-white !-bottom-1 !left-1/4"
      />
      <Handle
        type="source"
        position={Position.Bottom}
        id="false"
        className="w-3 h-3 !bg-red-500 border-2 border-white !-bottom-1 !left-3/4"
      />
    </div>
  );
});

ConditionNode.displayName = 'ConditionNode';
