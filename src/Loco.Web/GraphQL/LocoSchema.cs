using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using GraphQL.Resolvers;
using GraphQL.Subscription;
using GraphQL.DataLoader;
using Loco.Core.Models;
using Loco.Web.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Loco.Web.GraphQL.Schema;

/// <summary>
/// Main GraphQL schema for Loco API
/// </summary>
public class LocoSchema : GraphQL.Types.Schema
{
    public LocoSchema(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        Query = serviceProvider.GetRequiredService<LocoQuery>();
        Mutation = serviceProvider.GetRequiredService<LocoMutation>();
        Subscription = serviceProvider.GetRequiredService<LocoSubscription>();
    }
}

/// <summary>
/// GraphQL Query root
/// </summary>
public class LocoQuery : ObjectGraphType
{
    public LocoQuery(IFlowRepository flowRepository, IServiceProvider serviceProvider)
    {
        Name = "Query";
        Description = "Root query for Loco API";

        // Get all flows with pagination and filtering
        Field<ListGraphType<FlowType>>("flows")
            .Description("Get all flows with optional filtering")
            .Arguments(
                new QueryArguments(
                    new QueryArgument<IntGraphType> { Name = "skip", DefaultValue = 0 },
                    new QueryArgument<IntGraphType> { Name = "take", DefaultValue = 20 },
                    new QueryArgument<StringGraphType> { Name = "search" },
                    new QueryArgument<BooleanGraphType> { Name = "enabled" },
                    new QueryArgument<FlowSortInputType> { Name = "sort" }
                ))
            .ResolveAsync(async context =>
            {
                var skip = context.GetArgument<int>("skip");
                var take = context.GetArgument<int>("take");
                var search = context.GetArgument<string?>("search");
                var enabled = context.GetArgument<bool?>("enabled");
                var sort = context.GetArgument<FlowSortInput?>("sort");

                var flows = await flowRepository.GetAllAsync();
                
                // Apply filtering
                if (!string.IsNullOrEmpty(search))
                {
                    flows = flows.Where(f => 
                        f.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (f.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
                }

                if (enabled.HasValue)
                {
                    flows = flows.Where(f => f.Enabled == enabled.Value);
                }

                // Apply sorting
                if (sort != null)
                {
                    flows = ApplySorting(flows, sort);
                }

                // Apply pagination
                return flows.Skip(skip).Take(take).ToList();
            });

        // Get flow by ID
        Field<FlowType>("flow")
            .Description("Get a flow by ID")
            .Arguments(new QueryArguments(
                new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "id" }
            ))
            .ResolveAsync(async context =>
            {
                var id = context.GetArgument<string>("id");
                return await flowRepository.GetByIdAsync(id);
            });

        // Search flows with advanced filtering
        Field<FlowSearchResultType>("searchFlows")
            .Description("Advanced flow search with multiple criteria")
            .Arguments(new QueryArguments(
                new QueryArgument<NonNullGraphType<FlowSearchInputType>> { Name = "criteria" }
            ))
            .ResolveAsync(async context =>
            {
                var criteria = context.GetArgument<FlowSearchInput>("criteria");
                var flows = await flowRepository.GetAllAsync();
                
                var filtered = ApplyAdvancedFiltering(flows, criteria);
                var total = filtered.Count();
                var items = filtered
                    .Skip(criteria.Skip ?? 0)
                    .Take(criteria.Take ?? 20)
                    .ToList();

                return new FlowSearchResult
                {
                    Items = items,
                    TotalCount = total,
                    HasMore = total > (criteria.Skip ?? 0) + items.Count
                };
            });

        // Get flow statistics
        Field<FlowStatisticsType>("flowStatistics")
            .Description("Get statistics about flows")
            .ResolveAsync(async context =>
            {
                var flows = await flowRepository.GetAllAsync();
                return new FlowStatistics
                {
                    TotalFlows = flows.Count(),
                    EnabledFlows = flows.Count(f => f.Enabled),
                    DisabledFlows = flows.Count(f => !f.Enabled),
                    FlowsByCategory = flows
                        .GroupBy(f => f.Category ?? "Uncategorized")
                        .ToDictionary(g => g.Key, g => g.Count()),
                    LastUpdated = flows.Max(f => f.UpdatedAt) ?? DateTime.UtcNow
                };
            });

        // Validate flow configuration
        Field<FlowValidationResultType>("validateFlow")
            .Description("Validate a flow configuration")
            .Arguments(new QueryArguments(
                new QueryArgument<NonNullGraphType<FlowInputType>> { Name = "flow" }
            ))
            .Resolve(context =>
            {
                var flow = context.GetArgument<FlowDefinition>("flow");
                return ValidateFlow(flow);
            });
    }

    private IEnumerable<FlowDefinition> ApplySorting(IEnumerable<FlowDefinition> flows, FlowSortInput sort)
    {
        return sort.Field?.ToLower() switch
        {
            "name" => sort.Direction == SortDirection.Ascending 
                ? flows.OrderBy(f => f.Name) 
                : flows.OrderByDescending(f => f.Name),
            "createdat" => sort.Direction == SortDirection.Ascending 
                ? flows.OrderBy(f => f.CreatedAt) 
                : flows.OrderByDescending(f => f.CreatedAt),
            "updatedat" => sort.Direction == SortDirection.Ascending 
                ? flows.OrderBy(f => f.UpdatedAt) 
                : flows.OrderByDescending(f => f.UpdatedAt),
            _ => flows
        };
    }

    private IEnumerable<FlowDefinition> ApplyAdvancedFiltering(IEnumerable<FlowDefinition> flows, FlowSearchInput criteria)
    {
        if (!string.IsNullOrEmpty(criteria.Name))
            flows = flows.Where(f => f.Name.Contains(criteria.Name, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(criteria.Description))
            flows = flows.Where(f => f.Description?.Contains(criteria.Description, StringComparison.OrdinalIgnoreCase) ?? false);

        if (!string.IsNullOrEmpty(criteria.Category))
            flows = flows.Where(f => f.Category == criteria.Category);

        if (criteria.Enabled.HasValue)
            flows = flows.Where(f => f.Enabled == criteria.Enabled.Value);

        if (criteria.CreatedAfter.HasValue)
            flows = flows.Where(f => f.CreatedAt >= criteria.CreatedAfter.Value);

        if (criteria.CreatedBefore.HasValue)
            flows = flows.Where(f => f.CreatedAt <= criteria.CreatedBefore.Value);

        if (criteria.Tags?.Any() == true)
            flows = flows.Where(f => f.Tags?.Any(t => criteria.Tags.Contains(t)) ?? false);

        return flows;
    }

    private FlowValidationResult ValidateFlow(FlowDefinition flow)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate required fields
        if (string.IsNullOrEmpty(flow.Name))
            errors.Add("Flow name is required");

        if (flow.Triggers == null || !flow.Triggers.Any())
            warnings.Add("Flow has no triggers defined");

        if (flow.Actions == null || !flow.Actions.Any())
            errors.Add("Flow must have at least one action");

        // Validate trigger configurations
        if (flow.Triggers != null)
        {
            foreach (var trigger in flow.Triggers)
            {
                if (string.IsNullOrEmpty(trigger.Type))
                    errors.Add($"Trigger type is required");
            }
        }

        // Validate action configurations
        if (flow.Actions != null)
        {
            foreach (var action in flow.Actions)
            {
                if (string.IsNullOrEmpty(action.Type))
                    errors.Add($"Action type is required");
            }
        }

        return new FlowValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors,
            Warnings = warnings
        };
    }
}

/// <summary>
/// GraphQL Mutation root
/// </summary>
public class LocoMutation : ObjectGraphType
{
    public LocoMutation(IFlowRepository flowRepository, IServiceProvider serviceProvider)
    {
        Name = "Mutation";
        Description = "Root mutation for Loco API";

        // Create flow
        Field<FlowType>("createFlow")
            .Description("Create a new flow")
            .Arguments(new QueryArguments(
                new QueryArgument<NonNullGraphType<FlowInputType>> { Name = "flow" }
            ))
            .ResolveAsync(async context =>
            {
                var flow = context.GetArgument<FlowDefinition>("flow");
                flow.Id = Guid.NewGuid().ToString();
                flow.CreatedAt = DateTime.UtcNow;
                flow.UpdatedAt = DateTime.UtcNow;
                
                await flowRepository.CreateAsync(flow);
                return flow;
            });

        // Update flow
        Field<FlowType>("updateFlow")
            .Description("Update an existing flow")
            .Arguments(new QueryArguments(
                new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "id" },
                new QueryArgument<NonNullGraphType<FlowInputType>> { Name = "flow" }
            ))
            .ResolveAsync(async context =>
            {
                var id = context.GetArgument<string>("id");
                var flow = context.GetArgument<FlowDefinition>("flow");
                
                var existing = await flowRepository.GetByIdAsync(id);
                if (existing == null)
                    throw new ExecutionError($"Flow with ID {id} not found");

                flow.Id = id;
                flow.CreatedAt = existing.CreatedAt;
                flow.UpdatedAt = DateTime.UtcNow;
                
                await flowRepository.UpdateAsync(flow);
                return flow;
            });

        // Delete flow
        Field<BooleanGraphType>("deleteFlow")
            .Description("Delete a flow")
            .Arguments(new QueryArguments(
                new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "id" }
            ))
            .ResolveAsync(async context =>
            {
                var id = context.GetArgument<string>("id");
                return await flowRepository.DeleteAsync(id);
            });

        // Enable/Disable flow
        Field<FlowType>("toggleFlow")
            .Description("Enable or disable a flow")
            .Arguments(new QueryArguments(
                new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "id" },
                new QueryArgument<NonNullGraphType<BooleanGraphType>> { Name = "enabled" }
            ))
            .ResolveAsync(async context =>
            {
                var id = context.GetArgument<string>("id");
                var enabled = context.GetArgument<bool>("enabled");
                
                var flow = await flowRepository.GetByIdAsync(id);
                if (flow == null)
                    throw new ExecutionError($"Flow with ID {id} not found");

                flow.Enabled = enabled;
                flow.UpdatedAt = DateTime.UtcNow;
                
                await flowRepository.UpdateAsync(flow);
                return flow;
            });

        // Clone flow
        Field<FlowType>("cloneFlow")
            .Description("Clone an existing flow")
            .Arguments(new QueryArguments(
                new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "id" },
                new QueryArgument<StringGraphType> { Name = "newName" }
            ))
            .ResolveAsync(async context =>
            {
                var id = context.GetArgument<string>("id");
                var newName = context.GetArgument<string?>("newName");
                
                var original = await flowRepository.GetByIdAsync(id);
                if (original == null)
                    throw new ExecutionError($"Flow with ID {id} not found");

                var cloned = new FlowDefinition
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = newName ?? $"{original.Name} (Copy)",
                    Description = original.Description,
                    Enabled = false, // Cloned flows start disabled
                    Category = original.Category,
                    Tags = original.Tags?.ToList(),
                    Triggers = original.Triggers?.ToList(),
                    Conditions = original.Conditions?.ToList(),
                    Actions = original.Actions?.ToList(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                await flowRepository.CreateAsync(cloned);
                return cloned;
            });

        // Batch operations
        Field<BatchOperationResultType>("batchDeleteFlows")
            .Description("Delete multiple flows")
            .Arguments(new QueryArguments(
                new QueryArgument<NonNullGraphType<ListGraphType<StringGraphType>>> { Name = "ids" }
            ))
            .ResolveAsync(async context =>
            {
                var ids = context.GetArgument<List<string>>("ids");
                var results = new List<bool>();
                
                foreach (var id in ids)
                {
                    results.Add(await flowRepository.DeleteAsync(id));
                }

                return new BatchOperationResult
                {
                    TotalCount = ids.Count,
                    SuccessCount = results.Count(r => r),
                    FailedCount = results.Count(r => !r)
                };
            });

        Field<BatchOperationResultType>("batchEnableFlows")
            .Description("Enable or disable multiple flows")
            .Arguments(new QueryArguments(
                new QueryArgument<NonNullGraphType<ListGraphType<StringGraphType>>> { Name = "ids" },
                new QueryArgument<NonNullGraphType<BooleanGraphType>> { Name = "enabled" }
            ))
            .ResolveAsync(async context =>
            {
                var ids = context.GetArgument<List<string>>("ids");
                var enabled = context.GetArgument<bool>("enabled");
                var successCount = 0;
                
                foreach (var id in ids)
                {
                    var flow = await flowRepository.GetByIdAsync(id);
                    if (flow != null)
                    {
                        flow.Enabled = enabled;
                        flow.UpdatedAt = DateTime.UtcNow;
                        await flowRepository.UpdateAsync(flow);
                        successCount++;
                    }
                }

                return new BatchOperationResult
                {
                    TotalCount = ids.Count,
                    SuccessCount = successCount,
                    FailedCount = ids.Count - successCount
                };
            });
    }
}

/// <summary>
/// GraphQL Subscription root
/// </summary>
public class LocoSubscription : ObjectGraphType
{
    private readonly IFlowEventService _flowEventService;

    public LocoSubscription(IFlowEventService flowEventService)
    {
        Name = "Subscription";
        Description = "Root subscription for Loco API";
        _flowEventService = flowEventService;

        // Subscribe to flow changes
        AddField(new EventStreamFieldType
        {
            Name = "flowChanged",
            Type = typeof(FlowEventType),
            Resolver = new FuncFieldResolver<FlowEvent>(context => context.Source as FlowEvent),
            Subscriber = new EventStreamResolver<FlowEvent>(context =>
            {
                return _flowEventService.FlowChanges();
            })
        });

        // Subscribe to flow execution events
        AddField(new EventStreamFieldType
        {
            Name = "flowExecuted",
            Type = typeof(FlowExecutionEventType),
            Arguments = new QueryArguments(
                new QueryArgument<StringGraphType> { Name = "flowId" }
            ),
            Resolver = new FuncFieldResolver<FlowExecutionEvent>(context => context.Source as FlowExecutionEvent),
            Subscriber = new EventStreamResolver<FlowExecutionEvent>(context =>
            {
                var flowId = context.GetArgument<string?>("flowId");
                return _flowEventService.FlowExecutions(flowId);
            })
        });

        // Subscribe to system events
        AddField(new EventStreamFieldType
        {
            Name = "systemEvent",
            Type = typeof(SystemEventType),
            Resolver = new FuncFieldResolver<SystemEvent>(context => context.Source as SystemEvent),
            Subscriber = new EventStreamResolver<SystemEvent>(context =>
            {
                return _flowEventService.SystemEvents();
            })
        });
    }
}

// Supporting classes and enums
public enum SortDirection
{
    Ascending,
    Descending
}

public class FlowSortInput
{
    public string? Field { get; set; }
    public SortDirection Direction { get; set; } = SortDirection.Ascending;
}

public class FlowSearchInput
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool? Enabled { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public List<string>? Tags { get; set; }
    public int? Skip { get; set; }
    public int? Take { get; set; }
}

public class FlowSearchResult
{
    public List<FlowDefinition> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
}

public class FlowStatistics
{
    public int TotalFlows { get; set; }
    public int EnabledFlows { get; set; }
    public int DisabledFlows { get; set; }
    public Dictionary<string, int> FlowsByCategory { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

public class FlowValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class BatchOperationResult
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
}

public class FlowEvent
{
    public string Type { get; set; } = string.Empty; // created, updated, deleted
    public FlowDefinition Flow { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class FlowExecutionEvent
{
    public string FlowId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // started, completed, failed
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class SystemEvent
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // info, warning, error
    public DateTime Timestamp { get; set; }
}

// Service interfaces
public interface IFlowEventService
{
    IObservable<FlowEvent> FlowChanges();
    IObservable<FlowExecutionEvent> FlowExecutions(string? flowId = null);
    IObservable<SystemEvent> SystemEvents();
    Task PublishFlowChanged(string type, FlowDefinition flow);
    Task PublishFlowExecuted(FlowExecutionEvent executionEvent);
    Task PublishSystemEvent(SystemEvent systemEvent);
}
