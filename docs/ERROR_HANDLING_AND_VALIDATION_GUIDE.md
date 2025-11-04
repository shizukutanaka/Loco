# Error Handling and Validation Framework Guide

## Overview

This comprehensive guide covers the modern error handling and validation architecture implemented in Loco, following .NET 8+ best practices and RFC 7807 standards.

## Architecture Components

### 1. Exception Handlers (IExceptionHandler Pattern - .NET 8+)

Modern composable exception handling using the new `IExceptionHandler` interface.

**Files:**
- `src/Loco.Core/ErrorHandling/ExceptionHandlers.cs`

**Key Classes:**
- `BaseExceptionHandler<T>` - Base class for type-specific handlers
- `LocoExceptionHandler` - Handles Loco custom exceptions
- `ValidationExceptionHandler` - Handles validation exceptions
- `GlobalExceptionHandler` - Fallback for unhandled exceptions

**Usage in Program.cs:**
```csharp
// Register modern exception handlers
builder.Services.AddLocoExceptionHandlers();

// Build and configure
var app = builder.Build();
app.UseExceptionHandler();
```

### 2. Exception Filters (IExceptionFilter Pattern)

Per-controller or per-action exception handling.

**Files:**
- `src/Loco.Api/Filters/ExceptionFilters.cs`

**Key Classes:**
- `LocoExceptionFilter` - Handles LocoException and derivatives
- `ValidationExceptionFilter` - Handles ValidationException
- `ArgumentExceptionFilter` - Handles ArgumentException
- `UnauthorizedAccessFilter` - Handles UnauthorizedAccessException
- `NotFoundFilter` - Handles KeyNotFoundException
- `TimeoutExceptionFilter` - Handles TimeoutException
- `GlobalExceptionFilter` - Catches remaining exceptions

**Usage in Program.cs:**
```csharp
// Register exception filters
builder.Services
    .AddControllers()
    .AddLocoExceptionFilters();
```

**Filter Execution Order:**
1. LocoExceptionFilter (most specific)
2. ValidationExceptionFilter
3. ArgumentExceptionFilter
4. UnauthorizedAccessFilter
5. NotFoundFilter
6. TimeoutExceptionFilter
7. GlobalExceptionFilter (least specific)

### 3. Custom Exception Hierarchy

**File:** `src/Loco.Core/Exceptions/LocoException.cs`

```csharp
public class LocoException : Exception
{
    public string ErrorCode { get; set; }
    public Dictionary<string, object?> Context { get; set; }
}

// Specialized exceptions:
- WorkflowExecutionException
- WorkflowValidationException
- ActionException
- EngineException
- ResourceException
- TimeoutException
- SecurityException
- LocoConfigurationException
```

### 4. Validation Framework

Comprehensive input validation following FluentValidation patterns.

**Files:**
- `src/Loco.Core/Validation/ValidationFramework.cs` - Core framework
- `src/Loco.Core/Validation/ValidationExtensions.cs` - Integration helpers
- `src/Loco.Core/Validation/ExampleValidators.cs` - Example implementations

## Validation Framework

### Creating a Custom Validator

```csharp
using Microsoft.Extensions.Logging;
using Loco.Core.Validation;

public class WorkflowRequestValidator : AbstractValidator<WorkflowRequest>
{
    public WorkflowRequestValidator(ILogger<WorkflowRequestValidator> logger)
        : base(logger)
    {
    }

    protected override void InitializeRules()
    {
        // Required field
        RuleForRequired(w => w.Name, "Name", "Workflow name is required");

        // Length validation
        RuleForLength(w => w.Name, "Name", 3, 100,
            "Name must be between 3 and 100 characters");

        // Email validation
        RuleForEmail(w => w.OwnerEmail, "OwnerEmail");

        // Pattern validation
        RuleForPattern(w => w.Version, "Version", @"^\d+\.\d+\.\d+",
            "Version must be semantic (1.0.0)");

        // Custom validation
        RuleForCustom(
            w => w.Steps?.Any() ?? false,
            "Steps",
            "Workflow must have at least one step");
    }
}
```

### Fluent Validator API

For a more fluent experience:

```csharp
public class UserValidator : FluentValidator<User>
{
    public UserValidator(ILogger<UserValidator> logger) : base(logger)
    {
    }

    protected override void InitializeRules()
    {
        RuleFor(u => u.Email, "Email")
            .Required()
            .Email("Invalid email format");

        RuleForString(u => u.Username, "Username")
            .Required()
            .Length(3, 50)
            .Pattern(@"^[a-zA-Z0-9_-]+$", "Invalid username format");
    }
}
```

### Built-in Validation Rules

1. **RequiredRule** - Validates non-null/non-empty values
   ```csharp
   RuleForRequired(x => x.Name, "Name");
   ```

2. **LengthRule** - String length constraints
   ```csharp
   RuleForLength(x => x.Name, "Name", 3, 100);
   ```

3. **EmailRule** - Email format validation
   ```csharp
   RuleForEmail(x => x.Email, "Email");
   ```

4. **PatternRule** - Regex pattern matching
   ```csharp
   RuleForPattern(x => x.Code, "Code", @"^[A-Z]{3}\d{3}$");
   ```

5. **CustomRule** - Custom validation logic
   ```csharp
   RuleForCustom(
       x => x.StartDate < x.EndDate,
       "EndDate",
       "End date must be after start date");
   ```

### Registering Validators

**In Program.cs:**
```csharp
// Add validation framework
builder.Services.AddLocoValidation();

// Configure API validation
builder.Services
    .AddControllers()
    .ConfigureApiValidation()
    .AddLocoExceptionFilters();

// Register specific validators
var validatorFactory = builder.Services.BuildServiceProvider()
    .GetRequiredService<IValidatorFactory>();

validatorFactory.RegisterValidator(
    new WorkflowRequestValidator(loggerFactory.CreateLogger<WorkflowRequestValidator>()));
validatorFactory.RegisterValidator(
    new UserRegistrationValidator(loggerFactory.CreateLogger<UserRegistrationValidator>()));
```

## Using Validation in Controllers

### Method 1: Using Validator Factory

```csharp
[ApiController]
[Route("api/[controller]")]
public class WorkflowsController : ControllerBase
{
    private readonly IValidatorFactory _validatorFactory;

    public WorkflowsController(IValidatorFactory validatorFactory)
    {
        _validatorFactory = validatorFactory;
    }

    [HttpPost]
    public async Task<IActionResult> CreateWorkflow(WorkflowRequest request)
    {
        // Validate request
        var result = _validatorFactory.ValidateRequest(request);

        // If we get here, validation passed
        // ... create workflow ...

        return CreatedAtAction(nameof(GetWorkflow), new { id = workflow.Id }, workflow);
    }
}
```

### Method 2: Using Async Validation

```csharp
[HttpPost("register")]
public async Task<IActionResult> Register(
    UserRegistration request,
    CancellationToken cancellationToken)
{
    // Async validation
    var result = await _validatorFactory.ValidateRequestAsync(
        request,
        cancellationToken: cancellationToken);

    // ... create user ...

    return Ok(new { message = "Registration successful" });
}
```

### Method 3: Manual Validation Handling

```csharp
[HttpPost]
public IActionResult CreateJob(JobRequest request)
{
    var validator = _validatorFactory.GetValidator<JobRequest>();
    if (validator == null)
    {
        return BadRequest("No validator configured for JobRequest");
    }

    var result = validator.Validate(request);
    if (!result.IsValid)
    {
        return BadRequest(new ValidationProblemDetails
        {
            Title = "Job Validation Failed",
            Errors = result.ToErrorDictionary(),
            Status = 400
        });
    }

    // ... create job ...

    return CreatedAtAction(nameof(GetJob), new { id = job.Id }, job);
}
```

## Error Response Formats

### RFC 7807 ProblemDetails

Standard error response format:

```json
{
  "type": "https://api.example.com/errors/validation-error",
  "title": "Validation Failed",
  "status": 400,
  "detail": "Validation failed: 2 error(s)",
  "instance": "/api/workflows",
  "traceId": "0HMVLPG1GB0J8:00000001",
  "extensions": {
    "errorCode": "VALIDATION_FAILED"
  }
}
```

### ValidationProblemDetails

For validation errors with field-level details:

```json
{
  "title": "Validation Failed",
  "status": 400,
  "detail": "Validation failed with 3 error(s)",
  "instance": "/api/users/register",
  "traceId": "0HMVLPG1GB0J8:00000002",
  "errors": {
    "Email": [
      "Email is required",
      "Email must be a valid email address"
    ],
    "Password": [
      "Password must be at least 12 characters long"
    ]
  }
}
```

### Loco Custom Exception Response

```json
{
  "title": "Workflow Execution Error",
  "detail": "Failed to execute step: SendEmail",
  "status": 500,
  "instance": "/api/workflows/exec",
  "traceId": "0HMVLPG1GB0J8:00000003",
  "extensions": {
    "context": {
      "WorkflowId": "wf-123",
      "StepId": "step-456",
      "ErrorCode": "STEP_EXECUTION_FAILED"
    }
  }
}
```

## Integration Flow

### Request Validation Pipeline

```
1. Request arrives at controller
   ↓
2. ModelState validation (ASP.NET)
   ↓
3. Controller action executes
   ↓
4. Custom validator runs (ValidateRequest)
   ↓
5. ValidationException thrown if invalid
   ↓
6. Exception Filter catches it (ValidationExceptionFilter)
   ↓
7. ProblemDetails response returned (400 Bad Request)
```

### Exception Handling Pipeline

```
1. Exception thrown
   ↓
2. IExceptionHandler matches type
   ↓
3. StatusCode determined
   ↓
4. ProblemDetails built
   ↓
5. Response written to client
```

## Best Practices

### 1. Always Validate Input

```csharp
// Good
[HttpPost]
public IActionResult Create(CreateRequest request)
{
    _validatorFactory.ValidateRequest(request);
    // ... create entity ...
}

// Avoid
[HttpPost]
public IActionResult Create(CreateRequest request)
{
    // No validation - bad!
    // ... create entity ...
}
```

### 2. Use Specific Exceptions

```csharp
// Good
throw new WorkflowValidationException("Invalid workflow configuration");

// Avoid
throw new Exception("Invalid workflow configuration");
```

### 3. Include Context in Exceptions

```csharp
// Good
throw new WorkflowExecutionException("Step failed")
{
    Context = new Dictionary<string, object?>
    {
        { "WorkflowId", workflowId },
        { "StepId", stepId },
        { "ErrorCode", "STEP_TIMEOUT" }
    }
};

// Less informative
throw new WorkflowExecutionException("Step failed");
```

### 4. Log Before Throwing (Sometimes)

```csharp
// Log important failures
_logger.LogError("Critical operation failed: {Message}", errorDetails);
throw new EngineException("Operation failed");

// Let framework log simple validation errors
throw new ValidationException("Name is required");
```

### 5. Test Error Scenarios

```csharp
[Test]
public async Task Create_WithInvalidEmail_Returns400()
{
    var request = new UserRegistration { Email = "invalid" };

    var response = await _client.PostAsJsonAsync("/api/users/register", request);

    Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    var content = await response.Content.ReadAsAsync<ValidationProblemDetails>();
    Assert.IsTrue(content.Errors.ContainsKey("Email"));
}
```

## Configuration Examples

### Full Program.cs Integration

```csharp
var builder = WebApplicationBuilder.CreateBuilder(args);

// Add services
builder.Services.AddLocoValidation();
builder.Services.AddLocoExceptionHandlers();
builder.Services
    .AddControllers()
    .ConfigureApiValidation()
    .AddLocoExceptionFilters();

// Register validators
var sp = builder.Services.BuildServiceProvider();
var factory = sp.GetRequiredService<IValidatorFactory>();
var logger = sp.GetRequiredService<ILoggerFactory>();

factory.RegisterValidator(
    new WorkflowRequestValidator(logger.CreateLogger<WorkflowRequestValidator>()));
factory.RegisterValidator(
    new UserRegistrationValidator(logger.CreateLogger<UserRegistrationValidator>()));

var app = builder.Build();

// Add middleware
app.UseExceptionHandler();
app.UseLocoValidation();
app.UseRouting();
app.MapControllers();

await app.RunAsync();
```

## Monitoring and Logging

All exceptions are automatically logged with:
- Exception type and message
- Stack trace (in development only)
- HTTP context information
- Correlation/Trace ID
- User context (if available)

**Example Log Entry:**
```
2025-11-04T10:15:30.123Z [ERROR] Loco exception occurred:
  ErrorCode: WORKFLOW_VALIDATION_FAILED
  Message: Step configuration is invalid
  TraceId: 0HMVLPG1GB0J8:00000001
  WorkflowId: wf-abc123
```

## Summary

The error handling and validation framework provides:

✅ Modern .NET 8+ patterns (IExceptionHandler)
✅ Type-specific exception handling (filters)
✅ RFC 7807 standard responses
✅ Comprehensive validation framework
✅ Custom exception hierarchy
✅ Structured logging and tracing
✅ Development/Production differentiation
✅ Field-level validation error reporting
✅ Extensible design

This creates a robust, maintainable error handling experience for both API clients and developers.
