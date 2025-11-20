import { useState, useCallback } from 'react';
import { maskSecret } from '@/utils/environmentVariableValidation';

interface UseSecretVisibilityOptions {
  visibleChars?: number;
}

interface UseSecretVisibilityReturn {
  isVisible: boolean;
  toggleVisibility: () => void;
  setVisibility: (visible: boolean) => void;
  getMaskedValue: (value: string) => string;
  getDisplayValue: (value: string) => string;
}

export function useSecretVisibility(
  options: UseSecretVisibilityOptions = {}
): UseSecretVisibilityReturn {
  const { visibleChars = 4 } = options;
  const [isVisible, setIsVisibleState] = useState(false);

  const toggleVisibility = useCallback(() => {
    setIsVisibleState((prev) => !prev);
  }, []);

  const setVisibility = useCallback((visible: boolean) => {
    setIsVisibleState(visible);
  }, []);

  const getMaskedValue = useCallback(
    (value: string): string => {
      return maskSecret(value, visibleChars);
    },
    [visibleChars]
  );

  const getDisplayValue = useCallback(
    (value: string): string => {
      return isVisible ? value : getMaskedValue(value);
    },
    [isVisible, getMaskedValue]
  );

  return {
    isVisible,
    toggleVisibility,
    setVisibility,
    getMaskedValue,
    getDisplayValue,
  };
}
