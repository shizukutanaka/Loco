import { describe, it, expect, vi, afterEach } from 'vitest';
import { retryOperation, retryNetworkOperation, createRetryFunction } from './retry';

describe('retry', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  describe('retryOperation', () => {
    it('returns immediately on success without retrying', async () => {
      const fn = vi.fn().mockResolvedValue('ok');
      const result = await retryOperation(fn, { initialDelay: 1 });
      expect(result).toBe('ok');
      expect(fn).toHaveBeenCalledTimes(1);
    });

    it('retries then succeeds', async () => {
      const fn = vi
        .fn()
        .mockRejectedValueOnce(new Error('boom'))
        .mockRejectedValueOnce(new Error('boom'))
        .mockResolvedValue('recovered');

      const result = await retryOperation(fn, { initialDelay: 1, maxRetries: 3 });
      expect(result).toBe('recovered');
      expect(fn).toHaveBeenCalledTimes(3);
    });

    it('throws the last error after exhausting all retries', async () => {
      const fn = vi.fn().mockRejectedValue(new Error('always fails'));

      await expect(retryOperation(fn, { initialDelay: 1, maxRetries: 2 })).rejects.toThrow(
        'always fails'
      );
      // 1 initial + 2 retries
      expect(fn).toHaveBeenCalledTimes(3);
    });

    it('does not retry when shouldRetry returns false', async () => {
      const fn = vi.fn().mockRejectedValue(new Error('fatal'));
      const shouldRetry = vi.fn().mockReturnValue(false);

      await expect(
        retryOperation(fn, { initialDelay: 1, maxRetries: 5, shouldRetry })
      ).rejects.toThrow('fatal');
      expect(fn).toHaveBeenCalledTimes(1);
      expect(shouldRetry).toHaveBeenCalledTimes(1);
    });

    it('invokes onRetry with a 1-based attempt number and the error', async () => {
      const err = new Error('boom');
      const fn = vi.fn().mockRejectedValueOnce(err).mockResolvedValue('ok');
      const onRetry = vi.fn();

      await retryOperation(fn, { initialDelay: 1, onRetry });
      expect(onRetry).toHaveBeenCalledTimes(1);
      expect(onRetry).toHaveBeenCalledWith(1, err);
    });

    it('applies exponential backoff capped at maxDelay', async () => {
      vi.useFakeTimers();
      const delays: number[] = [];
      const spy = vi.spyOn(globalThis, 'setTimeout');

      const fn = vi.fn().mockRejectedValue(new Error('boom'));
      const promise = retryOperation(fn, {
        initialDelay: 100,
        backoffMultiplier: 2,
        maxDelay: 300,
        maxRetries: 4,
      }).catch(() => 'failed');

      // Drive all timers to completion
      await vi.runAllTimersAsync();
      await promise;

      for (const call of spy.mock.calls) {
        delays.push(call[1] as number);
      }
      // 100 -> 200 -> 300 (capped) -> 300 (capped)
      expect(delays).toEqual([100, 200, 300, 300]);
      spy.mockRestore();
    });
  });

  describe('retryNetworkOperation', () => {
    it('retries 5xx server errors', async () => {
      const fn = vi
        .fn()
        .mockRejectedValueOnce({ code: 'HTTP_503' })
        .mockResolvedValue('ok');

      const result = await retryNetworkOperation(fn, { initialDelay: 1 });
      expect(result).toBe('ok');
      expect(fn).toHaveBeenCalledTimes(2);
    });

    it('does NOT retry 4xx client errors', async () => {
      const fn = vi.fn().mockRejectedValue({ code: 'HTTP_404' });

      await expect(retryNetworkOperation(fn, { initialDelay: 1 })).rejects.toEqual({
        code: 'HTTP_404',
      });
      expect(fn).toHaveBeenCalledTimes(1);
    });

    it('retries NETWORK_ERROR and TIMEOUT', async () => {
      const netFn = vi.fn().mockRejectedValueOnce({ code: 'NETWORK_ERROR' }).mockResolvedValue('a');
      expect(await retryNetworkOperation(netFn, { initialDelay: 1 })).toBe('a');
      expect(netFn).toHaveBeenCalledTimes(2);

      const toFn = vi.fn().mockRejectedValueOnce({ code: 'TIMEOUT' }).mockResolvedValue('b');
      expect(await retryNetworkOperation(toFn, { initialDelay: 1 })).toBe('b');
      expect(toFn).toHaveBeenCalledTimes(2);
    });

    it('retries errors with no recognizable code by default', async () => {
      const fn = vi.fn().mockRejectedValueOnce(new Error('mystery')).mockResolvedValue('ok');
      expect(await retryNetworkOperation(fn, { initialDelay: 1 })).toBe('ok');
      expect(fn).toHaveBeenCalledTimes(2);
    });
  });

  describe('createRetryFunction', () => {
    it('builds a retrier that uses the supplied predicate', async () => {
      const onlyRetryOops = createRetryFunction<string>(
        (err) => err instanceof Error && err.message === 'oops'
      );

      const retryable = vi.fn().mockRejectedValueOnce(new Error('oops')).mockResolvedValue('done');
      expect(await onlyRetryOops(retryable, { initialDelay: 1 })).toBe('done');
      expect(retryable).toHaveBeenCalledTimes(2);

      const fatal = vi.fn().mockRejectedValue(new Error('nope'));
      await expect(onlyRetryOops(fatal, { initialDelay: 1 })).rejects.toThrow('nope');
      expect(fatal).toHaveBeenCalledTimes(1);
    });
  });
});
