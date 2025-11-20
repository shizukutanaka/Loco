import { useState } from 'react';
import {
  validateLabel as validateLabelFn,
  validateCondition as validateConditionFn,
  validateCode as validateCodeFn,
  validateNodeField,
} from '@/utils/nodeValidation';

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
 *
 * Uses consolidated validation functions from nodeValidation.ts
 */
export function usePropertyPanelValidation() {
  const [errors, setErrors] = useState<ValidationError>({});

  const validateLabel = (label: string): string | undefined => {
    const error = validateLabelFn(label);
    return error ?? undefined;
  };

  const validateCondition = (condition: string): string | undefined => {
    const error = validateConditionFn(condition);
    return error ?? undefined;
  };

  const validateCode = (code: string): string | undefined => {
    const error = validateCodeFn(code);
    return error ?? undefined;
  };

  const validateField = (fieldName: string, value: string | number): string | undefined => {
    const error = validateNodeField(fieldName, value);
    return error ?? undefined;
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
