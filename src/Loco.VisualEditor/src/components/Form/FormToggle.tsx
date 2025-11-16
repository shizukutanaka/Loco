/**
 * FormToggle Component
 *
 * Renders a toggle switch with consistent styling, validation, and accessibility.
 * Includes error handling, help text, and ARIA attributes.
 */

import { memo } from 'react';

export interface FormToggleProps {
  /** Toggle checked state */
  checked: boolean;
  /** Change handler */
  onChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
  /** Toggle label */
  label?: string;
  /** Label position: left or right of toggle */
  labelPosition?: 'left' | 'right';
  /** Help text displayed below toggle */
  helpText?: string;
  /** Error message if validation failed */
  error?: string;
  /** Additional description for screen readers */
  description?: string;
  /** Disable toggle */
  disabled?: boolean;
  /** Custom CSS class */
  className?: string;
  /** Toggle ID for label association */
  id?: string;
  /** Size of toggle: small, medium, large */
  size?: 'sm' | 'md' | 'lg';
  /** Auto-focus toggle */
  autoFocus?: boolean;
}

interface ToggleSizeConfig {
  container: string;
  slider: string;
  dot: string;
}

const sizes: Record<string, ToggleSizeConfig> = {
  sm: {
    container: 'w-9 h-5',
    slider: 'peer-checked:after:translate-x-4',
    dot: 'after:w-4 after:h-4',
  },
  md: {
    container: 'w-11 h-6',
    slider: 'peer-checked:after:translate-x-5',
    dot: 'after:w-5 after:h-5',
  },
  lg: {
    container: 'w-14 h-7',
    slider: 'peer-checked:after:translate-x-7',
    dot: 'after:w-6 after:h-6',
  },
};

export const FormToggle = memo(
  ({
    checked,
    onChange,
    label,
    labelPosition = 'right',
    helpText,
    error,
    description,
    disabled = false,
    className = '',
    id,
    size = 'md',
    autoFocus = false,
  }: FormToggleProps) => {
    const errorId = error ? `${id}-error` : undefined;
    const descriptionId = description ? `${id}-description` : undefined;
    const ariaDescribedBy = [errorId, descriptionId].filter(Boolean).join(' ') || undefined;

    const sizeConfig = sizes[size];

    const toggleElement = (
      <label className={`relative inline-flex items-center cursor-pointer ${disabled ? 'opacity-50 cursor-not-allowed' : ''}`}>
        <input
          id={id}
          type="checkbox"
          checked={checked}
          onChange={onChange}
          disabled={disabled}
          autoFocus={autoFocus}
          aria-invalid={!!error}
          aria-describedby={ariaDescribedBy}
          className="sr-only peer"
        />
        <div
          className={`${sizeConfig.container} bg-gray-200 peer-focus:outline-none peer-focus:ring-2 peer-focus:ring-loco-primary peer-focus:ring-offset-0 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full ${sizeConfig.dot} after:transition-all peer-checked:bg-loco-primary`}
        />
      </label>
    );

    return (
      <div className={`flex flex-col gap-1 ${className}`}>
        <div className={`flex items-center gap-3 ${labelPosition === 'left' ? 'flex-row-reverse' : ''}`}>
          {toggleElement}
          {label && (
            <label
              htmlFor={id}
              className={`text-sm font-medium text-gray-700 cursor-pointer ${
                disabled ? 'cursor-not-allowed' : ''
              }`}
            >
              {label}
            </label>
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

FormToggle.displayName = 'FormToggle';
