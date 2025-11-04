using Microsoft.Extensions.Logging;

namespace Loco.Core.Validation;

/// <summary>
/// Example: Workflow request validator
/// </summary>
public class WorkflowRequestValidator : AbstractValidator<WorkflowRequest>
{
    public WorkflowRequestValidator(ILogger<WorkflowRequestValidator> logger) : base(logger)
    {
    }

    protected override void InitializeRules()
    {
        // Name validation: required, length between 3-100
        RuleForRequired(w => w.Name, "Name", "Workflow name is required");
        RuleForLength(w => w.Name, "Name", 3, 100, "Workflow name must be between 3 and 100 characters");

        // Description validation: optional, max 500 characters
        RuleForLength(w => w.Description, "Description", 0, 500, "Description must not exceed 500 characters");

        // Owner email validation
        RuleForRequired(w => w.OwnerEmail, "OwnerEmail", "Owner email is required");
        RuleForEmail(w => w.OwnerEmail, "OwnerEmail", "Owner email must be a valid email address");

        // Version validation: required, semantic versioning pattern
        RuleForRequired(w => w.Version, "Version");
        RuleForPattern(w => w.Version, "Version", @"^\d+\.\d+\.\d+", "Version must follow semantic versioning (e.g., 1.0.0)");

        // Custom rule: ensure at least one step
        RuleForCustom(w => w.Steps?.Any() ?? false, "Steps", "Workflow must have at least one step");
    }
}

/// <summary>
/// Example: Workflow step request validator
/// </summary>
public class WorkflowStepValidator : AbstractValidator<WorkflowStep>
{
    public WorkflowStepValidator(ILogger<WorkflowStepValidator> logger) : base(logger)
    {
    }

    protected override void InitializeRules()
    {
        // Step name validation
        RuleForRequired(s => s.Name, "Name", "Step name is required");
        RuleForLength(s => s.Name, "Name", 1, 100, "Step name must be between 1 and 100 characters");

        // Action type validation
        RuleForRequired(s => s.ActionType, "ActionType", "Action type is required");

        // Timeout validation: custom rule
        RuleForCustom(
            s => s.TimeoutSeconds <= 0 || s.TimeoutSeconds <= 3600,
            "TimeoutSeconds",
            "Timeout must be between 1 and 3600 seconds");
    }
}

/// <summary>
/// Example: User registration validator
/// </summary>
public class UserRegistrationValidator : AbstractValidator<UserRegistration>
{
    public UserRegistrationValidator(ILogger<UserRegistrationValidator> logger) : base(logger)
    {
    }

    protected override void InitializeRules()
    {
        // Email validation
        RuleForRequired(u => u.Email, "Email");
        RuleForEmail(u => u.Email, "Email");

        // Username validation
        RuleForRequired(u => u.Username, "Username");
        RuleForLength(u => u.Username, "Username", 3, 50);
        RuleForPattern(u => u.Username, "Username", @"^[a-zA-Z0-9_-]+$",
            "Username must contain only letters, numbers, underscores, and hyphens");

        // Password validation: strong password requirements
        RuleForRequired(u => u.Password, "Password");
        RuleForLength(u => u.Password, "Password", 12, 128,
            "Password must be between 12 and 128 characters");

        // Custom rule: password complexity
        RuleForCustom(u => ValidatePasswordStrength(u.Password), "Password",
            "Password must contain uppercase, lowercase, numbers, and special characters");

        // Confirm password
        RuleForRequired(u => u.ConfirmPassword, "ConfirmPassword");
        RuleForCustom(u => u.Password == u.ConfirmPassword, "ConfirmPassword",
            "Passwords do not match");
    }

    private bool ValidatePasswordStrength(string? password)
    {
        if (string.IsNullOrEmpty(password)) return false;

        var hasUpperCase = password.Any(c => char.IsUpper(c));
        var hasLowerCase = password.Any(c => char.IsLower(c));
        var hasDigit = password.Any(c => char.IsDigit(c));
        var hasSpecialChar = password.Any(c => !char.IsLetterOrDigit(c));

        return hasUpperCase && hasLowerCase && hasDigit && hasSpecialChar;
    }
}

/// <summary>
/// Example: API key request validator
/// </summary>
public class ApiKeyRequestValidator : AbstractValidator<ApiKeyRequest>
{
    public ApiKeyRequestValidator(ILogger<ApiKeyRequestValidator> logger) : base(logger)
    {
    }

    protected override void InitializeRules()
    {
        // Name validation
        RuleForRequired(k => k.Name, "Name");
        RuleForLength(k => k.Name, "Name", 1, 100);

        // Scopes validation
        RuleForCustom(k => k.Scopes?.Any() ?? false, "Scopes",
            "At least one scope must be specified");

        // Expiration validation
        RuleForCustom(k => !k.ExpirationDate.HasValue || k.ExpirationDate > DateTime.UtcNow,
            "ExpirationDate",
            "Expiration date must be in the future");
    }
}

/// <summary>
/// Example: Job request validator
/// </summary>
public class JobRequestValidator : AbstractValidator<JobRequest>
{
    public JobRequestValidator(ILogger<JobRequestValidator> logger) : base(logger)
    {
    }

    protected override void InitializeRules()
    {
        // Job name validation
        RuleForRequired(j => j.Name, "Name");
        RuleForLength(j => j.Name, "Name", 1, 200);

        // Cron expression validation (for recurring jobs)
        RuleForCustom(j => string.IsNullOrEmpty(j.CronExpression) || IsCronExpressionValid(j.CronExpression),
            "CronExpression",
            "Invalid cron expression format");

        // Retry count validation
        RuleForCustom(j => j.RetryCount >= 0 && j.RetryCount <= 10,
            "RetryCount",
            "Retry count must be between 0 and 10");

        // Timeout validation
        RuleForCustom(j => j.TimeoutMinutes > 0 && j.TimeoutMinutes <= 1440,
            "TimeoutMinutes",
            "Timeout must be between 1 minute and 24 hours");
    }

    private bool IsCronExpressionValid(string cron)
    {
        try
        {
            // Basic validation: cron expressions should have 5 or 6 parts
            var parts = cron.Trim().Split(' ');
            return parts.Length is 5 or 6;
        }
        catch
        {
            return false;
        }
    }
}

// ==================== Example Domain Classes ====================

/// <summary>
/// Workflow request model
/// </summary>
public class WorkflowRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? OwnerEmail { get; set; }
    public string? Version { get; set; }
    public List<WorkflowStep>? Steps { get; set; }
}

/// <summary>
/// Workflow step model
/// </summary>
public class WorkflowStep
{
    public string? Name { get; set; }
    public string? ActionType { get; set; }
    public int TimeoutSeconds { get; set; } = 300;
}

/// <summary>
/// User registration model
/// </summary>
public class UserRegistration
{
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ConfirmPassword { get; set; }
}

/// <summary>
/// API key request model
/// </summary>
public class ApiKeyRequest
{
    public string? Name { get; set; }
    public List<string>? Scopes { get; set; }
    public DateTime? ExpirationDate { get; set; }
}

/// <summary>
/// Job request model
/// </summary>
public class JobRequest
{
    public string? Name { get; set; }
    public string? CronExpression { get; set; }
    public int RetryCount { get; set; } = 3;
    public int TimeoutMinutes { get; set; } = 30;
}
