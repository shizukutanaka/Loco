import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  ApplicationError,
  createValidationError,
  createApiError,
  createNetworkError,
  createWorkflowError,
  executeWithRetry,
  safeAsync,
  safeSync,
  getErrorToastConfig,
} from './errorHandler';

describe('Error Handler Utilities', () => {
  describe('ApplicationError', () => {
    it('should create error with all properties', () => {
      const error = new ApplicationError({
        code: 'TEST_ERROR',
        message: 'Test message',
        userMessage: 'User friendly message',
        severity: 'error',
        recoverable: false,
      });

      expect(error.code).toBe('TEST_ERROR');
      expect(error.message).toBe('Test message');
      expect(error.userMessage).toBe('User friendly message');
      expect(error.severity).toBe('error');
      expect(error.recoverable).toBe(false);
      expect(error.timestamp).toBeDefined();
    });

    it('should support optional details', () => {
      const details = { field: 'email', value: 'invalid' };
      const error = new ApplicationError({
        code: 'VALIDATION_ERROR',
        message: 'Validation failed',
        userMessage: 'Invalid input',
        severity: 'warning',
        recoverable: true,
        details,
      });

      expect(error.details).toEqual(details);
    });

    it('should extend Error class', () => {
      const error = new ApplicationError({
        code: 'TEST',
        message: 'Test',
        userMessage: 'Test',
        severity: 'error',
        recoverable: false,
      });

      expect(error instanceof Error).toBe(true);
      expect(error.name).toBe('ApplicationError');
    });
  });

  describe('createValidationError', () => {
    it('should create validation error with correct properties', () => {
      const error = createValidationError('email', 'invalid@', 'Invalid email format');

      expect(error.code).toBe('VALIDATION_ERROR');
      expect(error.severity).toBe('warning');
      expect(error.recoverable).toBe(true);
      expect(error.details).toEqual({
        field: 'email',
        value: 'invalid@',
        reason: 'Invalid email format',
      });
    });

    it('should create user-friendly message', () => {
      const error = createValidationError('password', 'short', 'Too short');
      expect(error.userMessage).toContain('password');
      expect(error.userMessage).toContain('Too short');
    });
  });

  describe('createApiError', () => {
    it('should create error for 500 status (recoverable)', () => {
      const error = createApiError('/api/data', 500, {
        code: 'SERVER_ERROR',
        message: 'Internal server error',
      });

      expect(error.code).toBe('SERVER_ERROR');
      expect(error.recoverable).toBe(true);
      expect(error.severity).toBe('warning');
    });

    it('should create error for 429 (too many requests - recoverable)', () => {
      const error = createApiError('/api/data', 429, {
        message: 'Rate limited',
      });

      expect(error.recoverable).toBe(true);
      expect(error.userMessage.toLowerCase()).toContain('too many requests');
    });

    it('should create error for 408 (timeout - recoverable)', () => {
      const error = createApiError('/api/data', 408, {
        message: 'Request timeout',
      });

      expect(error.recoverable).toBe(true);
    });

    it('should create error for 401 (unauthorized - non-recoverable)', () => {
      const error = createApiError('/api/data', 401, {
        message: 'Unauthorized',
      });

      expect(error.recoverable).toBe(false);
      expect(error.userMessage).toContain('Authentication failed');
    });

    it('should create error for 403 (forbidden - non-recoverable)', () => {
      const error = createApiError('/api/data', 403, {
        message: 'Forbidden',
      });

      expect(error.recoverable).toBe(false);
      expect(error.userMessage).toContain('permission');
    });

    it('should include endpoint in details', () => {
      const error = createApiError('/api/workflows', 500, {});
      expect(error.details?.endpoint).toBe('/api/workflows');
    });
  });

  describe('createNetworkError', () => {
    it('should create network error', () => {
      const originalError = new Error('Network timeout');
      const error = createNetworkError(originalError);

      expect(error.code).toBe('NETWORK_ERROR');
      expect(error.recoverable).toBe(true);
      expect(error.severity).toBe('warning');
    });

    it('should include original error message', () => {
      const originalError = new Error('Connection refused');
      const error = createNetworkError(originalError);

      expect(error.message).toContain('Connection refused');
    });

    it('should provide user-friendly message', () => {
      const originalError = new Error('Network error');
      const error = createNetworkError(originalError);

      expect(error.userMessage).toContain('internet connection');
    });
  });

  describe('createWorkflowError', () => {
    it('should create recoverable workflow error', () => {
      const error = createWorkflowError('save', 'Failed to save workflow', true);

      expect(error.code).toBe('WORKFLOW_ERROR');
      expect(error.recoverable).toBe(true);
      expect(error.severity).toBe('warning');
    });

    it('should create non-recoverable workflow error', () => {
      const error = createWorkflowError('load', 'Invalid workflow format', false);

      expect(error.recoverable).toBe(false);
      expect(error.severity).toBe('error');
    });

    it('should default to recoverable', () => {
      const error = createWorkflowError('execute', 'Execution failed');
      expect(error.recoverable).toBe(true);
    });
  });

  describe('executeWithRetry', () => {
    it('should execute function and return result on success', async () => {
      const mockFn = vi.fn().mockResolvedValue('success');
      const result = await executeWithRetry(mockFn);

      expect(result).toBe('success');
      expect(mockFn).toHaveBeenCalledOnce();
    });

    it('should retry on failure with exponential backoff', async () => {
      const mockFn = vi
        .fn()
        .mockRejectedValueOnce(new Error('Attempt 1'))
        .mockRejectedValueOnce(new Error('Attempt 2'))
        .mockResolvedValueOnce('success');

      const result = await executeWithRetry(mockFn, {
        maxRetries: 3,
        initialDelay: 10,
        backoffMultiplier: 2,
      });

      expect(result).toBe('success');
      expect(mockFn).toHaveBeenCalledTimes(3);
    });

    it('should throw after max retries exceeded', async () => {
      const mockFn = vi.fn().mockRejectedValue(new Error('Always fails'));

      await expect(
        executeWithRetry(mockFn, {
          maxRetries: 2,
          initialDelay: 10,
        })
      ).rejects.toThrow('Always fails');

      expect(mockFn).toHaveBeenCalledTimes(2);
    });

    it('should respect shouldRetry predicate', async () => {
      const mockFn = vi
        .fn()
        .mockRejectedValueOnce(new Error('Non-recoverable'))
        .mockResolvedValueOnce('success');

      await expect(
        executeWithRetry(mockFn, {
          maxRetries: 3,
          shouldRetry: () => false,
        })
      ).rejects.toThrow();

      expect(mockFn).toHaveBeenCalledOnce();
    });

    it('should call onRetry callback', async () => {
      const onRetry = vi.fn();
      const mockFn = vi
        .fn()
        .mockRejectedValueOnce(new Error('Fail 1'))
        .mockResolvedValueOnce('success');

      await executeWithRetry(mockFn, {
        maxRetries: 3,
        initialDelay: 10,
        onRetry,
      });

      expect(onRetry).toHaveBeenCalledTimes(1);
      expect(onRetry).toHaveBeenCalledWith(1, expect.any(Error));
    });

    it('should respect max delay limit', async () => {
      const mockFn = vi
        .fn()
        .mockRejectedValue(new Error('Fail'));

      const startTime = Date.now();
      await expect(
        executeWithRetry(mockFn, {
          maxRetries: 4,
          initialDelay: 100,
          maxDelay: 150,
          backoffMultiplier: 3,
        })
      ).rejects.toThrow();

      const elapsed = Date.now() - startTime;
      // Should be bounded by maxDelay: 100 + 150 + 150 + overhead
      expect(elapsed).toBeLessThan(1000);
      // Also verify it doesn't take excessively long (sanity check)
      expect(elapsed).toBeGreaterThan(300);
    });
  });

  describe('safeAsync', () => {
    it('should return result on success', async () => {
      const mockFn = vi.fn().mockResolvedValue('success');
      const [result, error] = await safeAsync(mockFn);

      expect(result).toBe('success');
      expect(error).toBeNull();
    });

    it('should return error on failure', async () => {
      const mockFn = vi.fn().mockRejectedValue(new Error('Failed'));
      const [result, error] = await safeAsync(mockFn);

      expect(result).toBeNull();
      expect(error).not.toBeNull();
      expect(error?.code).toBe('UNKNOWN_ERROR');
    });

    it('should preserve ApplicationError', async () => {
      const appError = new ApplicationError({
        code: 'CUSTOM_ERROR',
        message: 'Custom error',
        userMessage: 'Custom user message',
        severity: 'error',
        recoverable: false,
      });

      const mockFn = vi.fn().mockRejectedValue(appError);
      const [result, error] = await safeAsync(mockFn);

      expect(result).toBeNull();
      expect(error?.code).toBe('CUSTOM_ERROR');
    });

    it('should include context in error details', async () => {
      const mockFn = vi.fn().mockRejectedValue(new Error('Failed'));
      const [, error] = await safeAsync(mockFn, 'custom operation');

      expect(error?.details?.context).toBe('custom operation');
    });
  });

  describe('safeSync', () => {
    it('should return result on success', () => {
      const mockFn = vi.fn().mockReturnValue('success');
      const wrapped = safeSync(mockFn);
      const [result, error] = wrapped();

      expect(result).toBe('success');
      expect(error).toBeNull();
    });

    it('should return error on failure', () => {
      const mockFn = vi.fn().mockImplementation(() => {
        throw new Error('Sync error');
      });
      const wrapped = safeSync(mockFn);
      const [result, error] = wrapped();

      expect(result).toBeNull();
      expect(error).not.toBeNull();
      expect(error?.code).toBe('UNKNOWN_ERROR');
    });

    it('should pass through function arguments', () => {
      const mockFn = vi.fn((a, b) => a + b);
      const wrapped = safeSync(mockFn);
      const [result] = wrapped(5, 3);

      expect(result).toBe(8);
      expect(mockFn).toHaveBeenCalledWith(5, 3);
    });

    it('should preserve ApplicationError', () => {
      const appError = new ApplicationError({
        code: 'SYNC_ERROR',
        message: 'Sync error',
        userMessage: 'User message',
        severity: 'error',
        recoverable: false,
      });

      const mockFn = vi.fn().mockImplementation(() => {
        throw appError;
      });
      const wrapped = safeSync(mockFn);
      const [, error] = wrapped();

      expect(error?.code).toBe('SYNC_ERROR');
    });
  });

  describe('getErrorToastConfig', () => {
    it('should create config for ApplicationError', () => {
      const error = new ApplicationError({
        code: 'TEST',
        message: 'Test message',
        userMessage: 'User friendly',
        severity: 'error',
        recoverable: true,
        details: { field: 'email' },
      });

      const config = getErrorToastConfig(error);

      expect(config.message).toBe('User friendly');
      expect(config.description).toContain('field');
      expect(config.variant).toBe('destructive');
    });

    it('should have longer duration for non-recoverable errors', () => {
      const error = new ApplicationError({
        code: 'TEST',
        message: 'Test',
        userMessage: 'User message',
        severity: 'error',
        recoverable: false,
      });

      const config = getErrorToastConfig(error);
      expect(config.duration).toBe(10000);
    });

    it('should have shorter duration for recoverable errors', () => {
      const error = new ApplicationError({
        code: 'TEST',
        message: 'Test',
        userMessage: 'User message',
        severity: 'warning',
        recoverable: true,
      });

      const config = getErrorToastConfig(error);
      expect(config.duration).toBe(5000);
    });

    it('should include retry action for recoverable errors', () => {
      const error = new ApplicationError({
        code: 'TEST',
        message: 'Test',
        userMessage: 'User message',
        severity: 'warning',
        recoverable: true,
      });

      const config = getErrorToastConfig(error);
      expect(config.action).not.toBeUndefined();
      expect(config.action?.label).toBe('Retry');
    });

    it('should not include retry action for non-recoverable errors', () => {
      const error = new ApplicationError({
        code: 'TEST',
        message: 'Test',
        userMessage: 'User message',
        severity: 'error',
        recoverable: false,
      });

      const config = getErrorToastConfig(error);
      expect(config.action).toBeUndefined();
    });

    it('should handle plain Error objects', () => {
      const error = new Error('Plain error');
      const config = getErrorToastConfig(error);

      expect(config.message).toBe('An error occurred');
    });
  });
});
