# Loco Platform - Commercial Metrics and KPI Framework

## Executive Summary

This document defines the metrics, KPIs, and SLOs for Loco Platform in production environments. These metrics enable operators to understand system health, performance, and business value delivery.

---

## Service Level Objectives (SLOs)

### Availability SLO
- **Target**: 99.9% uptime (43.2 minutes downtime per month acceptable)
- **Measurement**: Minutes service is responding to health checks / Total minutes per month
- **Alert Threshold**: 99.5% (triggers escalation review)

### Latency SLO
- **Target**: P99 execution latency < 5 seconds
- **P95 latency**: < 2 seconds
- **P50 latency**: < 500ms
- **Measurement**: Percentile of all rule execution times

### Error Rate SLO
- **Target**: < 0.1% error rate (1 error per 1000 executions)
- **Definition**: Failed executions / Total execution attempts
- **Alert Threshold**: > 0.5% (5 errors per 1000)

### Data Completeness SLO
- **Target**: 100% of rule definitions persisted
- **Measurement**: Successful persist operations / Total persist attempts
- **Alert Threshold**: < 99.9%

---

## Core Metrics

### Execution Metrics

```
loco_rule_execution_total
  Type: Counter
  Labels: rule_id, rule_name, status (success/failure)
  Description: Total number of rule executions
  Business Value: Throughput, usage patterns

loco_rule_execution_duration_seconds
  Type: Histogram
  Labels: rule_id, rule_name
  Buckets: 0.1, 0.5, 1, 2, 5, 10, 30, 60
  Description: Time taken to execute rules
  Business Value: Performance, SLO compliance

loco_rule_execution_errors_total
  Type: Counter
  Labels: rule_id, error_type
  Description: Total failed rule executions
  Business Value: Reliability, troubleshooting

loco_workflow_execution_total
  Type: Counter
  Labels: workflow_id, status
  Description: Total workflow executions
  Business Value: Automation activity

loco_workflow_success_rate
  Type: Gauge
  Range: 0-1 (0-100%)
  Description: Success rate of all workflows
  Business Value: Operational reliability
```

### System Metrics

```
process_resident_memory_bytes
  Type: Gauge
  Description: Memory usage of service process
  Alert: > 2GB (degraded), > 3GB (critical)

process_cpu_seconds_total
  Type: Counter
  Description: Total CPU time used by process
  Derived Metric: CPU utilization percentage

process_virtual_memory_bytes
  Type: Gauge
  Description: Virtual memory usage
  Alert: > 5GB (investigate)

process_open_file_descriptors
  Type: Gauge
  Description: Number of open file handles
  Alert: > 900 (resource exhaustion risk)
```

### Disk and Storage Metrics

```
disk_free_bytes
  Type: Gauge
  Description: Available disk space on working directory
  Alert: < 500MB (critical), < 2GB (warning)

disk_used_percent
  Type: Gauge
  Range: 0-100
  Description: Percentage of disk used
  Alert: > 85% (planning needed), > 95% (critical)

backup_latest_duration_seconds
  Type: Gauge
  Description: Time taken for latest backup operation
  Alert: > 600s (backup taking too long)

backup_success_rate
  Type: Gauge
  Description: Success rate of backup operations
  Alert: < 95% (investigate backup issues)
```

### Reliability Metrics

```
circuit_breaker_trips_total
  Type: Counter
  Labels: service_name
  Description: Times circuit breaker has opened
  Business Value: Failure detection, cascading failure prevention

circuit_breaker_duration_seconds
  Type: Gauge
  Labels: service_name
  Description: How long circuit breaker has been open
  Alert: > 300s (service degradation)

retry_attempts_total
  Type: Counter
  Labels: operation, status
  Description: Retry attempts and outcomes
  Business Value: Resilience effectiveness

timeout_exceeded_total
  Type: Counter
  Labels: operation
  Description: Operations exceeding timeout threshold
  Alert: > 1% of operations (performance issue)
```

### Observability Metrics

```
loco_engine_running
  Type: Gauge
  Values: 0 (down), 1 (up)
  Description: Service health status
  Alert: = 0 (critical)

loco_active_rules
  Type: Gauge
  Description: Number of currently executing rules
  Threshold: Should not exceed MaxConcurrentFlows

loco_scheduled_rules_pending
  Type: Gauge
  Description: Rules waiting in execution queue
  Alert: Growing continuously (bottleneck)

telemetry_exported_spans_total
  Type: Counter
  Description: Spans exported to telemetry collector
  Business Value: Observability coverage

telemetry_dropped_spans_total
  Type: Counter
  Description: Spans not exported (buffer overflow, etc.)
  Alert: > 0 (telemetry loss)
```

---

## Business Metrics

### Volume Metrics

```
rules_total_count
  Type: Gauge
  Description: Total number of rules in system

flows_created_total
  Type: Counter
  Description: Cumulative flows created

executions_per_day
  Type: Gauge
  Description: Average daily execution count
  Useful for: Capacity planning, growth tracking

peak_concurrent_executions
  Type: Gauge
  Description: Maximum concurrent executions observed
  Alert: > 90% of MaxConcurrentFlows (capacity approaching)
```

### Operational Efficiency

```
automation_coverage_percent
  Type: Gauge
  Range: 0-100
  Description: % of intended operations automated
  Business Value: ROI measurement

rule_success_rate_percent
  Type: Gauge
  Range: 0-100
  Description: % of rule executions succeeding
  Target: > 99.9%

average_execution_time_seconds
  Type: Gauge
  Description: Mean rule execution time
  Useful for: SLA compliance, cost estimation

deployment_frequency
  Type: Gauge
  Unit: deployments/day
  Description: How often rules are updated
  Business Value: Agility measurement
```

### Cost and Resource Metrics

```
resource_utilization_percent
  Type: Gauge
  Range: 0-100
  Description: Effective CPU/Memory utilization
  Alert: < 20% (over-provisioned), > 85% (under-provisioned)

cost_per_execution
  Type: Gauge
  Unit: USD (or local currency)
  Description: Estimated cost per rule execution
  Useful for: ROI calculation, pricing

data_retention_days
  Type: Gauge
  Description: How long execution history is retained
  Compliance: Determine based on regulations
```

---

## Dashboard Recommendations

### Real-Time Dashboard (Updated every 30 seconds)
- Service status (health check result)
- Active rule count
- Error rate (last 5 minutes)
- P95 latency (last 5 minutes)
- CPU and memory usage
- Disk space available

### Daily Review Dashboard
- Total executions (24 hours)
- Success rate (24 hours)
- Peak concurrent executions
- Error distribution by type
- Top 5 longest-running rules
- Backup status

### Weekly Review Dashboard
- Execution trend (7 days)
- Error rate trend
- Resource utilization trend
- Latency percentiles (P50, P95, P99)
- Backups completed successfully
- Rule changes made

### Monthly Report
- Total executions
- Success/failure rates
- Cost analysis
- Capacity utilization
- Incident summary
- SLO compliance percentage

---

## Alert Configuration

### Critical Alerts (Page on-call immediately)
```yaml
- Service Down: loco_engine_running == 0
- High Error Rate: error_rate > 1%
- Disk Full: disk_free < 100MB
- Memory Exhaustion: memory > 3GB
- Circuit Breaker Open: duration > 10 minutes
```

### High Priority Alerts (Wake up if overnight)
```yaml
- Elevated Error Rate: error_rate > 0.5%
- Low Disk Space: disk_free < 500MB
- High P99 Latency: p99_latency > 10s
- Backup Failure: backup_success_rate < 95%
- Memory High: memory > 2.5GB
```

### Medium Priority Alerts (Team chat notification)
```yaml
- Moderate Error Rate: error_rate > 0.2%
- Moderate Latency: p95_latency > 5s
- Elevated CPU: cpu_percent > 80%
- Capacity Approaching: concurrent > 80% of max
```

### Low Priority Alerts (Daily summary)
```yaml
- Disk Usage: disk_used > 80%
- Sub-optimal Latency: p50_latency > 1s
- Retry Activity: retry_rate > 5%
- Slow Rules: execution > 30s
```

---

## Logging Levels and Patterns

### Log Levels by Component

```
SimpleLightEngine:
  DEBUG: Rule creation, configuration changes
  INFO: Rule execution start/complete
  WARN: Retry attempts, degradation
  ERROR: Execution failures, exceptions

WorkflowExecutionEngine:
  DEBUG: Step transitions, variable changes
  INFO: Workflow completion, step status
  WARN: Compensation actions, circuit breaker
  ERROR: Unhandled exceptions, catastrophic failures

HealthCheckService:
  INFO: Check results, status changes
  WARN: Degraded health, threshold crossing
  ERROR: Check execution failures
```

### Searchable Log Fields

```
timestamp: ISO 8601 UTC
level: DEBUG|INFO|WARN|ERROR
component: service/module name
correlation_id: unique request identifier
user_id: user who initiated
rule_id: rule being executed
workflow_id: workflow identifier
duration_ms: operation time
status: success|failure
error_code: standardized error code
error_message: human-readable message
```

---

## Data Retention Policy

### Metrics Retention
- Raw metrics: 7 days (resolution: 1 minute)
- 1-hour aggregates: 30 days
- Daily aggregates: 1 year
- Monthly summary: Indefinite

### Log Retention
- Application logs: 30 days (hot storage)
- Archived logs: 1 year (cold storage)
- Audit logs: 7 years (compliance)
- Error logs: 1 year

### Execution History
- Detailed executions: 7 days
- Summary executions: 30 days
- Monthly reports: 1 year

---

## Performance Benchmarks

### Expected Performance (Under Normal Load)

```
Rule Execution:
  P50 latency: 100-500ms
  P95 latency: 500ms-2s
  P99 latency: 2-5s
  Throughput: 100-1000 rules/second

Workflow Execution:
  Simple (1-5 steps): 1-5 seconds
  Complex (10+ steps): 10-30 seconds
  Success rate: 99.9%+

Resource Usage:
  Memory: 200-500MB baseline
  CPU: 5-20% under load
  Disk I/O: < 100MB/s peaks
```

### Stress Test Results

```
At MaxConcurrentFlows = 100:
  Avg latency: 2-3 seconds
  Memory usage: 1.5-2GB
  CPU usage: 60-80%
  Error rate: < 0.1%
```

---

## Compliance and Audit

### Metrics for Compliance
- Execution audit trail (who, what, when)
- Configuration change history
- Access logs with user identification
- Backup verification records
- Security scan results

### Audit Log Fields
- timestamp (ISO 8601 UTC)
- event_type (execution, configuration, access, etc.)
- user_id (person making change)
- resource_id (rule, workflow, etc.)
- change_description
- before_value / after_value (for changes)
- ip_address / source

---

## Implementation Checklist

- [ ] Configure Prometheus or equivalent metrics collection
- [ ] Set up Grafana or equivalent dashboarding
- [ ] Configure AlertManager or equivalent for alerting
- [ ] Implement ELK stack or equivalent for log aggregation
- [ ] Set up OpenTelemetry exporter
- [ ] Configure retention policies in all systems
- [ ] Train operations team on dashboard reading
- [ ] Establish on-call rotation with runbooks
- [ ] Schedule weekly metric reviews
- [ ] Document custom metrics added
- [ ] Set SLO targets with stakeholders
- [ ] Establish baseline metrics from day 1

---

Generated: 2025-10-31
Last Updated: 2025-10-31
