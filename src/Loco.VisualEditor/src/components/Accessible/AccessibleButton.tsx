// Phase 3: WCAG 2.1 AA Accessible Button Component
// Fully accessible button with keyboard navigation and ARIA support

import React, { ReactNode, MouseEvent, useCallback, useRef } from 'react';
import { generateId } from '../../utils/a11y';

interface AccessibleButtonProps {
  onClick?: (e: React.MouseEvent<HTMLButtonElement>) => void | Promise<void>;
  children: ReactNode;
  disabled?: boolean;
  loading?: boolean;
  type?: 'button' | 'submit' | 'reset';
  variant?: 'primary' | 'secondary' | 'danger' | 'success';
  size?: 'small' | 'medium' | 'large';
  className?: string;
  ariaLabel?: string;
  ariaPressed?: boolean;
  ariaExpanded?: boolean;
  ariaControls?: string;
  title?: string;
  id?: string;
  form?: string;
  fullWidth?: boolean;
}

/**
 * Accessible Button Component
 * - Proper keyboard navigation (Enter, Space)
 * - Clear focus indicators
 * - Disabled state handling
 * - Loading state with aria-busy
 * - ARIA attributes for button state
 * - Minimum touch target size (44x44px)
 */
export const AccessibleButton: React.FC<AccessibleButtonProps> = ({
  onClick,
  children,
  disabled = false,
  loading = false,
  type = 'button',
  variant = 'primary',
  size = 'medium',
  className = '',
  ariaLabel,
  ariaPressed,
  ariaExpanded,
  ariaControls,
  title,
  id,
  form,
  fullWidth = false,
}) => {
  const buttonRef = useRef<HTMLButtonElement>(null);
  const buttonId = useRef(id || generateId('button'));
  const isDisabled = disabled || loading;

  const handleClick = useCallback(
    async (e: MouseEvent<HTMLButtonElement>) => {
      if (isDisabled) {
        e.preventDefault();
        return;
      }

      try {
        await Promise.resolve(onClick?.(e));
      } catch (error) {
        console.error('Button click error:', error);
      }
    },
    [onClick, isDisabled]
  );

  const handleKeyDown = useCallback((e: React.KeyboardEvent<HTMLButtonElement>) => {
    // Space bar activates button (in addition to Enter)
    if (e.key === ' ') {
      e.preventDefault();
      buttonRef.current?.click();
    }
  }, []);

  return (
    <button
      ref={buttonRef}
      id={buttonId.current}
      type={type}
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      disabled={isDisabled}
      aria-label={ariaLabel}
      aria-pressed={ariaPressed}
      aria-expanded={ariaExpanded}
      aria-controls={ariaControls}
      aria-busy={loading}
      title={title}
      form={form}
      className={`
        accessible-button
        button-${variant}
        button-${size}
        ${disabled ? 'button-disabled' : ''}
        ${loading ? 'button-loading' : ''}
        ${fullWidth ? 'button-full-width' : ''}
        ${className}
      `.trim()}
    >
      {loading && <span className="button-spinner" aria-hidden="true" />}
      <span className="button-content">{children}</span>
    </button>
  );
};

interface AccessibleIconButtonProps {
  onClick?: (e: React.MouseEvent<HTMLButtonElement>) => void;
  ariaLabel: string; // Required for icon buttons
  icon: ReactNode;
  disabled?: boolean;
  title?: string;
  className?: string;
  size?: 'small' | 'medium' | 'large';
  id?: string;
}

/**
 * Accessible Icon Button Component
 * - aria-label is REQUIRED for icon-only buttons
 * - Clear focus indicators
 * - Minimum touch target size (44x44px)
 */
export const AccessibleIconButton: React.FC<AccessibleIconButtonProps> = ({
  onClick,
  ariaLabel,
  icon,
  disabled = false,
  title,
  className = '',
  size = 'medium',
  id,
}) => {
  const buttonRef = useRef<HTMLButtonElement>(null);
  const buttonId = useRef(id || generateId('icon-button'));

  const handleClick = useCallback((e: React.MouseEvent<HTMLButtonElement>) => {
    if (!disabled) {
      onClick?.(e);
    }
  }, [onClick, disabled]);

  return (
    <button
      ref={buttonRef}
      id={buttonId.current}
      type="button"
      onClick={handleClick}
      disabled={disabled}
      aria-label={ariaLabel}
      title={title || ariaLabel}
      className={`
        accessible-icon-button
        icon-button-${size}
        ${disabled ? 'icon-button-disabled' : ''}
        ${className}
      `.trim()}
    >
      <span className="icon-button-icon" aria-hidden="true">
        {icon}
      </span>
    </button>
  );
};

interface AccessibleToggleButtonProps {
  pressed: boolean;
  onToggle: (pressed: boolean) => void;
  children: ReactNode;
  ariaLabel: string;
  disabled?: boolean;
  className?: string;
  id?: string;
}

/**
 * Accessible Toggle Button Component
 * - aria-pressed attribute for toggle state
 * - Visual and semantic state
 * - Keyboard accessible (Enter, Space)
 */
export const AccessibleToggleButton: React.FC<AccessibleToggleButtonProps> = ({
  pressed,
  onToggle,
  children,
  ariaLabel,
  disabled = false,
  className = '',
  id,
}) => {
  const buttonRef = useRef<HTMLButtonElement>(null);
  const buttonId = useRef(id || generateId('toggle-button'));

  const handleClick = useCallback(() => {
    if (!disabled) {
      onToggle(!pressed);
    }
  }, [pressed, onToggle, disabled]);

  const handleKeyDown = useCallback((e: React.KeyboardEvent<HTMLButtonElement>) => {
    if (e.key === ' ') {
      e.preventDefault();
      handleClick();
    }
  }, [handleClick]);

  return (
    <button
      ref={buttonRef}
      id={buttonId.current}
      type="button"
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      disabled={disabled}
      aria-pressed={pressed}
      aria-label={ariaLabel}
      className={`
        accessible-toggle-button
        ${pressed ? 'toggle-button-pressed' : ''}
        ${disabled ? 'toggle-button-disabled' : ''}
        ${className}
      `.trim()}
    >
      {children}
    </button>
  );
};

interface AccessibleButtonGroupProps {
  children: ReactNode;
  ariaLabel?: string;
  role?: 'group' | 'toolbar';
  vertical?: boolean;
  className?: string;
}

/**
 * Accessible Button Group Component
 * - Groups related buttons for semantic meaning
 * - Supports horizontal/vertical layouts
 * - Proper ARIA roles
 */
export const AccessibleButtonGroup: React.FC<AccessibleButtonGroupProps> = ({
  children,
  ariaLabel,
  role = 'group',
  vertical = false,
  className = '',
}) => {
  return (
    <div
      role={role}
      aria-label={ariaLabel}
      className={`
        accessible-button-group
        ${vertical ? 'button-group-vertical' : 'button-group-horizontal'}
        ${className}
      `.trim()}
    >
      {children}
    </div>
  );
};

/**
 * Accessible Button Styles (CSS-in-JS example)
 * Include in your stylesheet:
 */
export const accessibleButtonStyles = `
  /* Base button styles */
  .accessible-button {
    /* Minimum touch target size per WCAG 2.1 */
    min-width: 44px;
    min-height: 44px;
    padding: 0.5rem 1rem;
    font-size: 1rem;
    font-weight: 600;
    border: 2px solid transparent;
    border-radius: 4px;
    cursor: pointer;
    transition: all 0.2s ease-in-out;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 0.5rem;
  }

  /* Focus visible for keyboard navigation */
  .accessible-button:focus-visible {
    outline: 3px solid #4A90E2;
    outline-offset: 2px;
  }

  /* Hover state */
  .accessible-button:hover:not(:disabled) {
    transform: translateY(-2px);
    box-shadow: 0 4px 8px rgba(0, 0, 0, 0.15);
  }

  /* Active state */
  .accessible-button:active:not(:disabled) {
    transform: translateY(0);
  }

  /* Disabled state */
  .accessible-button:disabled,
  .button-disabled {
    opacity: 0.6;
    cursor: not-allowed;
    pointer-events: none;
  }

  /* Loading state */
  .button-loading {
    position: relative;
  }

  .button-spinner {
    display: inline-block;
    width: 1em;
    height: 1em;
    border: 2px solid rgba(255, 255, 255, 0.3);
    border-top-color: white;
    border-radius: 50%;
    animation: spin 0.6s linear infinite;
  }

  @keyframes spin {
    to { transform: rotate(360deg); }
  }

  /* Variant styles */
  .button-primary {
    background-color: #4A90E2;
    color: white;
  }

  .button-primary:hover:not(:disabled) {
    background-color: #357ABD;
  }

  .button-secondary {
    background-color: #f0f0f0;
    color: #333;
    border-color: #ccc;
  }

  .button-secondary:hover:not(:disabled) {
    background-color: #e0e0e0;
  }

  .button-danger {
    background-color: #E74C3C;
    color: white;
  }

  .button-danger:hover:not(:disabled) {
    background-color: #C0392B;
  }

  .button-success {
    background-color: #27AE60;
    color: white;
  }

  .button-success:hover:not(:disabled) {
    background-color: #229954;
  }

  /* Size variants */
  .button-small {
    padding: 0.4rem 0.8rem;
    font-size: 0.875rem;
    min-width: 36px;
    min-height: 36px;
  }

  .button-large {
    padding: 0.75rem 1.5rem;
    font-size: 1.125rem;
    min-width: 48px;
    min-height: 48px;
  }

  /* Full width button */
  .button-full-width {
    width: 100%;
  }

  /* Icon button styles */
  .accessible-icon-button {
    min-width: 44px;
    min-height: 44px;
    width: 44px;
    height: 44px;
    padding: 0;
    border: none;
    border-radius: 4px;
    background: transparent;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    transition: background-color 0.2s ease;
  }

  .accessible-icon-button:focus-visible {
    outline: 3px solid #4A90E2;
    outline-offset: 2px;
  }

  .accessible-icon-button:hover:not(:disabled) {
    background-color: rgba(0, 0, 0, 0.05);
  }

  /* Toggle button styles */
  .accessible-toggle-button {
    border: 2px solid #ccc;
    background: white;
    color: #333;
  }

  .toggle-button-pressed {
    background-color: #4A90E2;
    color: white;
    border-color: #4A90E2;
  }

  /* Button group styles */
  .button-group-horizontal {
    display: flex;
    gap: 0.5rem;
    flex-wrap: wrap;
  }

  .button-group-vertical {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }
`;
