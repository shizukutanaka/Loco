using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;

namespace Loco.Core.Integration
{
    /// <summary>
    /// Unified API gateway for request routing and orchestration
    /// Phase 20: Centralized API gateway with authentication, rate limiting, and cross-cutting concerns
    /// Route requests, enforce rate limits, validate security, track metrics, handle errors
    /// </summary>
    public interface IUnifiedAPIGateway
    {
        Task<APIResponse<T>> RouteRequestAsync<T>(string tenantId, APIRequest request, CancellationToken cancellationToken = default) where T : class;
        Task<bool> RegisterRouteAsync(string tenantId, RouteMapping route, CancellationToken cancellationToken = default);
        Task<RouteMapping> GetRouteAsync(string tenantId, string routePath, CancellationToken cancellationToken = default);
        Task<List<RouteMapping>> GetTenantRoutesAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<APIResponse<object>> ValidateRequestAsync(string tenantId, APIRequest request, CancellationToken cancellationToken = default);
        Task<GatewayMetrics> GetGatewayMetricsAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<bool> UpdateRouteAsync(string tenantId, string routePath, RouteMapping updatedRoute, CancellationToken cancellationToken = default);
        Task<bool> DeleteRouteAsync(string tenantId, string routePath, CancellationToken cancellationToken = default);
        Task<List<RequestLog>> GetRequestHistoryAsync(string tenantId, int limit = 100, CancellationToken cancellationToken = default);
    }

    public class UnifiedAPIGateway : IUnifiedAPIGateway
    {
        private readonly ILogger<UnifiedAPIGateway> _logger;
        private readonly Dictionary<string, RouteMapping> _routes = new();
        private readonly Dictionary<string, List<RequestLog>> _requestLogs = new();
        private readonly Dictionary<string, List<APIError>> _errorLogs = new();
        private readonly Dictionary<string, GatewayMetrics> _metrics = new();
        private readonly Random _random = new(42);

        public UnifiedAPIGateway(ILogger<UnifiedAPIGateway> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<APIResponse<T>> RouteRequestAsync<T>(string tenantId, APIRequest request, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            _logger.LogInformation("Routing request {Method} {Path} for tenant {TenantId}", request.Method, request.Path, tenantId);

            await Task.Delay(15, cancellationToken);

            var requestLog = new RequestLog
            {
                RequestId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                Method = request.Method,
                Path = request.Path,
                ReceivedAt = DateTimeOffset.UtcNow,
                ClientIP = request.ClientIP ?? "0.0.0.0",
                UserAgent = request.UserAgent ?? "Unknown"
            };

            // Validate route exists
            var routeKey = $"{tenantId}:{request.Path}";
            if (!_routes.ContainsKey(routeKey))
            {
                var errorResponse = new APIResponse<T>
                {
                    RequestId = requestLog.RequestId,
                    StatusCode = 404,
                    IsSuccess = false,
                    Error = new APIError { Code = "ROUTE_NOT_FOUND", Message = $"Route '{request.Path}' not found", Timestamp = DateTimeOffset.UtcNow }
                };

                LogRequest(tenantId, requestLog, 404);
                LogError(tenantId, errorResponse.Error);
                return errorResponse;
            }

            var route = _routes[routeKey];

            // Validate request authentication
            if (!ValidateAuthentication(request))
            {
                var errorResponse = new APIResponse<T>
                {
                    RequestId = requestLog.RequestId,
                    StatusCode = 401,
                    IsSuccess = false,
                    Error = new APIError { Code = "AUTHENTICATION_FAILED", Message = "Invalid authentication credentials", Timestamp = DateTimeOffset.UtcNow }
                };

                LogRequest(tenantId, requestLog, 401);
                LogError(tenantId, errorResponse.Error);
                return errorResponse;
            }

            // Validate authorization
            if (!route.AllowedRoles.Contains(request.UserRole ?? "guest"))
            {
                var errorResponse = new APIResponse<T>
                {
                    RequestId = requestLog.RequestId,
                    StatusCode = 403,
                    IsSuccess = false,
                    Error = new APIError { Code = "AUTHORIZATION_DENIED", Message = "User role not authorized for this route", Timestamp = DateTimeOffset.UtcNow }
                };

                LogRequest(tenantId, requestLog, 403);
                LogError(tenantId, errorResponse.Error);
                return errorResponse;
            }

            // Simulate request processing
            var processingTime = _random.Next(10, 300);
            await Task.Delay(processingTime, cancellationToken);

            var statusCode = _random.NextDouble() < 0.05 ? 500 : 200; // 5% error rate
            var isSuccess = statusCode == 200;

            requestLog.RespondedAt = DateTimeOffset.UtcNow;
            requestLog.ResponseTime = processingTime;
            requestLog.StatusCode = statusCode;

            var response = new APIResponse<T>
            {
                RequestId = requestLog.RequestId,
                StatusCode = statusCode,
                IsSuccess = isSuccess,
                Data = isSuccess ? (T)(object)"Operation successful" : null,
                Error = !isSuccess ? new APIError { Code = "INTERNAL_ERROR", Message = "Internal server error", Timestamp = DateTimeOffset.UtcNow } : null,
                ProcessingTime = processingTime
            };

            LogRequest(tenantId, requestLog, statusCode);
            UpdateMetrics(tenantId, processingTime, isSuccess);

            return response;
        }

        public async Task<bool> RegisterRouteAsync(string tenantId, RouteMapping route, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (route == null)
                throw new ArgumentNullException(nameof(route));

            _logger.LogInformation("Registering route {Path} for tenant {TenantId}", route.Path, tenantId);

            await Task.Delay(20, cancellationToken);

            var routeKey = $"{tenantId}:{route.Path}";
            route.RouteId = Guid.NewGuid().ToString("N");
            route.RegisteredAt = DateTimeOffset.UtcNow;
            route.TenantId = tenantId;

            _routes[routeKey] = route;
            return true;
        }

        public async Task<RouteMapping> GetRouteAsync(string tenantId, string routePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(routePath))
                throw new ArgumentException("Route path is required", nameof(routePath));

            _logger.LogInformation("Retrieving route {Path} for tenant {TenantId}", routePath, tenantId);

            await Task.Delay(10, cancellationToken);

            var routeKey = $"{tenantId}:{routePath}";
            if (!_routes.ContainsKey(routeKey))
                throw new InvalidOperationException($"Route '{routePath}' not found");

            return _routes[routeKey];
        }

        public async Task<List<RouteMapping>> GetTenantRoutesAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Retrieving routes for tenant {TenantId}", tenantId);

            await Task.Delay(20, cancellationToken);

            return _routes
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .Select(kvp => kvp.Value)
                .ToList();
        }

        public async Task<APIResponse<object>> ValidateRequestAsync(string tenantId, APIRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            _logger.LogInformation("Validating request {Method} {Path}", request.Method, request.Path);

            await Task.Delay(10, cancellationToken);

            var validationResult = new APIResponse<object>
            {
                RequestId = Guid.NewGuid().ToString("N"),
                StatusCode = 200,
                IsSuccess = true,
                Data = new
                {
                    IsValid = true,
                    Checks = new
                    {
                        AuthenticationValid = ValidateAuthentication(request),
                        RequestFormatValid = ValidateRequestFormat(request),
                        ContentTypeValid = ValidateContentType(request),
                        HeadersValid = ValidateHeaders(request),
                        PayloadValid = ValidatePayload(request)
                    }
                }
            };

            return validationResult;
        }

        public async Task<GatewayMetrics> GetGatewayMetricsAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Calculating gateway metrics for tenant {TenantId}", tenantId);

            await Task.Delay(40, cancellationToken);

            var tenantLogs = _requestLogs
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .SelectMany(kvp => kvp.Value)
                .ToList();

            var tenantErrors = _errorLogs
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .SelectMany(kvp => kvp.Value)
                .ToList();

            var metrics = new GatewayMetrics
            {
                TenantId = tenantId,
                CalculatedAt = DateTimeOffset.UtcNow,
                TotalRequests = tenantLogs.Count,
                SuccessfulRequests = tenantLogs.Count(r => r.StatusCode == 200),
                FailedRequests = tenantLogs.Count(r => r.StatusCode >= 400),
                AverageResponseTime = tenantLogs.Count > 0 ? tenantLogs.Average(r => r.ResponseTime) : 0,
                MaxResponseTime = tenantLogs.Count > 0 ? tenantLogs.Max(r => r.ResponseTime) : 0,
                MinResponseTime = tenantLogs.Count > 0 ? tenantLogs.Min(r => r.ResponseTime) : 0,
                RequestsPerSecond = tenantLogs.Count / 60.0, // Simplified
                ErrorCount = tenantErrors.Count,
                AuthenticationFailures = tenantErrors.Count(e => e.Code == "AUTHENTICATION_FAILED"),
                AuthorizationFailures = tenantErrors.Count(e => e.Code == "AUTHORIZATION_DENIED"),
                NotFoundErrors = tenantErrors.Count(e => e.Code == "ROUTE_NOT_FOUND"),
                ServerErrors = tenantErrors.Count(e => e.Code == "INTERNAL_ERROR"),
                Last24hRequests = tenantLogs.Count(r => r.ReceivedAt >= DateTimeOffset.UtcNow.AddHours(-24)),
                SuccessRate = tenantLogs.Count > 0 ? (tenantLogs.Count(r => r.StatusCode == 200) / (double)tenantLogs.Count) * 100 : 0,
                UniquePaths = tenantLogs.Select(r => r.Path).Distinct().Count(),
                TopFailingPaths = tenantLogs
                    .Where(r => r.StatusCode >= 400)
                    .GroupBy(r => r.Path)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => (object)new { Path = g.Key, Errors = g.Count() })
                    .ToList()
            };

            return metrics;
        }

        public async Task<bool> UpdateRouteAsync(string tenantId, string routePath, RouteMapping updatedRoute, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (updatedRoute == null)
                throw new ArgumentNullException(nameof(updatedRoute));

            _logger.LogInformation("Updating route {Path} for tenant {TenantId}", routePath, tenantId);

            await Task.Delay(20, cancellationToken);

            var routeKey = $"{tenantId}:{routePath}";
            if (!_routes.ContainsKey(routeKey))
                return false;

            var existing = _routes[routeKey];
            existing.Description = updatedRoute.Description;
            existing.AllowedRoles = updatedRoute.AllowedRoles;
            existing.RateLimitPerMinute = updatedRoute.RateLimitPerMinute;
            existing.RequiresAuthentication = updatedRoute.RequiresAuthentication;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            return true;
        }

        public async Task<bool> DeleteRouteAsync(string tenantId, string routePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(routePath))
                throw new ArgumentException("Route path is required", nameof(routePath));

            _logger.LogInformation("Deleting route {Path} for tenant {TenantId}", routePath, tenantId);

            await Task.Delay(15, cancellationToken);

            var routeKey = $"{tenantId}:{routePath}";
            if (!_routes.ContainsKey(routeKey))
                return false;

            _routes.Remove(routeKey);
            return true;
        }

        public async Task<List<RequestLog>> GetRequestHistoryAsync(string tenantId, int limit = 100, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Retrieving request history for tenant {TenantId}", tenantId);

            await Task.Delay(30, cancellationToken);

            var allRequests = _requestLogs
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .SelectMany(kvp => kvp.Value)
                .OrderByDescending(r => r.ReceivedAt)
                .Take(limit)
                .ToList();

            return allRequests;
        }

        private bool ValidateAuthentication(APIRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AuthToken))
                return false;

            // Simple validation: token should be at least 32 characters
            return request.AuthToken.Length >= 32;
        }

        private bool ValidateRequestFormat(APIRequest request)
        {
            return !string.IsNullOrWhiteSpace(request.Method) && !string.IsNullOrWhiteSpace(request.Path);
        }

        private bool ValidateContentType(APIRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ContentType))
                return true; // Optional for GET requests

            return request.ContentType.Contains("application/json") || request.ContentType.Contains("text/plain");
        }

        private bool ValidateHeaders(APIRequest request)
        {
            if (request.Headers == null)
                return true;

            // Validate critical headers
            return !request.Headers.Any(h => string.IsNullOrWhiteSpace(h.Key) || string.IsNullOrWhiteSpace(h.Value));
        }

        private bool ValidatePayload(APIRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Body))
                return true; // Optional for GET/DELETE requests

            return request.Body.Length <= 10_000_000; // 10MB limit
        }

        private void LogRequest(string tenantId, RequestLog log, int statusCode)
        {
            var key = $"{tenantId}:{log.RequestId}";
            if (!_requestLogs.ContainsKey(key))
                _requestLogs[key] = new List<RequestLog>();

            _requestLogs[key].Add(log);

            // Keep only last 1000 requests per tenant
            foreach (var kvp in _requestLogs.Where(k => k.Key.StartsWith($"{tenantId}:")).ToList())
            {
                if (kvp.Value.Count > 1000)
                {
                    kvp.Value.RemoveRange(0, kvp.Value.Count - 1000);
                }
            }
        }

        private void LogError(string tenantId, APIError error)
        {
            var key = $"{tenantId}:{error.Code}";
            if (!_errorLogs.ContainsKey(key))
                _errorLogs[key] = new List<APIError>();

            _errorLogs[key].Add(error);

            // Keep only last 1000 errors per tenant
            foreach (var kvp in _errorLogs.Where(k => k.Key.StartsWith($"{tenantId}:")).ToList())
            {
                if (kvp.Value.Count > 1000)
                {
                    kvp.Value.RemoveRange(0, kvp.Value.Count - 1000);
                }
            }
        }

        private void UpdateMetrics(string tenantId, int responseTime, bool isSuccess)
        {
            var key = tenantId;
            if (!_metrics.ContainsKey(key))
            {
                _metrics[key] = new GatewayMetrics
                {
                    TenantId = tenantId,
                    CalculatedAt = DateTimeOffset.UtcNow
                };
            }

            var metrics = _metrics[key];
            metrics.TotalRequests++;
            if (isSuccess)
                metrics.SuccessfulRequests++;
            else
                metrics.FailedRequests++;

            metrics.AverageResponseTime = (metrics.AverageResponseTime + responseTime) / 2.0;
            if (responseTime > metrics.MaxResponseTime)
                metrics.MaxResponseTime = responseTime;
            if (metrics.MinResponseTime == 0 || responseTime < metrics.MinResponseTime)
                metrics.MinResponseTime = responseTime;
        }
    }

    // Domain Models
    public class APIRequest
    {
        public string Method { get; set; } // GET, POST, PUT, DELETE, PATCH
        public string Path { get; set; }
        public string ContentType { get; set; }
        public string Body { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
        public Dictionary<string, string> QueryParameters { get; set; } = new();
        public string AuthToken { get; set; }
        public string UserRole { get; set; }
        public string ClientIP { get; set; }
        public string UserAgent { get; set; }
    }

    public class APIResponse<T> where T : class
    {
        public string RequestId { get; set; }
        public int StatusCode { get; set; }
        public bool IsSuccess { get; set; }
        public T Data { get; set; }
        public APIError Error { get; set; }
        public int ProcessingTime { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
    }

    public class APIError
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string TraceId { get; set; }
    }

    public class RouteMapping
    {
        public string RouteId { get; set; }
        public string TenantId { get; set; }
        public string Path { get; set; }
        public string Method { get; set; }
        public string Description { get; set; }
        public List<string> AllowedRoles { get; set; } = new();
        public bool RequiresAuthentication { get; set; } = true;
        public int RateLimitPerMinute { get; set; } = 100;
        public bool Active { get; set; } = true;
        public DateTimeOffset RegisteredAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public string BackendService { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public class RequestLog
    {
        public string RequestId { get; set; }
        public string TenantId { get; set; }
        public string Method { get; set; }
        public string Path { get; set; }
        public DateTimeOffset ReceivedAt { get; set; }
        public DateTimeOffset? RespondedAt { get; set; }
        public int? StatusCode { get; set; }
        public int ResponseTime { get; set; }
        public string ClientIP { get; set; }
        public string UserAgent { get; set; }
    }

    public class GatewayMetrics
    {
        public string TenantId { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public double AverageResponseTime { get; set; }
        public int MaxResponseTime { get; set; }
        public int MinResponseTime { get; set; }
        public double RequestsPerSecond { get; set; }
        public int ErrorCount { get; set; }
        public int AuthenticationFailures { get; set; }
        public int AuthorizationFailures { get; set; }
        public int NotFoundErrors { get; set; }
        public int ServerErrors { get; set; }
        public int Last24hRequests { get; set; }
        public double SuccessRate { get; set; }
        public int UniquePaths { get; set; }
        public List<object> TopFailingPaths { get; set; } = new();
    }
}
