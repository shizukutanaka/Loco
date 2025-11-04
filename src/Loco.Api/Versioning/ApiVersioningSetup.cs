#nullable enable

using Asp.Versioning;
using Microsoft.OpenApi.Models;

namespace Loco.Api.Versioning;

/// <summary>
/// API versioning strategy enumeration
/// </summary>
public enum VersioningStrategy
{
    /// <summary>
    /// URI path versioning: /api/v1/resource
    /// </summary>
    UrlPath,

    /// <summary>
    /// Query string versioning: /api/resource?api-version=1.0
    /// </summary>
    QueryString,

    /// <summary>
    /// Header versioning: X-API-Version: 1.0
    /// </summary>
    Header,

    /// <summary>
    /// Media type versioning: application/vnd.company.v1+json
    /// </summary>
    MediaType,

    /// <summary>
    /// Combined URL + Query + Header (most flexible)
    /// </summary>
    Combined
}

/// <summary>
/// API versioning configuration
/// </summary>
public class ApiVersioningConfig
{
    /// <summary>
    /// Versioning strategy to use
    /// </summary>
    public VersioningStrategy Strategy { get; set; } = VersioningStrategy.UrlPath;

    /// <summary>
    /// Assume default version when not specified
    /// </summary>
    public bool AssumeDefaultVersion { get; set; } = true;

    /// <summary>
    /// Report API versions in response headers
    /// </summary>
    public bool ReportApiVersions { get; set; } = true;

    /// <summary>
    /// Default API version
    /// </summary>
    public string DefaultVersion { get; set; } = "1.0";

    /// <summary>
    /// Deprecated versions
    /// </summary>
    public List<string> DeprecatedVersions { get; set; } = new();

    /// <summary>
    /// Sunset date for versions (mapping of version to sunset date)
    /// </summary>
    public Dictionary<string, DateTime> VersionSunsetDates { get; set; } = new();
}

/// <summary>
/// API version information
/// </summary>
public class ApiVersionInfo
{
    public string Version { get; set; } = string.Empty;
    public string? Status { get; set; } // Current, Deprecated, Sunset
    public DateTime? SunsetDate { get; set; }
    public string? MigrationGuide { get; set; }
    public List<string> BreakingChanges { get; set; } = new();
    public DateTime ReleasedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// API version registry for managing version information
/// </summary>
public class ApiVersionRegistry
{
    private readonly Dictionary<string, ApiVersionInfo> _versions = new();
    private readonly ILogger<ApiVersionRegistry> _logger;

    public ApiVersionRegistry(ILogger<ApiVersionRegistry> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers API version information
    /// </summary>
    public void RegisterVersion(string version, ApiVersionInfo info)
    {
        _versions[version] = info;
        _logger.LogInformation("Registered API version: {Version} (Status: {Status})", version, info.Status);
    }

    /// <summary>
    /// Gets version information
    /// </summary>
    public ApiVersionInfo? GetVersionInfo(string version)
    {
        _versions.TryGetValue(version, out var info);
        return info;
    }

    /// <summary>
    /// Gets all versions
    /// </summary>
    public IEnumerable<string> GetAllVersions() => _versions.Keys;

    /// <summary>
    /// Checks if version is deprecated
    /// </summary>
    public bool IsDeprecated(string version)
    {
        return _versions.TryGetValue(version, out var info) && info.Status == "Deprecated";
    }

    /// <summary>
    /// Checks if version is sunset
    /// </summary>
    public bool IsSunset(string version)
    {
        return _versions.TryGetValue(version, out var info) &&
               info.SunsetDate.HasValue &&
               DateTime.UtcNow > info.SunsetDate;
    }

    /// <summary>
    /// Gets versions by status
    /// </summary>
    public IEnumerable<string> GetVersionsByStatus(string status)
    {
        return _versions
            .Where(kvp => kvp.Value.Status == status)
            .Select(kvp => kvp.Key);
    }
}

/// <summary>
/// Backward compatibility helper for version mapping
/// </summary>
public class VersionMappingHelper
{
    private readonly Dictionary<(string fromVersion, string toVersion), Func<object, object>> _mappings = new();
    private readonly ILogger<VersionMappingHelper> _logger;

    public VersionMappingHelper(ILogger<VersionMappingHelper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers mapping between versions
    /// </summary>
    public void RegisterMapping<TFrom, TTo>(
        string fromVersion,
        string toVersion,
        Func<TFrom, TTo> mapper)
        where TFrom : class
        where TTo : class
    {
        var key = (fromVersion, toVersion);
        _mappings[key] = obj => mapper((TFrom)obj)!;
        _logger.LogDebug("Registered mapping: v{FromVersion} -> v{ToVersion}", fromVersion, toVersion);
    }

    /// <summary>
    /// Maps object between versions
    /// </summary>
    public object? MapBetweenVersions(object source, string fromVersion, string toVersion)
    {
        var key = (fromVersion, toVersion);
        if (_mappings.TryGetValue(key, out var mapper))
        {
            return mapper(source);
        }

        _logger.LogWarning("No mapping found: v{FromVersion} -> v{ToVersion}", fromVersion, toVersion);
        return source;
    }
}

/// <summary>
/// API version deprecation middleware
/// Adds warnings for deprecated versions
/// </summary>
public class ApiVersionDeprecationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiVersionRegistry _versionRegistry;
    private readonly ILogger<ApiVersionDeprecationMiddleware> _logger;

    public ApiVersionDeprecationMiddleware(
        RequestDelegate next,
        ApiVersionRegistry versionRegistry,
        ILogger<ApiVersionDeprecationMiddleware> logger)
    {
        _next = next;
        _versionRegistry = versionRegistry;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestedVersion = ExtractVersion(context);

        if (!string.IsNullOrEmpty(requestedVersion))
        {
            // Check if version is sunset
            if (_versionRegistry.IsSunset(requestedVersion))
            {
                _logger.LogWarning("Request to sunset API version: {Version}", requestedVersion);
                context.Response.Headers.Add("Sunset", _versionRegistry.GetVersionInfo(requestedVersion)?.SunsetDate?.ToUniversalTime().ToString("R") ?? "");
            }

            // Check if version is deprecated
            if (_versionRegistry.IsDeprecated(requestedVersion))
            {
                _logger.LogWarning("Request to deprecated API version: {Version}", requestedVersion);
                var versionInfo = _versionRegistry.GetVersionInfo(requestedVersion);
                if (!string.IsNullOrEmpty(versionInfo?.MigrationGuide))
                {
                    context.Response.Headers.Add("X-API-Warn-Deprecated", versionInfo.MigrationGuide);
                }
            }
        }

        await _next(context);
    }

    private string? ExtractVersion(HttpContext context)
    {
        // Try URL path version
        if (context.Request.Path.Value?.Contains("/v") == true)
        {
            var segments = context.Request.Path.Value.Split('/');
            var versionSegment = segments.FirstOrDefault(s => s.StartsWith("v"));
            if (!string.IsNullOrEmpty(versionSegment))
            {
                return versionSegment.Substring(1);
            }
        }

        // Try query string version
        if (context.Request.Query.TryGetValue("api-version", out var queryVersion))
        {
            return queryVersion.ToString();
        }

        // Try header version
        if (context.Request.Headers.TryGetValue("X-API-Version", out var headerVersion))
        {
            return headerVersion.ToString();
        }

        return null;
    }
}

/// <summary>
/// Extension methods for API versioning
/// </summary>
public static class ApiVersioningExtensions
{
    /// <summary>
    /// Adds API versioning services
    /// </summary>
    public static IServiceCollection AddApiVersioning(
        this IServiceCollection services,
        ApiVersioningConfig? config = null)
    {
        config ??= new ApiVersioningConfig();

        // Register version registry
        services.AddSingleton(new ApiVersionRegistry(
            services.BuildServiceProvider().GetRequiredService<ILogger<ApiVersionRegistry>>()));
        services.AddSingleton<VersionMappingHelper>();

        // Configure Asp.Versioning based on strategy
        var versioningBuilder = services.AddApiVersioning(options =>
        {
            options.AssumeDefaultVersionWhenUnspecified = config.AssumeDefaultVersion;
            options.ReportApiVersions = config.ReportApiVersions;
            options.DefaultApiVersion = new ApiVersion(ParseVersion(config.DefaultVersion));
        });

        // Configure strategy-specific settings
        switch (config.Strategy)
        {
            case VersioningStrategy.UrlPath:
                versioningBuilder.AddUrlSegmentApiVersioning();
                break;

            case VersioningStrategy.QueryString:
                versioningBuilder.AddQueryStringApiVersioning();
                break;

            case VersioningStrategy.Header:
                versioningBuilder.AddHeaderApiVersioning();
                break;

            case VersioningStrategy.MediaType:
                versioningBuilder.AddMediaTypeApiVersioning();
                break;

            case VersioningStrategy.Combined:
                versioningBuilder
                    .AddUrlSegmentApiVersioning()
                    .AddQueryStringApiVersioning()
                    .AddHeaderApiVersioning();
                break;
        }

        return services;
    }

    /// <summary>
    /// Uses API version deprecation middleware
    /// </summary>
    public static IApplicationBuilder UseApiVersionDeprecation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiVersionDeprecationMiddleware>();
    }

    /// <summary>
    /// Maps API version endpoints
    /// </summary>
    public static IEndpointRouteBuilder MapApiVersionEndpoints(
        this IEndpointRouteBuilder endpoints,
        ApiVersionRegistry versionRegistry)
    {
        endpoints.MapGet("/api/versions",
            (HttpContext context) =>
            {
                var versions = versionRegistry.GetAllVersions()
                    .Select(v => new
                    {
                        Version = v,
                        Info = versionRegistry.GetVersionInfo(v),
                        IsDeprecated = versionRegistry.IsDeprecated(v),
                        IsSunset = versionRegistry.IsSunset(v)
                    })
                    .ToList();

                context.Response.Headers.Add("X-API-Versions", string.Join(",", versionRegistry.GetAllVersions()));
                return Results.Ok(versions);
            })
            .WithName("GetApiVersions")
            .WithOpenApi()
            .WithTags("API Versioning");

        endpoints.MapGet("/api/versions/{version}",
            (string version, ApiVersionRegistry registry, HttpContext context) =>
            {
                var info = registry.GetVersionInfo(version);
                if (info == null)
                {
                    return Results.NotFound(new { message = $"Version {version} not found" });
                }

                return Results.Ok(new
                {
                    Version = version,
                    Info = info,
                    IsDeprecated = registry.IsDeprecated(version),
                    IsSunset = registry.IsSunset(version)
                });
            })
            .WithName("GetApiVersion")
            .WithOpenApi()
            .WithTags("API Versioning");

        return endpoints;
    }

    /// <summary>
    /// Parses version string to ApiVersion
    /// </summary>
    private static (int, int) ParseVersion(string version)
    {
        var parts = version.Split('.');
        var major = int.Parse(parts[0]);
        var minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        return (major, minor);
    }
}

/// <summary>
/// Attribute for marking deprecated endpoints
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class DeprecatedApiAttribute : Attribute
{
    public string? ReplacementVersion { get; set; }
    public string? MigrationGuide { get; set; }
    public DateTime? SunsetDate { get; set; }

    public DeprecatedApiAttribute(string? replacementVersion = null)
    {
        ReplacementVersion = replacementVersion;
    }
}

/// <summary>
/// Example API controllers showing versioning patterns
/// </summary>
public class ApiVersioningExamples
{
    /// <summary>
    /// Example of URL path versioning
    /// Endpoint: GET /api/v1/users
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/users")]
    public class UsersV1Controller : ControllerBase
    {
        /// <summary>
        /// Gets list of users (v1 - basic info only)
        /// </summary>
        [HttpGet]
        [MapToApiVersion("1.0")]
        public IActionResult GetUsers()
        {
            return Ok(new[] {
                new { id = 1, name = "User 1" },
                new { id = 2, name = "User 2" }
            });
        }
    }

    /// <summary>
    /// Example of URL path versioning with enhanced features
    /// Endpoint: GET /api/v2/users
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/users")]
    public class UsersV2Controller : ControllerBase
    {
        /// <summary>
        /// Gets list of users (v2 - extended info with email)
        /// </summary>
        [HttpGet]
        [MapToApiVersion("2.0")]
        public IActionResult GetUsers()
        {
            return Ok(new[] {
                new { id = 1, name = "User 1", email = "user1@example.com" },
                new { id = 2, name = "User 2", email = "user2@example.com" }
            });
        }
    }

    /// <summary>
    /// Example of deprecated endpoint
    /// Clients should migrate to v3
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/products")]
    [Deprecated(ReplacementVersion = "3.0", MigrationGuide = "https://docs.example.com/migration/v2-to-v3")]
    public class ProductsV2Controller : ControllerBase
    {
        /// <summary>
        /// Gets products (v2 - DEPRECATED, use v3)
        /// </summary>
        [HttpGet]
        [MapToApiVersion("2.0")]
        [Deprecated(ReplacementVersion = "3.0")]
        public IActionResult GetProducts()
        {
            return Ok(new[] {
                new { id = 1, name = "Product 1", price = 99.99 }
            });
        }
    }
}

/// <summary>
/// Swagger/OpenAPI configuration for API versioning
/// </summary>
public static class ApiVersioningSwaggerSetup
{
    /// <summary>
    /// Configures Swagger with API versioning support
    /// </summary>
    public static IServiceCollection AddVersionedSwaggerGen(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            // Add versioned API info
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Loco API",
                Version = "1.0",
                Description = "Workflow Automation Engine API - v1.0 (Legacy)"
            });

            options.SwaggerDoc("v2", new OpenApiInfo
            {
                Title = "Loco API",
                Version = "2.0",
                Description = "Workflow Automation Engine API - v2.0"
            });

            options.SwaggerDoc("v3", new OpenApiInfo
            {
                Title = "Loco API",
                Version = "3.0",
                Description = "Workflow Automation Engine API - v3.0 (Current)",
                Contact = new OpenApiContact
                {
                    Name = "API Support",
                    Email = "api@example.com"
                }
            });

            // Use only the version from the route
            options.DocInclusionPredicate((version, apiDescription) =>
            {
                var versionParameter = apiDescription.ParameterDescriptions
                    .SingleOrDefault(p => p.Name == "version");

                var values = (versionParameter?.DefaultValue as string)?.Split(',');
                if (values == null)
                    return false;

                return ApiVersionMatcher.TryParse(version, out var requestedVersion) &&
                       values.Any(v => $"v{v}" == requestedVersion.ToString());
            });

            // Add deprecation info to operation
            options.OperationFilter<ApiVersionDeprecationOperationFilter>();
        });

        return services;
    }

    /// <summary>
    /// Operation filter for adding deprecation notices to Swagger
    /// </summary>
    private class ApiVersionDeprecationOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var deprecated = context.MethodInfo?.GetCustomAttribute<DeprecatedApiAttribute>();
            if (deprecated != null)
            {
                operation.Deprecated = true;
                var description = $"**DEPRECATED** - ";
                if (!string.IsNullOrEmpty(deprecated.ReplacementVersion))
                {
                    description += $"Use v{deprecated.ReplacementVersion}. ";
                }
                if (deprecated.SunsetDate.HasValue)
                {
                    description += $"Sunset date: {deprecated.SunsetDate:yyyy-MM-dd}. ";
                }
                if (!string.IsNullOrEmpty(deprecated.MigrationGuide))
                {
                    description += $"[Migration Guide]({deprecated.MigrationGuide})";
                }

                operation.Description = $"{operation.Description}\n\n{description}";
            }
        }
    }

    /// <summary>
    /// Helper for parsing API versions in Swagger
    /// </summary>
    private static class ApiVersionMatcher
    {
        public static bool TryParse(string version, out (int Major, int Minor) parsedVersion)
        {
            if (version.StartsWith("v"))
            {
                version = version.Substring(1);
            }

            var parts = version.Split('.');
            if (parts.Length >= 1 && int.TryParse(parts[0], out var major))
            {
                var minor = parts.Length > 1 && int.TryParse(parts[1], out var m) ? m : 0;
                parsedVersion = (major, minor);
                return true;
            }

            parsedVersion = default;
            return false;
        }
    }
}
