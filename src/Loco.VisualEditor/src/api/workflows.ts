/**
 * Workflow API Methods
 *
 * Provides high-level API methods for workflow CRUD operations
 * and execution. Uses the LocoApiClient for HTTP communication.
 */

import { apiClient } from './client';
import {
  ApiResponse,
  WorkflowListResponse,
  WorkflowCreateRequest,
  WorkflowUpdateRequest,
  WorkflowExecutionRequest,
  WorkflowExecutionResponse,
  PaginationParams,
} from './types';
import { Workflow } from '@/types/workflow';

// ============================================================================
// Workflow CRUD Operations
// ============================================================================

/**
 * List all workflows with optional pagination
 */
export async function listWorkflows(
  params?: PaginationParams
): Promise<ApiResponse<WorkflowListResponse>> {
  const queryParams = new URLSearchParams();

  if (params?.page) queryParams.append('page', params.page.toString());
  if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
  if (params?.sortBy) queryParams.append('sortBy', params.sortBy);
  if (params?.sortOrder) queryParams.append('sortOrder', params.sortOrder);

  const url = `/workflows${queryParams.toString() ? `?${queryParams}` : ''}`;
  return apiClient.get<WorkflowListResponse>(url);
}

/**
 * Get a single workflow by ID
 */
export async function getWorkflow(workflowId: string): Promise<ApiResponse<Workflow>> {
  return apiClient.get<Workflow>(`/workflows/${workflowId}`);
}

/**
 * Create a new workflow
 */
export async function createWorkflow(
  request: WorkflowCreateRequest
): Promise<ApiResponse<Workflow>> {
  return apiClient.post<Workflow>('/workflows', request);
}

/**
 * Update an existing workflow
 */
export async function updateWorkflow(
  workflowId: string,
  request: Partial<WorkflowUpdateRequest>
): Promise<ApiResponse<Workflow>> {
  return apiClient.put<Workflow>(`/workflows/${workflowId}`, request);
}

/**
 * Delete a workflow
 */
export async function deleteWorkflow(
  workflowId: string
): Promise<ApiResponse<void>> {
  return apiClient.delete<void>(`/workflows/${workflowId}`);
}

// ============================================================================
// Workflow Execution
// ============================================================================

/**
 * Execute a workflow
 */
export async function executeWorkflow(
  request: WorkflowExecutionRequest
): Promise<ApiResponse<WorkflowExecutionResponse>> {
  const { workflowId, ...body } = request;
  return apiClient.post<WorkflowExecutionResponse>(
    `/workflows/${workflowId}/execute`,
    body
  );
}

/**
 * Get execution status by ID
 */
export async function getExecutionStatus(
  executionId: string
): Promise<ApiResponse<WorkflowExecutionResponse>> {
  return apiClient.get<WorkflowExecutionResponse>(`/executions/${executionId}`);
}

/**
 * Cancel a running execution
 */
export async function cancelExecution(
  executionId: string
): Promise<ApiResponse<void>> {
  return apiClient.post<void>(`/executions/${executionId}/cancel`);
}

// ============================================================================
// Workflow Validation
// ============================================================================

/**
 * Validate a workflow without executing it
 */
export async function validateWorkflow(
  workflow: Workflow
): Promise<ApiResponse<{ valid: boolean; errors: string[] }>> {
  return apiClient.post<{ valid: boolean; errors: string[] }>(
    '/workflows/validate',
    workflow
  );
}

// ============================================================================
// Utility Functions
// ============================================================================

/**
 * Convert local workflow to API request format
 */
export function workflowToCreateRequest(workflow: Workflow): WorkflowCreateRequest {
  return {
    name: workflow.name,
    description: workflow.description,
    nodes: workflow.nodes,
    edges: workflow.edges,
    metadata: workflow.metadata,
  };
}

/**
 * Convert local workflow to API update format
 */
export function workflowToUpdateRequest(
  workflow: Workflow
): WorkflowUpdateRequest {
  return {
    id: workflow.id,
    name: workflow.name,
    description: workflow.description,
    nodes: workflow.nodes,
    edges: workflow.edges,
    metadata: workflow.metadata,
  };
}
