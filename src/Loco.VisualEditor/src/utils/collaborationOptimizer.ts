/**
 * Collaboration Optimization Utilities
 *
 * Performance optimizations for real-time collaboration including:
 * - Event debouncing and throttling
 * - Batch processing for bulk operations
 * - Memory management
 * - Network efficiency
 */

// ============================================================================
// Debounce & Throttle
// ============================================================================

/**
 * Debounce function calls
 */
export function debounce<T extends (...args: any[]) => any>(
  func: T,
  wait: number
): (...args: Parameters<T>) => void {
  let timeout: number | null = null;

  return function (...args: Parameters<T>) {
    if (timeout) clearTimeout(timeout);
    timeout = setTimeout(() => {
      func(...args);
      timeout = null;
    }, wait) as unknown as number;
  };
}

/**
 * Throttle function calls
 */
export function throttle<T extends (...args: any[]) => any>(
  func: T,
  limit: number
): (...args: Parameters<T>) => void {
  let inThrottle: boolean = false;

  return function (...args: Parameters<T>) {
    if (!inThrottle) {
      func(...args);
      inThrottle = true;
      setTimeout(() => {
        inThrottle = false;
      }, limit);
    }
  };
}

// ============================================================================
// Batch Operations
// ============================================================================

/**
 * Batch queue for collecting multiple operations
 */
export class BatchQueue<T> {
  private queue: T[] = [];
  private timer: number | null = null;
  private readonly maxSize: number;
  private readonly maxDelay: number;
  private readonly callback: (items: T[]) => void;

  constructor(
    callback: (items: T[]) => void,
    maxSize: number = 10,
    maxDelay: number = 100
  ) {
    this.callback = callback;
    this.maxSize = maxSize;
    this.maxDelay = maxDelay;
  }

  /**
   * Add item to batch queue
   */
  add(item: T): void {
    this.queue.push(item);

    // Flush if max size reached
    if (this.queue.length >= this.maxSize) {
      this.flush();
    } else if (!this.timer) {
      // Start timer for max delay
      this.timer = setTimeout(() => this.flush(), this.maxDelay);
    }
  }

  /**
   * Flush queued items
   */
  flush(): void {
    if (this.queue.length === 0) return;

    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = null;
    }

    const items = this.queue;
    this.queue = [];
    this.callback(items);
  }

  /**
   * Clear queue
   */
  clear(): void {
    this.queue = [];
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }

  /**
   * Get queue size
   */
  size(): number {
    return this.queue.length;
  }
}

// ============================================================================
// Memory Management
// ============================================================================

/**
 * LRU Cache for storing collaboration state
 */
export class LRUCache<K, V> {
  private readonly maxSize: number;
  private cache: Map<K, V> = new Map();

  constructor(maxSize: number = 100) {
    this.maxSize = maxSize;
  }

  /**
   * Get value from cache
   */
  get(key: K): V | undefined {
    const value = this.cache.get(key);
    if (value !== undefined) {
      // Move to end (most recently used)
      this.cache.delete(key);
      this.cache.set(key, value);
    }
    return value;
  }

  /**
   * Set value in cache
   */
  set(key: K, value: V): void {
    // Remove if already exists to re-add at end
    if (this.cache.has(key)) {
      this.cache.delete(key);
    }

    this.cache.set(key, value);

    // Remove oldest item if cache is full
    if (this.cache.size > this.maxSize) {
      const oldestKey = this.cache.keys().next().value as K;
      if (oldestKey !== undefined) {
        this.cache.delete(oldestKey);
      }
    }
  }

  /**
   * Check if key exists
   */
  has(key: K): boolean {
    return this.cache.has(key);
  }

  /**
   * Delete key
   */
  delete(key: K): boolean {
    return this.cache.delete(key);
  }

  /**
   * Clear cache
   */
  clear(): void {
    this.cache.clear();
  }

  /**
   * Get cache size
   */
  size(): number {
    return this.cache.size;
  }
}

// ============================================================================
// Conflict Resolution
// ============================================================================

/**
 * Simple conflict resolution using Last-Write-Wins (LWW)
 */
export interface VersionedChange {
  id: string;
  timestamp: number;
  userId: string;
  data: any;
}

/**
 * Resolve conflicts using timestamp and user ID
 */
export function resolveConflict(
  local: VersionedChange,
  remote: VersionedChange
): VersionedChange {
  // Newer timestamp wins
  if (local.timestamp !== remote.timestamp) {
    return local.timestamp > remote.timestamp ? local : remote;
  }

  // If same timestamp, use user ID (alphabetical order for determinism)
  return local.userId > remote.userId ? local : remote;
}

/**
 * Check if two changes conflict
 */
export function hasConflict(
  change1: VersionedChange,
  change2: VersionedChange
): boolean {
  // Changes to same entity conflict
  return change1.id === change2.id && change1.userId !== change2.userId;
}

// ============================================================================
// Network Efficiency
// ============================================================================

/**
 * Compress data for network transmission (basic implementation)
 */
export function compressData(data: any): string {
  // In production, use more sophisticated compression like lz-string
  return JSON.stringify(data);
}

/**
 * Decompress received data
 */
export function decompressData(compressed: string): any {
  return JSON.parse(compressed);
}

/**
 * Calculate data size in bytes
 */
export function getDataSize(data: any): number {
  return new Blob([JSON.stringify(data)]).size;
}

// ============================================================================
// Rate Limiting
// ============================================================================

/**
 * Token bucket algorithm for rate limiting
 */
export class RateLimiter {
  private tokens: number;
  private readonly capacity: number;
  private readonly refillRate: number; // tokens per second
  private lastRefill: number = Date.now();

  constructor(capacity: number, refillRate: number) {
    this.capacity = capacity;
    this.refillRate = refillRate;
    this.tokens = capacity;
  }

  /**
   * Refill tokens based on elapsed time
   */
  private refill(): void {
    const now = Date.now();
    const elapsed = (now - this.lastRefill) / 1000;
    const tokensToAdd = elapsed * this.refillRate;
    this.tokens = Math.min(this.capacity, this.tokens + tokensToAdd);
    this.lastRefill = now;
  }

  /**
   * Try to consume tokens
   */
  tryConsume(tokens: number = 1): boolean {
    this.refill();

    if (this.tokens >= tokens) {
      this.tokens -= tokens;
      return true;
    }

    return false;
  }

  /**
   * Get current token count
   */
  getTokens(): number {
    this.refill();
    return this.tokens;
  }
}

// ============================================================================
// Monitoring & Analytics
// ============================================================================

/**
 * Collaboration performance metrics
 */
export interface CollaborationMetrics {
  eventsSent: number;
  eventsReceived: number;
  latency: number[]; // milliseconds
  dataTransferred: number; // bytes
  errors: number;
  reconnects: number;
}

/**
 * Collect and track collaboration metrics
 */
export class MetricsCollector {
  private metrics: CollaborationMetrics = {
    eventsSent: 0,
    eventsReceived: 0,
    latency: [],
    dataTransferred: 0,
    errors: 0,
    reconnects: 0,
  };

  private readonly maxLatencyHistory = 100;

  /**
   * Record event sent
   */
  recordEventSent(dataSize: number): void {
    this.metrics.eventsSent++;
    this.metrics.dataTransferred += dataSize;
  }

  /**
   * Record event received
   */
  recordEventReceived(latency: number): void {
    this.metrics.eventsReceived++;
    this.metrics.latency.push(latency);

    // Keep only recent latency measurements
    if (this.metrics.latency.length > this.maxLatencyHistory) {
      this.metrics.latency.shift();
    }
  }

  /**
   * Record error
   */
  recordError(): void {
    this.metrics.errors++;
  }

  /**
   * Record reconnection
   */
  recordReconnect(): void {
    this.metrics.reconnects++;
  }

  /**
   * Get current metrics
   */
  getMetrics(): CollaborationMetrics {
    return {
      ...this.metrics,
      latency: [...this.metrics.latency],
    };
  }

  /**
   * Get average latency
   */
  getAverageLatency(): number {
    if (this.metrics.latency.length === 0) return 0;
    const sum = this.metrics.latency.reduce((a, b) => a + b, 0);
    return sum / this.metrics.latency.length;
  }

  /**
   * Get max latency
   */
  getMaxLatency(): number {
    return Math.max(...this.metrics.latency, 0);
  }

  /**
   * Reset metrics
   */
  reset(): void {
    this.metrics = {
      eventsSent: 0,
      eventsReceived: 0,
      latency: [],
      dataTransferred: 0,
      errors: 0,
      reconnects: 0,
    };
  }
}