// Phase 3: WCAG 2.1 AA Accessibility Utilities
// Comprehensive accessibility helpers for compliance

/**
 * Generate unique ID for form labels and ARIA attributes
 * Prevents duplicate IDs in component trees
 */
const idCounter = new Map<string, number>();

export function generateId(prefix: string = 'id'): string {
  const count = idCounter.get(prefix) ?? 0;
  idCounter.set(prefix, count + 1);
  return `${prefix}-${count}`;
}

/**
 * WCAG 2.1 AA Keyboard Navigation Manager
 * Handles keyboard shortcuts and focus management
 */
export class KeyboardNavigationManager {
  /**
   * Setup keyboard shortcuts for common actions
   * ESC - Close modals, cancel operations
   * Enter - Submit forms, activate buttons
   * Space - Toggle checkboxes, activate buttons
   * Tab - Navigate between interactive elements
   * Shift+Tab - Reverse tab order
   */
  static setupKeyboardShortcuts(element: HTMLElement): void {
    element.addEventListener('keydown', (e) => {
      // ESC - Close modal (emit custom event)
      if (e.key === 'Escape') {
        element.dispatchEvent(new CustomEvent('close-requested'));
      }

      // Skip native keyboard handling for contenteditable
      if ((e.target as HTMLElement).contentEditable === 'true') {
        return;
      }
    });
  }

  /**
   * Focus trap for modals/dialogs
   * Ensures focus stays within dialog when open
   */
  static createFocusTrap(element: HTMLElement): () => void {
    const focusableElements = element.querySelectorAll(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
    );

    if (focusableElements.length === 0) return () => {};

    const first = focusableElements[0] as HTMLElement;
    const last = focusableElements[focusableElements.length - 1] as HTMLElement;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key !== 'Tab') return;

      if (e.shiftKey) {
        // Shift+Tab on first element -> wrap to last
        if (document.activeElement === first) {
          e.preventDefault();
          last.focus();
        }
      } else {
        // Tab on last element -> wrap to first
        if (document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    };

    element.addEventListener('keydown', handleKeyDown);

    // Return cleanup function
    return () => {
      element.removeEventListener('keydown', handleKeyDown);
    };
  }

  /**
   * Make element focusable if not already
   */
  static makeFocusable(element: HTMLElement): void {
    if (!element.hasAttribute('tabindex')) {
      element.setAttribute('tabindex', '0');
    }
  }

  /**
   * Announce message to screen readers via aria-live region
   */
  static announce(message: string, priority: 'polite' | 'assertive' = 'polite'): void {
    let liveRegion = document.querySelector(`[aria-live="${priority}"]`);

    if (!liveRegion) {
      liveRegion = document.createElement('div');
      liveRegion.setAttribute('aria-live', priority);
      liveRegion.setAttribute('aria-atomic', 'true');
      liveRegion.style.position = 'absolute';
      liveRegion.style.left = '-10000px';
      liveRegion.style.width = '1px';
      liveRegion.style.height = '1px';
      liveRegion.style.overflow = 'hidden';
      document.body.appendChild(liveRegion);
    }

    liveRegion.textContent = message;
  }
}

/**
 * Color Contrast Checker (WCAG 2.1 AA requires 4.5:1 for normal text)
 */
export class ContrastChecker {
  /**
   * Calculate relative luminance per WCAG spec
   */
  private static getLuminance(r: number, g: number, b: number): number {
    const [rs, gs, bs] = [r, g, b].map((c) => {
      c = c / 255;
      return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
    });

    return 0.2126 * rs + 0.7152 * gs + 0.0722 * bs;
  }

  /**
   * Parse color string (hex, rgb, rgb(), etc.)
   */
  private static parseColor(color: string): [r: number, g: number, b: number] {
    // Handle hex colors
    if (color.startsWith('#')) {
      const hex = color.slice(1);
      if (hex.length === 3) {
        return [
          parseInt(hex[0] + hex[0], 16),
          parseInt(hex[1] + hex[1], 16),
          parseInt(hex[2] + hex[2], 16),
        ];
      }
      return [
        parseInt(hex.slice(0, 2), 16),
        parseInt(hex.slice(2, 4), 16),
        parseInt(hex.slice(4, 6), 16),
      ];
    }

    // Handle rgb() colors
    const rgbMatch = color.match(/rgb\((\d+),\s*(\d+),\s*(\d+)\)/);
    if (rgbMatch) {
      return [parseInt(rgbMatch[1]), parseInt(rgbMatch[2]), parseInt(rgbMatch[3])];
    }

    return [0, 0, 0]; // Fallback to black
  }

  /**
   * Calculate contrast ratio between two colors
   */
  static getContrastRatio(color1: string, color2: string): number {
    const [r1, g1, b1] = this.parseColor(color1);
    const [r2, g2, b2] = this.parseColor(color2);

    const lum1 = this.getLuminance(r1, g1, b1);
    const lum2 = this.getLuminance(r2, g2, b2);

    const lighter = Math.max(lum1, lum2);
    const darker = Math.min(lum1, lum2);

    return (lighter + 0.05) / (darker + 0.05);
  }

  /**
   * Check if contrast meets WCAG AA standard (4.5:1 for normal text, 3:1 for large text)
   */
  static meetsAAStandard(color1: string, color2: string, isLargeText: boolean = false): boolean {
    const ratio = this.getContrastRatio(color1, color2);
    return isLargeText ? ratio >= 3 : ratio >= 4.5;
  }

  /**
   * Check if contrast meets WCAG AAA standard (7:1 for normal text, 4.5:1 for large text)
   */
  static meetsAAAStandard(color1: string, color2: string, isLargeText: boolean = false): boolean {
    const ratio = this.getContrastRatio(color1, color2);
    return isLargeText ? ratio >= 4.5 : ratio >= 7;
  }
}

/**
 * ARIA Attributes Helper
 */
export const AriaHelper = {
  /**
   * Setup label and form field relationship
   */
  labelFor(labelElement: HTMLElement, inputId: string): void {
    labelElement.setAttribute('for', inputId);
  },

  /**
   * Mark field as required with visual and accessibility indicator
   */
  required(inputElement: HTMLElement): void {
    inputElement.setAttribute('aria-required', 'true');
    inputElement.required = true;
  },

  /**
   * Link error message to form field
   */
  describeError(inputElement: HTMLElement, errorId: string): void {
    const existing = inputElement.getAttribute('aria-describedby') ?? '';
    const updated = existing ? `${existing} ${errorId}` : errorId;
    inputElement.setAttribute('aria-describedby', updated);
    inputElement.setAttribute('aria-invalid', 'true');
  },

  /**
   * Setup field as disabled with proper accessibility
   */
  disabled(element: HTMLElement, isDisabled: boolean = true): void {
    if (isDisabled) {
      element.setAttribute('aria-disabled', 'true');
      if (element.tagName === 'BUTTON' || element.tagName === 'A') {
        element.style.pointerEvents = 'none';
        element.style.opacity = '0.6';
      }
    } else {
      element.removeAttribute('aria-disabled');
      element.style.pointerEvents = '';
      element.style.opacity = '';
    }
  },

  /**
   * Mark section as expanded/collapsed (for accordions)
   */
  expanded(element: HTMLElement, isExpanded: boolean): void {
    element.setAttribute('aria-expanded', isExpanded ? 'true' : 'false');
  },

  /**
   * Setup button that controls another element
   */
  controls(buttonElement: HTMLElement, controlledElementId: string): void {
    buttonElement.setAttribute('aria-controls', controlledElementId);
  },

  /**
   * Mark list as busy/loading
   */
  busy(element: HTMLElement, isBusy: boolean = true): void {
    element.setAttribute('aria-busy', isBusy ? 'true' : 'false');
  },

  /**
   * Setup menu/listbox role
   */
  menuRole(element: HTMLElement, role: 'menu' | 'listbox' | 'tree'): void {
    element.setAttribute('role', role);
  },

  /**
   * Setup menu item
   */
  menuItem(element: HTMLElement, role: 'menuitem' | 'option' | 'treeitem' = 'menuitem'): void {
    element.setAttribute('role', role);
  },

  /**
   * Mark item as selected
   */
  selected(element: HTMLElement, isSelected: boolean): void {
    element.setAttribute('aria-selected', isSelected ? 'true' : 'false');
  },

  /**
   * Setup live region for dynamic updates
   */
  liveRegion(element: HTMLElement, polite: boolean = true): void {
    element.setAttribute('aria-live', polite ? 'polite' : 'assertive');
    element.setAttribute('aria-atomic', 'true');
  },
};

/**
 * React Hook: useA11y
 * Provides accessibility utilities in React components
 */
export function useA11y() {
  return {
    generateId,
    keyboard: KeyboardNavigationManager,
    contrast: ContrastChecker,
    aria: AriaHelper,
    announce: (msg: string, priority?: 'polite' | 'assertive') => {
      KeyboardNavigationManager.announce(msg, priority);
    },
  };
}

/**
 * Check element for common accessibility issues
 */
export function auditAccessibility(element: HTMLElement): string[] {
  const issues: string[] = [];

  // Check for images without alt text
  element.querySelectorAll('img').forEach((img) => {
    if (!img.hasAttribute('alt')) {
      issues.push(`Image missing alt text: ${img.src}`);
    }
  });

  // Check for buttons without accessible name
  element.querySelectorAll('button').forEach((btn, idx) => {
    const hasText = btn.textContent?.trim().length ?? 0 > 0;
    const hasAriaLabel = btn.hasAttribute('aria-label');
    if (!hasText && !hasAriaLabel) {
      issues.push(`Button ${idx} missing accessible name`);
    }
  });

  // Check for form fields without labels
  element.querySelectorAll('input, textarea, select').forEach((field) => {
    const id = field.id;
    const label = id ? element.querySelector(`label[for="${id}"]`) : null;
    const ariaLabel = field.hasAttribute('aria-label');

    if (!label && !ariaLabel) {
      issues.push(`Form field missing label: ${field.name || field.id}`);
    }
  });

  // Check for color contrast issues in text
  element.querySelectorAll('p, span, div').forEach((el) => {
    const style = window.getComputedStyle(el);
    const bg = style.backgroundColor;
    const fg = style.color;

    if (bg !== 'rgba(0, 0, 0, 0)' && !ContrastChecker.meetsAAStandard(fg, bg)) {
      const fontSize = parseFloat(style.fontSize);
      issues.push(
        `Low contrast (${fontSize > 18 ? 'large' : 'normal'} text): ${el.textContent?.substring(0, 30)}`
      );
    }
  });

  // Check for proper heading hierarchy
  const headings: number[] = [];
  element.querySelectorAll('h1, h2, h3, h4, h5, h6').forEach((h) => {
    const level = parseInt(h.tagName[1]);
    headings.push(level);
  });

  for (let i = 1; i < headings.length; i++) {
    if (headings[i] - headings[i - 1] > 1) {
      issues.push(`Heading hierarchy broken: h${headings[i - 1]} -> h${headings[i]}`);
    }
  }

  return issues;
}
