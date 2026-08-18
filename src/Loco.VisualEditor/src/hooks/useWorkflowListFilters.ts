import { useState, useEffect, useRef, useMemo } from 'react';
import { debounce } from '@/utils/debounceThrottle';
import { WorkflowListItem } from './useWorkflowListData';

/**
 * Custom hook for managing workflow list filtering and search
 * Handles: search with debounce, status filter, tag filter, and filtering logic
 * Note: Sorting is managed separately by the component to avoid circular dependency
 */
export function useWorkflowListFilters(workflows: WorkflowListItem[]) {
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearchQuery, setDebouncedSearchQuery] = useState('');
  const [filterStatus, setFilterStatus] = useState<'all' | 'completed' | 'failed' | 'running'>('all');
  const [filterTag, setFilterTag] = useState<string>('all');

  // Debounce search query to avoid excessive filtering
  const debouncedSearchRef = useRef(
    debounce((query: string) => {
      setDebouncedSearchQuery(query);
    }, 300)
  );

  // Update debounced search on query change
  useEffect(() => {
    // Capture the debounced function now: cleanup runs later, and reading
    // .current then could cancel a different instance than the one started here.
    const debouncedSearch = debouncedSearchRef.current;
    debouncedSearch(searchQuery);
    return () => {
      debouncedSearch.cancel();
    };
  }, [searchQuery]);

  // Filter workflows based on search, status, and tag - memoized for performance
  const filteredWorkflows = useMemo(() => {
    let filtered = [...workflows];

    // Search filter - using debounced query to avoid excessive filtering
    if (debouncedSearchQuery) {
      filtered = filtered.filter(
        (w) =>
          w.name.toLowerCase().includes(debouncedSearchQuery.toLowerCase()) ||
          w.description?.toLowerCase().includes(debouncedSearchQuery.toLowerCase()) ||
          w.tags?.some((tag) => tag.toLowerCase().includes(debouncedSearchQuery.toLowerCase()))
      );
    }

    // Status filter
    if (filterStatus !== 'all') {
      filtered = filtered.filter((w) => w.lastExecutionStatus === filterStatus);
    }

    // Tag filter
    if (filterTag !== 'all') {
      filtered = filtered.filter((w) => w.tags?.includes(filterTag));
    }

    return filtered;
  }, [workflows, debouncedSearchQuery, filterStatus, filterTag]);

  return {
    searchQuery,
    setSearchQuery,
    filterStatus,
    setFilterStatus,
    filterTag,
    setFilterTag,
    filteredWorkflows,
  };
}
