using Loco.Core;
using Loco.Core.Configuration;
using Loco.Core.Execution;
using Loco.Core.Health;
using Loco.Core.Interfaces;
using Loco.Core.Storage;
using Loco.Api.Services;
using Loco.Core.DataAccess;
using Loco.Core.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.AddFilter("Microsoft", LogLevel.Information);
    logging.AddFilter("System", LogLevel.Information);
});

// Add gRPC support (Phase 1B)
builder.Services.AddGrpc();

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
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings.GetValue<string>("SecretKey") ?? "DefaultSecretKeyChangeInProduction12345";
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
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanManageWorkflows", policy =>
        policy.RequireClaim("scope", "workflows:manage"));

    options.AddPolicy("CanViewWorkflows", policy =>
        policy.RequireClaim("scope", "workflows:read"));

    options.AddPolicy("CanExecuteWorkflows", policy =>
        policy.RequireClaim("scope", "workflows:execute"));
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
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
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

// Add Data Access Services (Phase 2 - EF Core + Dapper Hybrid)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=loco.db";

builder.Services.AddDbContext<Loco.Core.DataAccess.LocoDbContext>(options =>
    options.UseSqlite(connectionString));

// Register IDbConnection for Dapper (SQLite)
builder.Services.AddScoped<System.Data.IDbConnection>(sp =>
    new Microsoft.Data.Sqlite.SqliteConnection(connectionString));

// Register hybrid repositories
builder.Services.AddScoped<IWorkflowRepository, Loco.Core.DataAccess.HybridWorkflowRepository>();
builder.Services.AddScoped<IExecutionHistoryRepository, Loco.Core.DataAccess.HybridExecutionHistoryRepository>();
// Add Core Services
builder.Services.AddScoped<IAutomationEngine, WorkflowExecutionEngine>();
builder.Services.AddScoped<IRuleStore, JsonFileRuleStore>();
builder.Services.AddScoped<IHealthCheckService, HealthCheckService>();
builder.Services.AddSingleton<LocoConfig>(sp =>
{
    var configPath = builder.Configuration.GetValue<string>("ConfigPath") ?? "loco-config.json";
    return LocoConfig.LoadFromFile(configPath);
});

// Add Structured Logging
builder.Services.AddScoped<IStructuredLoggingHelper, StructuredLoggingHelper>();

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

// Map gRPC Services (Phase 1B)
app.MapGrpcService<WorkflowEngineGrpcService>();

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
