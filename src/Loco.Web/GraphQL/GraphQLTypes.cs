using System;
using System.Collections.Generic;
using GraphQL.Types;
using Loco.Core.Models;
using Loco.Web.GraphQL.Schema;

namespace Loco.Web.GraphQL.Types;

/// <summary>
/// GraphQL type for Flow
/// </summary>
public class FlowType : ObjectGraphType<FlowDefinition>
{
    public FlowType()
    {
        Name = "Flow";
        Description = "An automation flow definition";

        Field(f => f.Id).Description("The unique identifier of the flow");
        Field(f => f.Name).Description("The name of the flow");
        Field(f => f.Description, nullable: true).Description("The description of the flow");
        Field(f => f.Enabled).Description("Whether the flow is enabled");
        Field(f => f.Category, nullable: true).Description("The category of the flow");
        Field<ListGraphType<StringGraphType>>("tags", "The tags associated with the flow");
        
        Field<ListGraphType<TriggerType>>("triggers", "The triggers for the flow");
        Field<ListGraphType<ConditionType>>("conditions", "The conditions for the flow");
        Field<ListGraphType<ActionType>>("actions", "The actions for the flow");
        
        Field(f => f.CreatedAt, nullable: true).Description("When the flow was created");
        Field(f => f.UpdatedAt, nullable: true).Description("When the flow was last updated");
        
        Field<FlowMetadataType>("metadata", "Additional metadata for the flow", 
            resolve: context => context.Source.Metadata);
    }
}

/// <summary>
/// GraphQL input type for Flow
/// </summary>
public class FlowInputType : InputObjectGraphType<FlowDefinition>
{
    public FlowInputType()
    {
        Name = "FlowInput";
        Description = "Input for creating or updating a flow";

        Field(f => f.Name).Description("The name of the flow");
        Field(f => f.Description, nullable: true).Description("The description of the flow");
        Field(f => f.Enabled).Description("Whether the flow is enabled").DefaultValue(true);
        Field(f => f.Category, nullable: true).Description("The category of the flow");
        Field<ListGraphType<StringGraphType>>("tags", "The tags for the flow");
        
        Field<ListGraphType<TriggerInputType>>("triggers", "The triggers for the flow");
        Field<ListGraphType<ConditionInputType>>("conditions", "The conditions for the flow");
        Field<ListGraphType<ActionInputType>>("actions", "The actions for the flow");
    }
}

/// <summary>
/// GraphQL type for Trigger
/// </summary>
public class TriggerType : ObjectGraphType<TriggerDefinition>
{
    public TriggerType()
    {
        Name = "Trigger";
        Description = "A trigger that initiates a flow";

        Field(t => t.Type).Description("The type of trigger");
        Field<AnyScalarGraphType>("config", "The configuration for the trigger", 
            resolve: context => context.Source.Config);
    }
}

/// <summary>
/// GraphQL input type for Trigger
/// </summary>
public class TriggerInputType : InputObjectGraphType<TriggerDefinition>
{
    public TriggerInputType()
    {
        Name = "TriggerInput";
        Description = "Input for a trigger";

        Field(t => t.Type).Description("The type of trigger");
        Field<AnyScalarGraphType>("config", "The configuration for the trigger");
    }
}

/// <summary>
/// GraphQL type for Condition
/// </summary>
public class ConditionType : ObjectGraphType<ConditionDefinition>
{
    public ConditionType()
    {
        Name = "Condition";
        Description = "A condition that must be met for a flow to execute";

        Field(c => c.Type).Description("The type of condition");
        Field<AnyScalarGraphType>("config", "The configuration for the condition", 
            resolve: context => context.Source.Config);
    }
}

/// <summary>
/// GraphQL input type for Condition
/// </summary>
public class ConditionInputType : InputObjectGraphType<ConditionDefinition>
{
    public ConditionInputType()
    {
        Name = "ConditionInput";
        Description = "Input for a condition";

        Field(c => c.Type).Description("The type of condition");
        Field<AnyScalarGraphType>("config", "The configuration for the condition");
    }
}

/// <summary>
/// GraphQL type for Action
/// </summary>
public class ActionType : ObjectGraphType<ActionDefinition>
{
    public ActionType()
    {
        Name = "Action";
        Description = "An action to be executed in a flow";

        Field(a => a.Type).Description("The type of action");
        Field<AnyScalarGraphType>("config", "The configuration for the action", 
            resolve: context => context.Source.Config);
    }
}

/// <summary>
/// GraphQL input type for Action
/// </summary>
public class ActionInputType : InputObjectGraphType<ActionDefinition>
{
    public ActionInputType()
    {
        Name = "ActionInput";
        Description = "Input for an action";

        Field(a => a.Type).Description("The type of action");
        Field<AnyScalarGraphType>("config", "The configuration for the action");
    }
}

/// <summary>
/// GraphQL type for Flow Metadata
/// </summary>
public class FlowMetadataType : ObjectGraphType<Dictionary<string, object>>
{
    public FlowMetadataType()
    {
        Name = "FlowMetadata";
        Description = "Additional metadata for a flow";

        Field<StringGraphType>("author", "The author of the flow", 
            resolve: context => context.Source?.GetValueOrDefault("author"));
        Field<StringGraphType>("version", "The version of the flow", 
            resolve: context => context.Source?.GetValueOrDefault("version"));
        Field<IntGraphType>("executionCount", "Number of times the flow has been executed", 
            resolve: context => context.Source?.GetValueOrDefault("executionCount"));
        Field<DateTimeGraphType>("lastExecuted", "When the flow was last executed", 
            resolve: context => context.Source?.GetValueOrDefault("lastExecuted"));
    }
}

/// <summary>
/// GraphQL type for Flow Search Result
/// </summary>
public class FlowSearchResultType : ObjectGraphType<FlowSearchResult>
{
    public FlowSearchResultType()
    {
        Name = "FlowSearchResult";
        Description = "Result of a flow search operation";

        Field<ListGraphType<FlowType>>("items", "The flow items", 
            resolve: context => context.Source.Items);
        Field(r => r.TotalCount).Description("Total number of items matching the search");
        Field(r => r.HasMore).Description("Whether there are more items available");
    }
}

/// <summary>
/// GraphQL input type for Flow Search
/// </summary>
public class FlowSearchInputType : InputObjectGraphType<FlowSearchInput>
{
    public FlowSearchInputType()
    {
        Name = "FlowSearchInput";
        Description = "Input for searching flows";

        Field(s => s.Name, nullable: true).Description("Search by name");
        Field(s => s.Description, nullable: true).Description("Search by description");
        Field(s => s.Category, nullable: true).Description("Filter by category");
        Field(s => s.Enabled, nullable: true).Description("Filter by enabled status");
        Field(s => s.CreatedAfter, nullable: true).Description("Filter flows created after this date");
        Field(s => s.CreatedBefore, nullable: true).Description("Filter flows created before this date");
        Field<ListGraphType<StringGraphType>>("tags", "Filter by tags");
        Field(s => s.Skip, nullable: true).Description("Number of items to skip").DefaultValue(0);
        Field(s => s.Take, nullable: true).Description("Number of items to take").DefaultValue(20);
    }
}

/// <summary>
/// GraphQL input type for Flow Sort
/// </summary>
public class FlowSortInputType : InputObjectGraphType<FlowSortInput>
{
    public FlowSortInputType()
    {
        Name = "FlowSortInput";
        Description = "Input for sorting flows";

        Field(s => s.Field, nullable: true).Description("Field to sort by");
        Field<SortDirectionEnumType>("direction", "Sort direction");
    }
}

/// <summary>
/// GraphQL enum type for Sort Direction
/// </summary>
public class SortDirectionEnumType : EnumerationGraphType<SortDirection>
{
    public SortDirectionEnumType()
    {
        Name = "SortDirection";
        Description = "Sort direction";
    }
}

/// <summary>
/// GraphQL type for Flow Statistics
/// </summary>
public class FlowStatisticsType : ObjectGraphType<FlowStatistics>
{
    public FlowStatisticsType()
    {
        Name = "FlowStatistics";
        Description = "Statistics about flows";

        Field(s => s.TotalFlows).Description("Total number of flows");
        Field(s => s.EnabledFlows).Description("Number of enabled flows");
        Field(s => s.DisabledFlows).Description("Number of disabled flows");
        Field<AnyScalarGraphType>("flowsByCategory", "Flows grouped by category", 
            resolve: context => context.Source.FlowsByCategory);
        Field(s => s.LastUpdated).Description("When the statistics were last updated");
    }
}

/// <summary>
/// GraphQL type for Flow Validation Result
/// </summary>
public class FlowValidationResultType : ObjectGraphType<FlowValidationResult>
{
    public FlowValidationResultType()
    {
        Name = "FlowValidationResult";
        Description = "Result of flow validation";

        Field(v => v.IsValid).Description("Whether the flow is valid");
        Field<ListGraphType<StringGraphType>>("errors", "Validation errors", 
            resolve: context => context.Source.Errors);
        Field<ListGraphType<StringGraphType>>("warnings", "Validation warnings", 
            resolve: context => context.Source.Warnings);
    }
}

/// <summary>
/// GraphQL type for Batch Operation Result
/// </summary>
public class BatchOperationResultType : ObjectGraphType<BatchOperationResult>
{
    public BatchOperationResultType()
    {
        Name = "BatchOperationResult";
        Description = "Result of a batch operation";

        Field(b => b.TotalCount).Description("Total number of items processed");
        Field(b => b.SuccessCount).Description("Number of successful operations");
        Field(b => b.FailedCount).Description("Number of failed operations");
    }
}

/// <summary>
/// GraphQL type for Flow Event
/// </summary>
public class FlowEventType : ObjectGraphType<FlowEvent>
{
    public FlowEventType()
    {
        Name = "FlowEvent";
        Description = "An event related to a flow";

        Field(e => e.Type).Description("The type of event");
        Field<FlowType>("flow", "The flow associated with the event", 
            resolve: context => context.Source.Flow);
        Field(e => e.Timestamp).Description("When the event occurred");
    }
}

/// <summary>
/// GraphQL type for Flow Execution Event
/// </summary>
public class FlowExecutionEventType : ObjectGraphType<FlowExecutionEvent>
{
    public FlowExecutionEventType()
    {
        Name = "FlowExecutionEvent";
        Description = "An event related to flow execution";

        Field(e => e.FlowId).Description("The ID of the flow");
        Field(e => e.ExecutionId).Description("The ID of the execution");
        Field(e => e.Status).Description("The status of the execution");
        Field(e => e.Timestamp).Description("When the event occurred");
        Field<AnyScalarGraphType>("metadata", "Additional metadata", 
            resolve: context => context.Source.Metadata);
    }
}

/// <summary>
/// GraphQL type for System Event
/// </summary>
public class SystemEventType : ObjectGraphType<SystemEvent>
{
    public SystemEventType()
    {
        Name = "SystemEvent";
        Description = "A system-level event";

        Field(e => e.Type).Description("The type of event");
        Field(e => e.Message).Description("The event message");
        Field(e => e.Severity).Description("The severity of the event");
        Field(e => e.Timestamp).Description("When the event occurred");
    }
}

// Model classes for GraphQL types
public class TriggerDefinition
{
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object>? Config { get; set; }
}

public class ConditionDefinition
{
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object>? Config { get; set; }
}

public class ActionDefinition
{
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object>? Config { get; set; }
}
