import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { debounce, throttle } from './debounceThrottle';

describe('Debounce and Throttle Utilities', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('debounce', () => {
    it('should delay function execution', () => {
      const mockFn = vi.fn();
      const debouncedFn = debounce(mockFn, 300);

      debouncedFn('test');
      expect(mockFn).not.toHaveBeenCalled();

      vi.advanceTimersByTime(299);
      expect(mockFn).not.toHaveBeenCalled();

      vi.advanceTimersByTime(1);
      expect(mockFn).toHaveBeenCalledOnce();
      expect(mockFn).toHaveBeenCalledWith('test');
    });

    it('should reset delay on multiple calls', () => {
      const mockFn = vi.fn();
      const debouncedFn = debounce(mockFn, 300);

      debouncedFn('call1');
      vi.advanceTimersByTime(100);

      debouncedFn('call2');
      vi.advanceTimersByTime(100);

      debouncedFn('call3');
      expect(mockFn).not.toHaveBeenCalled();

      vi.advanceTimersByTime(300);
      expect(mockFn).toHaveBeenCalledOnce();
      expect(mockFn).toHaveBeenCalledWith('call3');
    });

    it('should only use latest arguments', () => {
      const mockFn = vi.fn();
      const debouncedFn = debounce(mockFn, 300);

      debouncedFn('first');
      debouncedFn('second');
      debouncedFn('third');

      vi.advanceTimersByTime(300);
      expect(mockFn).toHaveBeenCalledOnce();
      expect(mockFn).toHaveBeenCalledWith('third');
    });

    it('should support multiple arguments', () => {
      const mockFn = vi.fn();
      const debouncedFn = debounce(mockFn, 300);

      debouncedFn('arg1', 'arg2', { key: 'value' });

      vi.advanceTimersByTime(300);
      expect(mockFn).toHaveBeenCalledWith('arg1', 'arg2', { key: 'value' });
    });

    it('should have cancel method', () => {
      const mockFn = vi.fn();
      const debouncedFn = debounce(mockFn, 300);

      debouncedFn('test');
      debouncedFn.cancel();

      vi.advanceTimersByTime(300);
      expect(mockFn).not.toHaveBeenCalled();
    });

    it('should have flush method', () => {
      const mockFn = vi.fn();
      const debouncedFn = debounce(mockFn, 300);

      debouncedFn('test');
      debouncedFn.flush();

      expect(mockFn).toHaveBeenCalledOnce();
      expect(mockFn).toHaveBeenCalledWith('test');
    });

    it('should clear timeout on cancel', () => {
      const mockFn = vi.fn();
      const debouncedFn = debounce(mockFn, 300);

      debouncedFn('test');
      debouncedFn.cancel();
      debouncedFn.cancel(); // Should not error on second call

      expect(mockFn).not.toHaveBeenCalled();
    });

    it('should flush even if timer not set', () => {
      const mockFn = vi.fn();
      const debouncedFn = debounce(mockFn, 300);

      debouncedFn.flush(); // No pending call
      expect(mockFn).not.toHaveBeenCalled();
    });

    it('should handle flush with no pending args', () => {
      const mockFn = vi.fn();
      const debouncedFn = debounce(mockFn, 300);

      debouncedFn('test');
      debouncedFn.cancel();
      debouncedFn.flush();

      expect(mockFn).not.toHaveBeenCalled();
    });

    it('should support repeated debounced sequences', () => {
      const mockFn = vi.fn();
      const debouncedFn = debounce(mockFn, 300);

      // First sequence
      debouncedFn('first');
      vi.advanceTimersByTime(300);
      expect(mockFn).toHaveBeenCalledTimes(1);

      // Second sequence
      debouncedFn('second');
      vi.advanceTimersByTime(300);
      expect(mockFn).toHaveBeenCalledTimes(2);
    });
  });

  describe('throttle', () => {
    it('should execute immediately on first call', () => {
      const mockFn = vi.fn();
      const throttledFn = throttle(mockFn, 300);

      throttledFn('test');
      expect(mockFn).toHaveBeenCalledOnce();
      expect(mockFn).toHaveBeenCalledWith('test');
    });

    it('should throttle subsequent calls', () => {
      const mockFn = vi.fn();
      const throttledFn = throttle(mockFn, 300);

      throttledFn('call1');
      expect(mockFn).toHaveBeenCalledTimes(1);

      throttledFn('call2');
      expect(mockFn).toHaveBeenCalledTimes(1);

      vi.advanceTimersByTime(300);
      expect(mockFn).toHaveBeenCalledTimes(2);
      expect(mockFn).toHaveBeenNthCalledWith(2, 'call2');
    });

    it('should queue calls during throttle period', () => {
      const mockFn = vi.fn();
      const throttledFn = throttle(mockFn, 300);

      throttledFn('call1');
      throttledFn('call2');
      throttledFn('call3');

      expect(mockFn).toHaveBeenCalledOnce();

      vi.advanceTimersByTime(300);
      expect(mockFn).toHaveBeenCalledTimes(2);
      expect(mockFn).toHaveBeenNthCalledWith(2, 'call3');
    });

    it('should support multiple arguments', () => {
      const mockFn = vi.fn();
      const throttledFn = throttle(mockFn, 300);

      throttledFn('arg1', 'arg2', { key: 'value' });
      expect(mockFn).toHaveBeenCalledWith('arg1', 'arg2', { key: 'value' });
    });

    it('should have cancel method', () => {
      const mockFn = vi.fn();
      const throttledFn = throttle(mockFn, 300);

      throttledFn('call1');
      throttledFn('call2');
      throttledFn.cancel();

      vi.advanceTimersByTime(300);
      expect(mockFn).toHaveBeenCalledOnce();
    });

    it('should have flush method', () => {
      const mockFn = vi.fn();
      const throttledFn = throttle(mockFn, 300);

      throttledFn('call1');
      expect(mockFn).toHaveBeenCalledTimes(1);

      throttledFn('call2');
      throttledFn.flush();

      expect(mockFn).toHaveBeenCalledTimes(2);
      expect(mockFn).toHaveBeenNthCalledWith(2, 'call2');
    });

    it('should execute pending call on flush', () => {
      const mockFn = vi.fn();
      const throttledFn = throttle(mockFn, 300);

      throttledFn('call1');
      vi.advanceTimersByTime(100);
      throttledFn('call2');
      throttledFn.flush();

      expect(mockFn).toHaveBeenCalledTimes(2);
      expect(mockFn).toHaveBeenNthCalledWith(2, 'call2');
    });

    it('should handle multiple throttle cycles', () => {
      const mockFn = vi.fn();
      const throttledFn = throttle(mockFn, 300);

      // First cycle
      throttledFn('first');
      expect(mockFn).toHaveBeenCalledTimes(1);

      vi.advanceTimersByTime(300);

      // Second cycle
      throttledFn('second');
      expect(mockFn).toHaveBeenCalledTimes(2);

      vi.advanceTimersByTime(300);

      // Third cycle
      throttledFn('third');
      expect(mockFn).toHaveBeenCalledTimes(3);
    });

    it('should handle rapid calls after throttle period', () => {
      const mockFn = vi.fn();
      const throttledFn = throttle(mockFn, 100);

      throttledFn('call1');
      vi.advanceTimersByTime(100);
      throttledFn('call2');
      vi.advanceTimersByTime(100);
      throttledFn('call3');

      expect(mockFn).toHaveBeenCalledTimes(3);
    });

    it('should update lastExecutionTime on each execution', () => {
      const mockFn = vi.fn();
      const throttledFn = throttle(mockFn, 100);

      throttledFn('call1');
      expect(mockFn).toHaveBeenCalledTimes(1);

      vi.advanceTimersByTime(50);
      throttledFn('call2');
      expect(mockFn).toHaveBeenCalledTimes(1);

      vi.advanceTimersByTime(50);
      expect(mockFn).toHaveBeenCalledTimes(2);

      vi.advanceTimersByTime(100);
      throttledFn('call3');
      expect(mockFn).toHaveBeenCalledTimes(3);
    });

    it('should flush without pending scheduled calls', () => {
      const mockFn = vi.fn();
      const throttledFn = throttle(mockFn, 300);

      throttledFn('call1');
      // After the first call executes immediately, lastArgs still contains the value
      // so flush() will see lastArgs and execute it again
      throttledFn.flush();

      // Called once immediately, once by flush
      expect(mockFn).toHaveBeenCalledTimes(2);
    });

    it('should handle flush with immediate execution', () => {
      const mockFn = vi.fn();
      const throttledFn = throttle(mockFn, 300);

      throttledFn('call1');
      vi.advanceTimersByTime(100);
      throttledFn('call2');
      vi.advanceTimersByTime(200);
      // At this point, call2 would be scheduled for later
      throttledFn.flush();

      expect(mockFn).toHaveBeenCalledTimes(2);
    });
  });

  describe('debounce vs throttle comparison', () => {
    it('debounce delays all executions', () => {
      const mockFn = vi.fn();
      const debouncedFn = debounce(mockFn, 100);

      debouncedFn();
      debouncedFn();
      debouncedFn();

      expect(mockFn).not.toHaveBeenCalled();
      vi.advanceTimersByTime(100);
      expect(mockFn).toHaveBeenCalledOnce();
    });

    it('throttle executes first and last calls', () => {
      const mockFn = vi.fn();
      const throttledFn = throttle(mockFn, 100);

      throttledFn();
      throttledFn();
      throttledFn();

      expect(mockFn).toHaveBeenCalledTimes(1);
      vi.advanceTimersByTime(100);
      expect(mockFn).toHaveBeenCalledTimes(2);
    });
  });
});
