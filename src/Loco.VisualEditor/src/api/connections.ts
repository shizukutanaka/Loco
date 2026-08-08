/**
 * Connection (credential) API Methods
 *
 * A "connection" is a stored set of credentials for one connector - the thing
 * that makes a Slack/Stripe/GitHub node able to act as *you*.
 *
 * Two rules shape this module, and both are load-bearing:
 *
 * 1. Secret values travel in ONE direction only: client -> server. Every
 *    response type here is metadata (see `Connection`); none of them carries a
 *    secret. OWASP's Secrets Management guidance is that secrets must never be
 *    logged or transmitted in the clear, and a response body is the easiest
 *    place for them to leak into logs, error reporters, and browser history.
 *    Editing a connection therefore re-submits the secret rather than reading
 *    the old one back.
 *
 * 2. Workflows reference a connection by ID, never by embedding the secret.
 *    This mirrors n8n's model (credentials are separate entities, injected at
 *    execution time) and means an exported workflow JSON is safe to share.
 *
 * The server side of this contract is Opus task O-6 - see
 * docs/agent-instructions/INSTRUCTIONS_OPUS.md.
 */

import { apiClient } from './client';
import { ApiResponse, PaginationParams } from './types';

// ============================================================================
// Types
// ============================================================================

/**
 * A stored connection as the server reports it.
 *
 * Deliberately has no field for secret values. `configuredFields` lists WHICH
 * credential fields were supplied (e.g. ["apiKey"]) so the UI can show
 * completeness, without revealing any value.
 */
export interface Connection {
  id: string;
  /** Connector this belongs to, e.g. "slack". */
  connectorId: string;
  /** User-facing label, e.g. "Acme workspace". */
  name: string;
  /** Names of the credential fields that have been set - never their values. */
  configuredFields: string[];
  createdAt: string;
  updatedAt?: string;
  lastUsedAt?: string;
}

/** Request body for creating a connection. Secrets are write-only. */
export interface ConnectionCreateRequest {
  connectorId: string;
  name: string;
  /** Credential field name -> secret value. Sent, never returned. */
  secrets: Record<string, string>;
}

/**
 * Request body for updating a connection.
 *
 * `secrets` is optional: omit it to rename without resubmitting credentials.
 * When present it REPLACES the stored set, because a partial merge would make
 * "which fields are actually set" ambiguous.
 */
export interface ConnectionUpdateRequest {
  name?: string;
  secrets?: Record<string, string>;
}

export interface ConnectionListResponse {
  connections: Connection[];
  total: number;
  page: number;
  pageSize: number;
}

/** Result of exercising a connection against its real service. */
export interface ConnectionTestResult {
  success: boolean;
  message: string;
  /** Round-trip time in milliseconds, when the server reports it. */
  responseTimeMs?: number;
}

// ============================================================================
// CRUD
// ============================================================================

/** List connections. Responses contain metadata only. */
export async function listConnections(
  params?: PaginationParams & { connectorId?: string }
): Promise<ApiResponse<ConnectionListResponse>> {
  const queryParams = new URLSearchParams();

  if (params?.page) queryParams.append('page', params.page.toString());
  if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
  if (params?.connectorId) queryParams.append('connectorId', params.connectorId);

  const url = `/connections${queryParams.toString() ? `?${queryParams}` : ''}`;
  return apiClient.get<ConnectionListResponse>(url);
}

/** Get one connection's metadata. Never returns secret values. */
export async function getConnection(connectionId: string): Promise<ApiResponse<Connection>> {
  return apiClient.get<Connection>(`/connections/${connectionId}`);
}

export async function createConnection(
  request: ConnectionCreateRequest
): Promise<ApiResponse<Connection>> {
  return apiClient.post<Connection>('/connections', request);
}

export async function updateConnection(
  connectionId: string,
  request: ConnectionUpdateRequest
): Promise<ApiResponse<Connection>> {
  return apiClient.put<Connection>(`/connections/${connectionId}`, request);
}

export async function deleteConnection(connectionId: string): Promise<ApiResponse<void>> {
  return apiClient.delete<void>(`/connections/${connectionId}`);
}

/**
 * Verify a stored connection actually works, by connector-specific probe.
 * Runs server-side so the secret is never sent back to the browser.
 */
export async function testConnection(
  connectionId: string
): Promise<ApiResponse<ConnectionTestResult>> {
  return apiClient.post<ConnectionTestResult>(`/connections/${connectionId}/test`, {});
}

// ============================================================================
// Helpers
// ============================================================================

/**
 * Which required fields a connection is still missing.
 *
 * Since values never leave the server, completeness is derived from
 * `configuredFields`. Used to flag a connection as incomplete in the UI
 * rather than letting a workflow fail at execution time.
 */
export function getMissingFields(
  connection: Connection,
  requiredFields: string[]
): string[] {
  const configured = new Set(connection.configuredFields);
  return requiredFields.filter((field) => !configured.has(field));
}

/** True when every required credential field has been supplied. */
export function isConnectionComplete(
  connection: Connection,
  requiredFields: string[]
): boolean {
  return getMissingFields(connection, requiredFields).length === 0;
}
