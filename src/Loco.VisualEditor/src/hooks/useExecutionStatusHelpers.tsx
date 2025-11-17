import { ReactNode } from 'react';
import {
  PlayCircle,
  XCircle,
  CheckCircle,
  Clock,
} from 'lucide-react';
import type { WorkflowExecutionResponse } from '@/api/types';
import {
  getExecutionCompletionTime,
} from '@/utils/typeGuards';

/**
 * Custom hook for managing execution status display helpers
 * Handles: status icon rendering, color styling, duration calculation
 */
export function useExecutionStatusHelpers(execution: WorkflowExecutionResponse | null) {
  const getStatusIcon = (): ReactNode => {
    if (!execution) return null;

    switch (execution.status) {
      case 'running':
        return <PlayCircle className="w-5 h-5 text-blue-500 animate-pulse" />;
      case 'completed':
        return <CheckCircle className="w-5 h-5 text-green-500" />;
      case 'failed':
        return <XCircle className="w-5 h-5 text-red-500" />;
      case 'cancelled':
        return <XCircle className="w-5 h-5 text-orange-500" />;
      case 'pending':
      default:
        return <Clock className="w-5 h-5 text-gray-500" />;
    }
  };

  const getStatusColor = (): string => {
    if (!execution) return 'bg-gray-100 text-gray-700';

    switch (execution.status) {
      case 'running':
        return 'bg-blue-100 text-blue-700';
      case 'completed':
        return 'bg-green-100 text-green-700';
      case 'failed':
        return 'bg-red-100 text-red-700';
      case 'cancelled':
        return 'bg-orange-100 text-orange-700';
      case 'pending':
      default:
        return 'bg-gray-100 text-gray-700';
    }
  };

  const getDuration = (): string => {
    if (!execution || !execution.startedAt) return 'N/A';

    const start = new Date(execution.startedAt).getTime();
    const completionTime = getExecutionCompletionTime(execution);
    const end = completionTime
      ? new Date(completionTime).getTime()
      : Date.now();

    const duration = end - start;
    const seconds = Math.floor(duration / 1000);
    const minutes = Math.floor(seconds / 60);

    if (minutes > 0) {
      return `${minutes}m ${seconds % 60}s`;
    }
    return `${seconds}s`;
  };

  return {
    getStatusIcon,
    getStatusColor,
    getDuration,
  };
}
