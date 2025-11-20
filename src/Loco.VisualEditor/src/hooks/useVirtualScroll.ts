import { useState, useCallback, useRef, useEffect, useMemo } from 'react';

interface UseVirtualScrollOptions {
  items: unknown[];
  itemHeight: number;
  containerHeight: number;
  overscan?: number;
}

interface UseVirtualScrollReturn {
  visibleItems: unknown[];
  visibleStartIndex: number;
  visibleEndIndex: number;
  offsetY: number;
  totalHeight: number;
  handleScroll: (scrollTop: number) => void;
}

export function useVirtualScroll({
  items,
  itemHeight,
  containerHeight,
  overscan = 3,
}: UseVirtualScrollOptions): UseVirtualScrollReturn {
  const [scrollTop, setScrollTop] = useState(0);
  const previousItemsRef = useRef(items);

  useEffect(() => {
    if (previousItemsRef.current.length !== items.length) {
      setScrollTop(0);
    }
    previousItemsRef.current = items;
  }, [items]);

  const handleScroll = useCallback((newScrollTop: number) => {
    setScrollTop(Math.max(0, newScrollTop));
  }, []);

  const { visibleStartIndex, visibleEndIndex, offsetY, visibleItems, totalHeight } = useMemo(() => {
    const totalHeight = items.length * itemHeight;

    const startIndex = Math.max(0, Math.floor(scrollTop / itemHeight) - overscan);
    const endIndex = Math.min(
      items.length,
      Math.ceil((scrollTop + containerHeight) / itemHeight) + overscan
    );

    const visibleItems = items.slice(startIndex, endIndex);
    const offsetY = startIndex * itemHeight;

    return {
      visibleStartIndex: startIndex,
      visibleEndIndex: endIndex,
      offsetY,
      visibleItems,
      totalHeight,
    };
  }, [items, itemHeight, containerHeight, scrollTop, overscan]);

  return {
    visibleItems,
    visibleStartIndex,
    visibleEndIndex,
    offsetY,
    totalHeight,
    handleScroll,
  };
}
