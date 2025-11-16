/**
 * API Client for Loco Backend
 *
 * Provides a centralized axios-based HTTP client for communicating
 * with the Loco backend API. Handles authentication, error handling,
 * and request/response transformations.
 */

import axios, { AxiosInstance, AxiosError, AxiosRequestConfig } from 'axios';
import { ApiResponse, ApiError, AuthConfig } from './types';
import { logApiError, logNetworkError } from '@/utils/errorLogger';
import { retryNetworkOperation } from '@/utils/retry';
import { isApiError } from '@/utils/typeGuards';

// ============================================================================
// API Client Class
// ============================================================================

export class LocoApiClient {
  private client: AxiosInstance;
  private authConfig: AuthConfig;
  private enableRetry: boolean = true;

  constructor(baseURL: string = '/api/v1', authConfig?: AuthConfig) {
    this.authConfig = authConfig || {};

    // Create axios instance with default config
    this.client = axios.create({
      baseURL,
      timeout: 30000, // 30 seconds
      headers: {
        'Content-Type': 'application/json',
      },
    });

    // Request interceptor - Add authentication
    this.client.interceptors.request.use(
      (config) => {
        // Add API key or Bearer token if available
        if (this.authConfig.apiKey) {
          config.headers['X-API-Key'] = this.authConfig.apiKey;
        } else if (this.authConfig.token) {
          config.headers['Authorization'] = `Bearer ${this.authConfig.token}`;
        }

        return config;
      },
      (error) => Promise.reject(error)
    );

    // Response interceptor - Handle errors consistently
    this.client.interceptors.response.use(
      (response) => response,
      (error: AxiosError) => {
        const apiError = this.transformError(error);
        return Promise.reject(apiError);
      }
    );
  }

  // ==========================================================================
  // HTTP Methods
  // ==========================================================================

  async get<T>(url: string, config?: AxiosRequestConfig): Promise<ApiResponse<T>> {
    const operation = async () => {
      try {
        const response = await this.client.get<ApiResponse<T>>(url, config);
        return response.data;
      } catch (error) {
        return this.handleError<T>(error);
      }
    };

    if (this.enableRetry) {
      return retryNetworkOperation(operation, {
        maxRetries: 2,
        initialDelay: 1000,
        onRetry: (attempt) => {
          console.log(`Retrying GET ${url} (attempt ${attempt})`);
        },
      });
    }

    return operation();
  }

  async post<T>(
    url: string,
    data?: unknown,
    config?: AxiosRequestConfig
  ): Promise<ApiResponse<T>> {
    try {
      const response = await this.client.post<ApiResponse<T>>(url, data, config);
      return response.data;
    } catch (error) {
      return this.handleError<T>(error);
    }
  }

  async put<T>(
    url: string,
    data?: unknown,
    config?: AxiosRequestConfig
  ): Promise<ApiResponse<T>> {
    try {
      const response = await this.client.put<ApiResponse<T>>(url, data, config);
      return response.data;
    } catch (error) {
      return this.handleError<T>(error);
    }
  }

  async patch<T>(
    url: string,
    data?: unknown,
    config?: AxiosRequestConfig
  ): Promise<ApiResponse<T>> {
    try {
      const response = await this.client.patch<ApiResponse<T>>(url, data, config);
      return response.data;
    } catch (error) {
      return this.handleError<T>(error);
    }
  }

  async delete<T>(url: string, config?: AxiosRequestConfig): Promise<ApiResponse<T>> {
    try {
      const response = await this.client.delete<ApiResponse<T>>(url, config);
      return response.data;
    } catch (error) {
      return this.handleError<T>(error);
    }
  }

  // ==========================================================================
  // Authentication
  // ==========================================================================

  setApiKey(apiKey: string): void {
    this.authConfig.apiKey = apiKey;
  }

  setToken(token: string): void {
    this.authConfig.token = token;
  }

  clearAuth(): void {
    this.authConfig = {};
  }

  setRetryEnabled(enabled: boolean): void {
    this.enableRetry = enabled;
  }

  // ==========================================================================
  // Error Handling
  // ==========================================================================

  private transformError(error: AxiosError): ApiError {
    if (error.response) {
      // Server responded with error status
      const data = error.response.data as ApiResponse<unknown>;
      const apiError = isApiError(data) ? data.error : {
        code: `HTTP_${error.response.status}`,
        message: error.message || 'An error occurred',
        details: { status: error.response.status },
      };

      // Log API error
      logApiError(`API request failed: ${apiError.message}`, error, {
        url: error.config?.url,
        method: error.config?.method,
        status: error.response.status,
        code: apiError.code,
      });

      return apiError;
    } else if (error.request) {
      // Request made but no response received
      const networkError: ApiError = {
        code: 'NETWORK_ERROR',
        message: 'Unable to reach the server. Please check your connection.',
      };

      // Log network error
      logNetworkError('Network request failed', error, {
        url: error.config?.url,
        method: error.config?.method,
      });

      return networkError;
    } else {
      // Error in request configuration
      const requestError: ApiError = {
        code: 'REQUEST_ERROR',
        message: error.message || 'Failed to make the request',
      };

      // Log request error
      logApiError('Request configuration error', error, {
        url: error.config?.url,
        method: error.config?.method,
      });

      return requestError;
    }
  }

  private handleError<T>(error: unknown): ApiResponse<T> {
    const apiError = error as ApiError;
    return {
      success: false,
      error: apiError,
      message: apiError.message,
    };
  }
}

// ============================================================================
// Default Client Instance
// ============================================================================

// Create default client instance for the app
export const apiClient = new LocoApiClient();

// Export for custom instances
export default LocoApiClient;
