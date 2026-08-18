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
// A workflow is a node graph. The load test used to send
// { definition: { steps } }, which WorkflowCreateRequest has no property for,
// so it created empty workflows - and every execution after it then failed
// validation rather than exercising the engine.
//
// Built from engine built-ins only (trigger, transform, log), so a load run
// invokes no connector and needs no credentials.
function sampleWorkflow(id = generateId()) {
  return {
    // `id` is ignored by POST /workflows (the server assigns one) and used by
    // POST /workflows/validate, which takes a whole StoredWorkflow.
    id,
    name: `Load-Test-Workflow-${id}`,
    description: 'Load testing workflow',
    nodes: [
      {
        id: 'n1',
        type: 'trigger',
        position: { x: 0, y: 0 },
        data: { label: 'Start', config: {} },
      },
      {
        id: 'n2',
        type: 'transform',
        position: { x: 240, y: 0 },
        data: { label: 'Shape', config: { json: '{"ok":true}' } },
      },
      {
        id: 'n3',
        type: 'log',
        position: { x: 480, y: 0 },
        data: { label: 'Record', config: { message: 'Workflow executed' } },
      },
    ],
    edges: [
      { id: 'e1', source: 'n1', target: 'n2' },
      { id: 'e2', source: 'n2', target: 'n3' },
    ],
    metadata: {},
  };
}

function createWorkflow() {
  group('Create Workflow', () => {
    const workflowId = generateId();
    const payload = JSON.stringify(sampleWorkflow(workflowId));

    const res = http.post(`${BASE_URL}/api/v1/workflows`, payload, {
      headers: getAuthHeader(),
    });

    // Responses are enveloped: { success, data }. Reading `id` at the top
    // level always found undefined, so this check could never pass.
    const success = check(res, {
      'create workflow status is 201': (r) => r.status === 201,
      'response includes workflow ID': (r) => r.json('data.id') !== undefined,
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

// Scenario 4: Validate a workflow without saving or running it
//
// This used to GET /api/v1/workflows/{id}/metrics, a route the API has never
// exposed - so the scenario measured the latency of a 404 and its "status is
// 200" check failed on every iteration. Validation is the real read-heavy
// endpoint, and it exercises the mapper and validator without touching a
// connector.
function validateWorkflow(workflow) {
  group('Validate Workflow', () => {
    const res = http.post(
      `${BASE_URL}/api/v1/workflows/validate`,
      JSON.stringify(workflow),
      { headers: getAuthHeader() }
    );

    check(res, {
      'validate status is 200': (r) => r.status === 200,
      'validation reports a verdict': (r) => r.json('data.valid') !== undefined,
      'validate response time < 500ms': (r) => r.timings.duration < 500,
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
      'response includes workflows array': (r) =>
        Array.isArray(r.json('data.workflows')),
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

  // Validate
  if (Math.random() < 0.3) {
    validateWorkflow(sampleWorkflow());
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
