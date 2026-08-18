using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Loco.Api.Execution;
using Loco.Api.Middleware;
using Loco.Api.Security;
using Loco.Core.Integrations.Core;
using Loco.Core.Interfaces;
using Loco.Core.Storage;
using Loco.Core.Workflows;

var builder = WebApplication.CreateBuilder(args);

// ── One-off utility: hash a password for Auth:Users configuration ──────────
// Usage: dotnet run --project src/Loco.Api -- hash-password <password>
if (args.Length >= 2 && args[0] == "hash-password")
{
    Console.WriteLine(PasswordHasher.Hash(args[1]));
    return;
}

// ── Logging ─────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ── Options / security primitives ───────────────────────────────────────────
var authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton<JwtSigningKeyProvider>();

// ── Authentication: JWT bearer, fail-fast signing key ───────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Configure JwtBearerOptions through DI so the signing key comes from
// JwtSigningKeyProvider (whose ctor enforces fail-fast on a missing/weak key
// outside Development) without building a second service provider.
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtSigningKeyProvider>((options, keyProvider) =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = authOptions.Issuer,
            ValidAudience = authOptions.Audience,
            IssuerSigningKey = keyProvider.Key,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

// ── Authorization: scope policies ────────────────────────────────────────────
// The "scope" claim may be a single space-delimited value (OAuth convention,
// what AuthenticationController issues) or repeated claims. RequireAssertion
// handles both; the previous RequireClaim(exact-match) silently rejected
// space-delimited scopes.
static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string scope) =>
    user.FindAll("scope").Any(c =>
        c.Value == scope ||
        c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(scope));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanViewWorkflows", p => p.RequireAssertion(ctx => HasScope(ctx.User, "workflows:read")));
    options.AddPolicy("CanManageWorkflows", p => p.RequireAssertion(ctx => HasScope(ctx.User, "workflows:manage")));
    options.AddPolicy("CanExecuteWorkflows", p => p.RequireAssertion(ctx => HasScope(ctx.User, "workflows:execute")));
});

// ── Rate limiting (shared framework, no packages) ────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var partitionKey = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });

    // RFC 9457 ProblemDetails body + Retry-After on rejection, instead of an
    // empty 503 (the framework default status) that clients can't interpret.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc6585#section-4",
            title = "Too Many Requests",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Request rate limit exceeded. Retry after 60 seconds.",
            traceId = context.HttpContext.TraceIdentifier,
        }, cancellationToken);
    };
});

// ── CORS: config-driven allowlist (no AllowAll) ──────────────────────────────
// In development the Vite dev server (port 3000) proxies /api to this app, so
// requests are same-origin and this list is rarely exercised; it exists for
// deployments that serve the editor from a different origin.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClients", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyMethod()
        .AllowAnyHeader());
});

// ── MVC + JSON (camelCase to match the frontend contract) ────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ── Swagger ──────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Loco Workflow Automation API",
        Version = "v1.0",
        Description = "Lightweight workflow automation platform",
        License = new OpenApiLicense { Name = "MIT" },
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT from POST /api/v1/authentication/token",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

// ── Health checks: liveness (process up) vs readiness (dependencies OK) ─────
var configuredDataDirectory = builder.Configuration["Storage:DataDirectory"];
var dataDirectory = string.IsNullOrWhiteSpace(configuredDataDirectory)
    ? Path.Combine(AppContext.BaseDirectory, "data", "workflows")
    : configuredDataDirectory;
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
        tags: new[] { "live" })
    .AddCheck("workflow-store", () =>
    {
        try
        {
            Directory.CreateDirectory(dataDirectory);
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                "Workflow store directory is not writable", ex);
        }
    }, tags: new[] { "ready" });

// ── Application services ─────────────────────────────────────────────────────
// Engine + connector wiring are singletons: node handlers are registered once
// at startup (ConnectorStartupService); per-execution state lives in each
// WorkflowExecutionContext, so a shared engine instance is safe.
builder.Services.AddSingleton<ConnectorRegistry>(_ => new ConnectorRegistry());
builder.Services.AddSingleton<VisualWorkflowEngine>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<VisualWorkflowEngine>>();
    return new VisualWorkflowEngine(message => logger.LogDebug("{EngineMessage}", message));
});
builder.Services.AddSingleton<WorkflowConnectorBridge>(sp => new WorkflowConnectorBridge(
    sp.GetRequiredService<ConnectorRegistry>(),
    sp.GetRequiredService<VisualWorkflowEngine>()));
builder.Services.AddSingleton<IWorkflowStore>(sp => new JsonFileWorkflowStore(
    dataDirectory,
    sp.GetRequiredService<ILogger<JsonFileWorkflowStore>>()));
// Execution history survives restarts and eviction; without it a client
// polling across a deploy got a 404 for a run that had actually succeeded.
builder.Services.AddSingleton(sp => new JsonFileExecutionStore(
    dataDirectory, sp.GetRequiredService<ILogger<JsonFileExecutionStore>>()));
builder.Services.AddSingleton(sp => new ExecutionRegistry(
    sp.GetRequiredService<JsonFileExecutionStore>()));
// Stored connector credentials. Secrets are encrypted at rest by SecretsManager;
// WorkflowsController resolves a workflow's connections and initializes each
// connector immediately before executing it.
// Connector credentials are encrypted with a key derived from
// LOCO_SECRETS_PASSPHRASE. Without it SecretsManager falls back to a key file it
// generates NEXT TO the encrypted data - fine for a single-user CLI, but on a
// server it means anyone who can read the secrets can read the key, so the
// encryption protects nothing. Same rule the JWT signing key already follows:
// fail fast outside Development, warn loudly inside it.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOCO_SECRETS_PASSPHRASE")))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "LOCO_SECRETS_PASSPHRASE is not set. Connector credentials would be encrypted " +
            "with a key stored alongside them, which provides no protection on a server. " +
            "Set it to a strong passphrase, sourced from your secret manager.");
    }

    Console.WriteLine(
        "WARNING: LOCO_SECRETS_PASSPHRASE is not set; connector credentials will be " +
        "encrypted with a machine-local key file stored next to them (Development only).");
}

builder.Services.AddSingleton(_ => new JsonFileConnectionStore(dataDirectory));
// The one path from "a stored workflow" to "a running execution", shared by the
// HTTP controller and the scheduler so a scheduled run cannot drift from a
// manual one.
builder.Services.AddSingleton<WorkflowExecutionService>();
builder.Services.AddHostedService<ConnectorStartupService>();
// Runs cron-scheduled workflows with no human in the loop. Without this the
// product is a workflow runner, not automation.
builder.Services.AddHostedService<WorkflowSchedulerService>();

var app = builder.Build();

// ── Pipeline ─────────────────────────────────────────────────────────────────
// Exception handler outermost so failures anywhere below still produce the
// frontend's error envelope; logging next so every request gets a correlation id.
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseMiddleware<StructuredLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Loco API v1");
        c.RoutePrefix = "docs";
    });
}

app.UseCors("AllowClients");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});
app.MapHealthChecks("/health"); // all checks

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program { }
