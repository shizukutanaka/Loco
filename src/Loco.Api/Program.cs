using Loco.Api.Filters;
using Loco.Api.HealthChecks;
using Loco.Api.Middleware;
using Loco.Api.Security;
using Loco.Core;
using Loco.Core.Configuration;
using Loco.Core.Interfaces;
using Loco.Core.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure Logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.AddFilter("Microsoft", LogLevel.Information);
    logging.AddFilter("System", LogLevel.Information);
});

// Create ActivitySource for manual instrumentation
var activitySource = new ActivitySource("Loco.Workflow");
builder.Services.AddSingleton(activitySource);

// Add OpenTelemetry Tracing with gRPC and manual instrumentation (Phase 1B)
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Loco.Workflow")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddGrpcClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(builder.Configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint") ?? "http://localhost:4317");
            options.Protocol = OtlpExportProtocol.Grpc;
        })
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService("loco-api", serviceVersion: "1.0.0")));

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });

    options.AddPolicy("AllowClients", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Configure Authentication
//
// The signing secret is never a shipped constant (a fixed value in
// appsettings.json would be public and shared by every installation).
// JwtSecretResolver prefers an explicitly configured secret
// (Jwt:SecretKey, e.g. the Jwt__SecretKey environment variable) and
// otherwise generates a per-installation secret persisted next to the
// rule store, so issued tokens stay valid across restarts.
var jwtSettings = builder.Configuration.GetSection("Jwt");
var ruleStorePath = builder.Configuration.GetValue<string>("RULE_STORE_PATH") ?? "rules.json";
var secretKey = JwtSecretResolver.Resolve(
    jwtSettings.GetValue<string>("SecretKey"),
    JwtSecretResolver.GetSecretFilePath(ruleStorePath));
// Publish the resolved secret back into configuration so token issuance
// (AuthenticationController) uses the same key as token validation.
builder.Configuration["Jwt:SecretKey"] = secretKey;
var issuer = jwtSettings.GetValue<string>("Issuer") ?? "https://loco.local";
var audience = jwtSettings.GetValue<string>("Audience") ?? "loco-api";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsJsonAsync(new
                {
                    code = "AUTH_FAILED",
                    message = "Authentication failed",
                    details = context.Exception.Message
                });
            }
        };
    });

// Configure Authorization
//
// The token endpoint issues a single "scope" claim whose value is the
// space-delimited OAuth2 scope list ("workflows:read workflows:manage
// workflows:execute"). RequireClaim does an exact value match, so it never
// matched and every request with a freshly issued token was rejected with
// 403. The policies therefore split the scope claim(s) on spaces.
static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string scope) =>
    user.FindAll("scope")
        .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .Contains(scope);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanManageWorkflows", policy =>
        policy.RequireAssertion(context => HasScope(context.User, "workflows:manage")));

    options.AddPolicy("CanViewWorkflows", policy =>
        policy.RequireAssertion(context => HasScope(context.User, "workflows:read")));

    options.AddPolicy("CanExecuteWorkflows", policy =>
        policy.RequireAssertion(context => HasScope(context.User, "workflows:execute")));
});

// Add Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Global limiter
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var userId = httpContext.User.FindFirst("sub")?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(userId, partition =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1000,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Policy-specific limiters
    options.AddFixedWindowLimiter(policyName: "strict", config =>
    {
        config.PermitLimit = 10;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter(policyName: "moderate", config =>
    {
        config.PermitLimit = 100;
        config.Window = TimeSpan.FromMinutes(1);
    });
});

// Add API Services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Phase 2 JSON optimization: Reduce memory and improve throughput
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = null;

        // Reduce default buffer size from 16KB to 4KB (Phase 2 optimization)
        // Most API payloads are < 4KB, reducing default allocation
        options.JsonSerializerOptions.DefaultBufferSize = 4096;

        // Never pretty-print in production (10% serialization overhead)
        options.JsonSerializerOptions.WriteIndented = false;

        // Ignore null values to reduce JSON size
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Add Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Loco Workflow Automation API",
        Version = "v1.0",
        Description = "Enterprise-grade lightweight workflow automation platform",
        Contact = new OpenApiContact
        {
            Name = "Loco Support",
            Email = "support@loco.local"
        },
        License = new OpenApiLicense
        {
            Name = "MIT"
        }
    });

    // JWT Security Scheme
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });

    // API Key Security Scheme (optional)
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Description = "API Key for authentication"
    });

    // XML Comments
    var xmlFile = Path.Combine(AppContext.BaseDirectory, "Loco.Api.xml");
    if (File.Exists(xmlFile))
    {
        c.IncludeXmlComments(xmlFile);
    }

    // Custom operation filter for default responses
    c.OperationFilter<AddResponseHeadersFilter>();
});

// Data access services
//
// NOTE: This file previously registered an EF Core + Dapper "hybrid" data layer
// (LocoDbContext, HybridWorkflowRepository, HybridExecutionHistoryRepository),
// OAuth 2.0 services, an event-store repository, a health-check service and a
// structured-logging helper. None of those types exist anywhere in Loco.Core or
// Loco.Api (verified by full-text search), so the registrations could never
// compile or run. They have been removed; the services below are the real ones.
// The workflow surface is WorkflowsController backed by IAutomationEngine and
// IRuleStore, exactly as covered by tests/Loco.Api.Tests.

// Advanced Metrics Collector (real type in Loco.Core.Workflows)
builder.Services.AddSingleton<Loco.Core.Workflows.WorkflowMetricsCollector>();

// Add Core Services
builder.Services.AddSingleton<LocoConfig>();
builder.Services.AddSingleton<IRuleStore>(sp =>
{
    return new JsonFileRuleStore(
        ruleStorePath,
        sp.GetService<ILogger<JsonFileRuleStore>>());
});
builder.Services.AddSingleton<IAutomationEngine>(sp =>
    new SimpleLightEngine(
        sp.GetService<ILogger<SimpleLightEngine>>(),
        sp.GetRequiredService<LocoConfig>(),
        sp.GetRequiredService<IRuleStore>()));

// Health check service (real implementation in Loco.Core.Practical;
// fully qualified because Loco.Core.Practical also defines HttpContext,
// which would collide with Microsoft.AspNetCore.Http.HttpContext here)
builder.Services.AddSingleton(sp =>
{
    var healthChecks = new Loco.Core.Practical.SimpleHealthCheckService();
    healthChecks.AddCheck(new Loco.Core.Practical.CommonHealthChecks.MemoryHealthCheck());
    return healthChecks;
});

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<LocoHealthCheck>("loco-health");

var app = builder.Build();

// Use OpenTelemetry middleware
app.UseRouting();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Loco API v1");
        c.RoutePrefix = "docs";
        c.DocumentTitle = "Loco Workflow API - Interactive Documentation";
        c.DefaultModelsExpandDepth(-1);
    });
    app.UseDeveloperExceptionPage();
}

// Middleware Pipeline
app.UseHttpsRedirection();
app.UseCors("AllowClients");

// Rate Limiting Middleware
app.UseRateLimiter();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Global Exception Handling Middleware
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Structured Logging Middleware
app.UseMiddleware<StructuredLoggingMiddleware>();

// Map Controllers
app.MapControllers();

// Health Check Endpoint
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/detailed", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration
            })
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});

// Startup Information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Loco API starting...");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("Swagger UI available at: /docs");

app.Run();