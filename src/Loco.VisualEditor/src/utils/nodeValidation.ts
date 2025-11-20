/**
 * Node Validation Utilities
 *
 * Consolidated validation functions for workflow node properties.
 * Provides consistent validation logic used across property panel hooks.
 *
 * This is the single source of truth for:
 * - Node label validation
 * - Condition expression validation
 * - Transform code validation
 */

/**
 * Validates a node label
 * @param label - The label to validate
 * @param maxLength - Maximum allowed length (default: 100)
 * @returns Error message if invalid, null if valid
 */
export function validateLabel(label: string, maxLength: number = 100): string | null {
  if (!label || !label.trim()) {
    return 'Node label is required';
  }
  if (label.length > maxLength) {
    return `Label must be ${maxLength} characters or less`;
  }
  return null;
}

/**
 * Validates a condition expression
 * Ensures the condition is non-empty and contains valid JavaScript-like syntax
 * @param condition - The condition expression to validate
 * @returns Error message if invalid, null if valid
 */
export function validateCondition(condition: string): string | null {
  if (!condition || !condition.trim()) {
    return 'Condition expression is required';
  }

  // Validate JavaScript-like syntax
  try {
    new Function(`return ${condition}`);
    return null;
  } catch (_error) {
    return 'Invalid condition expression';
  }
}

/**
 * Validates transform code
 * Ensures the code is non-empty
 * @param code - The code to validate
 * @returns Error message if invalid, null if valid
 */
export function validateCode(code: string): string | null {
  if (!code || !code.trim()) {
    return 'Transform code is required';
  }
  return null;
}

/**
 * Validates a generic field in node config
 * Routes to appropriate validator based on field name
 * @param fieldName - Name of the field being validated
 * @param value - Value to validate
 * @returns Error message if invalid, null if valid
 */
export function validateNodeField(fieldName: string, value: string | number): string | null {
  if (fieldName === 'label') {
    return validateLabel(String(value));
  } else if (fieldName === 'condition') {
    return validateCondition(String(value));
  } else if (fieldName === 'code') {
    return validateCode(String(value));
  }
  return null;
}
