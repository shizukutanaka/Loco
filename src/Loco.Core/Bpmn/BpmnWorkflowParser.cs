using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Bpmn;

/// <summary>
/// BPMN 2.0 workflow parser and executor
/// </summary>
public interface IBpmnWorkflowParser
{
    /// <summary>
    /// Parses BPMN XML definition
    /// </summary>
    Task<BpmnWorkflowDefinition?> ParseAsync(string bpmnXml);

    /// <summary>
    /// Validates BPMN definition
    /// </summary>
    Task<bool> ValidateAsync(BpmnWorkflowDefinition definition);

    /// <summary>
    /// Executes BPMN workflow
    /// </summary>
    Task<BpmnExecutionResult> ExecuteAsync(
        BpmnWorkflowDefinition definition,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// BPMN Workflow Definition
/// </summary>
public class BpmnWorkflowDefinition
{
    /// <summary>
    /// Workflow ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Workflow name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Process elements
    /// </summary>
    public List<BpmnElement> Elements { get; set; } = new();

    /// <summary>
    /// Sequence flows (connections between elements)
    /// </summary>
    public List<BpmnSequenceFlow> SequenceFlows { get; set; } = new();

    /// <summary>
    /// Gateway definitions
    /// </summary>
    public List<BpmnGateway> Gateways { get; set; } = new();
}

/// <summary>
/// BPMN Element (Task, StartEvent, EndEvent, etc.)
/// </summary>
public class BpmnElement
{
    /// <summary>
    /// Element ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Element name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Element type (Task, StartEvent, EndEvent, etc.)
    /// </summary>
    public BpmnElementType Type { get; set; }

    /// <summary>
    /// Task type for service tasks
    /// </summary>
    public string? TaskType { get; set; }

    /// <summary>
    /// Associated workflow or action ID
    /// </summary>
    public string? AssociatedWorkflowId { get; set; }

    /// <summary>
    /// Incoming flow IDs
    /// </summary>
    public List<string> IncomingFlows { get; set; } = new();

    /// <summary>
    /// Outgoing flow IDs
    /// </summary>
    public List<string> OutgoingFlows { get; set; } = new();
}

/// <summary>
/// BPMN Element Type
/// </summary>
public enum BpmnElementType
{
    StartEvent,
    EndEvent,
    Task,
    ServiceTask,
    UserTask,
    ScriptTask,
    ExclusiveGateway,
    ParallelGateway,
    InclusiveGateway,
    EventBasedGateway
}

/// <summary>
/// BPMN Sequence Flow (connection between elements)
/// </summary>
public class BpmnSequenceFlow
{
    /// <summary>
    /// Flow ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Flow name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Source element ID
    /// </summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>
    /// Target element ID
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// Condition for conditional flows
    /// </summary>
    public string? Condition { get; set; }
}

/// <summary>
/// BPMN Gateway
/// </summary>
public class BpmnGateway
{
    /// <summary>
    /// Gateway ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gateway type
    /// </summary>
    public BpmnGatewayType Type { get; set; }

    /// <summary>
    /// Default flow ID
    /// </summary>
    public string? DefaultFlow { get; set; }
}

/// <summary>
/// BPMN Gateway Type
/// </summary>
public enum BpmnGatewayType
{
    Exclusive,
    Parallel,
    Inclusive,
    EventBased
}

/// <summary>
/// BPMN Execution Result
/// </summary>
public class BpmnExecutionResult
{
    /// <summary>
    /// Success flag
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Execution output
    /// </summary>
    public Dictionary<string, object?> Output { get; set; } = new();

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Execution duration
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Elements executed
    /// </summary>
    public List<string> ExecutedElements { get; set; } = new();
}

/// <summary>
/// BPMN Workflow Parser Implementation
/// </summary>
public class BpmnWorkflowParser : IBpmnWorkflowParser
{
    private const string BpmnNamespace = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    private const string BpmnDiNamespace = "http://www.omg.org/spec/BPMN/20100524/DI";
    private readonly ILogger<BpmnWorkflowParser> _logger;

    public BpmnWorkflowParser(ILogger<BpmnWorkflowParser> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BpmnWorkflowDefinition?> ParseAsync(string bpmnXml)
    {
        try
        {
            var doc = XDocument.Parse(bpmnXml);
            var root = doc.Root;

            if (root == null)
            {
                _logger.LogError("Invalid BPMN XML: empty document");
                return null;
            }

            var ns = XNamespace.Get(BpmnNamespace);
            var processes = root.Descendants(ns + "process").ToList();

            if (!processes.Any())
            {
                _logger.LogError("No processes found in BPMN document");
                return null;
            }

            var process = processes.First();
            var definition = new BpmnWorkflowDefinition
            {
                Id = process.Attribute("id")?.Value ?? "default-process",
                Name = process.Attribute("name")?.Value ?? "Default Process"
            };

            // Parse elements
            ParseElements(process, ns, definition);

            // Parse sequence flows
            ParseSequenceFlows(process, ns, definition);

            // Parse gateways
            ParseGateways(process, ns, definition);

            _logger.LogInformation(
                "BPMN workflow parsed: {WorkflowId}, Elements: {ElementCount}",
                definition.Id, definition.Elements.Count);

            return definition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse BPMN workflow");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateAsync(BpmnWorkflowDefinition definition)
    {
        try
        {
            // Check for start and end events
            var hasStartEvent = definition.Elements.Any(e => e.Type == BpmnElementType.StartEvent);
            var hasEndEvent = definition.Elements.Any(e => e.Type == BpmnElementType.EndEvent);

            if (!hasStartEvent || !hasEndEvent)
            {
                _logger.LogWarning("BPMN validation failed: missing start or end event");
                return false;
            }

            // Check sequence flows reference valid elements
            var elementIds = definition.Elements.Select(e => e.Id).ToHashSet();
            foreach (var flow in definition.SequenceFlows)
            {
                if (!elementIds.Contains(flow.SourceId) || !elementIds.Contains(flow.TargetId))
                {
                    _logger.LogWarning(
                        "BPMN validation failed: invalid sequence flow {FlowId}",
                        flow.Id);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BPMN validation error");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<BpmnExecutionResult> ExecuteAsync(
        BpmnWorkflowDefinition definition,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new BpmnExecutionResult { Success = false };

        try
        {
            if (!await ValidateAsync(definition))
            {
                result.ErrorMessage = "BPMN definition validation failed";
                return result;
            }

            var executedElements = new HashSet<string>();
            var context = new BpmnExecutionContext
            {
                Definition = definition,
                Variables = new Dictionary<string, object?>(parameters),
                ExecutedElements = executedElements
            };

            // Find start event
            var startEvent = definition.Elements.FirstOrDefault(e => e.Type == BpmnElementType.StartEvent);
            if (startEvent == null)
            {
                result.ErrorMessage = "No start event found";
                return result;
            }

            // Execute workflow
            await ExecuteElementsAsync(startEvent, context, cancellationToken);

            result.Success = true;
            result.Output = context.Variables;
            result.ExecutedElements = executedElements.ToList();

            _logger.LogInformation(
                "BPMN workflow execution completed: {WorkflowId}, Success: true",
                definition.Id);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "BPMN workflow execution failed");
        }
        finally
        {
            result.Duration = DateTime.UtcNow - startTime;
        }

        return result;
    }

    private void ParseElements(XElement process, XNamespace ns, BpmnWorkflowDefinition definition)
    {
        var elementTypes = new Dictionary<string, BpmnElementType>
        {
            { "startEvent", BpmnElementType.StartEvent },
            { "endEvent", BpmnElementType.EndEvent },
            { "task", BpmnElementType.Task },
            { "serviceTask", BpmnElementType.ServiceTask },
            { "userTask", BpmnElementType.UserTask },
            { "scriptTask", BpmnElementType.ScriptTask }
        };

        foreach (var (tagName, elementType) in elementTypes)
        {
            var elements = process.Descendants(ns + tagName);
            foreach (var element in elements)
            {
                var id = element.Attribute("id")?.Value;
                var name = element.Attribute("name")?.Value;

                if (id != null)
                {
                    definition.Elements.Add(new BpmnElement
                    {
                        Id = id,
                        Name = name ?? id,
                        Type = elementType
                    });
                }
            }
        }
    }

    private void ParseSequenceFlows(XElement process, XNamespace ns, BpmnWorkflowDefinition definition)
    {
        var flows = process.Descendants(ns + "sequenceFlow");
        foreach (var flow in flows)
        {
            var id = flow.Attribute("id")?.Value;
            var sourceRef = flow.Attribute("sourceRef")?.Value;
            var targetRef = flow.Attribute("targetRef")?.Value;
            var name = flow.Attribute("name")?.Value;

            if (id != null && sourceRef != null && targetRef != null)
            {
                definition.SequenceFlows.Add(new BpmnSequenceFlow
                {
                    Id = id,
                    Name = name,
                    SourceId = sourceRef,
                    TargetId = targetRef
                });
            }
        }
    }

    private void ParseGateways(XElement process, XNamespace ns, BpmnWorkflowDefinition definition)
    {
        var gatewayTypes = new Dictionary<string, BpmnGatewayType>
        {
            { "exclusiveGateway", BpmnGatewayType.Exclusive },
            { "parallelGateway", BpmnGatewayType.Parallel },
            { "inclusiveGateway", BpmnGatewayType.Inclusive },
            { "eventBasedGateway", BpmnGatewayType.EventBased }
        };

        foreach (var (tagName, gatewayType) in gatewayTypes)
        {
            var gateways = process.Descendants(ns + tagName);
            foreach (var gateway in gateways)
            {
                var id = gateway.Attribute("id")?.Value;
                var defaultFlow = gateway.Attribute("default")?.Value;

                if (id != null)
                {
                    definition.Gateways.Add(new BpmnGateway
                    {
                        Id = id,
                        Type = gatewayType,
                        DefaultFlow = defaultFlow
                    });
                }
            }
        }
    }

    private async Task ExecuteElementsAsync(
        BpmnElement element,
        BpmnExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.ExecutedElements.Contains(element.Id))
            return;

        context.ExecutedElements.Add(element.Id);

        // Execute current element based on type
        switch (element.Type)
        {
            case BpmnElementType.StartEvent:
            case BpmnElementType.Task:
            case BpmnElementType.ServiceTask:
                // Execute task logic here
                break;
            case BpmnElementType.EndEvent:
                return; // End of flow
        }

        // Follow outgoing sequence flows
        var outgoingFlows = context.Definition.SequenceFlows
            .Where(sf => sf.SourceId == element.Id)
            .ToList();

        foreach (var flow in outgoingFlows)
        {
            var nextElement = context.Definition.Elements.FirstOrDefault(e => e.Id == flow.TargetId);
            if (nextElement != null)
            {
                await ExecuteElementsAsync(nextElement, context, cancellationToken);
            }
        }
    }
}

/// <summary>
/// BPMN Execution Context
/// </summary>
internal class BpmnExecutionContext
{
    public BpmnWorkflowDefinition Definition { get; set; } = new();
    public Dictionary<string, object?> Variables { get; set; } = new();
    public HashSet<string> ExecutedElements { get; set; } = new();
}
