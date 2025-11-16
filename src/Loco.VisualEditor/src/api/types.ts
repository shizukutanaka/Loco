/**
 * API Types for Loco Backend Integration
 *
 * Defines TypeScript types for API requests and responses
 * to ensure type safety when communicating with the backend.
 */

import { Workflow } from '@/types/workflow';

// ============================================================================
// API Response Types
// ============================================================================

/**
 * Discriminated union for API responses.
 * Ensures type safety: when success is true, data is guaranteed to exist.
 * When success is false, error is guaranteed to exist.
 */
export type ApiResponse<T> =
  | { success: true; data: T; message?: string }
  | { success: false; error: ApiError; message?: string };

export interface ApiError {
  code: string;
  message: string;
  details?: Record<string, unknown>;
}

// ============================================================================
// Workflow API Types
// ============================================================================

export interface WorkflowListResponse {
  workflows: Workflow[];
  total: number;
  page: number;
  pageSize: number;
}

export interface WorkflowCreateRequest {
  name: string;
  description?: string;
  nodes: Workflow['nodes'];
  edges: Workflow['edges'];
  metadata?: Workflow['metadata'];
}

export interface WorkflowUpdateRequest {
  id: string;
  name?: string;
  description?: string;
  nodes?: Workflow['nodes'];
  edges?: Workflow['edges'];
  metadata?: Workflow['metadata'];
}

export interface WorkflowExecutionRequest {
  workflowId: string;
  input?: Record<string, unknown>;
  dryRun?: boolean;
}

/**
 * Execution status discriminator type
 */
export type ExecutionStatus =
  | 'pending'
  | 'running'
  | 'completed'
  | 'failed'
  | 'cancelled';

/**
 * Discriminated union for workflow execution responses.
 * The required fields depend on the execution status:
 * - pending/running: only startedAt is available
 * - completed: includes output, completedAt is required
 * - failed/cancelled: includes error, completedAt is required
 */
export type WorkflowExecutionResponse =
  | {
      executionId: string;
      status: 'pending' | 'running';
      startedAt: string;
      logs?: ExecutionLog[];
    }
  | {
      executionId: string;
      status: 'completed';
      startedAt: string;
      completedAt: string;
      output: Record<string, unknown>;
      logs?: ExecutionLog[];
    }
  | {
      executionId: string;
      status: 'failed' | 'cancelled';
      startedAt: string;
      completedAt: string;
      error: ExecutionError;
      logs?: ExecutionLog[];
    };

export interface ExecutionError {
  nodeId: string;
  message: string;
  stack?: string;
}

export interface ExecutionLog {
  timestamp: string;
  level: 'info' | 'warn' | 'error' | 'debug';
  message: string;
  nodeId?: string;
}

// ============================================================================
// Authentication Types
// ============================================================================

export interface AuthConfig {
  apiKey?: string;
  token?: string;
}

// ============================================================================
// Pagination Types
// ============================================================================

export interface PaginationParams {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
}
