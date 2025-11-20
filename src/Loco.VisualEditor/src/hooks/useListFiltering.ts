import { useState, useCallback, useMemo } from 'react';

export type FilterPredicate<T> = (item: T, filterValue: unknown) => boolean;
export type SearchPredicate<T> = (item: T, query: string) => boolean;

interface UseListFilteringOptions<T> {
  items: T[];
  searchPredicate?: SearchPredicate<T>;
  defaultFilters?: Record<string, unknown>;
}

interface UseListFilteringReturn<T> {
  filteredItems: T[];
  searchQuery: string;
  setSearchQuery: (query: string) => void;
  filters: Record<string, unknown>;
  setFilter: (key: string, value: unknown) => void;
  clearFilter: (key: string) => void;
  clearFilters: () => void;
  addFilter: (key: string, predicate: FilterPredicate<T>) => void;
  removeFilter: (key: string) => void;
  getFilteredCount: () => number;
}

export function useListFiltering<T>({
  items,
  searchPredicate,
  defaultFilters = {},
}: UseListFilteringOptions<T>): UseListFilteringReturn<T> {
  const [searchQuery, setSearchQuery] = useState('');
  const [filters, setFilters] = useState<Record<string, unknown>>(defaultFilters);
  const [filterPredicates, setFilterPredicates] = useState<Record<string, FilterPredicate<T>>>({});

  const setFilter = useCallback((key: string, value: unknown) => {
    setFilters((prev) => ({
      ...prev,
      [key]: value,
    }));
  }, []);

  const clearFilter = useCallback((key: string) => {
    setFilters((prev) => {
      const newFilters = { ...prev };
      delete newFilters[key];
      return newFilters;
    });
  }, []);

  const clearFilters = useCallback(() => {
    setFilters({});
  }, []);

  const addFilter = useCallback((key: string, predicate: FilterPredicate<T>) => {
    setFilterPredicates((prev) => ({
      ...prev,
      [key]: predicate,
    }));
  }, []);

  const removeFilter = useCallback((key: string) => {
    setFilterPredicates((prev) => {
      const newPredicates = { ...prev };
      delete newPredicates[key];
      return newPredicates;
    });
  }, []);

  const filteredItems = useMemo(() => {
    let result = items;

    // Apply search filter
    if (searchQuery && searchPredicate) {
      result = result.filter((item) => searchPredicate(item, searchQuery));
    }

    // Apply custom filter predicates
    Object.entries(filterPredicates).forEach(([_key, predicate]) => {
      result = result.filter((item) => predicate(item, filters[_key]));
    });

    return result;
  }, [items, searchQuery, searchPredicate, filters, filterPredicates]);

  const getFilteredCount = useCallback(() => {
    return filteredItems.length;
  }, [filteredItems]);

  return {
    filteredItems,
    searchQuery,
    setSearchQuery,
    filters,
    setFilter,
    clearFilter,
    clearFilters,
    addFilter,
    removeFilter,
    getFilteredCount,
  };
}
