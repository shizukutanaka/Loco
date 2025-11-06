# Loco Workflow Examples

This directory contains example workflow definitions and code examples that demonstrate how to use Loco for automation tasks, from simple workflows to complex enterprise scenarios.

## 📚 Documentation Index

- **[Advanced Scenarios](ADVANCED_SCENARIOS.md)** - Real-world composite workflows combining multiple templates and integrations
- **[Complete Automation Example](CompleteAutomationExample.cs)** - Full integration demonstration
- **Basic Examples** (below) - Simple workflow JSON definitions

## 🚀 Advanced Scenarios (NEW)

**See [ADVANCED_SCENARIOS.md](ADVANCED_SCENARIOS.md) for complete documentation**

### E-Commerce Order Processing Pipeline
Complete order fulfillment automation using Stripe, Redis, Database, SendGrid, and Slack.
- **Business Value**: 10x faster processing, zero duplicates
- **Throughput**: 2,000 orders/sec
- **Code**: [AdvancedWorkflowScenarios.cs](AdvancedWorkflowScenarios.cs)

### SaaS Customer Lifecycle Automation
Fully automated customer journey from signup to active user.
- **Business Value**: 2 hours → 30 seconds per customer
- **Integrations**: Stripe, SendGrid, Slack, Google Sheets, Redis
- **Code**: [AdvancedWorkflowScenarios.cs](AdvancedWorkflowScenarios.cs)

### DevOps Incident Response Pipeline
Automated incident detection, classification, and response.
- **Business Value**: 15 min → 2 min MTTR
- **Integrations**: GitHub, Telegram, Slack, AWS S3
- **Code**: [AdvancedWorkflowScenarios.cs](AdvancedWorkflowScenarios.cs)

### Marketing Campaign Automation
AI-powered real-time campaign monitoring and engagement.
- **Business Value**: 20% → 95% mention coverage, 3x engagement ROI
- **Integrations**: Twitter, OpenAI GPT-4, Discord, Google Sheets
- **Code**: [AdvancedWorkflowScenarios.cs](AdvancedWorkflowScenarios.cs)

## 📋 Basic Workflow Examples

### 1. Backup Workflow (`backup-workflow.json`)

**Purpose**: Daily system backup automation with cloud upload verification

**Features**:
- Scheduled daily backup at 2 AM UTC
- Disk space validation before backup
- Database dumping
- Backup compression using gzip
- Cloud storage (S3) upload with retry logic
- Integrity verification using SHA256 checksums
- Email notifications on success and Slack alerts on failure

**Use Cases**:
- Automated database backups
- System file backups
- Compliance and disaster recovery
- Cloud archival

**Configuration**:
```json
{
  "DB_HOST": "your-database-host",
  "SLACK_WEBHOOK_URL": "your-slack-webhook"
}
```

### 2. File Organization Workflow (`file-organization-workflow.json`)

**Purpose**: Automatic file organization and cleanup

**Features**:
- Scheduled cleanup every 6 hours
- Real-time file system event triggers
- File organization by type (documents, images, videos, archives)
- Old file cleanup (>30 days)
- Report generation
- Desktop notification on completion

**Use Cases**:
- Automatic downloads folder organization
- Document management
- Media library organization
- Disk space optimization

**Configuration**:
```bash
HOME=/home/user  # User's home directory path
```

### 3. Health Monitoring Workflow (`health-monitoring-workflow.json`)

**Purpose**: Continuous system health monitoring with auto-remediation

**Features**:
- 5-minute health checks
- CPU and memory usage monitoring
- Service health verification (Database, Cache, API)
- Anomaly detection
- Automatic service remediation
- Multi-channel alerting (Slack, PagerDuty, Email)
- Detailed health reporting

**Use Cases**:
- Production environment monitoring
- Service uptime management
- Performance anomaly detection
- Automated incident response

**Configuration**:
```json
{
  "SLACK_WEBHOOK_URL": "your-slack-webhook",
  "PAGERDUTY_SERVICE_ID": "your-service-id"
}
```

## Running Examples

### Using the REST API

```bash
# Create a workflow from JSON file
curl -X POST http://localhost:5000/api/v1/workflows \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-api-key" \
  -d @backup-workflow.json

# Execute the workflow
curl -X POST http://localhost:5000/api/v1/workflows/backup-workflow/execute \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-api-key" \
  -d '{"parameters": {}}'

# Check execution status
curl http://localhost:5000/api/v1/workflows/backup-workflow/executions/{execution-id} \
  -H "X-Api-Key: your-api-key"
```

### Using Python SDK

```python
from loco_client import LocoClient
import json

client = LocoClient("http://localhost:5000", api_key="your-api-key")

# Load workflow definition
with open("backup-workflow.json") as f:
    workflow_def = json.load(f)

# Create workflow
workflow = await client.workflows.create(
    name=workflow_def["name"],
    description=workflow_def["description"],
    steps=workflow_def["steps"]
)

# Execute workflow
result = await client.workflows.execute(workflow.id, {})

# Wait for completion
execution = await client.workflows.wait_for_execution(
    workflow.id,
    result.execution_id,
    timeout=600000
)

print(f"Execution status: {execution.status}")
```

### Using TypeScript SDK

```typescript
import { LocoClient } from "loco-client";
import * as fs from "fs";

const client = new LocoClient("http://localhost:5000", {
  apiKey: "your-api-key"
});

// Load workflow definition
const workflowDef = JSON.parse(fs.readFileSync("backup-workflow.json", "utf-8"));

// Create workflow
const workflow = await client.workflows.create(
  workflowDef.name,
  workflowDef.description,
  workflowDef.steps
);

// Execute workflow
const result = await client.workflows.execute(workflow.id, {});

// Wait for completion
const execution = await client.workflows.waitForExecution(
  workflow.id,
  result.execution_id,
  600000
);

console.log(`Execution status: ${execution.status}`);
```

## Workflow Structure

Each workflow JSON follows this structure:

```json
{
  "id": "unique-workflow-id",
  "name": "Human Readable Name",
  "description": "What this workflow does",
  "triggers": [
    // Define when the workflow runs
  ],
  "steps": [
    // Define the sequence of actions
  ],
  "constraints": [
    // Define when the workflow should NOT run
  ],
  "error_handlers": [
    // Define how to handle errors
  ],
  "notifications": [
    // Define alerting and reporting
  ],
  "enabled": true,
  "version": "1.0.0"
}
```

## Creating Your Own Workflows

### Step 1: Define Triggers

```json
"triggers": [
  {
    "type": "schedule",
    "config": {
      "cron": "0 2 * * *",
      "timezone": "UTC"
    }
  }
]
```

### Step 2: Define Steps

```json
"steps": [
  {
    "id": "step-1",
    "order": 1,
    "name": "Step Name",
    "type": "action_type",
    "action_name": "action_name",
    "configuration": {
      // Action-specific config
    },
    "retry_policy": {
      "max_retries": 3,
      "backoff_multiplier": 2
    }
  }
]
```

### Step 3: Add Error Handling

```json
"error_handlers": [
  {
    "step_id": "step-1",
    "error_type": "network",
    "action": "retry"
  }
]
```

### Step 4: Configure Notifications

```json
"notifications": [
  {
    "event": "completion",
    "channel": "email",
    "recipients": ["admin@example.com"]
  }
]
```

## Best Practices

1. **Start Simple**: Begin with basic workflows and add complexity gradually
2. **Use Constraints**: Add time windows and resource constraints to prevent issues
3. **Plan Retries**: Configure appropriate retry policies for network operations
4. **Monitor Execution**: Set up notifications to track workflow execution
5. **Test Dry-Run**: Use dry-run mode to test workflows before enabling
6. **Document Parameters**: Add descriptions of all configurable parameters
7. **Version Workflows**: Update the version field when making changes
8. **Clean Up**: Archive completed workflows to maintain readability

## Workflow Triggers

- **Schedule**: CRON-based scheduling
- **File System Event**: Trigger on file creation/modification
- **Metric Threshold**: Trigger when metrics exceed thresholds
- **Manual**: Trigger via API
- **Webhook**: Trigger via HTTP webhook
- **Event**: Trigger on system events

## Action Types

- **file**: File operations (copy, move, organize, compress)
- **system**: System operations (check resources, reboot, restart services)
- **database**: Database operations (backup, restore, query)
- **cloud**: Cloud operations (S3, Azure, GCP)
- **http**: HTTP requests and API calls
- **notification**: Send alerts and notifications
- **conditional**: Conditional logic and branching
- **remediation**: Auto-healing and recovery

## Troubleshooting

### Workflow Not Triggering
- Check that the trigger is properly configured
- Verify timezone settings for scheduled triggers
- Check workflow enabled status

### Steps Failing
- Review error handlers in the workflow
- Check action configuration for missing parameters
- Review execution logs for detailed error messages
- Test individual steps with dry-run mode

### Performance Issues
- Reduce monitoring frequency
- Increase step timeouts for long-running operations
- Add resource constraints to prevent overload

## Support

For more information, see the main [README.md](../README.md) and [CONTRIBUTING.md](../CONTRIBUTING.md).
