/**
 * Accessibility Utilities
 *
 * Provides utilities for building WCAG 2.1 Level AA compliant interfaces
 * including keyboard navigation, ARIA attributes, and screen reader support.
 */

import React from 'react';

/**
 * Keyboard event handler for common navigation patterns
 */
export interface KeyboardHandlers {
  onEnter?: () => void;
  onSpace?: () => void;
  onEscape?: () => void;
  onArrowUp?: () => void;
  onArrowDown?: () => void;
  onArrowLeft?: () => void;
  onArrowRight?: () => void;
  onTab?: (shiftKey: boolean) => void;
}

/**
 * Handles keyboard events with proper event.preventDefault() management
 * Prevents default behavior for handled keys while allowing others
 *
 * @param event - Keyboard event
 * @param handlers - Object with handler functions for specific keys
 *
 * @example
 * <div
 *   onKeyDown={(e) => handleKeyboardEvent(e, {
 *     onEnter: () => submitForm(),
 *     onEscape: () => closeDialog(),
 *   })}
 * >
 */
export function handleKeyboardEvent(
  event: React.KeyboardEvent,
  handlers: KeyboardHandlers
): void {
  let handled = false;

  switch (event.key) {
    case 'Enter':
      if (handlers.onEnter) {
        handlers.onEnter();
        handled = true;
      }
      break;

    case ' ':
      if (handlers.onSpace) {
        handlers.onSpace();
        handled = true;
      }
      break;

    case 'Escape':
      if (handlers.onEscape) {
        handlers.onEscape();
        handled = true;
      }
      break;

    case 'ArrowUp':
      if (handlers.onArrowUp) {
        handlers.onArrowUp();
        handled = true;
      }
      break;

    case 'ArrowDown':
      if (handlers.onArrowDown) {
        handlers.onArrowDown();
        handled = true;
      }
      break;

    case 'ArrowLeft':
      if (handlers.onArrowLeft) {
        handlers.onArrowLeft();
        handled = true;
      }
      break;

    case 'ArrowRight':
      if (handlers.onArrowRight) {
        handlers.onArrowRight();
        handled = true;
      }
      break;

    case 'Tab':
      if (handlers.onTab) {
        handlers.onTab(event.shiftKey);
        handled = true;
      }
      break;
  }

  // Prevent default only for handled keys
  if (handled) {
    event.preventDefault();
  }
}

/**
 * Generates unique ID for elements that need aria-labelledby/aria-describedby
 * Ensures IDs are consistent and unique across renders
 *
 * @param baseId - Base identifier
 * @returns Stable unique ID
 */
const idRegistry = new Map<string, number>();

export function useId(baseId: string): string {
  if (!idRegistry.has(baseId)) {
    idRegistry.set(baseId, 0);
  }

  const count = idRegistry.get(baseId) || 0;
  idRegistry.set(baseId, count + 1);

  return `${baseId}-${count}`;
}

/**
 * Creates focus trap for modal dialogs
 * Ensures keyboard focus stays within the modal
 *
 * @param containerRef - Ref to modal container
 * @returns Event handler to attach to container
 */
export function createFocusTrap(
  containerRef: React.RefObject<HTMLDivElement>
) {
  return (event: React.KeyboardEvent) => {
    if (event.key !== 'Tab') return;

    const container = containerRef.current;
    if (!container) return;

    const focusableElements = container.querySelectorAll(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
    );

    const firstElement = focusableElements[0] as HTMLElement;
    const lastElement = focusableElements[
      focusableElements.length - 1
    ] as HTMLElement;

    if (event.shiftKey) {
      // Shift + Tab on first element
      if (document.activeElement === firstElement) {
        event.preventDefault();
        lastElement.focus();
      }
    } else {
      // Tab on last element
      if (document.activeElement === lastElement) {
        event.preventDefault();
        firstElement.focus();
      }
    }
  };
}

/**
 * Announces message to screen readers using aria-live region
 * Useful for dynamic content updates
 *
 * @param message - Message to announce
 * @param priority - 'polite' (default) or 'assertive'
 */
export function announceToScreenReader(
  message: string,
  priority: 'polite' | 'assertive' = 'polite'
): void {
  const announcement = document.createElement('div');
  announcement.setAttribute('role', 'status');
  announcement.setAttribute('aria-live', priority);
  announcement.setAttribute('aria-atomic', 'true');
  announcement.className = 'sr-only'; // Visually hidden
  announcement.textContent = message;

  document.body.appendChild(announcement);

  // Remove after announcement is made
  setTimeout(() => {
    document.body.removeChild(announcement);
  }, 1000);
}

/**
 * Skip to main content link helper
 * Should be first interactive element in DOM
 *
 * @returns JSX for skip link
 *
 * @example
 * <div>
 *   {createSkipToMainLink()}
 *   <Nav />
 *   <main id="main-content">Content</main>
 * </div>
 */
export function createSkipToMainLink(): React.ReactElement {
  return (
    <a
      href="#main-content"
      className="skip-to-main-content"
      style={{
        position: 'absolute',
        left: '-9999px',
        zIndex: 999,
      }}
      onFocus={(e) => {
        e.currentTarget.style.left = '0';
      }}
      onBlur={(e) => {
        e.currentTarget.style.left = '-9999px';
      }}
    >
      Skip to main content
    </a>
  );
}

/**
 * ARIA label builder for complex components
 * Combines multiple labels for clearer screen reader output
 *
 * @param parts - Label parts to combine
 * @returns Combined label string
 */
export function buildAriaLabel(...parts: (string | null | undefined)[]): string {
  return parts.filter((part) => part && part.length > 0).join(', ');
}
