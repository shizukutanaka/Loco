import { describe, it, expect } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useListFiltering } from './useListFiltering';

interface Item {
  id: string;
  name: string;
  category: string;
}

const items: Item[] = [
  { id: '1', name: 'Apple', category: 'fruit' },
  { id: '2', name: 'Banana', category: 'fruit' },
  { id: '3', name: 'Carrot', category: 'veg' },
];

describe('useListFiltering', () => {
  it('returns all items when there is no query or filter', () => {
    const { result } = renderHook(() => useListFiltering({ items }));
    expect(result.current.filteredItems).toHaveLength(3);
    expect(result.current.getFilteredCount()).toBe(3);
  });

  it('applies the search predicate only when a query is set', () => {
    const { result } = renderHook(() =>
      useListFiltering({
        items,
        searchPredicate: (item, q) => item.name.toLowerCase().includes(q.toLowerCase()),
      })
    );

    act(() => result.current.setSearchQuery('an')); // Banana
    expect(result.current.filteredItems.map((i) => i.name)).toEqual(['Banana']);

    act(() => result.current.setSearchQuery(''));
    expect(result.current.filteredItems).toHaveLength(3);
  });

  it('applies a registered custom filter predicate against its filter value', () => {
    const { result } = renderHook(() => useListFiltering({ items }));

    act(() => {
      result.current.addFilter('category', (item: Item, value) => item.category === value);
      result.current.setFilter('category', 'fruit');
    });

    expect(result.current.filteredItems.map((i) => i.name)).toEqual(['Apple', 'Banana']);
  });

  it('removeFilter drops the predicate so items are no longer filtered by it', () => {
    const { result } = renderHook(() => useListFiltering({ items }));

    act(() => {
      result.current.addFilter('category', (item: Item, value) => item.category === value);
      result.current.setFilter('category', 'veg');
    });
    expect(result.current.filteredItems).toHaveLength(1);

    act(() => result.current.removeFilter('category'));
    expect(result.current.filteredItems).toHaveLength(3);
  });

  it('clearFilter removes a single filter value and clearFilters removes all', () => {
    const { result } = renderHook(() =>
      useListFiltering({ items, defaultFilters: { a: 1, b: 2 } })
    );
    expect(result.current.filters).toEqual({ a: 1, b: 2 });

    act(() => result.current.clearFilter('a'));
    expect(result.current.filters).toEqual({ b: 2 });

    act(() => result.current.clearFilters());
    expect(result.current.filters).toEqual({});
  });

  it('combines search and custom filters in a single pass', () => {
    const { result } = renderHook(() =>
      useListFiltering({
        items,
        searchPredicate: (item, q) => item.name.toLowerCase().includes(q.toLowerCase()),
      })
    );

    act(() => {
      result.current.addFilter('category', (item: Item, value) => item.category === value);
      result.current.setFilter('category', 'fruit');
      result.current.setSearchQuery('a');
    });

    // fruit AND name contains 'a' -> Apple, Banana
    expect(result.current.filteredItems.map((i) => i.name).sort()).toEqual(['Apple', 'Banana']);
  });
});
