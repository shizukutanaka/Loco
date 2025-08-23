using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;
// Marketplace removed in OSS 0.0.1
using Loco.Core.Sharing;
using HealthChecks.UI.Client;
using Loco.Web.Data;
using Microsoft.EntityFrameworkCore;
// GraphQL imports
using GraphQL;
using GraphQL.Server;
using GraphQL.Server.Ui.Playground;
using GraphQL.Types;
using Loco.Web.GraphQL.Schema;
using Loco.Web.GraphQL.Services;
using Loco.Web.GraphQL.Types;
using Loco.Llm;
using Loco.Core.Interfaces;
using Loco.Core.Utilities;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// App configuration and environment-driven settings
// Load .env if present and prime env from preset to minimize required config
DotEnvLoader.Load();
LlmConfigurationEnv.PrimeEnvironmentFromPreset();
var informationalVersion = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
var appVersion = informationalVersion
    ?? typeof(Program).Assembly.GetName().Version?.ToString()
    ?? "unknown";

// Ensure LOCO_ environment variables (e.g., LOCO_LLM__*) are considered
builder.Configuration.AddEnvironmentVariables(prefix: "LOCO_");

// Ensure a stable data directory for SQLite, allow override via MVP_DB_PATH
var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDir);
var defaultDbPath = Path.Combine(dataDir, "loco.db");
var dbPathOverride = builder.Configuration["MVP_DB_PATH"] ?? Environment.GetEnvironmentVariable("MVP_DB_PATH");
var dbPath = string.IsNullOrWhiteSpace(dbPathOverride) ? defaultDbPath : dbPathOverride;
try { var dir = Path.GetDirectoryName(dbPath); if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir); } catch { }

// Optional CORS origins via config or environment (comma-separated)
var corsOriginsConfig = builder.Configuration["MVP_CORS_ORIGINS"]
    ?? Environment.GetEnvironmentVariable("MVP_CORS_ORIGINS");
var corsOrigins = corsOriginsConfig?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = HttpLoggingFields.RequestMethod | HttpLoggingFields.RequestPath | HttpLoggingFields.ResponseStatusCode;
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsOrigins != null && corsOrigins.Length > 0)
        {
            policy.WithOrigins(corsOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

builder.Services.AddDbContext<FlowContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<IFlowRepository, SqliteFlowRepository>();
builder.Services.AddScoped<SimpleShareService>();
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();
builder.Services.AddResponseCaching();

// LLM options & service registration
builder.Services.AddOptions<LlmConfiguration>()
    .Bind(builder.Configuration.GetSection("Llm"));
// Legacy env var fallbacks for UI/CLI/Web parity
builder.Services.PostConfigure<LlmConfiguration>(options =>
{
    LlmConfigurationEnv.ApplyEnvironmentVariables(options);
});
builder.Services.AddHttpClient<ILlmService, LlmService>();

// GraphQL services
builder.Services.AddSingleton<IFlowEventService, FlowEventService>();
builder.Services.AddSingleton<FlowAuthorizationService>();
builder.Services.AddSingleton<GraphQLMetricsService>();

// GraphQL types
builder.Services.AddSingleton<FlowType>();
builder.Services.AddSingleton<FlowInputType>();
builder.Services.AddSingleton<TriggerType>();
builder.Services.AddSingleton<TriggerInputType>();
builder.Services.AddSingleton<ConditionType>();
builder.Services.AddSingleton<ConditionInputType>();
builder.Services.AddSingleton<ActionType>();
builder.Services.AddSingleton<ActionInputType>();
builder.Services.AddSingleton<FlowMetadataType>();
builder.Services.AddSingleton<FlowSearchResultType>();
builder.Services.AddSingleton<FlowSearchInputType>();
builder.Services.AddSingleton<FlowSortInputType>();
builder.Services.AddSingleton<SortDirectionEnumType>();
builder.Services.AddSingleton<FlowStatisticsType>();
builder.Services.AddSingleton<FlowValidationResultType>();
builder.Services.AddSingleton<BatchOperationResultType>();
builder.Services.AddSingleton<FlowEventType>();
builder.Services.AddSingleton<FlowExecutionEventType>();
builder.Services.AddSingleton<SystemEventType>();

// GraphQL schema
builder.Services.AddScoped<LocoQuery>();
builder.Services.AddScoped<LocoMutation>();
builder.Services.AddScoped<LocoSubscription>();
builder.Services.AddScoped<ISchema, LocoSchema>();

// GraphQL server
builder.Services.AddGraphQL(options =>
{
    options.EnableMetrics = true;
    options.UnhandledExceptionDelegate = ctx =>
    {
        var logger = ctx.RequestServices?.GetRequiredService<ILogger<Program>>();
        logger?.LogError(ctx.OriginalException, "GraphQL execution error");
    };
})
.AddSystemTextJson()
.AddErrorInfoProvider(opt => opt.ExposeExceptionDetails = app.Environment.IsDevelopment())
.AddWebSockets()
.AddGraphTypes(typeof(LocoSchema).Assembly);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

// Apply database initialization on startup (migrate if available, else ensure created)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FlowContext>();
    var hasPending = dbContext.Database.GetPendingMigrations().Any();
    if (hasPending)
    {
        dbContext.Database.Migrate();
    }
    else
    {
        dbContext.Database.EnsureCreated();
    }
}

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseHttpLogging();
app.UseCors();
app.UseResponseCaching();

// GraphQL middleware
app.UseWebSockets();
app.UseGraphQL<ISchema>("/graphql");
app.UseGraphQLPlayground("/graphql/playground", new PlaygroundOptions
{
    GraphQLEndPoint = "/graphql",
    SubscriptionsEndPoint = "/graphql"
});

// API Endpoints
app.MapGet("/", () => new
{
    name = "Loco API",
    version = appVersion,
    endpoints = new[]
    {
        "/api/flows",
        "/api/flows/{id}",
        "/api/flows/{id}/download",
        "/api/install/{id}",
        "/api/share",
        "/api/llm/config",
        "/healthz",
        "/graphql",
        "/graphql/playground"
    },
    graphql = new
    {
        endpoint = "/graphql",
        playground = "/graphql/playground",
        description = "GraphQL API for advanced querying and real-time subscriptions"
    }
});

// Get flows with optional pagination and simple search
app.MapGet("/api/flows", async (HttpContext http, IFlowRepository repo, int? skip, int? take, string? q) =>
{
    var flows = await repo.GetAllAsync();

    if (!string.IsNullOrWhiteSpace(q))
    {
        var term = q.Trim();
        flows = flows.Where(f =>
            (!string.IsNullOrEmpty(f.Name) && f.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(f.Description) && f.Description.Contains(term, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    var total = flows.Count;
    if (skip.GetValueOrDefault() > 0) flows = flows.Skip(skip.Value).ToList();
    if (take.GetValueOrDefault() > 0) flows = flows.Take(take.Value).ToList();

    http.Response.Headers["X-Total-Count"] = total.ToString();
    http.Response.Headers["Cache-Control"] = "public, max-age=30";
    return Results.Ok(flows);
});

// (search/featured removed in 0.0.1)

// Get flow by ID
app.MapGet("/api/flows/{id}", async (string id, IFlowRepository repo) =>
{
    var flow = await repo.GetByIdAsync(id);
    return flow != null ? Results.Ok(flow) : Results.NotFound();
});

// Download flow
app.MapGet("/api/flows/{id}/download", async (string id, IFlowRepository repo, HttpContext http) =>
{
    var flow = await repo.GetByIdAsync(id);
    if (flow == null)
        return Results.NotFound();

    await repo.IncrementDownloadsAsync(id);

    http.Response.Headers["Cache-Control"] = "public, max-age=60";
    return Results.Json(flow);
});

// Upload flow (accept FlowDefinition directly)
app.MapPost("/api/flows", async (FlowDefinition flow, IFlowRepository repo) =>
{
    if (flow == null || string.IsNullOrWhiteSpace(flow.Name))
        return Results.BadRequest("Invalid flow definition");

    if (string.IsNullOrWhiteSpace(flow.Id))
        flow.Id = Guid.NewGuid().ToString();

    await repo.AddAsync(flow);

    return Results.Created($"/api/flows/{flow.Id}", new
    {
        FlowId = flow.Id,
        ShareUrl = "about:blank",
        ShortUrl = "about:blank"
    });
});

// Upsert flow by ID
app.MapPut("/api/flows/{id}", async (string id, FlowDefinition flow, IFlowRepository repo) =>
{
    if (flow == null)
        return Results.BadRequest("Invalid flow definition");

    flow.Id = id;
    if (string.IsNullOrWhiteSpace(flow.Name))
        return Results.BadRequest("Name is required");

    await repo.AddAsync(flow);
    return Results.Ok(flow);
});

// Delete flow by ID
app.MapDelete("/api/flows/{id}", async (string id, IFlowRepository repo) =>
{
    var deleted = await repo.DeleteAsync(id);
    return deleted ? Results.NoContent() : Results.NotFound();
});

// (packs removed in 0.0.1)

// One-click install endpoint
app.MapGet("/api/install/{id}", async (string id, IFlowRepository repo) =>
{
    var flow = await repo.GetByIdAsync(id);
    if (flow == null)
        return Results.NotFound();
    
    // Generate install protocol URL
    var installUrl = $"loco://install/{id}";
    
    // Return HTML with auto-redirect
    var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Install {flow.FlowDefinition.Name}</title>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }}
        .container {{
            background: white;
            padding: 40px;
            border-radius: 12px;
            box-shadow: 0 20px 40px rgba(0,0,0,0.1);
            text-align: center;
            max-width: 400px;
        }}
        h1 {{ color: #333; margin-bottom: 10px; }}
        p {{ color: #666; margin-bottom: 30px; }}
        .btn {{
            display: inline-block;
            padding: 12px 30px;
            background: #667eea;
            color: white;
            text-decoration: none;
            border-radius: 6px;
            font-weight: 600;
            transition: transform 0.2s;
        }}
        .btn:hover {{ transform: translateY(-2px); }}
    </style>
</head>
<body>
    <div class='container'>
        <h1>{flow.FlowDefinition.Name}</h1>
        <p>{flow.FlowDefinition.Description}</p>
        <a href='{installUrl}' class='btn'>Install with Loco</a>
        <p style='margin-top: 30px; font-size: 14px;'>
            Don't have Loco? <a href='https://github.com/shizukutanaka/Loco/releases/latest'>Download here</a>
        </p>
    </div>
    <script>
        // Auto-redirect to loco:// protocol
        setTimeout(() => {{
            window.location.href = '{installUrl}';
        }}, 100);
        
        // Fallback if protocol handler not installed
        setTimeout(() => {{
            if (!document.hidden) {{
                if (confirm('Loco doesn\'t seem to be installed. Would you like to download it?')) {{
                    window.location.href = 'https://github.com/shizukutanaka/Loco/releases/latest';
                }}
            }}
        }}, 2000);
    </script>
</body>
</html>";
    
    return Results.Content(html, "text/html");
});

// Share endpoint
app.MapPost("/api/share", async (FlowDefinition flow, SimpleShareService shareService) =>
{
    if (flow == null)
        return Results.BadRequest("Invalid flow");
    
    var shareCode = shareService.GenerateShareCode(flow);
    var shareLink = shareService.GenerateShareLink(flow);
    
    return Results.Ok(new
    {
        shareCode.ShareId,
        shareCode.ShortCode,
        shareCode.ShareUrl,
        shareCode.LocoUrl,
        shareLink.MarkdownLink,
        shareLink.HtmlLink,
        QrCode = shareCode.QrCodeAscii,
        shareCode.ExpiresAt
    });
});

// (rating/users endpoints removed in 0.0.1)

// LLM configuration inspection endpoint (API key redacted)
app.MapGet("/api/llm/config", (IOptions<LlmConfiguration> options) =>
{
    var c = options.Value;
    var presetEnv = Environment.GetEnvironmentVariable("LOCO_LLM__PRESET")
        ?? Environment.GetEnvironmentVariable("LOCO_LLM_PRESET");

    // Use Results.Json with explicit options to include null values for consistency with CLI output and tests
    return Results.Json(
        new
        {
            provider = c.Provider,
            model = c.Model,
            apiEndpoint = c.ApiEndpoint,
            maxTokens = c.MaxTokens,
            temperature = c.Temperature,
            httpTimeoutMs = c.HttpTimeoutMs,
            apiKey = string.IsNullOrWhiteSpace(c.ApiKey) ? string.Empty : "redacted",
            hasApiKey = !string.IsNullOrWhiteSpace(c.ApiKey),
            preset = presetEnv
        },
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        }
    );
});

app.MapHealthChecks("/healthz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Minimal problem details endpoint used by exception handler
app.MapGet("/error", (HttpContext http) => Results.Problem()).ExcludeFromDescription();

app.Run();

// Expose Program for WebApplicationFactory integration tests
public partial class Program { }
