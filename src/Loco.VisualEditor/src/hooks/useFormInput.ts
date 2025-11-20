/**
 * useFormInput Hook
 *
 * Provides a reusable pattern for managing form input state with validation.
 * Encapsulates value state and change handler to promote code reuse across all form components.
 *
 * Usage:
 * const { value, setValue, onChange } = useFormInput('default value');
 *
 * Benefits:
 * - Reduces boilerplate in components using form inputs
 * - Consistent input handling pattern
 * - Easier to add validation logic later
 * - Memoized onChange callback prevents unnecessary re-renders
 */

import { useState, useCallback } from 'react';

interface UseFormInputOptions {
  initialValue?: string | number | boolean;
  validator?: (value: string | number | boolean) => string | null;
}

interface UseFormInputReturn {
  value: string | number | boolean;
  setValue: (value: string | number | boolean) => void;
  onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => void;
  error: string | null;
  clearError: () => void;
}

export function useFormInput(options: UseFormInputOptions = {}): UseFormInputReturn {
  const { initialValue = '', validator } = options;
  const [value, setValue] = useState<string | number | boolean>(initialValue);
  const [error, setError] = useState<string | null>(null);

  const onChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
      const newValue = e.target.type === 'checkbox'
        ? (e.target as HTMLInputElement).checked
        : e.target.value;

      setValue(newValue);

      if (validator) {
        const validationError = validator(newValue);
        setError(validationError);
      } else {
        setError(null);
      }
    },
    [validator]
  );

  const clearError = useCallback(() => {
    setError(null);
  }, []);

  return {
    value,
    setValue,
    onChange,
    error,
    clearError,
  };
}
