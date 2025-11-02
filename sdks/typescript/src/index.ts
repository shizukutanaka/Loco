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
 *   apiKey: "loco_sk_xxx"
 * });
 *
 * const workflows = await client.workflows.list();
 * const result = await client.workflows.execute("workflow-1", {});
 * ```
 */

import { v4 as uuidv4 } from "uuid";

// Type definitions
export interface WorkflowData {
  id: string;
  name: string;
  description?: string;
  steps?: WorkflowStep[];
  created_at: string;
  updated_at: string;
}

export interface WorkflowStep {
  id: string;
  order: number;
  type: string;
  action_name: string;
  configuration?: Record<string, unknown>;
}

export interface ExecutionResult {
  execution_id: string;
  workflow_id: string;
  status: string;
  started_at: string;
  completed_at?: string;
  progress: number;
  result?: Record<string, unknown>;
}

export interface ExecutionStatus extends ExecutionResult {
  step_executions?: StepExecution[];
}

export interface StepExecution {
  step_id: string;
  status: string;
  started_at?: string;
  completed_at?: string;
  result?: Record<string, unknown>;
}

export interface TokenResponse {
  access_token: string;
  token_type: string;
  expires_in: number;
  scope: string;
  refresh_token?: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  skip: number;
  take: number;
  has_more: boolean;
}

export interface LocoClientConfig {
  apiKey?: string;
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
 * Loco Workflow Automation Client
 */
export class LocoClient {
  private baseUrl: string;
  private apiKey?: string;
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
    this.apiKey = config.apiKey;
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

      this.jwtToken = response.access_token;
      this.tokenExpiry = new Date(Date.now() + response.expires_in * 1000);
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
    if (!this.jwtToken && !this.apiKey) {
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

    if (this.jwtToken) {
      headers["Authorization"] = `Bearer ${this.jwtToken}`;
    } else if (this.apiKey) {
      headers["X-Api-Key"] = this.apiKey;
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

          return (await response.json()) as T;
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
  async list(skip = 0, take = 20): Promise<PaginatedResponse<WorkflowData>> {
    const params = new URLSearchParams({
      skip: String(skip),
      take: String(Math.min(take, 100)),
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
    steps?: WorkflowStep[]
  ): Promise<WorkflowData> {
    return (this.client as any)._request("POST", "/api/v1/workflows", {
      name,
      description,
      steps,
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
    parameters?: Record<string, unknown>,
    asyncExecution = true
  ): Promise<ExecutionResult> {
    return (this.client as any)._request(
      "POST",
      `/api/v1/workflows/${workflowId}/execute`,
      {
        parameters: parameters || {},
        asyncExecution,
      }
    );
  }

  /**
   * Get execution status
   */
  async getExecutionStatus(
    workflowId: string,
    executionId: string
  ): Promise<ExecutionStatus> {
    return (this.client as any)._request(
      "GET",
      `/api/v1/workflows/${workflowId}/executions/${executionId}`
    );
  }

  /**
   * Wait for execution to complete
   */
  async waitForExecution(
    workflowId: string,
    executionId: string,
    timeout = 300000, // milliseconds
    pollInterval = 1000
  ): Promise<ExecutionStatus> {
    const startTime = Date.now();

    while (true) {
      const status = await this.getExecutionStatus(workflowId, executionId);

      if (["Completed", "Failed", "Cancelled"].includes(status.status)) {
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
