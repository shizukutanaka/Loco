# Apache JMeter Performance Testing Guide for Loco

## Overview
JMeter distributed load testing configuration for comprehensive performance validation across multiple scenarios and load levels.

## Prerequisites
- Apache JMeter 5.6+ installed
- 2+ load generator machines for distributed testing
- Target Loco API running and accessible
- Sufficient network bandwidth (10+ Mbps recommended)

## Installation

### Local Machine (Controller)
```bash
# Download JMeter
wget https://archive.apache.org/dist/jmeter/binaries/apache-jmeter-5.6.2.tgz
tar -xzf apache-jmeter-5.6.2.tgz
cd apache-jmeter-5.6.2

# Start JMeter GUI
./bin/jmeter.sh
```

### Load Generator Machines (Agents)
```bash
# Copy JMeter to all load generators
scp -r apache-jmeter-5.6.2 user@loadgen1:/opt/
scp -r apache-jmeter-5.6.2 user@loadgen2:/opt/

# Start JMeter in server mode (remote)
./bin/jmeter-server -Dserver.rmi.localport=50000
# Output: Server started on port 50000
```

## Test Scenarios

### Scenario 1: Baseline Health Check (Smoke Test)
**Purpose**: Verify API is responsive before load testing
**Configuration**:
- Virtual Users: 1
- Ramp-up: 10 seconds
- Duration: 1 minute
- Expected Response Time: < 200ms
- Expected Success Rate: 100%

```
Thread Group
├── Constant Timer (1000ms)
├── HTTP Sampler - Health Check GET /health
│   └── Response Assertion: status == 200
└── View Results Tree (for debugging)
```

### Scenario 2: Normal Load (Sustained)
**Purpose**: Simulate typical production load
**Configuration**:
- Virtual Users: 50
- Ramp-up: 5 minutes
- Duration: 30 minutes
- Think time: 2-5 seconds between requests
- Expected Response Time P95: < 500ms
- Expected Success Rate: > 99%

```
Thread Group (50 users)
├── Constant Timer (Random 2-5 seconds)
├── HTTP Sampler - Create Workflow
│   ├── POST /api/v1/workflows
│   ├── Request body (JSON)
│   └── Assertion: status == 201
├── HTTP Sampler - Execute Workflow
│   ├── POST /api/v1/workflows/${workflowId}/execute
│   └── Assertion: status == 200
├── HTTP Sampler - Get Metrics
│   ├── GET /api/v1/workflows/${workflowId}/metrics
│   └── Assertion: status == 200
└── Listeners
    ├── Summary Report
    ├── Response Time Graph
    └── Active Threads Over Time
```

### Scenario 3: Ramp-up Load (Peak Hours)
**Purpose**: Test system behavior during traffic spikes
**Configuration**:
- Virtual Users: 200
- Ramp-up: 10 minutes (linear increase)
- Duration: 20 minutes sustained
- Expected Response Time P95: < 1000ms
- Expected Success Rate: > 98%

```
Thread Group (200 users, 10min ramp-up)
├── Gaussian Random Timer (1-8 seconds)
├── Workflow CRUD Operations
│   ├── Create (30% of requests)
│   ├── Read (40% of requests)
│   ├── Update (15% of requests)
│   └── Delete (15% of requests)
└── Assertions (response time SLA)
```

### Scenario 4: Stress Test (Breaking Point)
**Purpose**: Find system breaking point
**Configuration**:
- Virtual Users: 500 → 1000 → 2000
- Ramp-up: 5 minutes per level
- Duration: 15 minutes per level
- Target: Identify response time degradation > 2000ms
- Expected Failure Rate: < 5%

```
Thread Group (500-2000 users, stepped ramp-up)
├── Concurrent requests to multiple endpoints
├── Error Rate Listener
├── Response Time Distribution
└── System Resource Monitor
```

### Scenario 5: Endurance Test (Stability)
**Purpose**: Verify system stability under sustained load
**Configuration**:
- Virtual Users: 100
- Duration: 8 hours
- Monitor for: Memory leaks, connection pool exhaustion
- Expected Stable Response Times: Yes

```
Thread Group (100 users, 8 hours)
├── Workflow execution cycles
├── Metrics sampling (every 5 minutes)
├── GC monitoring
└── Memory usage tracking
```

### Scenario 6: Spike Test (Sudden Traffic)
**Purpose**: Test handling of sudden traffic spikes
**Configuration**:
- Base Load: 50 users
- Spike: Jump to 500 users instantly
- Spike Duration: 2 minutes
- Recovery: Return to 50 users
- Expected Behavior: Quick recovery, no data loss

```
Scenario
├── Phase 1: Ramp to 50 users (5 min)
├── Phase 2: Spike to 500 users (2 min)
├── Phase 3: Return to 50 users (5 min)
└── Phase 4: Verify data consistency
```

### Scenario 7: Concurrent Execution Test
**Purpose**: Stress test workflow execution with high concurrency
**Configuration**:
- Virtual Users: 100
- Concurrent Requests: 10 per user
- Expected Throughput: > 500 executions/sec
- Expected P99 Latency: < 2000ms

```
Thread Group
├── Synchronize Timer (barrier at start)
├── Parallel requests
│   ├── Execute workflow (primary)
│   ├── Get execution status (secondary)
│   └── Update metrics (tertiary)
└── Assertions
```

## Running Tests

### GUI Mode (Development/Debugging)
```bash
./bin/jmeter.sh -t loco-performance-test-plan.jmx
```

### CLI Mode (Production/CI-CD)
```bash
# Single machine test
./bin/jmeter.sh \
  -n \
  -t loco-performance-test-plan.jmx \
  -l results.jtl \
  -j jmeter.log \
  -Jhost=localhost \
  -Jport=5000 \
  -Jthreads=100 \
  -Jrampup=300 \
  -Jduration=1800

# Distributed test (multiple load generators)
./bin/jmeter.sh \
  -n \
  -t loco-performance-test-plan.jmx \
  -R loadgen1:50000,loadgen2:50000,loadgen3:50000 \
  -l results-distributed.jtl \
  -j jmeter-distributed.log
```

## Custom Variables

Define these in JMeter Test Plan:

```
BASE_URL = http://localhost:5000
API_TOKEN = <JWT token>
THREADS = 50
RAMPUP = 300
DURATION = 1800
THINK_TIME_MIN = 1000
THINK_TIME_MAX = 5000
ASSERT_RESPONSE_TIME = 500
ASSERT_SUCCESS_RATE = 99.5
```

## Assertions

### Response Assertions
```
- Response Code: 200, 201 (as appropriate)
- Response Contains: "success", "data", "id"
- Response Size: 100-50000 bytes
```

### Timing Assertions
```
- Response Time <= 500ms (normal load)
- Response Time <= 2000ms (stress test)
- Connect Time <= 100ms
```

### Throughput Assertions
```
- Throughput >= 100 requests/sec (minimum)
- Success Rate >= 99% (normal), 95% (stress)
- Error Rate <= 1% (normal), 5% (stress)
```

## Results Analysis

### Metrics to Monitor
```
1. Response Time Distribution
   - Min, Max, Mean, Median
   - P50, P75, P90, P95, P99
   - Standard Deviation

2. Throughput
   - Requests/sec
   - Bytes/sec
   - Errors/sec

3. Success Rate
   - Total Requests
   - Successful Responses
   - Failed Responses
   - Error Types

4. Resource Utilization
   - Active Threads
   - Thread Wait Time
   - Connection Pool Status

5. Business Metrics
   - Workflows Created/Executed
   - Average Execution Time
   - Success Rate by Workflow Type
```

### Generating Reports

#### HTML Report
```bash
./bin/jmeter.sh \
  -g results.jtl \
  -o html-report \
  -e
```

#### Summary Report
```bash
./bin/JMeterPluginsCMD.sh \
  --generate-png graph.png \
  --input-jtl results.jtl \
  --plugin-type ResponseTimesPercentiles
```

## Performance Baselines

### Expected Results (2-core, 4GB VM)

| Scenario | Users | Throughput | P95 Response | Success Rate |
|----------|-------|-----------|--------------|--------------|
| Baseline | 1 | 50 req/s | 100ms | 100% |
| Normal | 50 | 1500 req/s | 400ms | 99%+ |
| Ramp-up | 200 | 4000 req/s | 800ms | 98%+ |
| Stress | 500 | 6000 req/s | 1500ms | 95%+ |
| Spike | 500 | 4500 req/s | 2000ms | 90%+ |
| Endurance | 100 | 2000 req/s | 500ms | 99%+ |

## Performance Optimization Targets

### If P95 > 500ms (Normal Load)
1. Check database query performance
2. Verify connection pool settings
3. Monitor memory/GC pauses
4. Check network latency between regions
5. Profile API hot paths

### If Success Rate < 99%
1. Increase thread pool size
2. Check error logs for exceptions
3. Verify database capacity
4. Check file descriptor limits
5. Monitor CPU throttling

### If Throughput < Expected
1. Increase number of load generators
2. Check JMeter JVM heap size
3. Verify API server isn't CPU-bound
4. Check network saturation
5. Profile database query performance

## Continuous Integration

### GitHub Actions Integration
```yaml
name: Performance Test
on: [push]
jobs:
  performance-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Run JMeter Tests
        run: |
          wget https://archive.apache.org/dist/jmeter/binaries/apache-jmeter-5.6.2.tgz
          tar -xzf apache-jmeter-5.6.2.tgz
          ./apache-jmeter-5.6.2/bin/jmeter.sh \
            -n -t tests/jmeter/loco-performance-test-plan.jmx \
            -l results.jtl
      - name: Upload Results
        uses: actions/upload-artifact@v2
        with:
          name: jmeter-results
          path: results.jtl
```

## Troubleshooting

### Issue: "Connection refused" errors
**Solution**: Verify API is running and accessible from load generator machines

### Issue: High memory usage on load generators
**Solution**: Increase JMeter JVM heap size
```bash
export JVM_ARGS="-Xmx4G -Xms4G"
./bin/jmeter.sh ...
```

### Issue: Uneven load distribution across agents
**Solution**: Ensure all agents have identical thread group configuration

### Issue: Results show "Out of memory" exceptions
**Solution**:
1. Reduce concurrent threads
2. Add more load generator machines
3. Increase server memory
4. Implement request pooling

## Advanced Configuration

### Custom Timer Script
```groovy
// Gaussian distribution: 2-5 seconds
long delay = (long) (Math.random() * 3000 + 2000);
// Occasional spike: 10% of requests wait 10-20 seconds
if (Math.random() < 0.1) {
    delay = (long) (Math.random() * 10000 + 10000);
}
return delay;
```

### Connection Pooling
```
HTTP Request Defaults
├── Connection Timeout: 5000ms
├── Response Timeout: 30000ms
├── Implementation: HTTPClient4
└── Connection Pool Settings:
    ├── Max Connections: 100
    ├── Max Per Route: 50
```

## Resources

- [Apache JMeter Official Documentation](https://jmeter.apache.org/usermanual/index.html)
- [JMeter Best Practices](https://jmeter.apache.org/usermanual/best-practices.html)
- [JMeter Plugins](https://jmeter-plugins.org/)
- [Performance Testing with JMeter](https://www.baeldung.com/jmeter)
