/**
 * Debounce and Throttle Utilities
 *
 * Provides optimized debounce and throttle implementations following
 * React best practices with proper cleanup and type safety.
 */

/**
 * Debounced function type
 */
export interface DebouncedFunction<T extends (...args: any[]) => any> {
  (...args: Parameters<T>): void;
  cancel: () => void;
  flush: () => void;
}

/**
 * Creates a debounced version of a function
 * Useful for search, filter, and input validation
 *
 * @param func - Function to debounce
 * @param wait - Wait time in milliseconds
 * @returns Debounced function with cancel() and flush() methods
 *
 * @example
 * const handleSearch = debounce((query: string) => {
 *   console.log('Search:', query);
 * }, 300);
 *
 * // In cleanup:
 * return () => handleSearch.cancel();
 */
export function debounce<T extends (...args: any[]) => any>(
  func: T,
  wait: number
): DebouncedFunction<T> {
  let timeoutId: ReturnType<typeof setTimeout> | null = null;
  let lastArgs: Parameters<T> | null = null;

  const debouncedFn = (...args: Parameters<T>) => {
    lastArgs = args;

    // Clear existing timeout
    if (timeoutId) {
      clearTimeout(timeoutId);
    }

    // Set new timeout
    timeoutId = setTimeout(() => {
      if (lastArgs) {
        func(...lastArgs);
      }
      timeoutId = null;
      lastArgs = null;
    }, wait);
  };

  debouncedFn.cancel = () => {
    if (timeoutId) {
      clearTimeout(timeoutId);
      timeoutId = null;
    }
    lastArgs = null;
  };

  debouncedFn.flush = () => {
    if (timeoutId && lastArgs) {
      clearTimeout(timeoutId);
      func(...lastArgs);
      timeoutId = null;
      lastArgs = null;
    }
  };

  return debouncedFn as DebouncedFunction<T>;
}

/**
 * Throttled function type
 */
export interface ThrottledFunction<T extends (...args: any[]) => any> {
  (...args: Parameters<T>): void;
  cancel: () => void;
  flush: () => void;
}

/**
 * Creates a throttled version of a function
 * Useful for scroll, resize, and mousemove events
 *
 * @param func - Function to throttle
 * @param wait - Wait time in milliseconds between calls
 * @returns Throttled function with cancel() and flush() methods
 *
 * @example
 * const handleScroll = throttle(() => {
 *   console.log('Scrolling...');
 * }, 100);
 *
 * window.addEventListener('scroll', handleScroll);
 *
 * // In cleanup:
 * return () => {
 *   window.removeEventListener('scroll', handleScroll);
 *   handleScroll.cancel();
 * };
 */
export function throttle<T extends (...args: any[]) => any>(
  func: T,
  wait: number
): ThrottledFunction<T> {
  let timeoutId: ReturnType<typeof setTimeout> | null = null;
  let lastExecutionTime = 0;
  let lastArgs: Parameters<T> | null = null;

  const throttledFn = (...args: Parameters<T>) => {
    const now = Date.now();
    const timeSinceLastExecution = now - lastExecutionTime;

    lastArgs = args;

    if (timeSinceLastExecution >= wait) {
      // Enough time has passed, execute immediately
      func(...args);
      lastExecutionTime = now;

      // Clear any pending timeout
      if (timeoutId) {
        clearTimeout(timeoutId);
        timeoutId = null;
      }
    } else {
      // Schedule execution after remaining wait time
      if (!timeoutId) {
        const remainingWait = wait - timeSinceLastExecution;
        timeoutId = setTimeout(() => {
          if (lastArgs) {
            func(...lastArgs);
            lastExecutionTime = Date.now();
          }
          timeoutId = null;
          lastArgs = null;
        }, remainingWait);
      }
    }
  };

  throttledFn.cancel = () => {
    if (timeoutId) {
      clearTimeout(timeoutId);
      timeoutId = null;
    }
    lastArgs = null;
  };

  throttledFn.flush = () => {
    if (timeoutId) {
      clearTimeout(timeoutId);
      if (lastArgs) {
        func(...lastArgs);
        lastExecutionTime = Date.now();
      }
      timeoutId = null;
      lastArgs = null;
    } else if (lastArgs) {
      // If no pending timeout but we have args, execute immediately
      func(...lastArgs);
      lastExecutionTime = Date.now();
      lastArgs = null;
    }
  };

  return throttledFn as ThrottledFunction<T>;
}

/**
 * React Hook for debounced value
 * Returns debounced value that updates after delay
 *
 * @param value - Value to debounce
 * @param delay - Delay in milliseconds
 * @returns Debounced value
 *
 * @example
 * const [searchQuery, setSearchQuery] = useState('');
 * const debouncedQuery = useDebounce(searchQuery, 300);
 *
 * useEffect(() => {
 *   // Only search when debouncedQuery changes
 *   if (debouncedQuery) {
 *     performSearch(debouncedQuery);
 *   }
 * }, [debouncedQuery]);
 */
export function useDebounceValue<T>(value: T, delay: number): T {
  const [debouncedValue, setDebouncedValue] = React.useState<T>(value);

  React.useEffect(() => {
    const timeoutId = setTimeout(() => {
      setDebouncedValue(value);
    }, delay);

    return () => clearTimeout(timeoutId);
  }, [value, delay]);

  return debouncedValue;
}

// Import React for the hook
import React from 'react';
