/**
 * API Client for Loco Backend
 *
 * Provides a centralized axios-based HTTP client for communicating
 * with the Loco backend API. Handles authentication, error handling,
 * and request/response transformations.
 */

import axios, { AxiosInstance, AxiosError, AxiosRequestConfig } from 'axios';
import { ApiResponse, ApiError, AuthConfig } from './types';

// ============================================================================
// API Client Class
// ============================================================================

export class LocoApiClient {
  private client: AxiosInstance;
  private authConfig: AuthConfig;

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
    try {
      const response = await this.client.get<ApiResponse<T>>(url, config);
      return response.data;
    } catch (error) {
      return this.handleError<T>(error);
    }
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

  // ==========================================================================
  // Error Handling
  // ==========================================================================

  private transformError(error: AxiosError): ApiError {
    if (error.response) {
      // Server responded with error status
      const data = error.response.data as ApiResponse<unknown>;
      return data.error || {
        code: `HTTP_${error.response.status}`,
        message: error.message || 'An error occurred',
        details: { status: error.response.status },
      };
    } else if (error.request) {
      // Request made but no response received
      return {
        code: 'NETWORK_ERROR',
        message: 'Unable to reach the server. Please check your connection.',
      };
    } else {
      // Error in request configuration
      return {
        code: 'REQUEST_ERROR',
        message: error.message || 'Failed to make the request',
      };
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
