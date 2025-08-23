using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Loco.Core.AI;
using Loco.Core.Monitoring;
using Loco.Core.Security;
using Loco.Core.Models;

namespace Loco.Web.Controllers
{
    /// <summary>
    /// API controller for advanced features including optimization, metrics, and security audit
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AdvancedController : ControllerBase
    {
        private readonly ILogger<AdvancedController> _logger;
        private readonly FlowOptimizationEngine _optimizationEngine;
        private readonly AdvancedMetricsService _metricsService;
        private readonly SecurityAuditService _auditService;

        public AdvancedController(
            ILogger<AdvancedController> logger,
            FlowOptimizationEngine optimizationEngine,
            AdvancedMetricsService metricsService,
            SecurityAuditService auditService)
        {
            _logger = logger;
            _optimizationEngine = optimizationEngine;
            _metricsService = metricsService;
            _auditService = auditService;
        }

        /// <summary>
        /// Optimizes a flow using AI-powered analysis
        /// </summary>
        [HttpPost("optimize/{flowId}")]
        public async Task<IActionResult> OptimizeFlow(string flowId)
        {
            try
            {
                // Get flow from database
                var flow = await GetFlowFromDatabase(flowId);
                if (flow == null)
                    return NotFound($"Flow {flowId} not found");

                // Perform optimization
                var result = await _optimizationEngine.OptimizeFlowAsync(flow);
                
                if (result.Success)
                {
                    _logger.LogInformation($"Flow {flowId} optimized with improvement score: {result.ImprovementScore}");
                    return Ok(new
                    {
                        success = true,
                        flowId = flowId,
                        improvementScore = result.ImprovementScore,
                        opportunities = result.Opportunities,
                        suggestions = result.AISuggestions,
                        optimizedFlow = result.OptimizedFlow
                    });
                }
                else
                {
                    return BadRequest(new { success = false, error = result.ErrorMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error optimizing flow {flowId}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Batch optimization for multiple flows
        /// </summary>
        [HttpPost("optimize/batch")]
        public async Task<IActionResult> OptimizeFlowsBatch([FromBody] List<string> flowIds)
        {
            try
            {
                var flows = new List<FlowDefinition>();
                foreach (var id in flowIds)
                {
                    var flow = await GetFlowFromDatabase(id);
                    if (flow != null)
                        flows.Add(flow);
                }

                var result = await _optimizationEngine.OptimizeFlowsAsync(flows);
                
                return Ok(new
                {
                    success = true,
                    totalFlows = result.TotalFlows,
                    optimizedFlows = result.OptimizedFlows,
                    averageImprovement = result.AverageImprovementScore,
                    duration = (result.EndTime - result.StartTime).TotalSeconds,
                    results = result.Results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch optimization");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Gets real-time metrics snapshot
        /// </summary>
        [HttpGet("metrics")]
        public IActionResult GetMetrics()
        {
            try
            {
                var snapshot = _metricsService.GetSnapshot();
                return Ok(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting metrics");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Gets real-time dashboard data
        /// </summary>
        [HttpGet("dashboard")]
        public IActionResult GetDashboardData()
        {
            try
            {
                var data = _metricsService.GetDashboardData();
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard data");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Records a custom metric
        /// </summary>
        [HttpPost("metrics")]
        public IActionResult RecordMetric([FromBody] MetricRequest request)
        {
            try
            {
                _metricsService.RecordMetric(request.Name, request.Value, request.Tags);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording metric");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Analyzes metrics for a specific metric name
        /// </summary>
        [HttpGet("metrics/analyze/{metricName}")]
        public async Task<IActionResult> AnalyzeMetrics(string metricName, [FromQuery] int hours = 1)
        {
            try
            {
                var analysis = await _metricsService.AnalyzeMetrics(metricName, TimeSpan.FromHours(hours));
                
                if (analysis.Success)
                    return Ok(analysis);
                else
                    return BadRequest(new { success = false, message = analysis.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error analyzing metrics for {metricName}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Exports metrics in specified format
        /// </summary>
        [HttpGet("metrics/export")]
        public async Task<IActionResult> ExportMetrics(
            [FromQuery] string format = "json",
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            try
            {
                if (!Enum.TryParse<ExportFormat>(format, true, out var exportFormat))
                    return BadRequest(new { error = "Invalid export format" });

                var data = await _metricsService.ExportMetrics(exportFormat, from, to);
                
                var contentType = exportFormat switch
                {
                    ExportFormat.Csv => "text/csv",
                    ExportFormat.Prometheus => "text/plain",
                    _ => "application/json"
                };

                return File(data, contentType, $"metrics.{format.ToLower()}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting metrics");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Performs a comprehensive security audit
        /// </summary>
        [HttpPost("audit")]
        public async Task<IActionResult> PerformSecurityAudit()
        {
            try
            {
                var report = await _auditService.PerformAuditAsync();
                
                if (report.Success)
                {
                    _logger.LogInformation($"Security audit completed with score: {report.OverallScore}");
                    return Ok(report);
                }
                else
                {
                    return BadRequest(new { success = false, error = report.ErrorMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing security audit");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Gets security statistics
        /// </summary>
        [HttpGet("audit/statistics")]
        public IActionResult GetSecurityStatistics()
        {
            try
            {
                var stats = _auditService.GetStatistics();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security statistics");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Validates audit log integrity
        /// </summary>
        [HttpPost("audit/validate")]
        public async Task<IActionResult> ValidateAuditIntegrity()
        {
            try
            {
                var result = await _auditService.ValidateLogIntegrity();
                
                if (result.IsValid)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Audit logs are valid",
                        integrityScore = result.IntegrityScore,
                        totalLogs = result.TotalLogs,
                        validLogs = result.ValidLogs
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Audit log integrity check failed",
                        tamperedLogs = result.TamperedLogs,
                        integrityScore = result.IntegrityScore
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating audit integrity");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Exports audit logs
        /// </summary>
        [HttpGet("audit/export")]
        public async Task<IActionResult> ExportAuditLogs(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] string format = "json")
        {
            try
            {
                if (!Enum.TryParse<ExportFormat>(format, true, out var exportFormat))
                    return BadRequest(new { error = "Invalid export format" });

                var data = await _auditService.ExportAuditLogs(from, to, exportFormat);
                
                var contentType = exportFormat switch
                {
                    ExportFormat.Csv => "text/csv",
                    ExportFormat.Syslog => "text/plain",
                    ExportFormat.Encrypted => "application/octet-stream",
                    _ => "application/json"
                };

                return File(data, contentType, $"audit-{from:yyyyMMdd}-{to:yyyyMMdd}.{format.ToLower()}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit logs");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Logs a security event
        /// </summary>
        [HttpPost("audit/log")]
        public IActionResult LogSecurityEvent([FromBody] SecurityEventRequest request)
        {
            try
            {
                _auditService.LogSecurityEvent(
                    request.Type,
                    request.Message,
                    request.Context,
                    request.Severity);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging security event");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Helper method to get flow from database
        private async Task<FlowDefinition> GetFlowFromDatabase(string flowId)
        {
            // Implementation would fetch from actual database
            // For now, return a mock flow
            return await Task.FromResult(new FlowDefinition
            {
                Id = flowId,
                Name = $"Flow {flowId}",
                Description = "Sample flow for optimization",
                Actions = new List<ActionDefinition>
                {
                    new ActionDefinition { Id = "1", Type = "file.read" },
                    new ActionDefinition { Id = "2", Type = "data.process" },
                    new ActionDefinition { Id = "3", Type = "file.write" }
                }
            });
        }
    }

    // Request models
    public class MetricRequest
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public Dictionary<string, string> Tags { get; set; }
    }

    public class SecurityEventRequest
    {
        public SecurityEventType Type { get; set; }
        public string Message { get; set; }
        public SecurityContext Context { get; set; }
        public SecuritySeverity Severity { get; set; }
    }
}
