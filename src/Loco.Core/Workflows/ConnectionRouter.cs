namespace Loco.Core.Workflows;

/// <summary>
/// Whether one outgoing edge should be followed after its source node ran.
///
/// Extracted from VisualWorkflowEngine so it is a pure function of the four
/// things that actually decide it, and so the editor's simulator has something
/// specific to mirror. The cases both sides must satisfy live in
/// <c>tests/shared/connection-routing-table.json</c>.
///
/// It was extracted because the simulator was not mirroring it at all: its edge
/// filter read only the source handle and never the edge's condition, so an
/// "error" edge was followed after a node SUCCEEDED. A user marking a cleanup
/// branch as the error path and pressing "Test Workflow" watched it run, which
/// is the opposite of what the engine does.
///
/// Two independent things decide this and they are not the same:
///
///   SourceOutput - which HANDLE the edge leaves from. The editor's condition
///   node draws two, "true" and "false"; every other node has one unnamed
///   output, which maps to "default".
///
///   Condition - what the EdgeConditionPanel writes: success, error or always.
/// </summary>
public static class ConnectionRouter
{
    /// <summary>Edge conditions the engine understands.</summary>
    public static readonly IReadOnlyList<string> SupportedConditions = new[]
    {
        "default", "success", "error", "always",
    };

    /// <summary>
    /// </summary>
    /// <param name="sourceOutput">The handle the edge leaves from, or null for the default output.</param>
    /// <param name="condition">The edge's condition, or null for the default.</param>
    /// <param name="sourceSucceeded">Whether the source node ran without failing.</param>
    /// <param name="verdict">A condition node's verdict, or null if the source produced none.</param>
    /// <param name="sourceNodeName">Only used to name the node in an error message.</param>
    /// <exception cref="InvalidOperationException">
    /// The edge claims a true/false branch its source produced no verdict for.
    /// </exception>
    /// <exception cref="NotSupportedException">The edge carries a condition the engine cannot evaluate.</exception>
    public static bool ShouldFollow(
        string? sourceOutput,
        string? condition,
        bool sourceSucceeded,
        bool? verdict,
        string sourceNodeName = "")
    {
        // A named branch handle answers first: a false branch must not run just
        // because the node that evaluated the condition did not throw.
        if (sourceOutput is "true" or "false")
        {
            if (verdict is null)
            {
                // The edge claims a branch its source did not produce a verdict
                // for. Following it - or its sibling - would be a guess, and
                // guessing here silently sends work down the wrong path.
                throw new InvalidOperationException(
                    $"Node '{sourceNodeName}' has a '{sourceOutput}' branch edge " +
                    "but produced no condition verdict. Only a condition node has true/false " +
                    "outputs; connect this edge to the node's default output instead.");
            }

            if (verdict.Value != (sourceOutput == "true"))
            {
                return false;
            }
        }

        if (condition is null or "default" or "success")
            return sourceSucceeded;

        if (condition == "error")
            return !sourceSucceeded;

        // "Always" is a real routing choice - a cleanup step that must run
        // whether or not the step before it failed - and the edge panel offers
        // it by name. It used to work only by accident, falling through the
        // "anything else" branch below.
        if (condition == "always")
            return true;

        // An expression the engine cannot evaluate. This used to `return true`,
        // which meant a custom condition always fired - the one outcome that
        // looks like it works while ignoring what was written. Refusing is the
        // honest answer until expressions are actually implemented.
        throw new NotSupportedException(
            $"Edge condition '{condition}' is not supported. Use 'success', " +
            "'error' or 'always', or put the comparison in a condition node.");
    }
}
