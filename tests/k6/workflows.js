// Phase 4: k6 Load Testing - Workflow Scenarios
// Real-world load testing for Loco workflow engine
// Run: k6 run tests/k6/workflows.js
// Run with custom stages: k6 run -e STAGE=production tests/k6/workflows.js

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend, Counter, Gauge } from 'k6/metrics';

// Configuration
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const STAGE = __ENV.STAGE || 'staging'; // staging, production
const API_TOKEN = __ENV.API_TOKEN || ''; // JWT token for auth

// Custom Metrics
const errorRate = new Rate('errors');
const workflowCreationTime = new Trend('workflow_creation_time');
const workflowExecutionTime = new Trend('workflow_execution_time');
const apiResponseTime = new Trend('api_response_time');
const activeExecutions = new Gauge('active_executions');
const totalCreatedWorkflows = new Counter('total_workflows_created');
const totalExecutedWorkflows = new Counter('total_workflows_executed');

// Load Stage Configuration
const stages = {
  staging: {
    vus: 10,
    duration: '5m',
    rampUp: '30s',
    rampDown: '30s',
  },
  production: {
    vus: 100,
    duration: '30m',
    rampUp: '5m',
    rampDown: '2m',
  },
  stress: {
    vus: 500,
    duration: '15m',
    rampUp: '2m',
    rampDown: '2m',
  },
};

const config = stages[STAGE] || stages.staging;

export const options = {
  stages: [
    { duration: config.rampUp, target: config.vus }, // Ramp up
    { duration: config.duration, target: config.vus }, // Stay at target
    { duration: config.rampDown, target: 0 }, // Ramp down
  ],
  thresholds: {
    'http_req_duration': ['p(95)<500', 'p(99)<2000'], // Response time thresholds
    'http_req_failed': ['rate<0.1'], // 10% error rate threshold
    'errors': ['rate<0.05'], // 5% business logic error threshold
  },
  ext: {
    loadimpact: {
      projectID: 3456110,
      name: `Loco Workflow Load Test - ${STAGE}`,
    },
  },
};

// Helper: Generate unique ID
function generateId() {
  return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
}

// Helper: Get JWT header
function getAuthHeader() {
  if (!API_TOKEN) {
    console.warn('API_TOKEN not set - requests will be unauthenticated');
  }
  return {
    'Content-Type': 'application/json',
    'Authorization': API_TOKEN ? `Bearer ${API_TOKEN}` : '',
  };
}

// Scenario 1: Health Check (baseline)
function healthCheck() {
  group('Health Check', () => {
    const res = http.get(`${BASE_URL}/health`);
    check(res, {
      'health status is 200': (r) => r.status === 200,
      'health response time < 100ms': (r) => r.timings.duration < 100,
    });
    apiResponseTime.add(res.timings.duration);
  });
}

// Scenario 2: Create Workflow
function createWorkflow() {
  group('Create Workflow', () => {
    const workflowId = generateId();
    const payload = JSON.stringify({
      name: `Load-Test-Workflow-${workflowId}`,
      description: 'Load testing workflow',
      definition: {
        version: '1.0',
        trigger: {
          type: 'manual',
        },
        steps: [
          {
            id: 'step-1',
            name: 'Send Email',
            action: 'send-email',
            parameters: {
              to: 'test@example.com',
              subject: 'Load Test Notification',
            },
          },
          {
            id: 'step-2',
            name: 'Log Event',
            action: 'log-event',
            parameters: {
              level: 'info',
              message: 'Workflow executed',
            },
          },
        ],
      },
    });

    const res = http.post(`${BASE_URL}/api/v1/workflows`, payload, {
      headers: getAuthHeader(),
    });

    const success = check(res, {
      'create workflow status is 201': (r) => r.status === 201,
      'response includes workflow ID': (r) => r.json('id') !== undefined,
    });

    if (success) {
      totalCreatedWorkflows.add(1);
      workflowCreationTime.add(res.timings.duration);
    } else {
      errorRate.add(1);
    }

    apiResponseTime.add(res.timings.duration);
    return res.json('id');
  });
}

// Scenario 3: Execute Workflow
function executeWorkflow(workflowId) {
  group('Execute Workflow', () => {
    const executionPayload = JSON.stringify({
      input: {
        message: 'Test execution',
      },
    });

    const res = http.post(
      `${BASE_URL}/api/v1/workflows/${workflowId}/execute`,
      executionPayload,
      {
        headers: getAuthHeader(),
      }
    );

    const success = check(res, {
      'execute workflow status is 200': (r) => r.status === 200,
      'response includes execution ID': (r) => r.json('executionId') !== undefined,
      'execution started': (r) => r.json('status') === 'running',
    });

    if (success) {
      totalExecutedWorkflows.add(1);
      workflowExecutionTime.add(res.timings.duration);
    } else {
      errorRate.add(1);
    }

    apiResponseTime.add(res.timings.duration);
    return res.json('executionId');
  });
}

// Scenario 4: Get Workflow Metrics
function getMetrics(workflowId) {
  group('Get Metrics', () => {
    const res = http.get(`${BASE_URL}/api/v1/workflows/${workflowId}/metrics`, {
      headers: getAuthHeader(),
    });

    check(res, {
      'get metrics status is 200': (r) => r.status === 200,
      'metrics include success rate': (r) => r.json('successRate') !== undefined,
      'metrics response time < 500ms': (r) => r.timings.duration < 500,
    });

    apiResponseTime.add(res.timings.duration);
  });
}

// Scenario 5: List Workflows with Pagination
function listWorkflows() {
  group('List Workflows', () => {
    const res = http.get(`${BASE_URL}/api/v1/workflows?page=1&pageSize=20`, {
      headers: getAuthHeader(),
    });

    check(res, {
      'list workflows status is 200': (r) => r.status === 200,
      'response includes workflows array': (r) => Array.isArray(r.json('data')),
    });

    apiResponseTime.add(res.timings.duration);
  });
}

// Scenario 6: Concurrent Workflow Execution (spike test)
function concurrentExecutions(workflowId, count = 10) {
  group('Concurrent Executions', () => {
    const requests = [];

    // Queue all requests
    for (let i = 0; i < count; i++) {
      const payload = JSON.stringify({
        input: {
          index: i,
          timestamp: Date.now(),
        },
      });

      requests.push({
        method: 'POST',
        url: `${BASE_URL}/api/v1/workflows/${workflowId}/execute`,
        body: payload,
        params: {
          headers: getAuthHeader(),
        },
      });
    }

    // Execute all concurrently
    const responses = http.batch(requests);

    responses.forEach((res) => {
      check(res, {
        'concurrent execution status is 200': (r) => r.status === 200,
      });
      apiResponseTime.add(res.timings.duration);
    });

    const successCount = responses.filter((r) => r.status === 200).length;
    activeExecutions.add(successCount);
    totalExecutedWorkflows.add(successCount);
  });
}

// Scenario 7: Update Workflow
function updateWorkflow(workflowId) {
  group('Update Workflow', () => {
    const payload = JSON.stringify({
      name: `Updated-Workflow-${generateId()}`,
      description: 'Updated via load test',
    });

    const res = http.patch(
      `${BASE_URL}/api/v1/workflows/${workflowId}`,
      payload,
      {
        headers: getAuthHeader(),
      }
    );

    check(res, {
      'update workflow status is 200': (r) => r.status === 200,
    });

    apiResponseTime.add(res.timings.duration);
  });
}

// Scenario 8: Delete Workflow
function deleteWorkflow(workflowId) {
  group('Delete Workflow', () => {
    const res = http.del(
      `${BASE_URL}/api/v1/workflows/${workflowId}`,
      {
        headers: getAuthHeader(),
      }
    );

    check(res, {
      'delete workflow status is 200': (r) => r.status === 200,
    });

    apiResponseTime.add(res.timings.duration);
  });
}

// Main Test Execution
export default function () {
  // Always check health
  healthCheck();
  sleep(1);

  // Create workflow
  const workflowId = createWorkflow();
  sleep(1);

  // Execute workflow
  executeWorkflow(workflowId);
  sleep(1);

  // Get metrics
  if (Math.random() < 0.3) {
    getMetrics(workflowId);
    sleep(1);
  }

  // List workflows
  if (Math.random() < 0.2) {
    listWorkflows();
    sleep(1);
  }

  // Concurrent executions (spike)
  if (Math.random() < 0.1) {
    concurrentExecutions(workflowId, 5);
    sleep(2);
  }

  // Update workflow (10% chance)
  if (Math.random() < 0.1) {
    updateWorkflow(workflowId);
    sleep(1);
  }

  // Delete workflow (5% chance at end)
  if (Math.random() < 0.05) {
    deleteWorkflow(workflowId);
  }

  // Random sleep between iterations
  sleep(Math.random() * 5 + 1);
}

// Setup: Initialize test
export function setup() {
  console.log(`Starting ${STAGE} load test`);
  console.log(`VUs: ${config.vus}, Duration: ${config.duration}`);
  return {};
}

// Teardown: Cleanup
export function teardown(data) {
  console.log('Load test completed');
}
