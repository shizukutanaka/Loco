# Loco Workflow Guide

## Overview

Loco provides a powerful and lightweight workflow automation system with support for various action types, statistics tracking, and easy-to-use CLI commands.

## Quick Start

### Run a workflow
```bash
loco workflow workflows/hello-world.json
loco wf workflows/hello-world.json  # Short alias
```

### List all workflows
```bash
loco workflow list
```

### Show workflow information
```bash
loco workflow info workflows/hello-world.json
```

### View execution statistics
```bash
loco workflow stats
```

### Validate without executing (Dry-run)
```bash
loco workflow workflows/hello-world.json --dry-run
```

## Workflow Structure

```json
{
  "id": "my-workflow",
  "name": "My Workflow",
  "description": "Optional description",
  "timeoutSeconds": 300,
  "continueOnError": false,
  "steps": [
    {
      "id": "step1",
      "name": "Step Name",
      "type": "log",
      "message": "Hello World"
    }
  ]
}
```

## Supported Action Types

### 1. Log Action
Outputs a message to the console.

```json
{
  "id": "log1",
  "name": "Log message",
  "type": "log",
  "message": "This is a log message"
}
```

### 2. Delay Action
Pauses execution for a specified duration.

```json
{
  "id": "delay1",
  "name": "Wait 2 seconds",
  "type": "delay",
  "duration": "00:00:02"
}
```

### 3. File Action
Performs file operations (check existence, copy files).

```json
{
  "id": "file1",
  "name": "Check file exists",
  "type": "file",
  "path": "C:\\path\\to\\file.txt"
}
```

```json
{
  "id": "file2",
  "name": "Copy file",
  "type": "file",
  "source": "source.txt",
  "destination": "backup/source.txt"
}
```

### 4. Process Action
Executes system commands.

```json
{
  "id": "proc1",
  "name": "Run command",
  "type": "process",
  "command": "echo Hello World"
}
```

**Output Capture:**
```json
{
  "id": "proc2",
  "name": "Get system info",
  "type": "process",
  "command": "echo %computername%",
  "saveOutput": "hostname"
}
```
When `saveOutput` is specified, the command output is saved to a context variable and can be accessed as `${ctx:hostname}` in subsequent steps.

### 5. HTTP Action
Makes HTTP requests to APIs.

```json
{
  "id": "http1",
  "name": "Check API",
  "type": "http",
  "url": "https://api.example.com/health",
  "method": "GET"
}
```

**Response Capture:**
```json
{
  "id": "http2",
  "name": "Get API data",
  "type": "http",
  "url": "https://api.github.com",
  "method": "GET",
  "saveOutput": "api_response"
}
```
When `saveOutput` is specified for HTTP actions, the response body is saved to a context variable.

## Advanced Features

### Variable Substitution

Workflows support powerful variable substitution using `${variable}` syntax.

#### Built-in Variables
```json
{
  "id": "log1",
  "name": "Log system info",
  "type": "log",
  "message": "Executed by ${user} on ${machine} at ${datetime}"
}
```

Available built-in variables:
- `${date}` - Current date (yyyy-MM-dd)
- `${time}` - Current time (HH:mm:ss)
- `${datetime}` - Current date and time
- `${timestamp}` - Unix timestamp
- `${user}` - Current username
- `${machine}` - Machine name
- `${workdir}` - Current working directory

#### Custom Date/Time Formats
```json
{
  "id": "log1",
  "name": "Custom format",
  "type": "log",
  "message": "Backup created on ${date:yyyy-MM-dd} at ${time:HH-mm-ss}"
}
```

#### Environment Variables
```json
{
  "id": "log1",
  "name": "Show environment",
  "type": "log",
  "message": "Path: ${env:PATH}, TMP: ${env:TEMP}"
}
```

#### Workflow Variables (Command-line)
```bash
loco workflow backup.json --var source=C:\data --var dest=C:\backup
```

```json
{
  "id": "copy1",
  "name": "Copy files",
  "type": "process",
  "command": "xcopy \"${var:source}\" \"${var:dest}\" /E /I /Y"
}
```

#### Context Variables (Step Output)
Context variables are created by steps that use `saveOutput`:

```json
{
  "steps": [
    {
      "id": "get-hostname",
      "name": "Get hostname",
      "type": "process",
      "command": "echo %computername%",
      "saveOutput": "hostname"
    },
    {
      "id": "show-hostname",
      "name": "Display hostname",
      "type": "log",
      "message": "Computer name is: ${ctx:hostname}"
    }
  ]
}
```

**Automatic Context Variables:**
- `${ctx:stepid_exitcode}` - Exit code from process actions
- `${ctx:stepid_statuscode}` - HTTP status code from HTTP actions

### Output Capture

Process and HTTP actions can save their output to context variables for use in subsequent steps.

**Process Action Output:**
```json
{
  "id": "check1",
  "name": "Check system",
  "type": "process",
  "command": "echo %date%",
  "saveOutput": "current_date"
}
```

**HTTP Action Response:**
```json
{
  "id": "api1",
  "name": "Get API data",
  "type": "http",
  "url": "https://api.example.com/data",
  "method": "GET",
  "saveOutput": "api_data"
}
```

### Conditional Execution

Execute steps based on previous step results or variable values:

**Run If (execute only when condition is true):**
```json
{
  "id": "notify",
  "name": "Notify on success",
  "type": "log",
  "message": "Deployment succeeded!",
  "runIf": "deploy_result"
}
```

**Skip If (skip when condition is true):**
```json
{
  "id": "rollback",
  "name": "Rollback on failure",
  "type": "log",
  "message": "Rolling back changes",
  "skipIf": "deploy_success"
}
```

**Supported Conditions:**

1. **Variable Existence:**
   ```json
   "runIf": "variable_name"
   ```
   Runs if the variable exists and is not null.

2. **Equality Check:**
   ```json
   "runIf": "status==success"
   ```
   Runs if `status` variable equals "success".

3. **Inequality Check:**
   ```json
   "runIf": "error_count!=0"
   ```
   Runs if `error_count` is not equal to 0.

4. **Greater Than:**
   ```json
   "runIf": "file_size>1000"
   ```
   Runs if `file_size` is greater than 1000.

5. **Less Than:**
   ```json
   "runIf": "retry_count<5"
   ```
   Runs if `retry_count` is less than 5.

6. **Success Check:**
   ```json
   "runIf": "step_id_success"
   ```
   Runs if previous step with `step_id` succeeded.

7. **Exit Code Check:**
   ```json
   "runIf": "check_disk_exitcode==0"
   ```
   Runs if process step `check_disk` returned exit code 0.

**Practical Example:**
```json
{
  "steps": [
    {
      "id": "check-disk",
      "name": "Check disk space",
      "type": "process",
      "command": "check-disk.bat"
    },
    {
      "id": "proceed",
      "name": "Proceed if disk OK",
      "type": "log",
      "message": "Disk space is sufficient",
      "runIf": "check-disk_exitcode==0"
    },
    {
      "id": "alert",
      "name": "Alert if disk full",
      "type": "log",
      "message": "WARNING: Low disk space!",
      "skipIf": "check-disk_exitcode==0"
    }
  ]
}
```

### Error Handling
```json
{
  "id": "step1",
  "name": "Continue on error",
  "type": "process",
  "command": "may-fail-command",
  "continueOnError": true
}
```

### Retry Configuration

Automatically retry failed steps with exponential backoff:

```json
{
  "id": "step1",
  "name": "Retry on failure",
  "type": "http",
  "url": "https://api.example.com",
  "method": "GET",
  "retryCount": 3,
  "retryDelay": "00:00:02"
}
```

**Features:**
- `retryCount`: Number of retry attempts (0 = no retry)
- `retryDelay`: Initial delay between retries (e.g., "00:00:02" for 2 seconds)
- **Exponential backoff**: Delay doubles with each retry (2s, 4s, 8s, etc.)
- Works with all action types (process, http, file, etc.)

**Example with 3 retries:**
- Attempt 1: Execute immediately
- Attempt 2: Wait 2s, retry
- Attempt 3: Wait 4s, retry
- Attempt 4: Wait 8s, retry

### Timeout Configuration

Set maximum execution time for individual steps:

```json
{
  "id": "step1",
  "name": "API call with timeout",
  "type": "http",
  "url": "https://slow-api.example.com",
  "method": "GET",
  "timeoutSeconds": 10,
  "continueOnError": true
}
```

**Features:**
- `timeoutSeconds`: Maximum time allowed for step execution
- Step fails if it exceeds timeout
- Combines with retry for robust error handling

### Combined Retry and Timeout

```json
{
  "id": "robust-step",
  "name": "Robust API call",
  "type": "http",
  "url": "https://api.example.com/data",
  "method": "GET",
  "retryCount": 3,
  "retryDelay": "00:00:01",
  "timeoutSeconds": 15,
  "continueOnError": true,
  "saveOutput": "api_data"
}
```

This step will:
1. Try up to 4 times (1 initial + 3 retries)
2. Timeout after 15 seconds per attempt
3. Wait 1s, 2s, 4s between retries (exponential backoff)
4. Continue workflow even if all attempts fail
5. Save successful response to `api_data` variable

## Workflow Templates

Pre-built templates are available in `workflows/templates/`:

### Basic Templates
- **daily-health-check.json** - System health monitoring
- **backup-files.json** - File backup with verification
- **api-monitoring.json** - API endpoint monitoring

### Variable Examples
- **backup-with-variables.json** - Flexible backup using workflow variables
- **system-info.json** - System information report using built-in variables

### Output Capture Examples
- **system-check-output.json** - System checks with output capture
- **api-health-check.json** - API health check with response capture
- **disk-check-with-output.json** - Disk space check with output capture

### Retry and Timeout Examples
- **retry-example.json** - Demonstrates retry logic and timeout configuration
- **robust-api-check.json** - Production-ready API health check with comprehensive error handling

### Conditional Execution Examples
- **conditional-example.json** - Demonstrates all conditional execution features (runIf, skipIf, comparisons)
- **smart-deployment.json** - Intelligent deployment workflow with conditional steps based on environment checks

## Statistics Tracking

Workflow execution statistics are automatically tracked and persisted to `workflow-stats.json`:

- Total executions
- Success/failure counts
- Success rate
- Average, min, max execution time
- Last execution timestamp

View statistics with:
```bash
loco workflow stats
```

## Execution Reports

View detailed execution reports with the `--report` option:

```bash
loco workflow workflows/my-workflow.json --report
```

**Report includes:**
- Workflow name and ID
- Start and end times
- Total duration
- Execution status (SUCCESS/FAILED)
- Step statistics (total, executed, skipped, failed)
- Individual step details (when available)

**Example report:**
```
╔════════════════════════════════════════════════════════════════════╗
║              WORKFLOW EXECUTION REPORT                             ║
╠════════════════════════════════════════════════════════════════════╣
║ Workflow: Robust API Health Check                                  ║
║ ID: robust-api-check                                               ║
╠════════════════════════════════════════════════════════════════════╣
║ Start Time:  2025-10-18 22:04:46                                ║
║ End Time:    2025-10-18 22:04:52                                ║
║ Duration:    5.88s                                                 ║
╠════════════════════════════════════════════════════════════════════╣
║ Status:      SUCCESS ✓                                                ║
║ Total Steps: 7                                                        ║
║ Executed:    7                                                        ║
║ Skipped:     0                                                        ║
║ Failed:      0                                                        ║
╚════════════════════════════════════════════════════════════════════╝
```

## Command Reference

| Command | Description |
|---------|-------------|
| `workflow list` | List all available workflows |
| `workflow stats` | Show execution statistics |
| `workflow info <file>` | Show workflow details |
| `workflow <file>` | Execute a workflow |
| `workflow <file> --dry-run` | Validate without executing |
| `workflow <file> --verbose` | Show detailed logs |
| `workflow <file> --report` | Show detailed execution report |
| `workflow <file> --output <file>` | Save execution summary to file |
| `workflow <file> --var name=value` | Set workflow variable |

## Best Practices

1. **Use descriptive IDs and names** for steps
2. **Add descriptions** to complex workflows
3. **Set timeouts** for long-running workflows
4. **Use continueOnError** for non-critical steps
5. **Test with --dry-run** before executing
6. **Check workflow info** before first run
7. **Monitor stats** to track reliability

## Examples

### Simple Health Check
```json
{
  "id": "health-check",
  "name": "Quick Health Check",
  "steps": [
    {
      "id": "check-disk",
      "name": "Check disk space",
      "type": "process",
      "command": "df -h"
    },
    {
      "id": "check-memory",
      "name": "Check memory",
      "type": "process",
      "command": "free -h"
    }
  ]
}
```

### API Monitoring
```json
{
  "id": "api-monitor",
  "name": "API Health Monitor",
  "timeoutSeconds": 60,
  "continueOnError": true,
  "steps": [
    {
      "id": "check-api",
      "name": "Check main API",
      "type": "http",
      "url": "https://api.myservice.com/health",
      "method": "GET",
      "retryCount": 2
    }
  ]
}
```

## Troubleshooting

### Workflow fails to load
- Check JSON syntax
- Ensure all required fields are present
- Run with `--dry-run` to validate

### Steps not executing
- Check step type is supported
- Verify all required parameters
- Check `continueOnError` settings

### Performance issues
- Set appropriate `timeoutSeconds`
- Optimize command execution
- Use `delay` between API calls

## See Also

- Run `loco help workflow` for command help
- Check `workflows/templates/` for examples
- View execution logs in console output
