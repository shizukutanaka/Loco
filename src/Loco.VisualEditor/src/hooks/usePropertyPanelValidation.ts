import { useState } from 'react';

export interface ValidationError {
  label?: string;
  condition?: string;
  code?: string;
  action?: string;
  parameters?: Record<string, string>;
}

/**
 * Custom hook for managing property panel field validation
 * Handles: label, condition, code validation and error state
 */
export function usePropertyPanelValidation() {
  const [errors, setErrors] = useState<ValidationError>({});

  const validateLabel = (label: string): string | undefined => {
    if (!label.trim()) {
      return 'Node label is required';
    }
    if (label.length > 100) {
      return 'Label must be less than 100 characters';
    }
    return undefined;
  };

  const validateCondition = (condition: string): string | undefined => {
    if (!condition.trim()) {
      return 'Condition expression is required';
    }
    return undefined;
  };

  const validateCode = (code: string): string | undefined => {
    if (!code.trim()) {
      return 'Transform code is required';
    }
    return undefined;
  };

  const validateField = (fieldName: string, value: string | number): string | undefined => {
    if (fieldName === 'label') {
      return validateLabel(String(value));
    } else if (fieldName === 'condition') {
      return validateCondition(String(value));
    } else if (fieldName === 'code') {
      return validateCode(String(value));
    }
    return undefined;
  };

  const clearErrors = () => {
    setErrors({});
  };

  const updateError = (fieldName: string, error: string | undefined) => {
    setErrors({ ...errors, [fieldName]: error });
  };

  return {
    errors,
    setErrors,
    validateLabel,
    validateCondition,
    validateCode,
    validateField,
    clearErrors,
    updateError,
  };
}
