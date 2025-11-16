/**
 * FormCheckbox Component
 *
 * Renders a checkbox with consistent styling, validation, and accessibility.
 * Includes error handling, help text, and ARIA attributes.
 */

import { memo } from 'react';

export interface FormCheckboxProps {
  /** Checkbox checked state */
  checked: boolean;
  /** Change handler */
  onChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
  /** Checkbox label */
  label?: string;
  /** Help text displayed below checkbox */
  helpText?: string;
  /** Error message if validation failed */
  error?: string;
  /** Additional description for screen readers */
  description?: string;
  /** Disable checkbox */
  disabled?: boolean;
  /** Custom CSS class */
  className?: string;
  /** Checkbox ID for label association */
  id?: string;
  /** Auto-focus checkbox */
  autoFocus?: boolean;
  /** Indent for nested checkboxes */
  indent?: boolean;
}

export const FormCheckbox = memo(
  ({
    checked,
    onChange,
    label,
    helpText,
    error,
    description,
    disabled = false,
    className = '',
    id,
    autoFocus = false,
    indent = false,
  }: FormCheckboxProps) => {
    const errorId = error ? `${id}-error` : undefined;
    const descriptionId = description ? `${id}-description` : undefined;
    const ariaDescribedBy = [errorId, descriptionId].filter(Boolean).join(' ') || undefined;

    return (
      <div className={`flex flex-col gap-1 ${indent ? 'pl-6' : ''} ${className}`}>
        <label
          className={`flex items-center gap-3 cursor-pointer ${
            disabled ? 'opacity-50 cursor-not-allowed' : ''
          }`}
        >
          <input
            id={id}
            type="checkbox"
            checked={checked}
            onChange={onChange}
            disabled={disabled}
            autoFocus={autoFocus}
            aria-invalid={!!error}
            aria-describedby={ariaDescribedBy}
            className="w-4 h-4 text-loco-primary border-gray-300 rounded focus:ring-2 focus:ring-loco-primary focus:ring-offset-0 cursor-pointer"
          />
          {label && (
            <span className="text-sm font-medium text-gray-700">{label}</span>
          )}
        </label>

        {error && (
          <div
            id={errorId}
            className="text-sm text-red-600 flex items-center gap-1"
            role="alert"
          >
            <span>⚠</span> {error}
          </div>
        )}

        {!error && helpText && (
          <p className="text-xs text-gray-500">{helpText}</p>
        )}

        {description && (
          <div id={descriptionId} className="sr-only">
            {description}
          </div>
        )}
      </div>
    );
  }
);

FormCheckbox.displayName = 'FormCheckbox';
