/**
 * Loco TypeScript/JavaScript SDK
 * Enterprise-grade workflow automation client library
 *
 * Features:
 * - Promise-based async/await support
 * - Full TypeScript type definitions
 * - Multiple authentication methods
 * - Automatic JWT token management
 * - Built-in retry logic with exponential backoff
 * - Request correlation tracking
 *
 * @example
 * ```typescript
 * const client = new LocoClient("https://api.loco.io", {
 *   username: "u",
 *   password: "p"
 * });
 *
 * const workflows = await client.workflows.list();
 * const result = await client.workflows.execute("workflow-1", {});
 * ```
 */

import { v4 as uuidv4 } from "uuid";

// Type definitions.
//
// Field names are camelCase because the API serializes that way
// (JsonNamingPolicy.CamelCase, set in Program.cs). These were snake_case,
// which meant every property read was undefined on a response that had
// actually succeeded.

/** The statuses an execution can end on. Lowercase, as the API emits them. */
export const TERMINAL_STATUSES = ["completed", "failed", "cancelled"] as const;

/**
 * A stored workflow. Loco models a workflow as a node graph - `nodes` carry
 * the actions and `edges` connect them - not as an ordered step list.
 */
export interface WorkflowData {
  id: string;
  name: string;
  description?: string;
  nodes?: WorkflowNode[];
  edges?: WorkflowEdge[];
  metadata?: Record<string, unknown>;
  createdAt: string;
  updatedAt: string;
}

export interface WorkflowNode {
  id: string;
  type: string;
  position: { x: number; y: number };
  data: {
    label?: string;
    integration?: string;
    credentialId?: string;
    config?: Record<string, unknown>;
  };
}

export interface WorkflowEdge {
  id: string;
  source: string;
  target: string;
  data?: { condition?: string };
}

/**
 * One execution. `output` and `logs` appear once the run finishes; `error`
 * only on a failed or cancelled one.
 */
export interface ExecutionResult {
  executionId: string;
  status: string;
  startedAt: string;
  completedAt?: string;
  output?: Record<string, unknown>;
  error?: { nodeId: string; message: string };
  logs?: ExecutionLog[];
}

export type ExecutionStatus = ExecutionResult;

export interface ExecutionLog {
  timestamp: string;
  level: string;
  message: string;
  nodeId?: string;
}

export interface TokenResponse {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
  scope: string;
}

/** The list endpoint's payload. It keys the page by `workflows`, not `items`. */
export interface PaginatedResponse<T> {
  workflows: T[];
  total: number;
  page: number;
  pageSize: number;
}

/**
 * The envelope every endpoint answers with (Loco.Api.Contracts.ApiEnvelope).
 * `_request` unwraps it, so no method here ever returns one.
 */
interface ApiEnvelope<T> {
  success: boolean;
  data?: T;
  error?: { code: string; message: string };
  message?: string;
}

export interface LocoClientConfig {
  username?: string;
  password?: string;
  jwtToken?: string;
  timeout?: number;
  maxRetries?: number;
  verifySsl?: boolean;
  headers?: Record<string, string>;
}

// Custom exceptions
export class LocoException extends Error {
  constructor(message: string, public statusCode?: number) {
    super(message);
    this.name = "LocoException";
  }
}

export class LocoAuthError extends LocoException {
  constructor(message: string) {
    super(message, 401);
    this.name = "LocoAuthError";
  }
}

export class LocoNotFoundError extends LocoException {
  constructor(message: string) {
    super(message, 404);
    this.name = "LocoNotFoundError";
  }
}

export class LocoValidationError extends LocoException {
  constructor(message: string) {
    super(message, 400);
    this.name = "LocoValidationError";
  }
}

export class LocoServerError extends LocoException {
  constructor(message: string, statusCode: number = 500) {
    super(message, statusCode);
    this.name = "LocoServerError";
  }
}

export class RateLimitError extends LocoException {
  constructor(message: string) {
    super(message, 429);
    this.name = "RateLimitError";
  }
}

/**
 * Returns the payload from Loco's response envelope.
 *
 * Every endpoint answers with the same shape:
 *
 *   { success: true,  data: {...},  message?: string }
 *   { success: false, error: { code, message } }
 *
 * This client used to hand the whole envelope to the caller, so
 * `result.status` was undefined on a response that had actually succeeded,
 * and a failure arrived as the bare HTTP code with the server's explanation
 * thrown away.
 *
 * `/health` is ASP.NET Core's health endpoint and is not enveloped, so a body
 * without a `success` key is passed through unchanged.
 */
function unwrap<T>(body: unknown): T {
  if (typeof body !== "object" || body === null || !("success" in body)) {
    return body as T;
  }

  const envelope = body as ApiEnvelope<T>;
  if (envelope.success) {
    return (envelope.data ?? ({} as T)) as T;
  }

  const code = envelope.error?.code ?? "UNKNOWN";
  const message = envelope.error?.message ?? "Request failed";
  const detail = `${code}: ${message}`;

  switch (code) {
    case "UNAUTHORIZED":
    case "AUTH_NOT_CONFIGURED":
      throw new LocoAuthError(detail);
    case "NOT_FOUND":
      throw new LocoNotFoundError(detail);
    case "INVALID_ARGUMENT":
    case "INVALID_WORKFLOW":
    case "UNKNOWN_CONNECTOR":
      throw new LocoValidationError(detail);
    default:
      throw new LocoException(detail);
  }
}

/**
 * Loco Workflow Automation Client
 */
export class LocoClient {
  private baseUrl: string;
  private username?: string;
  private password?: string;
  private jwtToken?: string;
  private timeout: number;
  private maxRetries: number;
  private verifySsl: boolean;
  private headers: Record<string, string>;
  private tokenExpiry?: Date;
  private correlationId: string;
  private isInitialized = false;

  public workflows: WorkflowsAPI;

  constructor(baseUrl: string, config: LocoClientConfig = {}) {
    this.baseUrl = baseUrl.replace(/\/$/, ""); // Remove trailing slash
    this.username = config.username;
    this.password = config.password;
    this.jwtToken = config.jwtToken;
    this.timeout = config.timeout ?? 30000; // milliseconds
    this.maxRetries = config.maxRetries ?? 3;
    this.verifySsl = config.verifySsl !== false;
    this.headers = {
      "Content-Type": "application/json",
      "User-Agent": "loco-typescript-sdk/1.0.0",
      ...config.headers,
    };
    this.correlationId = uuidv4();

    // Initialize API endpoints
    this.workflows = new WorkflowsAPI(this);
  }

  /**
   * Authenticate with username and password
   */
  async authenticate(): Promise<TokenResponse> {
    if (!this.username || !this.password) {
      throw new LocoAuthError("Username and password are required");
    }

    try {
      const response = await this._request<TokenResponse>(
        "POST",
        "/api/v1/authentication/token",
        {
          username: this.username,
          password: this.password,
        },
        { skipAuth: true }
      );

      this.jwtToken = response.accessToken;
      this.tokenExpiry = new Date(Date.now() + response.expiresIn * 1000);
      console.log(`Authentication successful, token expires at ${this.tokenExpiry}`);

      return response;
    } catch (error) {
      throw new LocoAuthError(`Authentication failed: ${String(error)}`);
    }
  }

  /**
   * Ensure client is authenticated
   */
  private async ensureAuthenticated(): Promise<void> {
    if (!this.jwtToken) {
      if (this.username && this.password) {
        await this.authenticate();
      } else {
        throw new LocoAuthError("No authentication method provided");
      }
    }

    // Refresh token if expiring soon
    if (this.jwtToken && this.tokenExpiry) {
      const fiveMinutesFromNow = new Date(Date.now() + 5 * 60 * 1000);
      if (this.tokenExpiry < fiveMinutesFromNow) {
        console.log("Token expiring soon, refreshing...");
        await this.authenticate();
      }
    }
  }

  /**
   * Make HTTP request with retry logic
   */
  private async _request<T = unknown>(
    method: string,
    endpoint: string,
    body?: unknown,
    options: { skipAuth?: boolean } = {}
  ): Promise<T> {
    if (!options.skipAuth) {
      await this.ensureAuthenticated();
    }

    const headers: Record<string, string> = {
      ...this.headers,
      "X-Correlation-ID": this.correlationId,
    };

    // Bearer only. The API registers exactly one authentication scheme,
    // JwtBearer (Program.cs), and reads no X-Api-Key header - so the apiKey
    // option this client used to offer sent something the server ignored,
    // and every call came back 401.
    if (this.jwtToken) {
      headers["Authorization"] = `Bearer ${this.jwtToken}`;
    }

    let lastError: Error | null = null;

    for (let attempt = 0; attempt < this.maxRetries; attempt++) {
      try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), this.timeout);

        try {
          const response = await fetch(`${this.baseUrl}${endpoint}`, {
            method,
            headers,
            body: body ? JSON.stringify(body) : undefined,
            signal: controller.signal,
          });

          clearTimeout(timeoutId);

          // Handle different status codes
          if (response.status === 401) {
            throw new LocoAuthError("Unauthorized");
          }
          if (response.status === 403) {
            throw new LocoAuthError("Forbidden");
          }
          if (response.status === 404) {
            throw new LocoNotFoundError("Resource not found");
          }
          if (response.status === 429) {
            throw new RateLimitError("Rate limit exceeded");
          }
          if (response.status >= 500) {
            throw new LocoServerError(`Server error: ${response.status}`, response.status);
          }
          if (response.status >= 400) {
            throw new LocoValidationError(`Bad request: ${response.status}`);
          }

          if (!response.ok) {
            throw new LocoException(`Request failed with status ${response.status}`);
          }

          return unwrap<T>(await response.json());
        } finally {
          clearTimeout(timeoutId);
        }
      } catch (error) {
        lastError = error as Error;

        if (
          error instanceof TypeError &&
          (error.message.includes("fetch") || error.message.includes("AbortError"))
        ) {
          const waitTime = Math.pow(2, attempt) * 1000;
          if (attempt < this.maxRetries - 1) {
            console.warn(
              `Request failed (attempt ${attempt + 1}/${this.maxRetries}), retrying in ${waitTime}ms: ${error.message}`
            );
            await new Promise((resolve) => setTimeout(resolve, waitTime));
            continue;
          }
        }

        throw error;
      }
    }

    throw lastError || new LocoException("Request failed after retries");
  }

  /**
   * Health check
   */
  async healthCheck(): Promise<Record<string, unknown>> {
    return this._request("GET", "/health", undefined, { skipAuth: true });
  }
}

/**
 * Workflows API
 */
export class WorkflowsAPI {
  constructor(private client: LocoClient) {}

  /**
   * List workflows
   */
  async list(page = 1, pageSize = 20): Promise<PaginatedResponse<WorkflowData>> {
    // The API pages by page/pageSize (WorkflowsController.GetWorkflows).
    // This sent skip/take, which ASP.NET Core ignored - so every call
    // returned the first page whatever was asked for, with no error.
    const params = new URLSearchParams({
      page: String(page),
      pageSize: String(Math.min(pageSize, 100)),
    });
    return (this.client as any)._request(
      "GET",
      `/api/v1/workflows?${params}`
    );
  }

  /**
   * Get workflow by ID
   */
  async get(workflowId: string): Promise<WorkflowData> {
    return (this.client as any)._request("GET", `/api/v1/workflows/${workflowId}`);
  }

  /**
   * Create workflow
   */
  async create(
    name: string,
    description?: string,
    nodes: WorkflowNode[] = [],
    edges: WorkflowEdge[] = [],
    metadata: Record<string, unknown> = {}
  ): Promise<WorkflowData> {
    // WorkflowCreateRequest has no `steps` property, so a workflow created
    // through this method used to come back empty: accepted, stored, and
    // containing nothing.
    return (this.client as any)._request("POST", "/api/v1/workflows", {
      name,
      description,
      nodes,
      edges,
      metadata,
    });
  }

  /**
   * Update workflow
   */
  async update(
    workflowId: string,
    data: Partial<WorkflowData>
  ): Promise<WorkflowData> {
    return (this.client as any)._request(
      "PUT",
      `/api/v1/workflows/${workflowId}`,
      data
    );
  }

  /**
   * Delete workflow
   */
  async delete(workflowId: string): Promise<void> {
    await (this.client as any)._request(
      "DELETE",
      `/api/v1/workflows/${workflowId}`
    );
  }

  /**
   * Execute workflow
   */
  async execute(
    workflowId: string,
    input: Record<string, unknown> = {},
    dryRun = false
  ): Promise<ExecutionResult> {
    // The body is ExecuteRequest: { input, dryRun }. Sending
    // { parameters, asyncExecution } meant initial variables never reached
    // the workflow, and a dry run executed for real. Execution is always
    // asynchronous - the call returns as soon as the run is registered.
    return (this.client as any)._request(
      "POST",
      `/api/v1/workflows/${workflowId}/execute`,
      { input, dryRun }
    );
  }

  /**
   * Get execution status
   */
  async getExecutionStatus(executionId: string): Promise<ExecutionStatus> {
    // Executions are addressed globally by id (ExecutionsController), not
    // nested under their workflow. The nested route does not exist, so this
    // used to 404 on every call.
    return (this.client as any)._request(
      "GET",
      `/api/v1/executions/${executionId}`
    );
  }

  /** Ask a running execution to stop. */
  async cancelExecution(executionId: string): Promise<void> {
    await (this.client as any)._request(
      "POST",
      `/api/v1/executions/${executionId}/cancel`
    );
  }

  /**
   * Wait for execution to complete
   */
  async waitForExecution(
    executionId: string,
    timeout = 300000, // milliseconds
    pollInterval = 1000
  ): Promise<ExecutionStatus> {
    const startTime = Date.now();

    while (true) {
      const status = await this.getExecutionStatus(executionId);

      // Lowercase: ExecutionResponseFactory.ToFrontendStatus emits
      // pending/running/completed/failed/cancelled. Comparing against
      // "Completed" never matched, so this ran to timeout on runs that had
      // already succeeded.
      if ((TERMINAL_STATUSES as readonly string[]).includes(status.status)) {
        return status;
      }

      const elapsed = Date.now() - startTime;
      if (elapsed > timeout) {
        throw new Error(
          `Execution ${executionId} did not complete within ${timeout}ms`
        );
      }

      await new Promise((resolve) => setTimeout(resolve, pollInterval));
    }
  }
}

/**
 * Convenience function to create client
 */
export function createClient(
  baseUrl: string,
  config?: LocoClientConfig
): LocoClient {
  return new LocoClient(baseUrl, config);
}

// Export as default for CommonJS compatibility
export default LocoClient;
