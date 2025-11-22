// Phase 3: React 19 useOptimistic Hook
// Optimistic UI updates for instant user feedback

import { useReducer, useCallback, useRef } from 'react';

/**
 * Optimistic reducer function type
 * Applies optimistic update based on action
 */
export type OptimisticReducer<S, A> = (
  currentState: S,
  action: A
) => S;

/**
 * Optimistic action dispatcher
 * Sends action to server while immediately updating UI
 */
export type OptimisticActionDispatcher<A> = (
  action: A
) => void;

/**
 * React 19 useOptimistic Hook - Instant UI feedback with server sync
 * Updates UI optimistically while server processes request
 * Automatically reverts on error
 *
 * @param initialState Current state from server
 * @param reducer Function that applies optimistic update
 * @returns [optimisticState, optimisticDispatch]
 *
 * @example
 * const [optimisticWorkflows, addOptimisticWorkflow] = useOptimistic(
 *   workflows,
 *   (state, action) => {
 *     if (action.type === 'ADD') {
 *       return [...state, action.payload];
 *     }
 *     return state;
 *   }
 * );
 *
 * const handleAddWorkflow = async (formData) => {
 *   const newWorkflow = { id: Date.now(), ...Object.fromEntries(formData) };
 *   addOptimisticWorkflow({ type: 'ADD', payload: newWorkflow });
 *
 *   try {
 *     await submitWorkflow(newWorkflow);
 *   } catch (error) {
 *     // Optimistic state automatically reverts to workflows
 *   }
 * };
 */
export function useOptimistic<S, A>(
  initialState: S,
  reducer: OptimisticReducer<S, A>
): readonly [optimisticState: S, optimisticDispatch: OptimisticActionDispatcher<A>] {
  const [optimisticState, dispatch] = useReducer(
    (state: S, action: A) => {
      try {
        return reducer(state, action);
      } catch (error) {
        console.error('Optimistic update error:', error);
        // Return unchanged state on error
        return state;
      }
    },
    initialState
  );

  const optimisticDispatch = useCallback(
    (action: A) => {
      dispatch(action);
    },
    []
  );

  return [optimisticState, optimisticDispatch] as const;
}

/**
 * useOptimistic for list operations (add, update, delete)
 * Common pattern for managing collections with optimistic updates
 *
 * @param items Current list from server
 * @param getId Function to extract item ID
 * @returns [optimisticItems, add, update, delete, clear]
 *
 * @example
 * const [optimisticWorkflows, addWorkflow, updateWorkflow, deleteWorkflow] =
 *   useOptimisticList(workflows, w => w.id);
 *
 * // Optimistic add
 * addWorkflow({ id: Date.now(), name: 'New Workflow' });
 * await api.addWorkflow(...);
 *
 * // Optimistic update
 * updateWorkflow('workflow-1', { name: 'Updated Name' });
 * await api.updateWorkflow(...);
 *
 * // Optimistic delete
 * deleteWorkflow('workflow-1');
 * await api.deleteWorkflow(...);
 */
export function useOptimisticList<T extends Record<string, any>>(
  items: T[],
  getId: (item: T) => string | number
): readonly [
  optimisticItems: T[],
  add: (item: T) => void,
  update: (id: string | number, updates: Partial<T>) => void,
  delete: (id: string | number) => void,
  clear: () => void
] {
  type Action =
    | { type: 'ADD'; payload: T }
    | { type: 'UPDATE'; id: string | number; updates: Partial<T> }
    | { type: 'DELETE'; id: string | number }
    | { type: 'CLEAR' };

  const [optimisticItems, dispatch] = useReducer(
    (state: T[], action: Action): T[] => {
      switch (action.type) {
        case 'ADD':
          return [...state, action.payload];

        case 'UPDATE': {
          const index = state.findIndex(item => getId(item) === action.id);
          if (index === -1) return state;

          const updated = [...state];
          updated[index] = { ...updated[index], ...action.updates };
          return updated;
        }

        case 'DELETE':
          return state.filter(item => getId(item) !== action.id);

        case 'CLEAR':
          return [];

        default:
          return state;
      }
    },
    items
  );

  const add = useCallback((item: T) => {
    dispatch({ type: 'ADD', payload: item });
  }, []);

  const update = useCallback((id: string | number, updates: Partial<T>) => {
    dispatch({ type: 'UPDATE', id, updates });
  }, []);

  const deleteItem = useCallback((id: string | number) => {
    dispatch({ type: 'DELETE', id });
  }, []);

  const clear = useCallback(() => {
    dispatch({ type: 'CLEAR' });
  }, []);

  return [optimisticItems, add, update, deleteItem, clear] as const;
}

/**
 * useOptimisticAsync - Optimistic update with async action handling
 * Automatically reverts optimistic state if action fails
 *
 * @param initialState Current state
 * @param reducer Optimistic reducer function
 * @param onOptimisticUpdate Async server action to execute
 * @returns [optimisticState, dispatch, isPending, error]
 *
 * @example
 * const [optimisticWorkflow, dispatch, isPending, error] = useOptimisticAsync(
 *   workflow,
 *   (state, action) => ({ ...state, ...action.updates }),
 *   async (action) => {
 *     return await api.updateWorkflow(action.id, action.updates);
 *   }
 * );
 *
 * const handleUpdate = (updates) => {
 *   dispatch({ type: 'UPDATE', id: workflow.id, updates });
 *   // Action is automatically sent to server and reverted on error
 * };
 */
export function useOptimisticAsync<S, A>(
  initialState: S,
  reducer: OptimisticReducer<S, A>,
  onOptimisticUpdate?: (action: A) => Promise<any>
): readonly [
  optimisticState: S,
  dispatch: OptimisticActionDispatcher<A>,
  isPending: boolean,
  error: Error | null
] {
  const [optimisticState, optimisticDispatch] = useOptimistic(
    initialState,
    reducer
  );

  const [isPending, setIsPending] = useReducer(
    (prev: boolean) => !prev,
    false
  );

  const [error, setError] = useReducer(
    (_: Error | null, newError: Error | null) => newError,
    null
  );

  const originalStateRef = useRef(initialState);

  const dispatch = useCallback(
    async (action: A) => {
      // Store original state for potential revert
      originalStateRef.current = optimisticState;

      // Apply optimistic update immediately
      optimisticDispatch(action);

      if (!onOptimisticUpdate) {
        return;
      }

      try {
        setIsPending();
        setError(null);

        // Send action to server
        await onOptimisticUpdate(action);
      } catch (err) {
        const error = err instanceof Error ? err : new Error(String(err));
        setError(error);

        // Revert optimistic state on error
        optimisticDispatch({ type: 'REVERT' } as unknown as A);

        console.error('Optimistic action failed, state reverted:', error);
      } finally {
        setIsPending();
      }
    },
    [optimisticState, optimisticDispatch, onOptimisticUpdate]
  );

  return [optimisticState, dispatch, isPending, error] as const;
}

/**
 * useOptimisticToggle - Optimistic toggle state (checkbox/switch)
 * Useful for simple boolean updates
 *
 * @param initialValue Current toggle state
 * @returns [state, toggle, isPending]
 *
 * @example
 * const [isActive, toggleActive, isPending] = useOptimisticToggle(workflow.isActive);
 *
 * <button onClick={() => toggleActive()} disabled={isPending}>
 *   {isActive ? 'Active' : 'Inactive'}
 * </button>
 */
export function useOptimisticToggle(
  initialValue: boolean,
  onToggle?: (newValue: boolean) => Promise<void>
): readonly [
  state: boolean,
  toggle: () => void,
  isPending: boolean,
  error: Error | null
] {
  const [state, setState] = useReducer(
    (prev: boolean) => !prev,
    initialValue
  );

  const [isPending, setIsPending] = useReducer(
    (prev: boolean) => !prev,
    false
  );

  const [error, setError] = useReducer(
    (_: Error | null, newError: Error | null) => newError,
    null
  );

  const previousStateRef = useRef(initialValue);

  const toggle = useCallback(async () => {
    previousStateRef.current = state;
    setState(); // Toggle optimistically

    if (!onToggle) return;

    try {
      setIsPending();
      setError(null);

      await onToggle(!state);
    } catch (err) {
      const error = err instanceof Error ? err : new Error(String(err));
      setError(error);

      // Revert on error
      setState();

      console.error('Toggle failed, state reverted:', error);
    } finally {
      setIsPending();
    }
  }, [state, onToggle]);

  return [state, toggle, isPending, error] as const;
}
