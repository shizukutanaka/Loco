/**
 * ARIA Live Region Utilities
 *
 * Provides utilities for creating accessible live regions that announce
 * dynamic content changes to screen readers without visual disruption.
 */

/**
 * Announce a message to screen readers via ARIA live region
 * Useful for dynamically updated content
 *
 * @param message - Message to announce
 * @param priority - 'polite' (default, waits for current speech to finish) or 'assertive' (interrupts)
 * @param duration - How long to keep the announcement in the DOM (ms), default 3000
 */
export function announceToScreenReader(
  message: string,
  priority: 'polite' | 'assertive' = 'polite',
  duration: number = 3000
): void {
  // Create or get the live region container
  let liveRegion = document.querySelector('[data-aria-live-region]') as HTMLDivElement;

  if (!liveRegion) {
    liveRegion = document.createElement('div');
    liveRegion.setAttribute('data-aria-live-region', 'true');
    liveRegion.setAttribute('aria-live', priority);
    liveRegion.setAttribute('aria-atomic', 'true');
    liveRegion.className = 'sr-only'; // Visually hidden
    document.body.appendChild(liveRegion);
  }

  // Update the live region priority if needed
  const currentPriority = liveRegion.getAttribute('aria-live');
  if (currentPriority !== priority) {
    liveRegion.setAttribute('aria-live', priority);
  }

  // Add the message
  const announcement = document.createElement('div');
  announcement.textContent = message;
  liveRegion.appendChild(announcement);

  // Remove after duration
  setTimeout(() => {
    announcement.remove();
  }, duration);
}

/**
 * Create a live region for status updates
 * Useful for showing validation results, loading states, etc.
 *
 * @param id - Unique identifier for the live region
 * @param priority - 'polite' or 'assertive'
 * @returns The live region element
 */
export function createLiveRegion(
  id: string,
  priority: 'polite' | 'assertive' = 'polite'
): HTMLDivElement {
  let region = document.getElementById(id) as HTMLDivElement;

  if (!region) {
    region = document.createElement('div');
    region.id = id;
    region.setAttribute('aria-live', priority);
    region.setAttribute('aria-atomic', 'true');
    region.className = 'sr-only';
    document.body.appendChild(region);
  }

  return region;
}

/**
 * Update a live region with new content
 *
 * @param regionId - ID of the live region
 * @param message - Message to announce
 * @param replace - If true, replace content; if false, append
 */
export function updateLiveRegion(
  regionId: string,
  message: string,
  replace: boolean = true
): void {
  const region = document.getElementById(regionId);
  if (!region) {
    console.warn(`Live region with id "${regionId}" not found`);
    return;
  }

  if (replace) {
    region.textContent = message;
  } else {
    const line = document.createElement('div');
    line.textContent = message;
    region.appendChild(line);
  }
}

/**
 * Clear a live region
 *
 * @param regionId - ID of the live region
 */
export function clearLiveRegion(regionId: string): void {
  const region = document.getElementById(regionId);
  if (region) {
    region.textContent = '';
  }
}

/**
 * React Hook for using live regions
 * Automatically creates and cleans up the region
 *
 * @param id - Unique identifier for the live region
 * @param priority - 'polite' or 'assertive'
 * @returns Object with announce and clear functions
 *
 * @example
 * const { announce, clear } = useLiveRegion('validation-status');
 *
 * useEffect(() => {
 *   if (validationErrors) {
 *     announce(`${validationErrors.length} validation errors found`);
 *   }
 * }, [validationErrors, announce]);
 */
export function useLiveRegion(
  id: string,
  priority: 'polite' | 'assertive' = 'polite'
): {
  announce: (message: string, replace?: boolean) => void;
  clear: () => void;
} {
  // Create region on mount
  React.useEffect(() => {
    createLiveRegion(id, priority);
  }, [id, priority]);

  return {
    announce: (message: string, replace: boolean = true) => {
      updateLiveRegion(id, message, replace);
    },
    clear: () => {
      clearLiveRegion(id);
    },
  };
}

// Import React for the hook
import React from 'react';
