import { useState, useCallback } from 'react';
import {
  EnvironmentVariable,
  validateEnvKey,
  validateEnvValue,
  isDuplicateEnvKey,
  normalizeEnvKey,
  parseEnvValue,
} from '@/utils/environmentVariableValidation';

interface UseEnvironmentVariablesOptions {
  initialVariables?: EnvironmentVariable[];
  onUpdate?: (variables: EnvironmentVariable[]) => void;
}

interface UseEnvironmentVariablesReturn {
  variables: EnvironmentVariable[];
  addVariable: (key: string, value: string, isSecret?: boolean) => boolean;
  updateVariable: (key: string, updates: Partial<EnvironmentVariable>) => boolean;
  removeVariable: (key: string) => void;
  getVariable: (key: string) => EnvironmentVariable | undefined;
  getVariableValue: (key: string) => unknown;
  clearVariables: () => void;
  validateAll: () => Record<string, string | null>;
  errors: Record<string, string | null>;
}

export function useEnvironmentVariables(
  options: UseEnvironmentVariablesOptions = {}
): UseEnvironmentVariablesReturn {
  const { initialVariables = [], onUpdate } = options;
  const [variables, setVariables] = useState<EnvironmentVariable[]>(initialVariables);
  const [errors, setErrors] = useState<Record<string, string | null>>({});

  const addVariable = useCallback(
    (key: string, value: string, isSecret = false): boolean => {
      const normalizedKey = normalizeEnvKey(key);
      const keyError = validateEnvKey(normalizedKey);
      const valueError = validateEnvValue(value);
      const isDuplicate = isDuplicateEnvKey(normalizedKey, variables.map((v) => v.key));

      if (keyError || valueError || isDuplicate) {
        // Use functional setState to merge new errors with existing errors
        setErrors((prev) => ({
          ...prev,
          [`${normalizedKey}-key`]: keyError || null,
          [`${normalizedKey}-value`]: valueError || null,
          [`${normalizedKey}-duplicate`]: isDuplicate ? 'Duplicate key' : null,
        }));
        return false;
      }

      const newVariable: EnvironmentVariable = {
        key: normalizedKey,
        value,
        isSecret,
      };

      // Use functional setState to avoid stale closure - onUpdate gets called with actual new state
      setVariables((prev) => {
        const updated = [...prev, newVariable];
        onUpdate?.(updated);
        return updated;
      });
      // Clear only errors related to this key
      setErrors((prev) => {
        const newErrors = { ...prev };
        delete newErrors[`${normalizedKey}-key`];
        delete newErrors[`${normalizedKey}-value`];
        delete newErrors[`${normalizedKey}-duplicate`];
        return newErrors;
      });
      return true;
    },
    [onUpdate]
  );

  const updateVariable = useCallback(
    (key: string, updates: Partial<EnvironmentVariable>): boolean => {
      const variable = variables.find((v) => v.key === key);
      if (!variable) return false;

      const newKey = updates.key || key;
      const newValue = updates.value || variable.value;

      if (newKey !== key && isDuplicateEnvKey(newKey, variables.map((v) => v.key), key)) {
        // Use functional setState to merge errors with existing state
        setErrors((prev) => ({
          ...prev,
          [`${newKey}-duplicate`]: 'Duplicate key',
        }));
        return false;
      }

      const keyError = validateEnvKey(newKey);
      const valueError = validateEnvValue(newValue);

      if (keyError || valueError) {
        // Use functional setState to merge errors with existing state
        setErrors((prev) => ({
          ...prev,
          [`${newKey}-key`]: keyError || null,
          [`${newKey}-value`]: valueError || null,
        }));
        return false;
      }

      // Use functional setState to avoid stale closure - onUpdate gets called with actual new state
      setVariables((prev) => {
        const updated = prev.map((v) => (v.key === key ? { ...v, ...updates } : v));
        onUpdate?.(updated);
        return updated;
      });
      // Clear only errors related to this key
      setErrors((prev) => {
        const newErrors = { ...prev };
        delete newErrors[`${newKey}-key`];
        delete newErrors[`${newKey}-value`];
        delete newErrors[`${newKey}-duplicate`];
        // Also clear old key errors if key was changed
        if (newKey !== key) {
          delete newErrors[`${key}-key`];
          delete newErrors[`${key}-value`];
          delete newErrors[`${key}-duplicate`];
        }
        return newErrors;
      });
      return true;
    },
    [onUpdate, variables]
  );

  const removeVariable = useCallback(
    (key: string) => {
      // Use functional setState to avoid stale closure - onUpdate gets called with actual new state
      setVariables((prev) => {
        const updated = prev.filter((v) => v.key !== key);
        onUpdate?.(updated);
        return updated;
      });
      // Clear only errors related to this key
      setErrors((prev) => {
        const newErrors = { ...prev };
        delete newErrors[`${key}-key`];
        delete newErrors[`${key}-value`];
        delete newErrors[`${key}-duplicate`];
        return newErrors;
      });
    },
    [onUpdate]
  );

  const getVariable = useCallback(
    (key: string): EnvironmentVariable | undefined => {
      return variables.find((v) => v.key === key);
    },
    [variables]
  );

  const getVariableValue = useCallback(
    (key: string): unknown => {
      const variable = getVariable(key);
      return variable ? parseEnvValue(variable.value) : undefined;
    },
    [getVariable]
  );

  const clearVariables = useCallback(() => {
    setVariables([]);
    setErrors({});
    onUpdate?.([]);
  }, [onUpdate]);

  const validateAll = useCallback((): Record<string, string | null> => {
    const allErrors: Record<string, string | null> = {};
    const keys: string[] = [];

    variables.forEach((env) => {
      const keyError = validateEnvKey(env.key);
      const valueError = validateEnvValue(env.value);
      const isDuplicate = isDuplicateEnvKey(env.key, keys);

      if (keyError) allErrors[`${env.key}-key`] = keyError;
      if (valueError) allErrors[`${env.key}-value`] = valueError;
      if (isDuplicate) allErrors[`${env.key}-duplicate`] = 'Duplicate key';

      keys.push(env.key);
    });

    setErrors(allErrors);
    return allErrors;
  }, [variables]);

  return {
    variables,
    addVariable,
    updateVariable,
    removeVariable,
    getVariable,
    getVariableValue,
    clearVariables,
    validateAll,
    errors,
  };
}
