export interface EnvironmentVariable {
  key: string;
  value: string;
  isSecret?: boolean;
  description?: string;
}

export const ENV_KEY_PATTERN = /^[A-Z_][A-Z0-9_]*$/;
export const ENV_KEY_MIN_LENGTH = 1;
export const ENV_KEY_MAX_LENGTH = 256;

export function validateEnvKey(key: string): string | null {
  if (!key) return 'Key is required';
  if (key.length < ENV_KEY_MIN_LENGTH) return `Key must be at least ${ENV_KEY_MIN_LENGTH} character`;
  if (key.length > ENV_KEY_MAX_LENGTH) return `Key must be at most ${ENV_KEY_MAX_LENGTH} characters`;
  if (!ENV_KEY_PATTERN.test(key)) {
    return 'Key must start with letter or underscore and contain only uppercase letters, numbers, and underscores';
  }
  return null;
}

export function validateEnvValue(value: string): string | null {
  if (!value) return 'Value is required';
  return null;
}

export function isDuplicateEnvKey(
  key: string,
  existingKeys: string[],
  excludeKey?: string
): boolean {
  return existingKeys.some((k) => k === key && k !== excludeKey);
}

export function normalizeEnvKey(key: string): string {
  return key.replace(/[^A-Z0-9_]/gi, '_').toUpperCase();
}

export function parseEnvValue(value: string): unknown {
  // Try to parse as JSON first
  if (value.startsWith('{') || value.startsWith('[')) {
    try {
      return JSON.parse(value);
    } catch (_error) {
      // Not valid JSON, return as string
    }
  }

  // Check for boolean values
  if (value.toLowerCase() === 'true') return true;
  if (value.toLowerCase() === 'false') return false;

  // Check for null
  if (value.toLowerCase() === 'null') return null;

  // Check for numbers
  if (!isNaN(Number(value)) && value !== '') return Number(value);

  // Return as string
  return value;
}

export function formatEnvValue(value: unknown): string {
  if (typeof value === 'string') return value;
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  if (value === null) return 'null';
  if (typeof value === 'object') return JSON.stringify(value, null, 2);
  return String(value);
}

export function maskSecret(value: string, visibleChars: number = 4): string {
  if (value.length <= visibleChars) return '*'.repeat(value.length);
  return value.substring(0, visibleChars) + '*'.repeat(value.length - visibleChars);
}

export function sortEnvVariables(envVars: EnvironmentVariable[]): EnvironmentVariable[] {
  return [...envVars].sort((a, b) => a.key.localeCompare(b.key));
}

export function validateEnvVariables(envVars: EnvironmentVariable[]): Record<string, string | null> {
  const errors: Record<string, string | null> = {};
  const keys: string[] = [];

  envVars.forEach((env) => {
    const keyError = validateEnvKey(env.key);
    const valueError = validateEnvValue(env.value);
    const isDuplicate = isDuplicateEnvKey(env.key, keys);

    if (keyError) errors[`${env.key}-key`] = keyError;
    if (valueError) errors[`${env.key}-value`] = valueError;
    if (isDuplicate) errors[`${env.key}-duplicate`] = 'Duplicate key';

    keys.push(env.key);
  });

  return errors;
}
