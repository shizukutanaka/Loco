/**
 * FormInput Component
 *
 * Renders a text input with consistent styling, validation, and accessibility.
 * Supports multiple input types: text, email, password, url, number
 * Includes error handling, help text, and ARIA attributes.
 */

import { memo, ReactNode } from 'react';

type InputType = 'text' | 'email' | 'password' | 'url' | 'number' | 'tel';

export interface FormInputProps {
  /** Input type */
  type?: InputType;
  /** Current input value */
  value: string | number;
  /** Change handler */
  onChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
  /** Input label */
  label?: string;
  /** Placeholder text */
  placeholder?: string;
  /** Help text displayed below input */
  helpText?: string;
  /** Error message if validation failed */
  error?: string;
  /** Additional description for screen readers */
  description?: string;
  /** Make input required */
  required?: boolean;
  /** Disable input */
  disabled?: boolean;
  /** Custom CSS class */
  className?: string;
  /** Input ID for label association */
  id?: string;
  /** For number inputs: minimum value */
  min?: number;
  /** For number inputs: maximum value */
  max?: number;
  /** For number inputs: step value */
  step?: number | string;
  /** Icon or content to render before input */
  prefix?: ReactNode;
  /** Icon or content to render after input */
  suffix?: ReactNode;
  /** Auto-focus input */
  autoFocus?: boolean;
}

export const FormInput = memo(
  ({
    type = 'text',
    value,
    onChange,
    label,
    placeholder,
    helpText,
    error,
    description,
    required = false,
    disabled = false,
    className = '',
    id,
    min,
    max,
    step,
    prefix,
    suffix,
    autoFocus = false,
  }: FormInputProps) => {
    const errorId = error ? `${id}-error` : undefined;
    const descriptionId = description ? `${id}-description` : undefined;
    const ariaDescribedBy = [errorId, descriptionId].filter(Boolean).join(' ') || undefined;

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

        <div className="relative flex items-center">
          {prefix && (
            <div className="absolute left-3 text-gray-500 pointer-events-none">
              {prefix}
            </div>
          )}

          <input
            id={id}
            type={type}
            value={value}
            onChange={onChange}
            placeholder={placeholder}
            disabled={disabled}
            required={required}
            autoFocus={autoFocus}
            min={min}
            max={max}
            step={step}
            aria-invalid={!!error}
            aria-describedby={ariaDescribedBy}
            className={`w-full px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:border-transparent transition-colors ${
              prefix ? 'pl-10' : ''
            } ${
              suffix ? 'pr-10' : ''
            } ${
              error
                ? 'border-red-500 focus:ring-red-500 bg-red-50'
                : 'border-gray-300 focus:ring-loco-primary'
            } ${
              disabled ? 'bg-gray-50 cursor-not-allowed text-gray-500' : ''
            }`}
          />

          {suffix && (
            <div className="absolute right-3 text-gray-500 pointer-events-none">
              {suffix}
            </div>
          )}
        </div>

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

FormInput.displayName = 'FormInput';
