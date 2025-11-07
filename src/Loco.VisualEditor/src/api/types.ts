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

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: ApiError;
  message?: string;
}

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

export interface WorkflowExecutionResponse {
  executionId: string;
  status: ExecutionStatus;
  startedAt: string;
  completedAt?: string;
  output?: Record<string, unknown>;
  error?: ExecutionError;
  logs?: ExecutionLog[];
}

export type ExecutionStatus =
  | 'pending'
  | 'running'
  | 'completed'
  | 'failed'
  | 'cancelled';

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
