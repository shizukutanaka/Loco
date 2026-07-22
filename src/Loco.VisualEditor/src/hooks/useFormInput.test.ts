import { describe, it, expect, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useFormInput } from './useFormInput';

// Minimal change-event helpers
const textEvent = (value: string) =>
  ({ target: { type: 'text', value } } as unknown as React.ChangeEvent<HTMLInputElement>);
const checkboxEvent = (checked: boolean) =>
  ({ target: { type: 'checkbox', checked } } as unknown as React.ChangeEvent<HTMLInputElement>);

describe('useFormInput', () => {
  it('defaults to an empty string and no error', () => {
    const { result } = renderHook(() => useFormInput());
    expect(result.current.value).toBe('');
    expect(result.current.error).toBeNull();
  });

  it('honors the initial value', () => {
    const { result } = renderHook(() => useFormInput({ initialValue: 'hi' }));
    expect(result.current.value).toBe('hi');
  });

  it('onChange updates the value from a text input', () => {
    const { result } = renderHook(() => useFormInput());
    act(() => result.current.onChange(textEvent('hello')));
    expect(result.current.value).toBe('hello');
  });

  it('onChange reads .checked for checkbox inputs', () => {
    const { result } = renderHook(() => useFormInput());
    act(() => result.current.onChange(checkboxEvent(true)));
    expect(result.current.value).toBe(true);
  });

  it('runs the validator and stores the error on change', () => {
    const validator = vi.fn((v: string | number | boolean) =>
      String(v).length < 3 ? 'too short' : null
    );
    const { result } = renderHook(() => useFormInput({ validator }));

    act(() => result.current.onChange(textEvent('ab')));
    expect(result.current.error).toBe('too short');

    act(() => result.current.onChange(textEvent('abcd')));
    expect(result.current.error).toBeNull();
  });

  it('setValue updates the value directly and clearError resets the error', () => {
    const validator = () => 'always bad';
    const { result } = renderHook(() => useFormInput({ validator }));

    act(() => result.current.onChange(textEvent('x')));
    expect(result.current.error).toBe('always bad');

    act(() => result.current.clearError());
    expect(result.current.error).toBeNull();

    act(() => result.current.setValue('direct'));
    expect(result.current.value).toBe('direct');
  });
});
