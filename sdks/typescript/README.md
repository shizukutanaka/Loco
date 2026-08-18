# Loco TypeScript/JavaScript SDK

Enterprise-grade workflow automation client library for TypeScript and JavaScript.

[![npm version](https://img.shields.io/npm/v/loco-client.svg)](https://www.npmjs.com/package/loco-client)
[![Node.js Version](https://img.shields.io/badge/node-%3E%3D14.0.0-brightgreen)](https://nodejs.org)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.0%2B-blue)](https://www.typescriptlang.org)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

## Features

- ✅ **Promise-based Async/Await**: Full async support using native fetch
- 📘 **Full Type Safety**: Complete TypeScript type definitions
- 🔐 **Multiple Auth Methods**: API Keys, JWT tokens, username/password
- 🔄 **Automatic Retries**: Exponential backoff retry logic
- 📊 **Type Definitions**: Better IDE support and autocomplete
- 🔗 **Correlation Tracking**: Request correlation IDs
- ⚡ **Lightweight**: No external dependencies (except uuid)
- 🎯 **Workflow Orchestration**: Complete workflow management API

## Installation

```bash
npm install loco-client
# or
yarn add loco-client
# or
pnpm add loco-client
```

## Quick Start

### TypeScript

```typescript
import { LocoClient } from "loco-client";

const client = new LocoClient("https://api.loco.io", {
  username: "admin", password: "…",
});

// List workflows
const workflows = await client.workflows.list();
console.log(`Found ${workflows.total} workflows`);

// Get specific workflow
const workflow = await client.workflows.get("workflow-1");
console.log(`Workflow: ${workflow.name}`);

// Execute workflow
const execution = await client.workflows.execute("workflow-1", {
  invoice_id: "INV-001",
});
console.log(`Execution started: ${execution.executionId}`);

// Wait for completion. Executions are addressed by their own id.
const result = await client.workflows.waitForExecution(
  execution.executionId,
  300000 // 5 minute timeout
);
console.log(`Execution result:`, result);
```

### JavaScript (CommonJS)

```javascript
const { LocoClient } = require("loco-client");

const client = new LocoClient("https://api.loco.io", {
  username: "admin", password: "…",
});

// Use async/await
(async () => {
  const workflows = await client.workflows.list();
  console.log(workflows);
})();
```

### JavaScript (ES Modules)

```javascript
import LocoClient from "loco-client";

const client = new LocoClient("https://api.loco.io", {
  username: "admin", password: "…",
});

const workflows = await client.workflows.list();
console.log(workflows);
```

## Authentication

### API Key (Recommended)

```typescript
const client = new LocoClient("https://api.loco.io", {
  username: "admin", password: "…",
});
```

### Username/Password

```typescript
const client = new LocoClient("https://api.loco.io", {
  username: "admin",
  password: "secret",
});

// Authenticate
await client.authenticate();

// Now make requests
const workflows = await client.workflows.list();
```

### JWT Token

```typescript
const client = new LocoClient("https://api.loco.io", {
  jwtToken: "eyJhbGc...",
});
```

## API Reference

### Workflow Management

```typescript
// List workflows (paginated)
const response = await client.workflows.list(1, 20);
// { workflows: [...], total: 100, page: 1, pageSize: 20 }

// Get single workflow
const workflow = await client.workflows.get("workflow-id");

// Create workflow
// A workflow is a node graph, the same shape the visual editor saves.
const newWorkflow = await client.workflows.create(
  "Process Invoice",
  "Auto-processes invoices",
  [
    {
      id: "n1",
      type: "trigger",
      position: { x: 0, y: 0 },
      data: { label: "Start" },
    },
    {
      id: "n2",
      type: "action",
      position: { x: 240, y: 0 },
      data: {
        label: "Fetch invoice",
        integration: "http",
        credentialId: "conn-1",
        config: { action: "get", parameters: { url: "https://example.test" } },
      },
    },
  ],
  [{ id: "e1", source: "n1", target: "n2" }]
);

// Update workflow
const updated = await client.workflows.update("workflow-id", {
  name: "Updated Name",
  description: "Updated description",
});

// Delete workflow
await client.workflows.delete("workflow-id");
```

### Workflow Execution

```typescript
// Execute workflow asynchronously
// The second argument is the run's initial variables, available to every node.
const execution = await client.workflows.execute("workflow-id", {
  invoice_id: "INV-001",
  amount: 1500.00,
});
// { executionId: "...", status: "running", startedAt: "..." }

// Plan a run without invoking any connector
const planned = await client.workflows.execute("workflow-id", {}, true);

// Get execution status
const status = await client.workflows.getExecutionStatus("execution-id");

// Wait for execution to complete
const result = await client.workflows.waitForExecution(
  "execution-id",
  300000, // timeout in ms
  1000    // poll interval in ms
);

// Stop a run that is still going
await client.workflows.cancelExecution("execution-id");
```

### Health & Diagnostics

```typescript
// Check API health
const health = await client.healthCheck();
console.log(`API Status: ${health.status}`);
```

## Error Handling

```typescript
import {
  LocoException,
  LocoAuthError,
  LocoNotFoundError,
  LocoValidationError,
  RateLimitError,
} from "loco-client";

try {
  const workflow = await client.workflows.get("nonexistent");
} catch (error) {
  if (error instanceof LocoNotFoundError) {
    console.error("Workflow not found");
  } else if (error instanceof LocoAuthError) {
    console.error("Authentication failed");
  } else if (error instanceof RateLimitError) {
    console.error("Rate limit exceeded, retrying...");
  } else if (error instanceof LocoException) {
    console.error(`Loco error: ${error.message}`);
  } else {
    console.error("Unknown error:", error);
  }
}
```

## Advanced Examples

### Batch Workflow Execution

```typescript
async function executeBatch(
  client: LocoClient,
  workflowId: string,
  items: unknown[]
) {
  const promises = items.map((item) =>
    client.workflows.execute(workflowId, { item })
  );
  return Promise.all(promises);
}

// Usage
const items = [1, 2, 3, 4, 5];
const executions = await executeBatch(client, "workflow-1", items);
```

### Concurrent Execution with Error Handling

```typescript
async function executeWithErrorHandling(
  client: LocoClient,
  workflows: string[]
) {
  const results = await Promise.allSettled(
    workflows.map((wf) => client.workflows.execute(wf))
  );

  return results.map((result, index) => ({
    workflow: workflows[index],
    status: result.status,
    data: result.status === "fulfilled" ? result.value : result.reason,
  }));
}
```

### Polling with Custom Logic

```typescript
async function executeWithCustomPolling(
  client: LocoClient,
  workflowId: string,
  maxWaitTime: number = 60000
) {
  const execution = await client.workflows.execute(workflowId);
  const startTime = Date.now();

  while (Date.now() - startTime < maxWaitTime) {
    const status = await client.workflows.getExecutionStatus(
      execution.executionId
    );

    console.log(`Status: ${status.status}`);

    if (["completed", "failed", "cancelled"].includes(status.status)) {
      return status;
    }

    // Wait before polling again
    await new Promise((r) => setTimeout(r, 500));
  }

  throw new Error("Execution timeout");
}
```

### Environment-based Configuration

```typescript
const client = new LocoClient(
  process.env.LOCO_API_URL || "https://api.loco.io",
  {
    jwtToken: process.env.LOCO_TOKEN,
    timeout: parseInt(process.env.LOCO_TIMEOUT || "30000"),
    maxRetries: parseInt(process.env.LOCO_MAX_RETRIES || "3"),
    verifySsl: process.env.NODE_ENV === "production",
  }
);
```

## Configuration

### Constructor Options

```typescript
interface LocoClientConfig {
  // Authentication. The API speaks JWT bearer only - it registers no
  // API-key scheme - so either let the client fetch a token, or supply one.
  username?: string;         // Username for token auth
  password?: string;         // Password for token auth
  jwtToken?: string;         // A token you already hold

  // Client settings
  timeout?: number;          // Request timeout in ms (default: 30000)
  maxRetries?: number;       // Retry attempts (default: 3)
  verifySsl?: boolean;       // Verify SSL certs (default: true)
  headers?: Record<string, string>; // Custom headers
}
```

### Environment Variables

```bash
# API Configuration
LOCO_API_URL=https://api.loco.io
LOCO_API_KEY=loco_sk_xxxxxx

# Client Settings
LOCO_TIMEOUT=30000
LOCO_MAX_RETRIES=3
```

## Performance Considerations

### Connection Reuse

The SDK uses native fetch, which automatically manages connection pooling:

```typescript
// Create client once and reuse
const client = new LocoClient("https://api.loco.io", { jwtToken: "..." });

// Reuse for multiple requests
for (const workflowId of workflowIds) {
  const workflow = await client.workflows.get(workflowId);
  // Connection pool reused
}
```

### Concurrent Requests

```typescript
// Execute multiple workflows concurrently
const workflowIds = ["workflow-1", "workflow-2", "workflow-3"];
const results = await Promise.all(
  workflowIds.map((id) => client.workflows.execute(id))
);
```

### Retry Strategy

The SDK automatically retries on network errors with exponential backoff:

```typescript
const client = new LocoClient("https://api.loco.io", {
  username: "admin", password: "…",
  maxRetries: 5, // Customize retry count
  timeout: 60000, // 60 second timeout
});
```

## Testing

```typescript
import { LocoClient } from "loco-client";

describe("Loco Client", () => {
  it("should list workflows", async () => {
    const client = new LocoClient("https://api.loco.io", {
      jwtToken: "test-token",
    });

    // Mock fetch if needed
    global.fetch = jest.fn(() =>
      Promise.resolve({
        ok: true,
        // The client unwraps the API's envelope, so mock the whole shape.
        json: () =>
          Promise.resolve({
            success: true,
            data: { workflows: [], total: 0, page: 1, pageSize: 20 },
          }),
      })
    );

    const result = await client.workflows.list();
    expect(result.workflows).toEqual([]);
  });
});
```

## Browser Support

The SDK works in modern browsers that support:
- Fetch API
- Promise
- Async/Await
- ES2020+

For older browsers, use a transpiler like Babel.

## Node.js Support

- **Minimum**: Node.js 14.0.0+
- **Recommended**: Node.js 18.0.0+ (native fetch support)
- **Tested**: Node.js 18, 20, 22

## Requirements

- Node.js 14+ (or modern browser)
- UUID library (included)
- No other external dependencies

## Development

```bash
# Install dependencies
npm install

# Build TypeScript
npm run build

# Watch for changes
npm run watch

# Run tests
npm test

# Run linter
npm run lint

# Format code
npm run format

# Type check
npm run type-check
```

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Submit a pull request

## License

MIT License - see [LICENSE](LICENSE) file

## Support

- 📧 Email: support@loco.local
- 📚 Docs: https://docs.loco.io/sdk/typescript
- 🐛 Issues: https://github.com/loco-automation/typescript-sdk/issues
- 💬 Discussions: https://github.com/loco-automation/typescript-sdk/discussions

## Changelog

### 1.0.0 (2025-01-01)

- Initial release
- Full async/await support
- Multiple authentication methods
- Workflow management API
- Automatic retry logic
- Complete type definitions
- Error handling

---

Made with ❤️ by the Loco Team
