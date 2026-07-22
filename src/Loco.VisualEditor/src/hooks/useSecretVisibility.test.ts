import { describe, it, expect } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useSecretVisibility } from './useSecretVisibility';

describe('useSecretVisibility', () => {
  it('starts hidden and toggles', () => {
    const { result } = renderHook(() => useSecretVisibility());
    expect(result.current.isVisible).toBe(false);

    act(() => result.current.toggleVisibility());
    expect(result.current.isVisible).toBe(true);

    act(() => result.current.toggleVisibility());
    expect(result.current.isVisible).toBe(false);
  });

  it('setVisibility sets the state directly', () => {
    const { result } = renderHook(() => useSecretVisibility());
    act(() => result.current.setVisibility(true));
    expect(result.current.isVisible).toBe(true);
  });

  it('getMaskedValue masks using the default 4 visible chars', () => {
    const { result } = renderHook(() => useSecretVisibility());
    expect(result.current.getMaskedValue('supersecret')).toBe('supe*******');
  });

  it('honors a custom visibleChars option', () => {
    const { result } = renderHook(() => useSecretVisibility({ visibleChars: 2 }));
    expect(result.current.getMaskedValue('supersecret')).toBe('su*********');
  });

  it('getDisplayValue returns the raw value only when visible', () => {
    const { result } = renderHook(() => useSecretVisibility());
    expect(result.current.getDisplayValue('supersecret')).toBe('supe*******');

    act(() => result.current.setVisibility(true));
    expect(result.current.getDisplayValue('supersecret')).toBe('supersecret');
  });
});
