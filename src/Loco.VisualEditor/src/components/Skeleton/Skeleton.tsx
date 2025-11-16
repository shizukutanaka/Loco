/**
 * Skeleton Component
 *
 * Provides loading placeholder UI while data is being fetched.
 * Uses CSS animation to create a shimmer effect for better visual feedback.
 */

interface SkeletonProps {
  /** Width of the skeleton (CSS value) */
  width?: string | number;
  /** Height of the skeleton (CSS value) */
  height?: string | number;
  /** Border radius (CSS value) */
  borderRadius?: string;
  /** Whether to show shimmer animation */
  animate?: boolean;
  /** CSS class name for custom styling */
  className?: string;
  /** Accessible description for screen readers */
  ariaLabel?: string;
}

/**
 * Basic skeleton loader for content placeholders
 */
export function Skeleton({
  width = '100%',
  height = '20px',
  borderRadius = '0.5rem',
  animate = true,
  className = '',
  ariaLabel = 'Loading content',
}: SkeletonProps) {
  return (
    <div
      className={`bg-gray-200 ${animate ? 'animate-pulse' : ''} ${className}`}
      style={{
        width: typeof width === 'number' ? `${width}px` : width,
        height: typeof height === 'number' ? `${height}px` : height,
        borderRadius,
      }}
      role="status"
      aria-label={ariaLabel}
    />
  );
}

/**
 * Skeleton loader for text lines
 */
export function SkeletonText({
  lines = 1,
  animate = true,
  className = '',
}: {
  lines?: number;
  animate?: boolean;
  className?: string;
}) {
  return (
    <div className={`space-y-2 ${className}`}>
      {Array.from({ length: lines }).map((_, i) => (
        <Skeleton
          key={i}
          width={i === lines - 1 ? '60%' : '100%'}
          height="16px"
          animate={animate}
          ariaLabel={`Loading text line ${i + 1}`}
        />
      ))}
    </div>
  );
}

/**
 * Skeleton loader for card/box content
 */
export function SkeletonCard({
  animate = true,
  className = '',
  lines = 3,
}: {
  animate?: boolean;
  className?: string;
  lines?: number;
}) {
  return (
    <div
      className={`p-4 bg-white rounded-lg border border-gray-200 ${className}`}
      role="status"
      aria-label="Loading card content"
    >
      {/* Card header skeleton */}
      <Skeleton
        width="40%"
        height="24px"
        animate={animate}
        borderRadius="0.375rem"
        className="mb-3"
        ariaLabel="Loading card title"
      />

      {/* Card content skeleton */}
      <SkeletonText
        lines={lines}
        animate={animate}
        className="space-y-2"
      />
    </div>
  );
}

/**
 * Skeleton loader for list items
 */
export function SkeletonList({
  count = 3,
  animate = true,
  className = '',
}: {
  count?: number;
  animate?: boolean;
  className?: string;
}) {
  return (
    <div className={`space-y-3 ${className}`}>
      {Array.from({ length: count }).map((_, i) => (
        <SkeletonCard
          key={i}
          animate={animate}
          lines={2}
          className="h-24"
        />
      ))}
    </div>
  );
}

/**
 * Skeleton loader for node/workflow items on canvas
 */
export function SkeletonNode({
  animate = true,
  className = '',
}: {
  animate?: boolean;
  className?: string;
}) {
  return (
    <div
      className={`p-3 bg-white rounded-lg border border-gray-200 min-w-[180px] ${className}`}
      role="status"
      aria-label="Loading workflow node"
    >
      {/* Icon placeholder */}
      <Skeleton
        width="40px"
        height="40px"
        borderRadius="50%"
        animate={animate}
        className="mb-2"
        ariaLabel="Loading node icon"
      />

      {/* Title and description */}
      <SkeletonText
        lines={2}
        animate={animate}
        className="space-y-1"
      />
    </div>
  );
}
