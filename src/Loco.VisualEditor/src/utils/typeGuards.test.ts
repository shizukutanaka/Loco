import { describe, it, expect } from 'vitest';
import {
  isApiSuccess,
  isApiError,
  isExecutionInProgress,
  isExecutionCompleted,
  isExecutionTerminated,
  isExecutionEnded,
  getApiError,
  getApiData,
  getExecutionCompletionTime,
  getExecutionOutput,
  getExecutionError,
} from './typeGuards';

describe('Type Guards and Discriminators', () => {
  describe('API Response Guards', () => {
    describe('isApiSuccess', () => {
      it('should identify successful response', () => {
        const response = { success: true, data: { id: 1, name: 'Test' } } as any;
        expect(isApiSuccess(response)).toBe(true);
      });

      it('should reject failed response', () => {
        const response = { success: false, error: { message: 'Failed' } } as any;
        expect(isApiSuccess(response)).toBe(false);
      });

      it('should handle undefined success field', () => {
        const response = { data: { id: 1 } };
        expect(isApiSuccess(response as any)).toBe(false);
      });
    });

    describe('isApiError', () => {
      it('should identify error response', () => {
        const response = { success: false, error: { message: 'Error occurred' } } as any;
        expect(isApiError(response)).toBe(true);
      });

      it('should reject successful response', () => {
        const response = { success: true, data: { id: 1 } } as any;
        expect(isApiError(response)).toBe(false);
      });

      it('should handle various error formats', () => {
        const response = {
          success: false,
          error: { code: 'VALIDATION_ERROR', message: 'Invalid input' },
        } as any;
        expect(isApiError(response)).toBe(true);
      });
    });

    describe('getApiData', () => {
      it('should extract data from successful response', () => {
        const response = { success: true, data: { id: 1, value: 'test' } } as any;
        expect(getApiData(response)).toEqual({ id: 1, value: 'test' });
      });

      it('should return null for failed response', () => {
        const response = { success: false, error: { message: 'Failed' } } as any;
        expect(getApiData(response)).toBeNull();
      });

      it('should handle null data', () => {
        const response = { success: true, data: null };
        expect(getApiData(response as any)).toBeNull();
      });
    });

    describe('getApiError', () => {
      it('should extract error message from failed response', () => {
        const response = {
          success: false,
          error: { message: 'Server error' },
        } as any;
        expect(getApiError(response)).toBe('Server error');
      });

      it('should return unknown error when message missing', () => {
        const response = { success: false, error: {} };
        expect(getApiError(response as any)).toBe('Unknown error');
      });

      it('should return empty string for successful response', () => {
        const response = { success: true, data: {} };
        expect(getApiError(response as any)).toBe('');
      });
    });
  });

  describe('Execution Status Guards', () => {
    const baseExecution = { executionId: 'exec-123', startedAt: '2024-01-01T10:00:00Z' };

    describe('isExecutionInProgress', () => {
      it('should identify pending execution', () => {
        const execution = { ...baseExecution, status: 'pending' as const };
        expect(isExecutionInProgress(execution as any)).toBe(true);
      });

      it('should identify running execution', () => {
        const execution = { ...baseExecution, status: 'running' as const };
        expect(isExecutionInProgress(execution as any)).toBe(true);
      });

      it('should reject completed execution', () => {
        const execution = {
          ...baseExecution,
          status: 'completed' as const,
          completedAt: '2024-01-01T10:05:00Z',
          output: {},
        };
        expect(isExecutionInProgress(execution as any)).toBe(false);
      });

      it('should reject failed execution', () => {
        const execution = {
          ...baseExecution,
          status: 'failed' as const,
          completedAt: '2024-01-01T10:05:00Z',
          error: {},
        };
        expect(isExecutionInProgress(execution as any)).toBe(false);
      });

      it('should reject cancelled execution', () => {
        const execution = {
          ...baseExecution,
          status: 'cancelled' as const,
          completedAt: '2024-01-01T10:05:00Z',
          error: {},
        };
        expect(isExecutionInProgress(execution as any)).toBe(false);
      });
    });

    describe('isExecutionCompleted', () => {
      it('should identify completed execution', () => {
        const execution = {
          ...baseExecution,
          status: 'completed' as const,
          completedAt: '2024-01-01T10:05:00Z',
          output: { result: 'success' },
        };
        expect(isExecutionCompleted(execution as any)).toBe(true);
      });

      it('should reject non-completed statuses', () => {
        const statuses = ['pending', 'running', 'failed', 'cancelled'];
        for (const status of statuses) {
          const execution = {
            ...baseExecution,
            status: status as any,
          };
          expect(isExecutionCompleted(execution as any)).toBe(false);
        }
      });
    });

    describe('isExecutionTerminated', () => {
      it('should identify failed execution', () => {
        const execution = {
          ...baseExecution,
          status: 'failed' as const,
          completedAt: '2024-01-01T10:05:00Z',
          error: { message: 'Failed' },
        };
        expect(isExecutionTerminated(execution as any)).toBe(true);
      });

      it('should identify cancelled execution', () => {
        const execution = {
          ...baseExecution,
          status: 'cancelled' as const,
          completedAt: '2024-01-01T10:05:00Z',
          error: { message: 'Cancelled' },
        };
        expect(isExecutionTerminated(execution as any)).toBe(true);
      });

      it('should reject in-progress statuses', () => {
        const statuses = ['pending', 'running', 'completed'];
        for (const status of statuses) {
          const execution = {
            ...baseExecution,
            status: status as any,
          };
          expect(isExecutionTerminated(execution as any)).toBe(false);
        }
      });
    });

    describe('isExecutionEnded', () => {
      it('should identify completed execution as ended', () => {
        const execution = {
          ...baseExecution,
          status: 'completed' as const,
        };
        expect(isExecutionEnded(execution as any)).toBe(true);
      });

      it('should identify failed execution as ended', () => {
        const execution = {
          ...baseExecution,
          status: 'failed' as const,
        };
        expect(isExecutionEnded(execution as any)).toBe(true);
      });

      it('should identify cancelled execution as ended', () => {
        const execution = {
          ...baseExecution,
          status: 'cancelled' as const,
        };
        expect(isExecutionEnded(execution as any)).toBe(true);
      });

      it('should reject pending execution', () => {
        const execution = {
          ...baseExecution,
          status: 'pending' as const,
        };
        expect(isExecutionEnded(execution as any)).toBe(false);
      });

      it('should reject running execution', () => {
        const execution = {
          ...baseExecution,
          status: 'running' as const,
        };
        expect(isExecutionEnded(execution as any)).toBe(false);
      });
    });
  });

  describe('Execution Data Extractors', () => {
    const baseExecution = { executionId: 'exec-123', startedAt: '2024-01-01T10:00:00Z' };

    describe('getExecutionCompletionTime', () => {
      it('should extract completion time from completed execution', () => {
        const execution = {
          ...baseExecution,
          status: 'completed' as const,
          completedAt: '2024-01-01T10:05:00Z',
          output: {},
        };
        expect(getExecutionCompletionTime(execution as any)).toBe('2024-01-01T10:05:00Z');
      });

      it('should extract completion time from failed execution', () => {
        const execution = {
          ...baseExecution,
          status: 'failed' as const,
          completedAt: '2024-01-01T10:05:00Z',
          error: {},
        };
        expect(getExecutionCompletionTime(execution as any)).toBe('2024-01-01T10:05:00Z');
      });

      it('should return null for in-progress execution', () => {
        const execution = {
          ...baseExecution,
          status: 'running' as const,
        };
        expect(getExecutionCompletionTime(execution as any)).toBeNull();
      });
    });

    describe('getExecutionOutput', () => {
      it('should extract output from completed execution', () => {
        const output = { result: 'success', data: [1, 2, 3] };
        const execution = {
          ...baseExecution,
          status: 'completed' as const,
          completedAt: '2024-01-01T10:05:00Z',
          output,
        };
        expect(getExecutionOutput(execution as any)).toEqual(output);
      });

      it('should return null for failed execution', () => {
        const execution = {
          ...baseExecution,
          status: 'failed' as const,
          completedAt: '2024-01-01T10:05:00Z',
          error: {},
        };
        expect(getExecutionOutput(execution as any)).toBeNull();
      });

      it('should return null for in-progress execution', () => {
        const execution = {
          ...baseExecution,
          status: 'running' as const,
        };
        expect(getExecutionOutput(execution as any)).toBeNull();
      });
    });

    describe('getExecutionError', () => {
      it('should extract error from failed execution', () => {
        const error = { message: 'Validation failed', code: 'VALIDATION_ERROR' };
        const execution = {
          ...baseExecution,
          status: 'failed' as const,
          completedAt: '2024-01-01T10:05:00Z',
          error,
        };
        expect(getExecutionError(execution as any)).toEqual(error);
      });

      it('should extract error from cancelled execution', () => {
        const error = { message: 'User cancelled', code: 'CANCELLED' };
        const execution = {
          ...baseExecution,
          status: 'cancelled' as const,
          completedAt: '2024-01-01T10:05:00Z',
          error,
        };
        expect(getExecutionError(execution as any)).toEqual(error);
      });

      it('should return null for completed execution', () => {
        const execution = {
          ...baseExecution,
          status: 'completed' as const,
          completedAt: '2024-01-01T10:05:00Z',
          output: {},
        };
        expect(getExecutionError(execution as any)).toBeNull();
      });

      it('should return null for in-progress execution', () => {
        const execution = {
          ...baseExecution,
          status: 'running' as const,
        };
        expect(getExecutionError(execution as any)).toBeNull();
      });
    });
  });
});
