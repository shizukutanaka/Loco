/**
 * Type Guards and Discriminator Helpers
 *
 * Provides utility functions for safely working with discriminated union types.
 * These help narrow types and ensure exhaustiveness checking.
 */

import {
  ApiResponse,
  ApiError,
  WorkflowExecutionResponse,
  ExecutionError,
  ExecutionLog,
} from '@/api/types';

/**
 * Type guard to check if ApiResponse indicates success
 * Narrows type to success variant
 */
export function isApiSuccess<T>(response: ApiResponse<T>): response is { success: true; data: T; message?: string } {
  return response.success === true;
}

/**
 * Type guard to check if ApiResponse indicates failure
 * Narrows type to error variant
 */
export function isApiError<T>(response: ApiResponse<T>): response is { success: false; error: ApiError; message?: string } {
  return response.success === false;
}

/**
 * Type guard to check if execution is in progress (pending or running)
 */
export function isExecutionInProgress(
  execution: WorkflowExecutionResponse
): execution is { executionId: string; status: 'pending' | 'running'; startedAt: string; logs?: ExecutionLog[] } {
  return execution.status === 'pending' || execution.status === 'running';
}

/**
 * Type guard to check if execution is completed
 */
export function isExecutionCompleted(
  execution: WorkflowExecutionResponse
): execution is { executionId: string; status: 'completed'; startedAt: string; completedAt: string; output: Record<string, unknown>; logs?: ExecutionLog[] } {
  return execution.status === 'completed';
}

/**
 * Type guard to check if execution has failed or been cancelled
 */
export function isExecutionTerminated(
  execution: WorkflowExecutionResponse
): execution is { executionId: string; status: 'failed' | 'cancelled'; startedAt: string; completedAt: string; error: ExecutionError; logs?: ExecutionLog[] } {
  return execution.status === 'failed' || execution.status === 'cancelled';
}

/**
 * Type guard to check if execution has ended (completed, failed, or cancelled)
 */
export function isExecutionEnded(execution: WorkflowExecutionResponse): boolean {
  return execution.status === 'completed' || execution.status === 'failed' || execution.status === 'cancelled';
}

/**
 * Safely extract error from API response if it failed
 */
export function getApiError<T>(response: ApiResponse<T>): string {
  if (isApiError(response)) {
    return response.error.message || 'Unknown error';
  }
  return '';
}

/**
 * Safely extract data from API response if it succeeded
 */
export function getApiData<T>(response: ApiResponse<T>): T | null {
  if (isApiSuccess(response)) {
    return response.data;
  }
  return null;
}

/**
 * Safely get completion time of execution if available
 */
export function getExecutionCompletionTime(execution: WorkflowExecutionResponse): string | null {
  if (isExecutionCompleted(execution) || isExecutionTerminated(execution)) {
    return execution.completedAt;
  }
  return null;
}

/**
 * Safely get execution output if available
 */
export function getExecutionOutput(execution: WorkflowExecutionResponse): Record<string, unknown> | null {
  if (isExecutionCompleted(execution)) {
    return execution.output;
  }
  return null;
}

/**
 * Safely get execution error if available
 */
export function getExecutionError(execution: WorkflowExecutionResponse): ExecutionError | null {
  if (isExecutionTerminated(execution)) {
    return execution.error;
  }
  return null;
}
