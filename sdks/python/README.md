# Loco Python SDK

Enterprise-grade asynchronous workflow automation client library for Python.

[![Python Version](https://img.shields.io/badge/python-3.8%2B-blue)](https://www.python.org)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/loco-automation/python-sdk)

## Features

- 🚀 **Async/Await Support**: Full async support using `httpx` and `asyncio`
- 🔐 **Multiple Auth Methods**: API Keys, JWT tokens, username/password
- 🔄 **Automatic Retries**: Exponential backoff retry logic for resilience
- 📊 **Type Hints**: Full type annotations for better IDE support
- 🛡️ **Error Handling**: Structured exception hierarchy
- 📝 **Correlation Tracking**: Built-in request correlation IDs
- ⚡ **HTTP/2 Support**: Modern HTTP/2 protocol support
- 🎯 **Workflow Orchestration**: Complete workflow management API

## Installation

```bash
# Basic installation
pip install loco-client

# With scheduler support
pip install loco-client[scheduler]

# With Celery support
pip install loco-client[celery]

# Development installation
pip install -e ".[dev]"
```

## Quick Start

### Basic Usage (Async)

```python
import asyncio
from loco_client import LocoClient

async def main():
    # Create client with API key
    async with LocoClient(
        "https://api.loco.io",
        api_key="loco_sk_xxxxxx"
    ) as client:
        # List workflows
        workflows = await client.list_workflows()
        print(f"Found {workflows['total']} workflows")

        # Get specific workflow
        workflow = await client.get_workflow("workflow-1")
        print(f"Workflow: {workflow['name']}")

        # Execute workflow
        execution = await client.execute_workflow(
            "workflow-1",
            input={"invoice_id": "INV-001"}
        )
        print(f"Execution started: {execution['executionId']}")

        # Wait for completion
        result = await client.wait_for_execution(
            execution['executionId'],
            timeout=300
        )
        print(f"Execution result: {result}")

# Run
asyncio.run(main())
```

### Using Synchronous Wrapper

```python
import asyncio
from loco_client import create_client

# Create client
client = create_client("https://api.loco.io", api_key="loco_sk_xxxxxx")

async def main():
    async with client:
        workflows = await client.list_workflows()
        return workflows

result = asyncio.run(main())
```

### Authentication Methods

#### API Key (Recommended)

```python
client = LocoClient(
    "https://api.loco.io",
    api_key="loco_sk_xxxxxx"
)
```

#### Username/Password

```python
async with LocoClient(
    "https://api.loco.io",
    username="admin",
    password="secret"
) as client:
    await client.authenticate()
    workflows = await client.list_workflows()
```

#### Pre-generated JWT Token

```python
client = LocoClient(
    "https://api.loco.io",
    jwt_token="eyJhbGc..."
)
```

## API Methods

### Workflow Management

```python
# List workflows (paginated)
workflows = await client.list_workflows(page=1, page_size=20)

# Get single workflow
workflow = await client.get_workflow("workflow-id")

# Create new workflow
new_workflow = await client.create_workflow(
    name="Process Invoice",
    description="Auto-processes invoices",
    nodes=[...],   # the same node graph the visual editor saves
    edges=[...],
)

# Update workflow
updated = await client.update_workflow(
    "workflow-id",
    name="Updated Name"
)

# Delete workflow
await client.delete_workflow("workflow-id")
```

### Workflow Execution

```python
# Execute workflow (async)
execution = await client.execute_workflow(
    "workflow-id",
    input={"key": "value"},   # initial variables, available to every node
)

# Plan a run without invoking any connector
planned = await client.execute_workflow("workflow-id", dry_run=True)

# Get execution status
# Executions are addressed globally by id, not nested under a workflow
status = await client.get_execution_status(execution['executionId'])

# Wait for execution (blocking)
result = await client.wait_for_execution(
    "execution-id",
    timeout=300,
    poll_interval=1.0
)
```

### Health & Diagnostics

```python
# Check API health
health = await client.health_check()
print(f"API Status: {health['status']}")
```

## Error Handling

```python
from loco_client import (
    LocoException,
    LocoAuthError,
    LocoNotFoundError,
    LocoValidationError,
    RateLimitError,
)

try:
    workflow = await client.get_workflow("nonexistent")
except LocoNotFoundError:
    print("Workflow not found")
except LocoAuthError:
    print("Authentication failed")
except RateLimitError:
    print("Rate limit exceeded, retrying...")
except LocoException as e:
    print(f"Error: {e}")
```

## Advanced Examples

### Batch Workflow Execution

```python
async def execute_batch(client, workflow_id, items):
    """Execute workflow for each item in batch"""
    tasks = [
        client.execute_workflow(
            workflow_id,
            input={"item": item}
        )
        for item in items
    ]
    return await asyncio.gather(*tasks)

# Usage
items = [1, 2, 3, 4, 5]
executions = await execute_batch(client, "workflow-1", items)
```

### Polling with Timeout

```python
async def execute_with_timeout(client, workflow_id, variables, timeout=60):
    """Execute and wait with timeout"""
    execution = await client.execute_workflow(
        workflow_id,
        input=variables
    )

    try:
        result = await client.wait_for_execution(
            execution['executionId'],
            timeout=timeout
        )
        return result
    except asyncio.TimeoutError:
        print(f"Execution timed out after {timeout}s")
        return None
```

### Scheduled Workflow Execution (APScheduler)

```python
from apscheduler.schedulers.asyncio import AsyncIOScheduler
from datetime import datetime
import asyncio

async def scheduled_execution():
    """Execute workflow on schedule"""
    scheduler = AsyncIOScheduler()

    async def job():
        async with LocoClient("https://api.loco.io", api_key="loco_sk_xxx") as client:
            await client.execute_workflow("daily-report-workflow")

    # Schedule daily at 9 AM
    scheduler.add_job(job, "cron", hour=9, minute=0)
    scheduler.start()

    # Keep running
    try:
        await asyncio.Event().wait()
    except KeyboardInterrupt:
        scheduler.shutdown()

asyncio.run(scheduled_execution())
```

### Celery Integration

```python
from celery import Celery
from loco_client import LocoClient
import asyncio

app = Celery('loco_tasks')

@app.task
def execute_workflow_task(workflow_id, variables=None):
    """Celery task for workflow execution"""
    async def _execute():
        async with LocoClient("https://api.loco.io", api_key="loco_sk_xxx") as client:
            result = await client.execute_workflow(workflow_id, input=variables)
            return result

    return asyncio.run(_execute())

# Usage
result = execute_workflow_task.delay("workflow-1", {"key": "value"})
```

## Configuration

### Environment Variables

```bash
# API Configuration
LOCO_API_URL=https://api.loco.io
LOCO_API_KEY=loco_sk_xxxxxx
LOCO_USERNAME=admin
LOCO_PASSWORD=secret

# Client Configuration
LOCO_TIMEOUT=30
LOCO_MAX_RETRIES=3
LOCO_VERIFY_SSL=true
```

### Custom Configuration

```python
client = LocoClient(
    base_url="https://api.loco.io",
    api_key="loco_sk_xxxxxx",
    timeout=60.0,           # Request timeout
    max_retries=5,          # Retry attempts
    verify_ssl=True         # Verify SSL certificates
)
```

## Performance Considerations

### Connection Pooling

The `httpx.AsyncClient` automatically handles connection pooling:

```python
# Reuse client for multiple requests (better performance)
async with LocoClient("https://api.loco.io", api_key="loco_sk_xxx") as client:
    for workflow_id in workflow_ids:
        workflow = await client.get_workflow(workflow_id)
        # Connections are reused
```

### Concurrent Requests

```python
# Execute multiple workflows concurrently
workflows = ["workflow-1", "workflow-2", "workflow-3"]
tasks = [client.execute_workflow(wf) for wf in workflows]
results = await asyncio.gather(*tasks)
```

## Logging

```python
import logging

# Enable debug logging
logging.basicConfig(level=logging.DEBUG)
logger = logging.getLogger("loco_client")
logger.setLevel(logging.DEBUG)

# Logs will show correlation IDs for request tracing
```

## Testing

```python
import pytest
from unittest.mock import AsyncMock, patch
from loco_client import LocoClient

@pytest.mark.asyncio
async def test_list_workflows():
    with patch('loco_client.httpx.AsyncClient') as mock_client:
        mock_client.return_value.__aenter__.return_value.request = AsyncMock(
            return_value=Mock(status_code=200, json=lambda: {"items": []})
        )

        async with LocoClient("https://api.loco.io", api_key="test") as client:
            result = await client.list_workflows()
            assert result == {"items": []}
```

## Requirements

- Python 3.8+
- httpx 0.24.0+
- pyjwt 2.8.0+

## Development

```bash
# Clone repository
git clone https://github.com/loco-automation/python-sdk.git
cd python-sdk

# Install with dev dependencies
pip install -e ".[dev]"

# Run tests
pytest

# Run linter
ruff check .

# Format code
black .

# Type checking
mypy loco_client.py
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
- 📚 Docs: https://docs.loco.io/sdk/python
- 🐛 Issues: https://github.com/loco-automation/python-sdk/issues
- 💬 Discussions: https://github.com/loco-automation/python-sdk/discussions

## Changelog

### 1.0.0 (2025-01-01)
- Initial release
- Full async/await support
- Multiple authentication methods
- Workflow management API
- Automatic retry logic
- Type hints and error handling

---

Made with ❤️ by the Loco Team
