/**
 * FormSelect Component
 *
 * Renders a dropdown select with consistent styling, validation, and accessibility.
 * Includes error handling, help text, and ARIA attributes.
 */

import { memo } from 'react';

export interface SelectOption {
  value: string | number;
  label: string;
  disabled?: boolean;
}

export interface FormSelectProps {
  /** Current select value */
  value: string | number;
  /** Change handler */
  onChange: (e: React.ChangeEvent<HTMLSelectElement>) => void;
  /** Select label */
  label?: string;
  /** Available options */
  options: SelectOption[];
  /** Placeholder text for empty option */
  placeholder?: string;
  /** Help text displayed below select */
  helpText?: string;
  /** Error message if validation failed */
  error?: string;
  /** Additional description for screen readers */
  description?: string;
  /** Make select required */
  required?: boolean;
  /** Disable select */
  disabled?: boolean;
  /** Custom CSS class */
  className?: string;
  /** Select ID for label association */
  id?: string;
  /** Allow multiple selections */
  multiple?: boolean;
  /** Show empty option */
  showEmpty?: boolean;
  /** Auto-focus select */
  autoFocus?: boolean;
}

export const FormSelect = memo(
  ({
    value,
    onChange,
    label,
    options,
    placeholder = 'Select an option',
    helpText,
    error,
    description,
    required = false,
    disabled = false,
    className = '',
    id,
    multiple = false,
    showEmpty = true,
    autoFocus = false,
  }: FormSelectProps) => {
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

        <select
          id={id}
          value={value}
          onChange={onChange}
          disabled={disabled}
          required={required}
          multiple={multiple}
          autoFocus={autoFocus}
          aria-invalid={!!error}
          aria-describedby={ariaDescribedBy}
          className={`w-full px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:border-transparent transition-colors appearance-none bg-white cursor-pointer ${
            error
              ? 'border-red-500 focus:ring-red-500 bg-red-50'
              : 'border-gray-300 focus:ring-loco-primary'
          } ${
            disabled ? 'bg-gray-50 cursor-not-allowed text-gray-500' : ''
          }`}
          style={{
            backgroundImage: !disabled
              ? `url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20' fill='%236B7280'%3E%3Cpath fill-rule='evenodd' d='M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z' clip-rule='evenodd'/%3E%3C/svg%3E")`
              : 'none',
            backgroundPosition: 'right 0.75rem center',
            backgroundRepeat: 'no-repeat',
            backgroundSize: '1.25em 1.25em',
            paddingRight: '2.5rem',
          }}
        >
          {showEmpty && <option value="">{placeholder}</option>}
          {options.map((option) => (
            <option
              key={option.value}
              value={option.value}
              disabled={option.disabled}
            >
              {option.label}
            </option>
          ))}
        </select>

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

FormSelect.displayName = 'FormSelect';
