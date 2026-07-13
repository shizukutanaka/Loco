using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loco.Core.Workflows;

/// <summary>
/// The persisted shape of a workflow, mirroring the Visual Editor's
/// <c>Workflow</c> TypeScript interface (src/Loco.VisualEditor/src/types/workflow.ts)
/// one-to-one so that GET returns exactly what PUT saved - a lossless round trip
/// with no server-side mapping in the CRUD path. Mapping into the execution
/// engine's <see cref="VisualWorkflow"/> happens only at execute/validate time.
///
/// Every class carries <see cref="JsonExtensionDataAttribute"/> so fields added
/// by newer frontend versions survive a save/load cycle instead of being
/// silently dropped.
/// </summary>
public class StoredWorkflow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<StoredWorkflowNode> Nodes { get; set; } = new();
    public List<StoredWorkflowEdge> Edges { get; set; } = new();
    public StoredWorkflowMetadata Metadata { get; set; } = new();

    /// <summary>ISO-8601 string, as produced by the frontend (new Date().toISOString()).</summary>
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>ISO-8601 string, as produced by the frontend.</summary>
    public string UpdatedAt { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>Mirrors the frontend <c>WorkflowNode</c> interface.</summary>
public class StoredWorkflowNode
{
    public string Id { get; set; } = string.Empty;

    /// <summary>trigger | action | condition | transform | loop (kept as string for losslessness).</summary>
    public string Type { get; set; } = string.Empty;

    public StoredPosition Position { get; set; } = new();
    public StoredNodeData Data { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>Mirrors the frontend <c>Position</c> interface.</summary>
public class StoredPosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>Mirrors the frontend <c>NodeData</c> interface.</summary>
public class StoredNodeData
{
    public string Label { get; set; } = string.Empty;
    public string? Integration { get; set; }

    /// <summary>
    /// Open-ended per-node configuration (frontend: Record&lt;string, any&gt;).
    /// JsonElement values preserve arbitrary JSON losslessly.
    /// </summary>
    public Dictionary<string, JsonElement> Config { get; set; } = new();

    public string? Description { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>Mirrors the frontend <c>WorkflowEdge</c> interface.</summary>
public class StoredWorkflowEdge
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? SourceHandle { get; set; }
    public string? TargetHandle { get; set; }

    /// <summary>default | conditional (kept as string for losslessness).</summary>
    public string? Type { get; set; }

    public StoredEdgeData? Data { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>Mirrors the frontend <c>EdgeData</c> interface.</summary>
public class StoredEdgeData
{
    public string? Condition { get; set; }
    public string? Label { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>Mirrors the frontend <c>WorkflowMetadata</c> interface.</summary>
public class StoredWorkflowMetadata
{
    public string Version { get; set; } = "1.0.0";
    public string? Author { get; set; }
    public List<string>? Tags { get; set; }
    public bool IsPublic { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
