/**
 * Focus Trap Hook
 *
 * Manages focus within modal and dialog components to ensure keyboard navigation
 * is contained within the modal (WCAG 2.1 Success Criterion 2.1.2).
 *
 * When a modal opens:
 * 1. Focus is moved to the first focusable element
 * 2. Tab/Shift+Tab cycles within the modal
 * 3. Escape key closes the modal
 * 4. When closed, focus returns to the trigger element
 */

import { useEffect, useRef, useCallback, useMemo } from 'react';

/**
 * List of selectors for focusable elements
 */
const FOCUSABLE_SELECTORS = [
  'a[href]',
  'button:not([disabled])',
  'textarea:not([disabled])',
  'input:not([disabled])',
  'select:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
];

const FOCUSABLE_SELECTOR = FOCUSABLE_SELECTORS.join(',');

interface UseFocusTrapOptions {
  isActive: boolean;
  onEscape?: () => void;
  initialFocusRef?: React.RefObject<HTMLElement>;
  restoreFocusRef?: React.RefObject<HTMLElement>;
}

/**
 * Hook to trap focus within a modal or dialog
 *
 * @param containerRef - Reference to the modal container element
 * @param options - Configuration options
 *
 * @example
 * const modalRef = useRef<HTMLDivElement>(null);
 * useFocusTrap(modalRef, {
 *   isActive: isOpen,
 *   onEscape: () => setIsOpen(false),
 * });
 *
 * return <div ref={modalRef} role="dialog"> ... </div>;
 */
export function useFocusTrap(
  containerRef: React.RefObject<HTMLElement>,
  options: UseFocusTrapOptions
): void {
  const { isActive, onEscape, initialFocusRef, restoreFocusRef } = options;
  const previousFocusRef = useRef<HTMLElement | null>(null);

  // Cache focusable elements array - recompute only when container changes
  // Avoids DOM queries on every Tab keypress
  const focusableElements = useMemo((): HTMLElement[] => {
    if (!containerRef.current) return [];

    return Array.from(
      containerRef.current.querySelectorAll(FOCUSABLE_SELECTOR)
    ).filter((element): element is HTMLElement => {
      const htmlElement = element as HTMLElement;
      return htmlElement.offsetParent !== null; // Exclude hidden elements
    });
  }, [containerRef]);

  // Create a Map for O(1) element-to-index lookups instead of O(n) indexOf() calls
  const elementIndexMap = useMemo(() => {
    const map = new Map<HTMLElement, number>();
    focusableElements.forEach((element, index) => {
      map.set(element, index);
    });
    return map;
  }, [focusableElements]);

  const handleKeyDown = useCallback(
    (event: KeyboardEvent) => {
      // Close on Escape
      if (event.key === 'Escape') {
        event.preventDefault();
        onEscape?.();
        return;
      }

      // Trap Tab/Shift+Tab within focusable elements
      if (event.key === 'Tab') {
        if (focusableElements.length === 0) return;

        const currentElement = document.activeElement as HTMLElement;
        const currentIndex = elementIndexMap.get(currentElement) ?? -1;

        if (event.shiftKey) {
          // Shift+Tab: move to previous element
          event.preventDefault();
          const previousIndex = currentIndex <= 0 ? focusableElements.length - 1 : currentIndex - 1;
          focusableElements[previousIndex]?.focus();
        } else {
          // Tab: move to next element
          event.preventDefault();
          const nextIndex = currentIndex === -1 || currentIndex >= focusableElements.length - 1 ? 0 : currentIndex + 1;
          focusableElements[nextIndex]?.focus();
        }
      }
    },
    [focusableElements, elementIndexMap, onEscape]
  );

  useEffect(() => {
    if (!isActive || !containerRef.current) return;

    // Store the previously focused element to restore later
    previousFocusRef.current = document.activeElement as HTMLElement;

    // Focus the initial element
    if (initialFocusRef?.current) {
      initialFocusRef.current.focus();
    } else {
      focusableElements[0]?.focus();
    }

    // Add keyboard event listener
    containerRef.current.addEventListener('keydown', handleKeyDown);

    // Prevent scrolling on body when modal is open
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    return () => {
      // Remove event listener
      containerRef.current?.removeEventListener('keydown', handleKeyDown);

      // Restore body overflow
      document.body.style.overflow = previousOverflow;

      // Restore focus to the trigger element or previous focus
      const restoreFocus = restoreFocusRef?.current ?? previousFocusRef.current;
      if (restoreFocus && (restoreFocus as HTMLElement).offsetParent !== null) {
        // Use setTimeout to ensure focus is restored after the modal is removed from DOM
        setTimeout(() => {
          restoreFocus.focus();
        }, 0);
      }
    };
  }, [isActive, containerRef, focusableElements, handleKeyDown, initialFocusRef, restoreFocusRef]);
}

/**
 * Hook to manage focus on a specific element
 *
 * @param shouldFocus - Whether the element should be focused
 *
 * @example
 * const buttonRef = useRef<HTMLButtonElement>(null);
 * useFocusElement(isOpen, buttonRef);
 */
export function useFocusElement(
  shouldFocus: boolean,
  elementRef: React.RefObject<HTMLElement>
): void {
  useEffect(() => {
    if (shouldFocus && elementRef.current && elementRef.current.offsetParent !== null) {
      elementRef.current.focus();
    }
  }, [shouldFocus, elementRef]);
}

/**
 * Hook to get focusable elements within a container
 *
 * @param containerRef - Reference to the container
 * @returns Array of focusable HTML elements
 *
 * @example
 * const modalRef = useRef<HTMLDivElement>(null);
 * const focusableElements = useFocusableElements(modalRef);
 */
export function useFocusableElements(
  containerRef: React.RefObject<HTMLElement>
): HTMLElement[] {
  // Use useMemo instead of useState + useEffect to avoid unnecessary state updates
  // Memoized computation is more efficient for derived data that doesn't trigger re-renders
  return useMemo((): HTMLElement[] => {
    if (!containerRef.current) return [];

    return Array.from(
      containerRef.current.querySelectorAll(FOCUSABLE_SELECTOR)
    ).filter((element): element is HTMLElement => {
      const htmlElement = element as HTMLElement;
      return htmlElement.offsetParent !== null;
    });
  }, [containerRef]);
}

// Import React for hooks
import React from 'react';
