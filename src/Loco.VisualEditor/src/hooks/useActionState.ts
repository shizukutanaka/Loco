// Phase 3: React 19 useActionState Hook
// Server actions state management with built-in loading/error handling

import { useReducer, useCallback, useRef, Dispatch, SetStateAction } from 'react';

/**
 * Server Action function type
 * Receives FormData and returns a promise with next state
 */
export type ServerAction<T = unknown> = (
  previousState: T | null,
  formData: FormData
) => Promise<T | null>;

/**
 * Action State result tuple: [state, formAction, isPending]
 */
export type ActionState<T = unknown> = readonly [
  state: T | null,
  formAction: (formData: FormData) => void,
  isPending: boolean
];

/**
 * React 19 useActionState Hook - Advanced form action state management
 * Manages server action state without useTransition boilerplate
 *
 * @param action Server action function (async, receives FormData)
 * @param initialState Initial state value
 * @returns [state, formAction, isPending]
 *
 * @example
 * const [formState, formAction, isPending] = useActionState(
 *   async (prevState, formData) => {
 *     const result = await submitWorkflow(formData);
 *     return result;
 *   },
 *   null
 * );
 *
 * <form action={formAction}>
 *   <input name="workflowName" />
 *   <button disabled={isPending}>
 *     {isPending ? 'Saving...' : 'Save'}
 *   </button>
 * </form>
 */
export function useActionState<T = unknown>(
  action: ServerAction<T>,
  initialState: T | null
): ActionState<T> {
  const [state, setState] = useReducer(
    (_: T | null, newState: T | null) => newState,
    initialState
  );

  const isPendingRef = useRef(false);
  const [, setIsPending] = useReducer(
    (prev: boolean) => !prev,
    false
  );

  const formAction = useCallback(
    async (formData: FormData) => {
      try {
        isPendingRef.current = true;
        setIsPending(); // Trigger pending state update

        // Execute server action
        const result = await action(state, formData);

        // Update state with result
        setState(result);
      } catch (error) {
        // Error handling - state remains unchanged
        console.error('Action error:', error);
        throw error;
      } finally {
        isPendingRef.current = false;
        setIsPending(); // Toggle pending state back to false
      }
    },
    [action, state]
  );

  return [state, formAction, isPendingRef.current] as const;
}

/**
 * Enhanced useActionState with error handling
 *
 * @param action Server action function
 * @param initialState Initial state
 * @param onError Error callback handler
 * @returns [state, formAction, isPending, error]
 */
export function useActionStateWithError<T = unknown>(
  action: ServerAction<T>,
  initialState: T | null,
  onError?: (error: Error) => void
): readonly [
  state: T | null,
  formAction: (formData: FormData) => void,
  isPending: boolean,
  error: Error | null
] {
  const [state, setState] = useReducer(
    (_: T | null, newState: T | null) => newState,
    initialState
  );

  const [error, setError] = useReducer(
    (_: Error | null, newError: Error | null) => newError,
    null
  );

  const isPendingRef = useRef(false);
  const [, setIsPending] = useReducer(
    (prev: boolean) => !prev,
    false
  );

  const formAction = useCallback(
    async (formData: FormData) => {
      try {
        setError(null); // Clear previous errors
        isPendingRef.current = true;
        setIsPending();

        const result = await action(state, formData);
        setState(result);
      } catch (err) {
        const error = err instanceof Error ? err : new Error(String(err));
        setError(error);
        onError?.(error);
      } finally {
        isPendingRef.current = false;
        setIsPending();
      }
    },
    [action, state, onError]
  );

  return [state, formAction, isPendingRef.current, error] as const;
}

/**
 * useActionState for multi-step forms
 * Tracks which step is pending and manages step-specific state
 *
 * @param action Server action for current step
 * @param stepId Current step identifier
 * @param initialState Initial state
 * @returns [state, formAction, isPending, currentStep]
 */
export function useActionStateMultiStep<T = unknown>(
  action: ServerAction<T>,
  stepId: string | number,
  initialState: T | null
): readonly [
  state: T | null,
  formAction: (formData: FormData) => void,
  isPending: boolean,
  currentStep: string | number
] {
  const [state, setState] = useReducer(
    (_: T | null, newState: T | null) => newState,
    initialState
  );

  const [currentStep, setCurrentStep] = useReducer(
    (_: string | number, newStep: string | number) => newStep,
    stepId
  );

  const isPendingRef = useRef(false);
  const [, setIsPending] = useReducer(
    (prev: boolean) => !prev,
    false
  );

  const formAction = useCallback(
    async (formData: FormData) => {
      try {
        isPendingRef.current = true;
        setIsPending();
        setCurrentStep(stepId);

        const result = await action(state, formData);
        setState(result);

        return result;
      } catch (error) {
        console.error(`Action error in step ${stepId}:`, error);
        throw error;
      } finally {
        isPendingRef.current = false;
        setIsPending();
      }
    },
    [action, state, stepId]
  );

  return [state, formAction, isPendingRef.current, currentStep] as const;
}
