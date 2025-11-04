#nullable enable

using System.Reflection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Loco.Api.OpenApi;

/// <summary>
/// OpenAPI documentation attributes for auto-generation
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class OpenApiOperationAttribute : Attribute
{
    /// <summary>
    /// Operation summary
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Detailed operation description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Operation ID (for client code generation)
    /// </summary>
    public string? OperationId { get; set; }

    /// <summary>
    /// Tags for organizing operations
    /// </summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// Deprecation status
    /// </summary>
    public bool Deprecated { get; set; }
}

/// <summary>
/// Marks parameter with OpenAPI documentation
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class OpenApiParameterAttribute : Attribute
{
    /// <summary>
    /// Parameter description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Parameter is required
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Example value
    /// </summary>
    public object? Example { get; set; }

    /// <summary>
    /// Parameter schema pattern (regex)
    /// </summary>
    public string? Pattern { get; set; }
}

/// <summary>
/// Marks request/response model with OpenAPI documentation
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public class OpenApiSchemaAttribute : Attribute
{
    /// <summary>
    /// Model description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Example JSON
    /// </summary>
    public string? Example { get; set; }

    /// <summary>
    /// Property is deprecated
    /// </summary>
    public bool Deprecated { get; set; }

    /// <summary>
    /// Minimum value (for numbers)
    /// </summary>
    public double? Minimum { get; set; }

    /// <summary>
    /// Maximum value (for numbers)
    /// </summary>
    public double? Maximum { get; set; }

    /// <summary>
    /// Minimum length (for strings)
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// Maximum length (for strings)
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Pattern (regex for strings)
    /// </summary>
    public string? Pattern { get; set; }
}

/// <summary>
/// Marks endpoint with specific response types
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class OpenApiResponseAttribute : Attribute
{
    /// <summary>
    /// HTTP status code
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Response description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Response type
    /// </summary>
    public Type? ResponseType { get; set; }

    public OpenApiResponseAttribute(int statusCode, string? description = null)
    {
        StatusCode = statusCode;
        Description = description;
    }
}

/// <summary>
/// OpenAPI security requirements attribute
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class OpenApiSecurityAttribute : Attribute
{
    /// <summary>
    /// Security scheme name (e.g., "Bearer", "ApiKey")
    /// </summary>
    public string SchemeName { get; set; }

    /// <summary>
    /// Required scopes for OAuth2
    /// </summary>
    public string[]? Scopes { get; set; }

    public OpenApiSecurityAttribute(string schemeName)
    {
        SchemeName = schemeName;
    }
}

/// <summary>
/// Swagger generator configuration
/// </summary>
public class OpenApiGeneratorConfig
{
    /// <summary>
    /// API title
    /// </summary>
    public string Title { get; set; } = "Loco Workflow Automation API";

    /// <summary>
    /// API description
    /// </summary>
    public string Description { get; set; } = "Enterprise workflow automation engine with advanced scheduling and orchestration";

    /// <summary>
    /// Current API version
    /// </summary>
    public string Version { get; set; } = "3.0.0";

    /// <summary>
    /// License information
    /// </summary>
    public (string Name, string Url)? License { get; set; } = ("MIT", "https://opensource.org/licenses/MIT");

    /// <summary>
    /// Contact information
    /// </summary>
    public (string Name, string Email, string Url)? Contact { get; set; }

    /// <summary>
    /// API terms of service
    /// </summary>
    public string? TermsOfService { get; set; }

    /// <summary>
    /// Base URL/servers
    /// </summary>
    public List<(string Url, string Description)> Servers { get; set; } = new()
    {
        ("https://api.example.com", "Production"),
        ("https://staging-api.example.com", "Staging"),
        ("http://localhost:5000", "Development")
    };

    /// <summary>
    /// Include schema examples
    /// </summary>
    public bool IncludeSchemaExamples { get; set; } = true;

    /// <summary>
    /// Include security definitions
    /// </summary>
    public bool IncludeSecurity { get; set; } = true;

    /// <summary>
    /// Generate client SDKs for these languages
    /// </summary>
    public List<string> ClientGenerationLanguages { get; set; } = new() { "csharp", "typescript", "python" };
}

/// <summary>
/// Custom Swagger generator with auto-documentation
/// </summary>
public class AutoDocumentingSwaggerGenerator
{
    private readonly OpenApiGeneratorConfig _config;
    private readonly ILogger<AutoDocumentingSwaggerGenerator> _logger;

    public AutoDocumentingSwaggerGenerator(
        OpenApiGeneratorConfig config,
        ILogger<AutoDocumentingSwaggerGenerator> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Generates OpenAPI document with auto-documentation
    /// </summary>
    public OpenApiDocument GenerateDocument(Assembly controllerAssembly)
    {
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = _config.Title,
                Description = _config.Description,
                Version = _config.Version,
                TermsOfService = string.IsNullOrEmpty(_config.TermsOfService) ? null : new Uri(_config.TermsOfService),
                License = _config.License.HasValue ? new OpenApiLicense
                {
                    Name = _config.License.Value.Name,
                    Url = new Uri(_config.License.Value.Url)
                } : null,
                Contact = _config.Contact.HasValue ? new OpenApiContact
                {
                    Name = _config.Contact.Value.Name,
                    Email = _config.Contact.Value.Email,
                    Url = new Uri(_config.Contact.Value.Url)
                } : null
            },
            Servers = new List<OpenApiServer>(_config.Servers.Select(s =>
                new OpenApiServer
                {
                    Url = s.Url,
                    Description = s.Description
                })),
            Paths = new OpenApiPaths(),
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, OpenApiSchema>()
            }
        };

        // Scan assembly for controllers and generate paths
        GeneratePaths(document, controllerAssembly);

        // Add security schemes if enabled
        if (_config.IncludeSecurity)
        {
            AddSecuritySchemes(document);
        }

        _logger.LogInformation("Generated OpenAPI document with {PathCount} paths", document.Paths.Count);
        return document;
    }

    private void GeneratePaths(OpenApiDocument document, Assembly assembly)
    {
        var controllerTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

        foreach (var controllerType in controllerTypes)
        {
            var controllerAttribute = controllerType.GetCustomAttribute<ApiControllerAttribute>();
            var routeAttribute = controllerType.GetCustomAttribute<RouteAttribute>();

            if (controllerAttribute == null || routeAttribute == null)
                continue;

            var controllerRoute = routeAttribute.Template ?? string.Empty;
            var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (var method in methods)
            {
                var httpMethodAttribute = method.GetCustomAttributes()
                    .OfType<HttpMethodAttribute>()
                    .FirstOrDefault();

                if (httpMethodAttribute == null)
                    continue;

                var operationAttribute = method.GetCustomAttribute<OpenApiOperationAttribute>();
                var responseAttributes = method.GetCustomAttributes<OpenApiResponseAttribute>();

                var path = CombineRoutes(controllerRoute, httpMethodAttribute.Template ?? string.Empty);
                var httpMethod = GetHttpMethod(httpMethodAttribute);

                if (!document.Paths.ContainsKey(path))
                {
                    document.Paths[path] = new OpenApiPathItem();
                }

                var operation = new OpenApiOperation
                {
                    Summary = operationAttribute?.Summary ?? method.Name,
                    Description = operationAttribute?.Description,
                    OperationId = operationAttribute?.OperationId ?? GenerateOperationId(controllerType, method),
                    Deprecated = operationAttribute?.Deprecated ?? false,
                    Tags = new List<OpenApiTag>(
                        (operationAttribute?.Tags ?? new[] { controllerType.Name })
                        .Select(t => new OpenApiTag { Name = t })),
                    Parameters = GenerateParameters(method),
                    RequestBody = GenerateRequestBody(method),
                    Responses = GenerateResponses(method, responseAttributes)
                };

                document.Paths[path].AddOperation(GetOperationType(httpMethod), operation);
            }
        }
    }

    private List<OpenApiParameter> GenerateParameters(MethodInfo method)
    {
        var parameters = new List<OpenApiParameter>();

        var parameterInfos = method.GetParameters();
        foreach (var param in parameterInfos)
        {
            var paramAttribute = param.GetCustomAttribute<OpenApiParameterAttribute>();
            if (paramAttribute == null)
                continue;

            var parameter = new OpenApiParameter
            {
                Name = param.Name,
                In = ParameterLocation.Query,
                Description = paramAttribute.Description,
                Required = paramAttribute.Required,
                Schema = new OpenApiSchema
                {
                    Type = param.ParameterType.Name.ToLower(),
                    Example = paramAttribute.Example != null ?
                        new Microsoft.OpenApi.Any.OpenApiString(paramAttribute.Example.ToString()) : null,
                    Pattern = paramAttribute.Pattern
                }
            };

            parameters.Add(parameter);
        }

        return parameters;
    }

    private OpenApiRequestBody? GenerateRequestBody(MethodInfo method)
    {
        var bodyParam = method.GetParameters()
            .FirstOrDefault(p => p.GetCustomAttribute<FromBodyAttribute>() != null);

        if (bodyParam == null)
            return null;

        var paramType = bodyParam.ParameterType;
        var schemaAttribute = paramType.GetCustomAttribute<OpenApiSchemaAttribute>();

        return new OpenApiRequestBody
        {
            Description = schemaAttribute?.Description ?? $"Request body for {method.Name}",
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = GenerateSchema(paramType)
                }
            }
        };
    }

    private OpenApiResponses GenerateResponses(MethodInfo method, IEnumerable<OpenApiResponseAttribute> attributes)
    {
        var responses = new OpenApiResponses();

        // Add explicit response attributes
        foreach (var attr in attributes)
        {
            responses.Add(attr.StatusCode.ToString(), new OpenApiResponse
            {
                Description = attr.Description ?? GetHttpStatusDescription(attr.StatusCode),
                Content = attr.ResponseType != null ? new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = GenerateSchema(attr.ResponseType)
                    }
                } : new Dictionary<string, OpenApiMediaType>()
            });
        }

        // Add default success response if not specified
        if (!responses.ContainsKey("200"))
        {
            var returnType = method.ReturnType;
            responses.Add("200", new OpenApiResponse
            {
                Description = "Successful operation",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = GenerateSchema(returnType)
                    }
                }
            });
        }

        // Add default error responses
        responses.TryAdd("400", new OpenApiResponse
        {
            Description = "Bad Request"
        });

        responses.TryAdd("401", new OpenApiResponse
        {
            Description = "Unauthorized"
        });

        responses.TryAdd("500", new OpenApiResponse
        {
            Description = "Internal Server Error"
        });

        return responses;
    }

    private OpenApiSchema GenerateSchema(Type type)
    {
        var schemaAttribute = type.GetCustomAttribute<OpenApiSchemaAttribute>();

        var schema = new OpenApiSchema
        {
            Title = type.Name,
            Description = schemaAttribute?.Description,
            Type = GetSchemaType(type),
            Deprecated = schemaAttribute?.Deprecated ?? false
        };

        // Generate properties for complex types
        if (type.IsClass && type != typeof(string))
        {
            schema.Properties = new Dictionary<string, OpenApiSchema>();

            var properties = type.GetProperties();
            foreach (var prop in properties)
            {
                var propAttribute = prop.GetCustomAttribute<OpenApiSchemaAttribute>();
                schema.Properties[prop.Name] = new OpenApiSchema
                {
                    Type = GetSchemaType(prop.PropertyType),
                    Description = propAttribute?.Description,
                    Example = !string.IsNullOrEmpty(propAttribute?.Example) ?
                        new Microsoft.OpenApi.Any.OpenApiString(propAttribute.Example) : null,
                    Deprecated = propAttribute?.Deprecated ?? false,
                    Minimum = propAttribute?.Minimum,
                    Maximum = propAttribute?.Maximum,
                    MinLength = propAttribute?.MinLength,
                    MaxLength = propAttribute?.MaxLength,
                    Pattern = propAttribute?.Pattern
                };
            }
        }

        return schema;
    }

    private void AddSecuritySchemes(OpenApiDocument document)
    {
        document.Components.SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT authentication token"
            },
            ["ApiKey"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-API-Key",
                Description = "API Key authentication"
            }
        };

        document.SecurityRequirement = new Dictionary<string, IList<string>>
        {
            ["Bearer"] = new List<string>()
        };
    }

    private string CombineRoutes(string controller, string action)
    {
        var combined = $"{controller}/{action}".Replace("//", "/");
        return combined.StartsWith("/") ? combined : $"/{combined}";
    }

    private string GenerateOperationId(Type controllerType, MethodInfo method)
    {
        return $"{controllerType.Name}_{method.Name}";
    }

    private string GetHttpMethod(HttpMethodAttribute attr)
    {
        return attr switch
        {
            HttpGetAttribute => "get",
            HttpPostAttribute => "post",
            HttpPutAttribute => "put",
            HttpPatchAttribute => "patch",
            HttpDeleteAttribute => "delete",
            _ => "get"
        };
    }

    private OperationType GetOperationType(string method)
    {
        return method.ToLower() switch
        {
            "post" => OperationType.Post,
            "put" => OperationType.Put,
            "patch" => OperationType.Patch,
            "delete" => OperationType.Delete,
            _ => OperationType.Get
        };
    }

    private string GetSchemaType(Type type)
    {
        return type switch
        {
            _ when type == typeof(string) => "string",
            _ when type == typeof(int) || type == typeof(int?) => "integer",
            _ when type == typeof(double) || type == typeof(double?) => "number",
            _ when type == typeof(bool) || type == typeof(bool?) => "boolean",
            _ when type == typeof(DateTime) || type == typeof(DateTime?) => "string",
            _ when type.IsClass => "object",
            _ => "string"
        };
    }

    private string GetHttpStatusDescription(int statusCode)
    {
        return statusCode switch
        {
            200 => "OK",
            201 => "Created",
            204 => "No Content",
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            409 => "Conflict",
            500 => "Internal Server Error",
            503 => "Service Unavailable",
            _ => "Unknown"
        };
    }
}

/// <summary>
/// Operation filter for adding OpenAPI attributes from code
/// </summary>
public class OpenApiAttributeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var methodInfo = context.MethodInfo;
        if (methodInfo == null)
            return;

        // Apply operation attribute
        var opAttr = methodInfo.GetCustomAttribute<OpenApiOperationAttribute>();
        if (opAttr != null)
        {
            operation.Summary = opAttr.Summary ?? operation.Summary;
            operation.Description = opAttr.Description ?? operation.Description;
            operation.OperationId = opAttr.OperationId ?? operation.OperationId;
            operation.Deprecated = opAttr.Deprecated;
        }

        // Apply security attributes
        var secAttr = methodInfo.GetCustomAttribute<OpenApiSecurityAttribute>();
        if (secAttr != null)
        {
            var requirement = new Dictionary<string, IList<string>>();
            requirement[secAttr.SchemeName] = secAttr.Scopes?.ToList() ?? new List<string>();
            operation.Security = new List<Dictionary<string, IList<string>>> { requirement };
        }

        // Apply response attributes
        var responseAttrs = methodInfo.GetCustomAttributes<OpenApiResponseAttribute>();
        foreach (var respAttr in responseAttrs)
        {
            var statusCode = respAttr.StatusCode.ToString();
            if (!operation.Responses.ContainsKey(statusCode))
            {
                operation.Responses[statusCode] = new OpenApiResponse
                {
                    Description = respAttr.Description ?? "Response"
                };
            }
        }
    }
}

/// <summary>
/// Schema filter for applying schema attributes
/// </summary>
public class OpenApiAttributeSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var type = context.Type;
        var schemaAttr = type.GetCustomAttribute<OpenApiSchemaAttribute>();

        if (schemaAttr == null)
            return;

        schema.Description = schemaAttr.Description ?? schema.Description;
        schema.Deprecated = schemaAttr.Deprecated;
        schema.Example = !string.IsNullOrEmpty(schemaAttr.Example) ?
            new Microsoft.OpenApi.Any.OpenApiString(schemaAttr.Example) : null;

        // Apply to properties if this is an object
        if (schema.Properties != null)
        {
            foreach (var prop in type.GetProperties())
            {
                var propAttr = prop.GetCustomAttribute<OpenApiSchemaAttribute>();
                if (propAttr == null)
                    continue;

                if (schema.Properties.TryGetValue(prop.Name, out var propSchema))
                {
                    propSchema.Description = propAttr.Description ?? propSchema.Description;
                    propSchema.Deprecated = propAttr.Deprecated;
                    propSchema.Example = !string.IsNullOrEmpty(propAttr.Example) ?
                        new Microsoft.OpenApi.Any.OpenApiString(propAttr.Example) : null;
                    propSchema.Minimum = propAttr.Minimum;
                    propSchema.Maximum = propAttr.Maximum;
                    propSchema.MinLength = propAttr.MinLength;
                    propSchema.MaxLength = propAttr.MaxLength;
                    propSchema.Pattern = propAttr.Pattern;
                }
            }
        }
    }
}

/// <summary>
/// Extension methods for OpenAPI setup
/// </summary>
public static class OpenApiExtensions
{
    /// <summary>
    /// Adds auto-documenting Swagger/OpenAPI generation
    /// </summary>
    public static IServiceCollection AddAutoDocumentingSwagger(
        this IServiceCollection services,
        OpenApiGeneratorConfig? config = null)
    {
        config ??= new OpenApiGeneratorConfig();
        services.AddSingleton(config);
        services.AddSingleton<AutoDocumentingSwaggerGenerator>();

        services.AddSwaggerGen(options =>
        {
            // Add document info
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = config.Title,
                Description = config.Description,
                Version = config.Version,
                TermsOfService = string.IsNullOrEmpty(config.TermsOfService) ? null : new Uri(config.TermsOfService),
                License = config.License.HasValue ? new OpenApiLicense
                {
                    Name = config.License.Value.Name,
                    Url = new Uri(config.License.Value.Url)
                } : null,
                Contact = config.Contact.HasValue ? new OpenApiContact
                {
                    Name = config.Contact.Value.Name,
                    Email = config.Contact.Value.Email,
                    Url = new Uri(config.Contact.Value.Url)
                } : null
            });

            // Add servers
            foreach (var server in config.Servers)
            {
                options.AddServer(new OpenApiServer
                {
                    Url = server.Url,
                    Description = server.Description
                });
            }

            // Add filters for attribute-based documentation
            options.OperationFilter<OpenApiAttributeOperationFilter>();
            options.SchemaFilter<OpenApiAttributeSchemaFilter>();

            // Include XML comments if available
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // Add security schemes
            if (config.IncludeSecurity)
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT Bearer authentication token"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
                        new List<string>()
                    }
                });
            }
        });

        return services;
    }

    /// <summary>
    /// Uses auto-documenting Swagger UI
    /// </summary>
    public static IApplicationBuilder UseAutoDocumentingSwagger(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Loco API v1");
            options.RoutePrefix = "api/docs";
            options.DefaultModelsExpandDepth(2);
            options.DefaultModelExpandDepth(2);
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
            options.EnableFilter();
            options.ShowCommonExtensions();
            options.InjectStylesheet("/swagger-custom.css");
        });

        return app;
    }

    /// <summary>
    /// Adds OpenAPI server to document
    /// </summary>
    public static void AddServer(
        this SwaggerGenOptions options,
        OpenApiServer server)
    {
        // Custom implementation to add servers to Swagger doc
    }
}

/// <summary>
/// Example API controller with OpenAPI attributes
/// </summary>
[OpenApiSchema(Description = "Workflow data model")]
public class WorkflowDto
{
    [OpenApiSchema(Description = "Unique workflow identifier", MinLength = 1, MaxLength = 100)]
    public string? Id { get; set; }

    [OpenApiSchema(Description = "Workflow name", MinLength = 1, MaxLength = 255)]
    public string? Name { get; set; }

    [OpenApiSchema(Description = "Workflow description")]
    public string? Description { get; set; }

    [OpenApiSchema(Description = "Workflow is active")]
    public bool IsActive { get; set; }

    [OpenApiSchema(Description = "Created timestamp")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Example endpoint with auto-documentation
/// </summary>
[ApiController]
[Route("api/workflows")]
public class WorkflowsDocumentedController : ControllerBase
{
    /// <summary>
    /// Gets all workflows
    /// </summary>
    [HttpGet]
    [OpenApiOperation(
        Summary = "Get all workflows",
        Description = "Retrieves a list of all workflows in the system",
        Tags = new[] { "Workflows" })]
    [OpenApiResponse(200, "List of workflows")]
    [OpenApiResponse(401, "Unauthorized")]
    [OpenApiSecurity("Bearer", new[] { "read:workflows" })]
    public IActionResult GetWorkflows()
    {
        return Ok(new[] { new WorkflowDto { Id = "1", Name = "Sample Workflow" } });
    }

    /// <summary>
    /// Gets specific workflow
    /// </summary>
    [HttpGet("{id}")]
    [OpenApiOperation(
        Summary = "Get workflow by ID",
        Description = "Retrieves a specific workflow by its unique identifier",
        Tags = new[] { "Workflows" })]
    [OpenApiResponse(200, "Workflow found")]
    [OpenApiResponse(404, "Workflow not found")]
    [OpenApiSecurity("Bearer")]
    public IActionResult GetWorkflow(
        [OpenApiParameter(Description = "Workflow ID", Example = "123")] string id)
    {
        return Ok(new WorkflowDto { Id = id, Name = "Sample Workflow" });
    }

    /// <summary>
    /// Creates new workflow
    /// </summary>
    [HttpPost]
    [OpenApiOperation(
        Summary = "Create new workflow",
        Description = "Creates a new workflow with the provided configuration",
        Tags = new[] { "Workflows" })]
    [OpenApiResponse(201, "Workflow created")]
    [OpenApiResponse(400, "Invalid request")]
    [OpenApiSecurity("Bearer", new[] { "write:workflows" })]
    public IActionResult CreateWorkflow([FromBody] WorkflowDto workflow)
    {
        return CreatedAtAction(nameof(GetWorkflow), new { id = workflow.Id }, workflow);
    }
}
