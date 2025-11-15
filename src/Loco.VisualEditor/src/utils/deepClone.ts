/**
 * Deep Clone Utility
 *
 * Provides efficient deep cloning of objects.
 * Uses structuredClone() when available, falls back to JSON method.
 */

/**
 * Deep clone an object
 * @param obj - Object to clone
 * @returns Cloned object
 */
export function deepClone<T>(obj: T): T {
  // Use native structuredClone if available (faster and more reliable)
  if (typeof structuredClone === 'function') {
    return structuredClone(obj);
  }

  // Fallback for older browsers
  return JSON.parse(JSON.stringify(obj));
}
