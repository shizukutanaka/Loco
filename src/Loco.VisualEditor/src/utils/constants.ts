/**
 * Global Constants
 *
 * Centralized location for all hardcoded values used throughout the application.
 */

// ============================================================================
// UI Timing Constants
// ============================================================================

/**
 * Duration for copy feedback animation (ms)
 * Used when user copies content to clipboard
 */
export const COPY_FEEDBACK_DURATION = 2000;

/**
 * Execution polling interval (ms)
 * How frequently to check execution status when workflow is running
 */
export const EXECUTION_POLLING_INTERVAL = 2000;

/**
 * Toast notification duration for long messages (ms)
 */
export const TOAST_LONG_DURATION = 7000;

// ============================================================================
// Data Limits & Thresholds
// ============================================================================

/**
 * Maximum number of items to keep in workflow history
 */
export const MAX_HISTORY_SIZE = 50;

/**
 * Analysis cache TTL (ms)
 * How long to cache workflow analysis results
 */
export const ANALYSIS_CACHE_TTL = 5 * 60 * 1000; // 5 minutes

/**
 * Maximum nodes to display in performance metrics
 */
export const MAX_NODES_DISPLAY = 100;

// ============================================================================
// Performance Monitoring
// ============================================================================

/**
 * FPS warning threshold
 * Below this FPS, show performance warning
 */
export const FPS_WARNING_THRESHOLD = 30;

/**
 * Memory warning threshold (MB)
 * Above this, show memory warning
 */
export const MEMORY_WARNING_THRESHOLD = 100;

/**
 * Latency warning threshold (ms)
 * Above this, show latency warning
 */
export const LATENCY_WARNING_THRESHOLD = 500;

// ============================================================================
// WebSocket & Collaboration
// ============================================================================

/**
 * WebSocket reconnection delay (ms)
 */
export const WEBSOCKET_RECONNECT_DELAY = 3000;

/**
 * Maximum WebSocket reconnection attempts
 */
export const WEBSOCKET_MAX_RETRIES = 5;

/**
 * Collaboration cursor update throttle (ms)
 */
export const COLLABORATION_CURSOR_THROTTLE = 100;

/**
 * Collaboration presence ping interval (ms)
 */
export const COLLABORATION_PRESENCE_PING = 30000; // 30 seconds

// ============================================================================
// UI Breakpoints
// ============================================================================

/**
 * Responsive design breakpoints (px)
 */
export const BREAKPOINTS = {
  sm: 640,
  md: 768,
  lg: 1024,
  xl: 1280,
  '2xl': 1536,
} as const;

// ============================================================================
// Default Values
// ============================================================================

/**
 * Default workflow node count for new workflows
 */
export const DEFAULT_WORKFLOW_NODES = 0;

/**
 * Default workflow edge count for new workflows
 */
export const DEFAULT_WORKFLOW_EDGES = 0;

/**
 * Default zoom level for canvas (1.0 = 100%)
 */
export const DEFAULT_CANVAS_ZOOM = 1.0;

/**
 * Default canvas pan offset
 */
export const DEFAULT_CANVAS_PAN = { x: 0, y: 0 };

// ============================================================================
// API Configuration
// ============================================================================

/**
 * API request timeout (ms)
 */
export const API_REQUEST_TIMEOUT = 30000; // 30 seconds

/**
 * API retry delay (ms)
 */
export const API_RETRY_DELAY = 1000;

/**
 * Maximum API retries
 */
export const API_MAX_RETRIES = 3;
