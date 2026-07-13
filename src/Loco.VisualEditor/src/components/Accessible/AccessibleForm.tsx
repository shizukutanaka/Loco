// Phase 3: WCAG 2.1 AA Accessible Form Component
// Fully accessible form with proper labels, error messages, and keyboard navigation

import React, { ReactNode, FormEvent, useCallback, useEffect, useRef } from 'react';
import { generateId, KeyboardNavigationManager } from '../../utils/a11y';

interface AccessibleFormProps {
  onSubmit: (formData: FormData) => void | Promise<void>;
  children: ReactNode;
  className?: string;
  ariaLabel?: string;
  id?: string;
  noValidate?: boolean;
}

/**
 * Accessible Form Component
 * - Proper form semantics
 * - Error message association
 * - Keyboard navigation
 * - ARIA attributes
 */
export const AccessibleForm: React.FC<AccessibleFormProps> = ({
  onSubmit,
  children,
  className = '',
  ariaLabel,
  id,
  noValidate = false,
}) => {
  const formRef = useRef<HTMLFormElement>(null);
  const formId = useRef(id || generateId('form'));

  useEffect(() => {
    if (formRef.current) {
      KeyboardNavigationManager.setupKeyboardShortcuts(formRef.current);
    }
  }, []);

  const handleSubmit = useCallback(
    async (e: FormEvent<HTMLFormElement>) => {
      e.preventDefault();

      const formData = new FormData(e.currentTarget);

      try {
        await Promise.resolve(onSubmit(formData));
        KeyboardNavigationManager.announce('Form submitted successfully', 'assertive');
      } catch (error) {
        const message = error instanceof Error ? error.message : 'Form submission failed';
        KeyboardNavigationManager.announce(`Error: ${message}`, 'assertive');
      }
    },
    [onSubmit]
  );

  return (
    <form
      ref={formRef}
      id={formId.current}
      onSubmit={handleSubmit}
      className={className}
      aria-label={ariaLabel}
      noValidate={noValidate}
      role="form"
    >
      {children}
    </form>
  );
};

interface AccessibleFieldProps {
  name: string;
  label: ReactNode;
  type?: string;
  required?: boolean;
  disabled?: boolean;
  error?: string;
  helperText?: string;
  placeholder?: string;
  value?: string;
  onChange?: (value: string) => void;
  onBlur?: () => void;
  className?: string;
  id?: string;
  autoComplete?: string;
  pattern?: string;
  maxLength?: number;
  minLength?: number;
}

/**
 * Accessible Form Field Component
 * - Properly associated labels
 * - Error message links via aria-describedby
 * - Required field indicators
 * - Helper text support
 */
export const AccessibleField: React.FC<AccessibleFieldProps> = ({
  name,
  label,
  type = 'text',
  required = false,
  disabled = false,
  error,
  helperText,
  placeholder,
  value,
  onChange,
  onBlur,
  className = '',
  id,
  autoComplete,
  pattern,
  maxLength,
  minLength,
}) => {
  const fieldId = useRef(id || generateId(name));
  const errorId = useRef(generateId(`${name}-error`));
  const helperId = useRef(generateId(`${name}-helper`));

  return (
    <div className={`accessible-field ${className}`}>
      <label htmlFor={fieldId.current} className="field-label">
        {label}
        {required && <span aria-label="required">*</span>}
      </label>

      <input
        id={fieldId.current}
        name={name}
        type={type}
        value={value}
        onChange={(e) => onChange?.(e.target.value)}
        onBlur={onBlur}
        disabled={disabled}
        placeholder={placeholder}
        autoComplete={autoComplete}
        pattern={pattern}
        maxLength={maxLength}
        minLength={minLength}
        required={required}
        aria-required={required}
        aria-invalid={!!error}
        aria-describedby={
          [error && errorId.current, helperText && helperId.current].filter(Boolean).join(' ')
        }
        className={`field-input ${error ? 'field-error' : ''}`}
      />

      {error && (
        <div id={errorId.current} className="field-error-message" role="alert">
          {error}
        </div>
      )}

      {helperText && !error && (
        <div id={helperId.current} className="field-helper-text">
          {helperText}
        </div>
      )}
    </div>
  );
};

interface AccessibleSelectProps {
  name: string;
  label: ReactNode;
  options: Array<{ value: string; label: string }>;
  required?: boolean;
  disabled?: boolean;
  error?: string;
  value?: string;
  onChange?: (value: string) => void;
  className?: string;
  id?: string;
}

/**
 * Accessible Select Component
 * - Proper label association
 * - Error handling
 */
export const AccessibleSelect: React.FC<AccessibleSelectProps> = ({
  name,
  label,
  options,
  required = false,
  disabled = false,
  error,
  value,
  onChange,
  className = '',
  id,
}) => {
  const selectId = useRef(id || generateId(name));
  const errorId = useRef(generateId(`${name}-error`));

  return (
    <div className={`accessible-field ${className}`}>
      <label htmlFor={selectId.current} className="field-label">
        {label}
        {required && <span aria-label="required">*</span>}
      </label>

      <select
        id={selectId.current}
        name={name}
        value={value}
        onChange={(e) => onChange?.(e.target.value)}
        disabled={disabled}
        required={required}
        aria-required={required}
        aria-invalid={!!error}
        aria-describedby={error ? errorId.current : undefined}
        className={`field-select ${error ? 'field-error' : ''}`}
      >
        <option value="">Select an option</option>
        {options.map((opt) => (
          <option key={opt.value} value={opt.value}>
            {opt.label}
          </option>
        ))}
      </select>

      {error && (
        <div id={errorId.current} className="field-error-message" role="alert">
          {error}
        </div>
      )}
    </div>
  );
};

interface AccessibleCheckboxProps {
  name: string;
  label: ReactNode;
  checked?: boolean;
  onChange?: (checked: boolean) => void;
  disabled?: boolean;
  required?: boolean;
  className?: string;
  id?: string;
}

/**
 * Accessible Checkbox Component
 * - Proper label association
 * - Keyboard accessible
 */
export const AccessibleCheckbox: React.FC<AccessibleCheckboxProps> = ({
  name,
  label,
  checked = false,
  onChange,
  disabled = false,
  required = false,
  className = '',
  id,
}) => {
  const checkboxId = useRef(id || generateId(name));

  return (
    <div className={`accessible-checkbox ${className}`}>
      <input
        id={checkboxId.current}
        name={name}
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange?.(e.target.checked)}
        disabled={disabled}
        required={required}
        aria-required={required}
        className="checkbox-input"
      />
      <label htmlFor={checkboxId.current} className="checkbox-label">
        {label}
        {required && <span aria-label="required">*</span>}
      </label>
    </div>
  );
};

interface AccessibleTextareaProps {
  name: string;
  label: ReactNode;
  value?: string;
  onChange?: (value: string) => void;
  required?: boolean;
  disabled?: boolean;
  error?: string;
  placeholder?: string;
  rows?: number;
  maxLength?: number;
  className?: string;
  id?: string;
}

/**
 * Accessible Textarea Component
 * - Proper label association
 * - Error message support
 */
export const AccessibleTextarea: React.FC<AccessibleTextareaProps> = ({
  name,
  label,
  value,
  onChange,
  required = false,
  disabled = false,
  error,
  placeholder,
  rows = 4,
  maxLength,
  className = '',
  id,
}) => {
  const textareaId = useRef(id || generateId(name));
  const errorId = useRef(generateId(`${name}-error`));

  return (
    <div className={`accessible-field ${className}`}>
      <label htmlFor={textareaId.current} className="field-label">
        {label}
        {required && <span aria-label="required">*</span>}
      </label>

      <textarea
        id={textareaId.current}
        name={name}
        value={value}
        onChange={(e) => onChange?.(e.target.value)}
        disabled={disabled}
        placeholder={placeholder}
        rows={rows}
        maxLength={maxLength}
        required={required}
        aria-required={required}
        aria-invalid={!!error}
        aria-describedby={error ? errorId.current : undefined}
        className={`field-textarea ${error ? 'field-error' : ''}`}
      />

      {maxLength && (
        <div className="field-counter" aria-live="polite">
          {(value?.length ?? 0)} / {maxLength}
        </div>
      )}

      {error && (
        <div id={errorId.current} className="field-error-message" role="alert">
          {error}
        </div>
      )}
    </div>
  );
};
