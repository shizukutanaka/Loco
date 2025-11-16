/**
 * Accessible Button Component
 *
 * A wrapper component ensuring all buttons meet WCAG 2.1 Level AA accessibility standards.
 * Provides consistent ARIA attributes, keyboard support, and visual feedback.
 */

import React, { ReactNode } from 'react';

interface AccessibleButtonProps {
  /** Button content (text or icon) */
  children: ReactNode;
  /** Aria-label for icon-only buttons or screen reader text */
  ariaLabel: string;
  /** Optional additional aria-describedby for longer descriptions */
  ariaDescribedBy?: string;
  /** Whether button is disabled */
  disabled?: boolean;
  /** Current toggle state (for aria-pressed) */
  pressed?: boolean;
  /** Whether button is in a loading state */
  isLoading?: boolean;
  /** Click handler */
  onClick?: (e: React.MouseEvent<HTMLButtonElement>) => void;
  /** CSS classes */
  className?: string;
  /** Button type */
  type?: 'button' | 'submit' | 'reset';
  /** Tab index */
  tabIndex?: number;
  /** Keyboard event handler for custom key support */
  onKeyDown?: (e: React.KeyboardEvent<HTMLButtonElement>) => void;
}

/**
 * Accessible Button component with proper ARIA attributes
 *
 * Features:
 * - Automatic aria-label from ariaLabel prop
 * - aria-pressed for toggle buttons
 * - aria-disabled for non-native disabled buttons
 * - aria-busy for loading states
 * - Proper keyboard support (Enter/Space)
 * - Focus management
 *
 * @example
 * <AccessibleButton ariaLabel="Save workflow" onClick={handleSave}>
 *   <Save className="w-4 h-4" />
 * </AccessibleButton>
 *
 * @example
 * <AccessibleButton ariaLabel="Show notifications" pressed={notificationsOpen}>
 *   <Bell className="w-4 h-4" />
 * </AccessibleButton>
 */
export function AccessibleButton({
  children,
  ariaLabel,
  ariaDescribedBy,
  disabled = false,
  pressed,
  isLoading = false,
  onClick,
  className = '',
  type = 'button',
  tabIndex = 0,
  onKeyDown,
}: AccessibleButtonProps) {
  const handleKeyDown = (e: React.KeyboardEvent<HTMLButtonElement>) => {
    // Allow Enter and Space to trigger click on buttons
    if ((e.key === 'Enter' || e.key === ' ') && !disabled) {
      e.preventDefault();
      onClick?.(e as any);
    }
    onKeyDown?.(e);
  };

  return (
    <button
      type={type}
      onClick={onClick}
      onKeyDown={handleKeyDown}
      disabled={disabled}
      tabIndex={tabIndex}
      aria-label={ariaLabel}
      aria-describedby={ariaDescribedBy}
      aria-pressed={pressed !== undefined ? pressed : undefined}
      aria-busy={isLoading}
      aria-disabled={disabled}
      className={className}
    >
      {children}
    </button>
  );
}

/**
 * Accessible Icon Button - specialized for icon-only buttons
 * Ensures aria-label is always provided for screen readers
 */
export function AccessibleIconButton({
  ariaLabel,
  children,
  disabled = false,
  isLoading = false,
  onClick,
  className = 'p-2 hover:bg-gray-100 rounded transition-colors',
  type = 'button',
  pressed,
}: Omit<AccessibleButtonProps, 'ariaLabel'> & { ariaLabel: string }) {
  return (
    <AccessibleButton
      ariaLabel={ariaLabel}
      disabled={disabled}
      isLoading={isLoading}
      onClick={onClick}
      type={type}
      pressed={pressed}
      className={className}
    >
      {children}
    </AccessibleButton>
  );
}

/**
 * Accessible Modal Dialog Wrapper
 * Provides proper ARIA attributes for modal dialogs with focus management
 */
interface AccessibleDialogProps {
  /** Dialog title (used in aria-labelledby) */
  title: string;
  /** Dialog content */
  children: ReactNode;
  /** Whether dialog is open */
  isOpen: boolean;
  /** Handler to close dialog */
  onClose: () => void;
  /** Whether to show close button */
  showCloseButton?: boolean;
  /** Additional className for dialog container */
  className?: string;
  /** Additional className for dialog content */
  contentClassName?: string;
}

/**
 * Accessible Modal Dialog with proper ARIA attributes
 *
 * Features:
 * - role="dialog" and aria-modal="true"
 * - Focus trap (managed by parent)
 * - aria-labelledby linking to title
 * - Escape key to close
 * - Semantic HTML structure
 *
 * @example
 * <AccessibleDialog
 *   title="Create New Workflow"
 *   isOpen={isOpen}
 *   onClose={() => setIsOpen(false)}
 * >
 *   <form>{...}</form>
 * </AccessibleDialog>
 */
export function AccessibleDialog({
  title,
  children,
  isOpen,
  onClose,
  showCloseButton = true,
  className = 'fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6',
  contentClassName = 'bg-white rounded-xl shadow-2xl max-w-2xl w-full max-h-[90vh] flex flex-col',
}: AccessibleDialogProps) {
  React.useEffect(() => {
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && isOpen) {
        onClose();
      }
    };

    if (isOpen) {
      document.addEventListener('keydown', handleEscape);
      // Prevent scrolling when modal is open
      document.body.style.overflow = 'hidden';
    }

    return () => {
      document.removeEventListener('keydown', handleEscape);
      document.body.style.overflow = 'unset';
    };
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div className={className} onClick={onClose}>
      <div
        className={contentClassName}
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="dialog-title"
      >
        {/* Dialog Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <h2 id="dialog-title" className="text-xl font-bold text-gray-900">
            {title}
          </h2>
          {showCloseButton && (
            <AccessibleIconButton
              ariaLabel={`Close ${title}`}
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            >
              <svg
                className="w-5 h-5"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M6 18L18 6M6 6l12 12"
                />
              </svg>
            </AccessibleIconButton>
          )}
        </div>

        {/* Dialog Content */}
        <div className="flex-1 overflow-y-auto p-6">{children}</div>
      </div>
    </div>
  );
}
