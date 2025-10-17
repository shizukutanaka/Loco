using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loco.Core.Visualization;

/// <summary>
/// State machine visualizer for generating workflow diagrams
/// Based on 2025 best practices: Mermaid.js, Graphviz, Temporal.io patterns
/// ステートマシン可視化 - ワークフロー図を生成
/// </summary>
public class StateMachineVisualizer
{
    /// <summary>
    /// Generate a Mermaid diagram from workflow definition
    /// ワークフロー定義からMermaid図を生成
    /// </summary>
    public string GenerateMermaidDiagram(WorkflowVisualization workflow, MermaidOptions? options = null)
    {
        options ??= MermaidOptions.Default;
        var sb = new StringBuilder();

        // Mermaid state diagram
        sb.AppendLine(options.DiagramType switch
        {
            DiagramType.StateDiagram => "stateDiagram-v2",
            DiagramType.Flowchart => "flowchart TD",
            _ => "graph TD"
        });

        // Add title if provided
        if (!string.IsNullOrEmpty(workflow.Title))
        {
            sb.AppendLine($"    title {SanitizeMermaid(workflow.Title)}");
        }

        // Add nodes
        foreach (var node in workflow.Nodes)
        {
            var nodeText = GenerateMermaidNode(node, options);
            sb.AppendLine($"    {nodeText}");
        }

        // Add edges (transitions)
        foreach (var edge in workflow.Edges)
        {
            var edgeText = GenerateMermaidEdge(edge, options);
            sb.AppendLine($"    {edgeText}");
        }

        // Add state annotations
        foreach (var node in workflow.Nodes.Where(n => n.NodeType == NodeType.Start))
        {
            sb.AppendLine($"    [{node.Id}] --> |start|");
        }

        foreach (var node in workflow.Nodes.Where(n => n.NodeType == NodeType.End))
        {
            sb.AppendLine($"    --> [{node.Id}] : end");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate a DOT (Graphviz) diagram from workflow definition
    /// ワークフロー定義からDOT（Graphviz）図を生成
    /// </summary>
    public string GenerateDotGraph(WorkflowVisualization workflow, DotOptions? options = null)
    {
        options ??= DotOptions.Default;
        var sb = new StringBuilder();

        sb.AppendLine("digraph Workflow {");
        sb.AppendLine("    rankdir=TB;");
        sb.AppendLine("    node [shape=box, style=rounded];");

        // Add title
        if (!string.IsNullOrEmpty(workflow.Title))
        {
            sb.AppendLine($"    labelloc=\"t\";");
            sb.AppendLine($"    label=\"{SanitizeDot(workflow.Title)}\";");
        }

        // Add nodes
        foreach (var node in workflow.Nodes)
        {
            var nodeText = GenerateDotNode(node, options);
            sb.AppendLine($"    {nodeText}");
        }

        // Add edges
        foreach (var edge in workflow.Edges)
        {
            var edgeText = GenerateDotEdge(edge, options);
            sb.AppendLine($"    {edgeText}");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generate an ASCII art diagram (for terminal display)
    /// ASCII アート図を生成（ターミナル表示用）
    /// </summary>
    public string GenerateAsciiDiagram(WorkflowVisualization workflow)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(workflow.Title))
        {
            sb.AppendLine($"=== {workflow.Title} ===");
            sb.AppendLine();
        }

        // Simple vertical flow
        var sortedNodes = TopologicalSort(workflow);

        foreach (var node in sortedNodes)
        {
            // Draw node
            var nodeBox = GenerateAsciiBox(node.Label);
            sb.AppendLine(nodeBox);

            // Draw arrows to next nodes
            var outgoingEdges = workflow.Edges.Where(e => e.From == node.Id).ToList();

            if (outgoingEdges.Any())
            {
                sb.AppendLine("    |");
                sb.AppendLine("    v");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate live execution diagram with current state highlighted
    /// 現在の状態をハイライトしたライブ実行図を生成
    /// </summary>
    public string GenerateLiveExecutionDiagram(
        WorkflowVisualization workflow,
        WorkflowExecutionState executionState)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {workflow.Title} - Live Execution ===");
        sb.AppendLine($"Status: {executionState.Status}");
        sb.AppendLine($"Started: {executionState.StartTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        var sortedNodes = TopologicalSort(workflow);

        foreach (var node in sortedNodes)
        {
            var nodeStatus = GetNodeExecutionStatus(node.Id, executionState);
            var symbol = nodeStatus switch
            {
                ExecutionStatus.Completed => "[✓]",
                ExecutionStatus.Running => "[►]",
                ExecutionStatus.Failed => "[✗]",
                ExecutionStatus.Pending => "[ ]",
                _ => "[ ]"
            };

            var label = $"{symbol} {node.Label}";

            if (nodeStatus == ExecutionStatus.Running)
            {
                label = $">>> {label} <<<";
            }

            sb.AppendLine($"  {label}");

            var outgoingEdges = workflow.Edges.Where(e => e.From == node.Id).ToList();
            if (outgoingEdges.Any())
            {
                sb.AppendLine("      |");
            }
        }

        return sb.ToString();
    }

    private string GenerateMermaidNode(VisualizationNode node, MermaidOptions options)
    {
        var shape = node.NodeType switch
        {
            NodeType.Start => $"{node.Id}([{SanitizeMermaid(node.Label)}])",
            NodeType.End => $"{node.Id}([{SanitizeMermaid(node.Label)}])",
            NodeType.Decision => $"{node.Id}{{{{{SanitizeMermaid(node.Label)}}}}}",
            NodeType.Process => $"{node.Id}[{SanitizeMermaid(node.Label)}]",
            _ => $"{node.Id}[{SanitizeMermaid(node.Label)}]"
        };

        // Add styling
        if (!string.IsNullOrEmpty(node.Color))
        {
            return $"{shape}:::{node.Color}";
        }

        return shape;
    }

    private string GenerateMermaidEdge(VisualizationEdge edge, MermaidOptions options)
    {
        var label = !string.IsNullOrEmpty(edge.Label) ? $"|{SanitizeMermaid(edge.Label)}|" : "";
        var arrow = edge.EdgeType switch
        {
            EdgeType.Conditional => "-.->",
            EdgeType.Loop => "-->",
            _ => "-->"
        };

        return $"{edge.From} {arrow}{label} {edge.To}";
    }

    private string GenerateDotNode(VisualizationNode node, DotOptions options)
    {
        var shape = node.NodeType switch
        {
            NodeType.Start => "ellipse",
            NodeType.End => "doublecircle",
            NodeType.Decision => "diamond",
            _ => "box"
        };

        var color = !string.IsNullOrEmpty(node.Color) ? $", fillcolor=\"{node.Color}\", style=filled" : "";

        return $"{SanitizeDot(node.Id)} [label=\"{SanitizeDot(node.Label)}\", shape={shape}{color}];";
    }

    private string GenerateDotEdge(VisualizationEdge edge, DotOptions options)
    {
        var label = !string.IsNullOrEmpty(edge.Label) ? $" [label=\"{SanitizeDot(edge.Label)}\"]" : "";
        var style = edge.EdgeType == EdgeType.Conditional ? " [style=dashed]" : "";

        return $"{SanitizeDot(edge.From)} -> {SanitizeDot(edge.To)}{label}{style};";
    }

    private string GenerateAsciiBox(string text)
    {
        var width = Math.Max(text.Length + 4, 20);
        var padding = (width - text.Length - 2) / 2;

        var sb = new StringBuilder();
        sb.AppendLine("  +" + new string('-', width - 2) + "+");
        sb.AppendLine("  |" + new string(' ', padding) + text + new string(' ', width - text.Length - padding - 2) + "|");
        sb.AppendLine("  +" + new string('-', width - 2) + "+");

        return sb.ToString();
    }

    private List<VisualizationNode> TopologicalSort(WorkflowVisualization workflow)
    {
        var result = new List<VisualizationNode>();
        var visited = new HashSet<string>();
        var adjacency = workflow.Edges.GroupBy(e => e.From).ToDictionary(g => g.Key, g => g.Select(e => e.To).ToList());

        // Find start nodes
        var startNodes = workflow.Nodes.Where(n => n.NodeType == NodeType.Start).ToList();
        if (!startNodes.Any())
        {
            startNodes = workflow.Nodes.Where(n => !workflow.Edges.Any(e => e.To == n.Id)).ToList();
        }

        void Visit(VisualizationNode node)
        {
            if (visited.Contains(node.Id))
                return;

            visited.Add(node.Id);
            result.Add(node);

            if (adjacency.TryGetValue(node.Id, out var neighbors))
            {
                foreach (var neighborId in neighbors)
                {
                    var neighbor = workflow.Nodes.FirstOrDefault(n => n.Id == neighborId);
                    if (neighbor != null)
                    {
                        Visit(neighbor);
                    }
                }
            }
        }

        foreach (var startNode in startNodes)
        {
            Visit(startNode);
        }

        // Add any remaining nodes
        foreach (var node in workflow.Nodes.Where(n => !visited.Contains(n.Id)))
        {
            result.Add(node);
        }

        return result;
    }

    private ExecutionStatus GetNodeExecutionStatus(string nodeId, WorkflowExecutionState state)
    {
        if (state.CurrentNodeId == nodeId)
            return ExecutionStatus.Running;

        if (state.CompletedNodes.Contains(nodeId))
            return ExecutionStatus.Completed;

        if (state.FailedNodes.Contains(nodeId))
            return ExecutionStatus.Failed;

        return ExecutionStatus.Pending;
    }

    private static string SanitizeMermaid(string text)
    {
        return text.Replace("\"", "'").Replace("\n", " ").Replace("\r", "");
    }

    private static string SanitizeDot(string text)
    {
        return text.Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}

#region Supporting Classes

/// <summary>
/// Workflow visualization definition
/// ワークフロー可視化定義
/// </summary>
public class WorkflowVisualization
{
    public string Title { get; set; } = string.Empty;
    public List<VisualizationNode> Nodes { get; set; } = new();
    public List<VisualizationEdge> Edges { get; set; } = new();
}

public class VisualizationNode
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public NodeType NodeType { get; set; } = NodeType.Process;
    public string? Color { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class VisualizationEdge
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string? Label { get; set; }
    public EdgeType EdgeType { get; set; } = EdgeType.Normal;
}

public enum NodeType
{
    Start,
    End,
    Process,
    Decision,
    SubProcess
}

public enum EdgeType
{
    Normal,
    Conditional,
    Loop
}

public class MermaidOptions
{
    public DiagramType DiagramType { get; set; } = DiagramType.Flowchart;
    public bool IncludeTitle { get; set; } = true;

    public static MermaidOptions Default => new();
}

public class DotOptions
{
    public string Rankdir { get; set; } = "TB"; // Top to Bottom
    public bool IncludeTitle { get; set; } = true;

    public static DotOptions Default => new();
}

public enum DiagramType
{
    Flowchart,
    StateDiagram,
    Graph
}

/// <summary>
/// Workflow execution state for live visualization
/// ライブ可視化用ワークフロー実行状態
/// </summary>
public class WorkflowExecutionState
{
    public string Status { get; set; } = "Running";
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public string? CurrentNodeId { get; set; }
    public HashSet<string> CompletedNodes { get; set; } = new();
    public HashSet<string> FailedNodes { get; set; } = new();
}

public enum ExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

#endregion
