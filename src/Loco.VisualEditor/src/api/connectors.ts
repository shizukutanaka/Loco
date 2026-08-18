/**
 * Connector catalogue API.
 *
 * A connector declares exactly which credential fields it reads - Slack's
 * `botToken`, Jira's `domain`/`email`/`apiToken`, Postgres' `host`/`port`/... -
 * and reads them by those exact names at execution time. That declaration lives
 * in `IConnector.AuthConfig.RequiredCredentials` and had no route to the
 * browser, so the connections form asked the user to type the names from memory
 * under the warning "must match the connector's field name".
 *
 * A typo there was undetectable: the connection saved, listed, and showed its
 * fields as set, then failed at execution with a credential the connector never
 * found. Publishing the declaration turns a memory test into a form.
 *
 * Nothing here carries a secret in either direction - it describes what a
 * credential is called, never what one is. Values are still write-only through
 * `./connections`.
 */

import { apiClient } from './client';
import { ApiResponse } from './types';

// ============================================================================
// Types
// ============================================================================

/** One credential field a connector reads, as the connector declares it. */
export interface CredentialFieldDescriptor {
  /** The exact key the connector reads, e.g. "botToken". Submitted verbatim. */
  name: string;
  /** Human label, e.g. "Bot User OAuth Token". */
  label: string;
  /** "password" to mask the input; anything else is plain text. */
  type: string;
  /** Whether the connector cannot work without it. */
  required: boolean;
  description?: string;
}

/** A connector as the credential form needs it. */
export interface ConnectorDescriptor {
  id: string;
  name: string;
  description: string;
  category: string;
  /** Authentication style, e.g. "ApiKey", "OAuth2", "Basic", "None". */
  authType: string;
  credentialFields: CredentialFieldDescriptor[];
}

export interface ConnectorListResponse {
  connectors: ConnectorDescriptor[];
  total: number;
}

// ============================================================================
// Requests
// ============================================================================

/** Every registered connector and the credential fields it declares. */
export async function listConnectors(): Promise<ApiResponse<ConnectorListResponse>> {
  return apiClient.get<ConnectorListResponse>('/connectors');
}

/** One connector's declaration. */
export async function getConnector(
  connectorId: string
): Promise<ApiResponse<ConnectorDescriptor>> {
  return apiClient.get<ConnectorDescriptor>(`/connectors/${connectorId}`);
}

// ============================================================================
// Helpers
// ============================================================================

/**
 * Which declared fields a submission is still missing.
 *
 * Only `required` fields count: a connector that declares an optional
 * `signingSecret` still works without one, and demanding it would block a
 * perfectly good connection.
 */
export function getMissingRequiredFields(
  connector: ConnectorDescriptor,
  values: Record<string, string>
): CredentialFieldDescriptor[] {
  return connector.credentialFields.filter(
    (field) => field.required && !values[field.name]?.trim()
  );
}
