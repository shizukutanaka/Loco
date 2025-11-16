/**
 * FormTextarea Component
 *
 * Renders a textarea with consistent styling, validation, and accessibility.
 * Includes error handling, help text, character counter, and ARIA attributes.
 */

import { memo } from 'react';

export interface FormTextareaProps {
  /** Current textarea value */
  value: string;
  /** Change handler */
  onChange: (e: React.ChangeEvent<HTMLTextAreaElement>) => void;
  /** Textarea label */
  label?: string;
  /** Placeholder text */
  placeholder?: string;
  /** Number of rows */
  rows?: number;
  /** Help text displayed below textarea */
  helpText?: string;
  /** Error message if validation failed */
  error?: string;
  /** Additional description for screen readers */
  description?: string;
  /** Make textarea required */
  required?: boolean;
  /** Disable textarea */
  disabled?: boolean;
  /** Custom CSS class */
  className?: string;
  /** Textarea ID for label association */
  id?: string;
  /** Show character counter */
  showCounter?: boolean;
  /** Maximum characters allowed */
  maxLength?: number;
  /** Monospace font for code input */
  isCode?: boolean;
  /** Auto-focus textarea */
  autoFocus?: boolean;
}

export const FormTextarea = memo(
  ({
    value,
    onChange,
    label,
    placeholder,
    rows = 4,
    helpText,
    error,
    description,
    required = false,
    disabled = false,
    className = '',
    id,
    showCounter = false,
    maxLength,
    isCode = false,
    autoFocus = false,
  }: FormTextareaProps) => {
    const errorId = error ? `${id}-error` : undefined;
    const descriptionId = description ? `${id}-description` : undefined;
    const ariaDescribedBy = [errorId, descriptionId].filter(Boolean).join(' ') || undefined;

    const characterCount = value.length;
    const isNearLimit = maxLength && characterCount >= maxLength * 0.9;

    return (
      <div className={`flex flex-col gap-1 ${className}`}>
        {label && (
          <label
            htmlFor={id}
            className="text-sm font-medium text-gray-700"
          >
            {label}
            {required && <span className="text-red-500 ml-1">*</span>}
          </label>
        )}

        <textarea
          id={id}
          value={value}
          onChange={onChange}
          placeholder={placeholder}
          rows={rows}
          disabled={disabled}
          required={required}
          autoFocus={autoFocus}
          maxLength={maxLength}
          aria-invalid={!!error}
          aria-describedby={ariaDescribedBy}
          className={`w-full px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:border-transparent transition-colors resize-none ${
            isCode ? 'font-mono text-sm' : ''
          } ${
            error
              ? 'border-red-500 focus:ring-red-500 bg-red-50'
              : 'border-gray-300 focus:ring-loco-primary'
          } ${
            disabled ? 'bg-gray-50 cursor-not-allowed text-gray-500' : ''
          }`}
        />

        <div className="flex items-center justify-between gap-2">
          <div>
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
          </div>

          {showCounter && maxLength && (
            <div
              className={`text-xs whitespace-nowrap ${
                isNearLimit ? 'text-orange-600 font-medium' : 'text-gray-500'
              }`}
            >
              {characterCount}/{maxLength}
            </div>
          )}
        </div>

        {description && (
          <div id={descriptionId} className="sr-only">
            {description}
          </div>
        )}
      </div>
    );
  }
);

FormTextarea.displayName = 'FormTextarea';
