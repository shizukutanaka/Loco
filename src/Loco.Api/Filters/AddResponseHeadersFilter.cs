using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Loco.Api.Filters;

/// <summary>
/// Swagger/OpenAPI filter to add standard response headers to all operations
/// </summary>
public class AddResponseHeadersFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Responses.Add("401", new OpenApiResponse
        {
            Description = "Unauthorized - Missing or invalid authentication token",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["code"] = new OpenApiSchema { Type = "string" },
                            ["message"] = new OpenApiSchema { Type = "string" },
                            ["traceId"] = new OpenApiSchema { Type = "string" }
                        }
                    }
                }
            }
        });

        operation.Responses.Add("429", new OpenApiResponse
        {
            Description = "Too Many Requests - Rate limit exceeded",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["code"] = new OpenApiSchema { Type = "string" },
                            ["message"] = new OpenApiSchema { Type = "string" },
                            ["retryAfter"] = new OpenApiSchema { Type = "integer" }
                        }
                    }
                }
            }
        });

        operation.Responses.Add("500", new OpenApiResponse
        {
            Description = "Internal Server Error",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["code"] = new OpenApiSchema { Type = "string" },
                            ["message"] = new OpenApiSchema { Type = "string" },
                            ["traceId"] = new OpenApiSchema { Type = "string" }
                        }
                    }
                }
            }
        });

        foreach (var response in operation.Responses.Values)
        {
            response.Headers.Add("X-Correlation-ID", new OpenApiHeader
            {
                Description = "Unique correlation ID for request tracing",
                Schema = new OpenApiSchema { Type = "string" }
            });

            response.Headers.Add("X-API-Version", new OpenApiHeader
            {
                Description = "API version",
                Schema = new OpenApiSchema { Type = "string" }
            });
        }
    }
}
