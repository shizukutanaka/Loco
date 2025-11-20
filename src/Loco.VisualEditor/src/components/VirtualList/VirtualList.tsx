import { ReactNode, useRef, useCallback, memo, FC } from 'react';
import { useVirtualScroll } from '@/hooks/useVirtualScroll';

export interface VirtualListProps<T = unknown> {
  items: T[];
  itemHeight: number;
  containerHeight: number;
  renderItem: (item: T, index: number) => ReactNode;
  className?: string;
  overscan?: number;
  keyExtractor?: (item: T, index: number) => string | number;
  emptyMessage?: ReactNode;
  testId?: string;
}

const VirtualListComponent: FC<VirtualListProps> = ({
  items,
  itemHeight,
  containerHeight,
  renderItem,
  className = '',
  overscan = 3,
  keyExtractor,
  emptyMessage = 'No items to display',
  testId,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);

  const { visibleItems, visibleStartIndex, offsetY, totalHeight, handleScroll } = useVirtualScroll({
    items,
    itemHeight,
    containerHeight,
    overscan,
  });

  const onScroll = useCallback(
    (e: React.UIEvent<HTMLDivElement>) => {
      const target = e.currentTarget;
      handleScroll(target.scrollTop);
    },
    [handleScroll]
  );

  if (items.length === 0) {
    return (
      <div
        className={`flex items-center justify-center ${className}`}
        style={{ height: containerHeight }}
        data-testid={testId}
      >
        {emptyMessage}
      </div>
    );
  }

  return (
    <div
      ref={containerRef}
      className={className}
      style={{ height: containerHeight, overflow: 'auto', position: 'relative' }}
      onScroll={onScroll}
      data-testid={testId}
    >
      <div style={{ height: totalHeight, position: 'relative' }}>
        <div style={{ transform: `translateY(${offsetY}px)` }}>
          {visibleItems.map((item, index) => {
            const actualIndex = visibleStartIndex + index;
            const key = keyExtractor ? keyExtractor(item as unknown, actualIndex) : actualIndex;

            return (
              <div key={key} style={{ height: itemHeight, overflow: 'hidden' }}>
                {renderItem(item as unknown, actualIndex)}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};

export const VirtualList = memo(VirtualListComponent);
VirtualList.displayName = 'VirtualList';
