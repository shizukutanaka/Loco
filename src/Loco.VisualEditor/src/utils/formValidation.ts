export const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
export const URL_REGEX = /^(https?:\/\/)?([\da-z\.-]+)\.([a-z\.]{2,6})([\/\w \.-]*)*\/?$/;

export function validateEmail(email: string): string | null {
  if (!email) return 'Email is required';
  if (!EMAIL_REGEX.test(email)) return 'Invalid email format';
  return null;
}

export function validateLabel(label: string, maxLength: number = 100): string | null {
  if (!label) return 'Label is required';
  if (label.length > maxLength) return `Label must be ${maxLength} characters or less`;
  return null;
}

export function validateCondition(condition: string): string | null {
  if (!condition) return 'Condition is required';
  if (condition.trim().length === 0) return 'Condition cannot be empty';
  // Basic validation - ensure it contains valid JavaScript-like syntax
  try {
    new Function(`return ${condition}`);
    return null;
  } catch (_error) {
    return 'Invalid condition expression';
  }
}

export function validateCode(code: string): string | null {
  if (!code) return 'Code is required';
  if (code.trim().length === 0) return 'Code cannot be empty';
  return null;
}

export function validateUrl(url: string): string | null {
  if (!url) return 'URL is required';
  if (!URL_REGEX.test(url)) return 'Invalid URL format';
  return null;
}

export function validateServerUrl(url: string): string | null {
  if (!url) return 'Server URL is required';
  try {
    new URL(url);
    return null;
  } catch (_error) {
    return 'Invalid URL format';
  }
}

export function validateNonEmpty(value: string, fieldName: string): string | null {
  if (!value || value.trim().length === 0) {
    return `${fieldName} is required`;
  }
  return null;
}

export function validateNumber(value: string | number, min?: number, max?: number): string | null {
  const num = typeof value === 'string' ? parseFloat(value) : value;
  if (isNaN(num)) return 'Must be a number';
  if (min !== undefined && num < min) return `Must be at least ${min}`;
  if (max !== undefined && num > max) return `Must be at most ${max}`;
  return null;
}
