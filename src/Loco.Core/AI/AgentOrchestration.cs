using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.AI;

/// <summary>
/// AI Agent Orchestration Framework
/// AI エージェントオーケストレーションフレームワーク
///
/// Problem: 93% of executives interested in agentic workflows, 37% already using (2025 research)
/// 問題: 93%の経営者がエージェントワークフローに興味、37%が既に使用（2025年調査）
///
/// Solution: Multi-agent coordination with reflection, planning, and collaboration patterns
/// 解決策: リフレクション、プランニング、協調パターンを持つマルチエージェント調整
///
/// Key Features:
/// - Reflection Pattern: Agents self-evaluate and improve iteratively
/// - Planning Pattern: Autonomous multi-step workflow planning
/// - Collaboration Pattern: Multiple agents working together
/// - Tool Use Pattern: Agents can call external tools and APIs
/// - Parallelization: Split tasks for concurrent execution
/// </summary>
public class AgentOrchestrator
{
    private readonly Dictionary<string, Agent> _agents = new();
    private readonly Dictionary<string, AgentTeam> _teams = new();
    private readonly List<AgentExecution> _executionHistory = new();
    private readonly Dictionary<string, Tool> _tools = new();

    public AgentOrchestrator()
    {
        RegisterBuiltInTools();
    }

    #region Agent Management

    public void RegisterAgent(Agent agent)
    {
        _agents[agent.AgentId] = agent;
    }

    public Agent? GetAgent(string agentId)
    {
        return _agents.TryGetValue(agentId, out var agent) ? agent : null;
    }

    public List<Agent> GetAllAgents()
    {
        return _agents.Values.ToList();
    }

    #endregion

    #region Team Management

    public AgentTeam CreateTeam(string teamName, string description, List<string> agentIds, TeamOrchestrationMode mode)
    {
        var team = new AgentTeam
        {
            TeamId = Guid.NewGuid().ToString(),
            TeamName = teamName,
            Description = description,
            AgentIds = agentIds,
            OrchestrationMode = mode,
            CreatedAt = DateTime.UtcNow
        };

        _teams[team.TeamId] = team;
        return team;
    }

    public async Task<TeamExecutionResult> ExecuteTeamAsync(
        string teamId,
        string task,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var team = _teams[teamId];
        var result = new TeamExecutionResult
        {
            TeamId = teamId,
            TeamName = team.TeamName,
            Task = task,
            StartTime = DateTime.UtcNow
        };

        try
        {
            switch (team.OrchestrationMode)
            {
                case TeamOrchestrationMode.Sequential:
                    result.AgentResults = await ExecuteSequentialAsync(team, task, context, cancellationToken);
                    break;

                case TeamOrchestrationMode.Parallel:
                    result.AgentResults = await ExecuteParallelAsync(team, task, context, cancellationToken);
                    break;

                case TeamOrchestrationMode.Hierarchical:
                    result.AgentResults = await ExecuteHierarchicalAsync(team, task, context, cancellationToken);
                    break;

                case TeamOrchestrationMode.Collaborative:
                    result.AgentResults = await ExecuteCollaborativeAsync(team, task, context, cancellationToken);
                    break;
            }

            result.Success = result.AgentResults.All(r => r.Success);
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
        }

        return result;
    }

    private async Task<List<AgentExecutionResult>> ExecuteSequentialAsync(
        AgentTeam team,
        string task,
        Dictionary<string, object>? context,
        CancellationToken cancellationToken)
    {
        var results = new List<AgentExecutionResult>();
        var currentContext = context ?? new Dictionary<string, object>();

        foreach (var agentId in team.AgentIds)
        {
            var agent = _agents[agentId];
            var result = await ExecuteAgentAsync(agent, task, currentContext, cancellationToken);
            results.Add(result);

            // Pass output to next agent
            if (result.Success && result.Output != null)
            {
                currentContext[$"output_{agentId}"] = result.Output;
            }

            if (!result.Success)
                break; // Stop on failure in sequential mode
        }

        return results;
    }

    private async Task<List<AgentExecutionResult>> ExecuteParallelAsync(
        AgentTeam team,
        string task,
        Dictionary<string, object>? context,
        CancellationToken cancellationToken)
    {
        var tasks = team.AgentIds.Select(agentId =>
        {
            var agent = _agents[agentId];
            return ExecuteAgentAsync(agent, task, context ?? new Dictionary<string, object>(), cancellationToken);
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private async Task<List<AgentExecutionResult>> ExecuteHierarchicalAsync(
        AgentTeam team,
        string task,
        Dictionary<string, object>? context,
        CancellationToken cancellationToken)
    {
        var results = new List<AgentExecutionResult>();

        // First agent is the coordinator
        var coordinator = _agents[team.AgentIds[0]];
        var plan = await ExecuteAgentAsync(coordinator, $"Create execution plan for: {task}", context ?? new Dictionary<string, object>(), cancellationToken);
        results.Add(plan);

        if (!plan.Success || plan.Output == null)
            return results;

        // Execute worker agents based on coordinator's plan
        var workerAgents = team.AgentIds.Skip(1).Select(id => _agents[id]).ToList();
        var workerTasks = workerAgents.Select(agent =>
            ExecuteAgentAsync(agent, task, new Dictionary<string, object> { ["plan"] = plan.Output }, cancellationToken)
        );

        var workerResults = await Task.WhenAll(workerTasks);
        results.AddRange(workerResults);

        return results;
    }

    private async Task<List<AgentExecutionResult>> ExecuteCollaborativeAsync(
        AgentTeam team,
        string task,
        Dictionary<string, object>? context,
        CancellationToken cancellationToken)
    {
        var results = new List<AgentExecutionResult>();
        var sharedContext = context ?? new Dictionary<string, object>();
        const int maxIterations = 5;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            sharedContext["iteration"] = iteration;
            sharedContext["previous_results"] = results.Where(r => r.Success).Select(r => r.Output).ToList();

            foreach (var agentId in team.AgentIds)
            {
                var agent = _agents[agentId];
                var result = await ExecuteAgentAsync(agent, task, sharedContext, cancellationToken);
                results.Add(result);

                // Update shared context with this agent's contribution
                if (result.Success && result.Output != null)
                {
                    sharedContext[$"agent_{agentId}_output"] = result.Output;
                }
            }

            // Check if consensus/completion reached
            var latestResults = results.TakeLast(team.AgentIds.Count).ToList();
            if (latestResults.All(r => r.Success && r.Metadata.ContainsKey("completed") && (bool)r.Metadata["completed"]))
            {
                break;
            }
        }

        return results;
    }

    #endregion

    #region Agent Execution Patterns

    /// <summary>
    /// Reflection Pattern: Agent evaluates and improves its output iteratively
    /// リフレクションパターン: エージェントが出力を反復的に評価・改善
    /// </summary>
    public async Task<AgentExecutionResult> ExecuteWithReflectionAsync(
        string agentId,
        string task,
        Dictionary<string, object>? context = null,
        int maxReflections = 3,
        CancellationToken cancellationToken = default)
    {
        var agent = _agents[agentId];
        var currentContext = context ?? new Dictionary<string, object>();
        AgentExecutionResult? bestResult = null;
        double bestScore = 0;

        for (int i = 0; i < maxReflections; i++)
        {
            // Generate solution
            var result = await ExecuteAgentAsync(agent, task, currentContext, cancellationToken);

            if (!result.Success)
                return result;

            // Self-evaluate
            var evaluation = await ExecuteAgentAsync(
                agent,
                $"Evaluate this solution and provide a quality score (0-1) and suggestions for improvement:\n{result.Output}",
                currentContext,
                cancellationToken
            );

            var score = ExtractScore(evaluation.Output?.ToString());
            result.ReflectionScore = score;
            result.ReflectionFeedback = evaluation.Output?.ToString();

            if (score > bestScore)
            {
                bestScore = score;
                bestResult = result;
            }

            // If quality is good enough, stop
            if (score >= 0.9)
                break;

            // Add reflection feedback for next iteration
            currentContext["previous_attempt"] = result.Output;
            currentContext["feedback"] = evaluation.Output;
        }

        return bestResult ?? new AgentExecutionResult { Success = false, Error = "No valid result produced" };
    }

    /// <summary>
    /// Planning Pattern: Agent creates and executes multi-step plan
    /// プランニングパターン: エージェントがマルチステッププランを作成・実行
    /// </summary>
    public async Task<PlanExecutionResult> ExecuteWithPlanningAsync(
        string agentId,
        string goal,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var agent = _agents[agentId];
        var currentContext = context ?? new Dictionary<string, object>();

        // Step 1: Create plan
        var planningResult = await ExecuteAgentAsync(
            agent,
            $"Create a detailed step-by-step plan to achieve this goal: {goal}",
            currentContext,
            cancellationToken
        );

        if (!planningResult.Success)
        {
            return new PlanExecutionResult
            {
                Success = false,
                Error = "Failed to create plan: " + planningResult.Error
            };
        }

        var plan = ParsePlan(planningResult.Output?.ToString() ?? "");
        var result = new PlanExecutionResult
        {
            Goal = goal,
            Plan = plan,
            StartTime = DateTime.UtcNow
        };

        // Step 2: Execute each step
        foreach (var step in plan.Steps)
        {
            var stepResult = await ExecuteAgentAsync(agent, step.Description, currentContext, cancellationToken);
            step.Status = stepResult.Success ? StepStatus.Completed : StepStatus.Failed;
            step.Output = stepResult.Output;
            step.Error = stepResult.Error;

            result.StepResults.Add(stepResult);

            if (!stepResult.Success)
            {
                result.Success = false;
                result.Error = $"Step {step.StepNumber} failed: {stepResult.Error}";
                break;
            }

            // Update context with step output
            currentContext[$"step_{step.StepNumber}_output"] = stepResult.Output;
        }

        result.Success = plan.Steps.All(s => s.Status == StepStatus.Completed);
        result.EndTime = DateTime.UtcNow;
        result.Duration = result.EndTime - result.StartTime;

        return result;
    }

    /// <summary>
    /// Tool Use Pattern: Agent can call external tools/APIs
    /// ツール使用パターン: エージェントが外部ツール/APIを呼び出し可能
    /// </summary>
    public async Task<AgentExecutionResult> ExecuteWithToolsAsync(
        string agentId,
        string task,
        List<string> availableTools,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var agent = _agents[agentId];
        var currentContext = context ?? new Dictionary<string, object>();
        currentContext["available_tools"] = availableTools;

        var result = await ExecuteAgentAsync(agent, task, currentContext, cancellationToken);

        // Check if agent wants to use tools
        if (result.Metadata.ContainsKey("tool_calls"))
        {
            var toolCalls = result.Metadata["tool_calls"] as List<ToolCall>;
            if (toolCalls != null)
            {
                foreach (var toolCall in toolCalls)
                {
                    if (_tools.TryGetValue(toolCall.ToolName, out var tool))
                    {
                        var toolResult = await tool.ExecuteAsync(toolCall.Parameters, cancellationToken);
                        currentContext[$"tool_{toolCall.ToolName}_result"] = toolResult;
                    }
                }

                // Re-execute agent with tool results
                result = await ExecuteAgentAsync(agent, task, currentContext, cancellationToken);
            }
        }

        return result;
    }

    #endregion

    #region Core Agent Execution

    private async Task<AgentExecutionResult> ExecuteAgentAsync(
        Agent agent,
        string task,
        Dictionary<string, object> context,
        CancellationToken cancellationToken)
    {
        var execution = new AgentExecution
        {
            ExecutionId = Guid.NewGuid().ToString(),
            AgentId = agent.AgentId,
            AgentName = agent.Name,
            Task = task,
            StartTime = DateTime.UtcNow
        };

        var result = new AgentExecutionResult
        {
            AgentId = agent.AgentId,
            AgentName = agent.Name,
            Task = task,
            StartTime = execution.StartTime
        };

        try
        {
            // Simulate AI agent execution (in real implementation, call LLM API)
            await Task.Delay(100, cancellationToken); // Simulate processing

            var prompt = BuildPrompt(agent, task, context);
            var output = await SimulateAgentThinking(agent, prompt, cancellationToken);

            result.Output = output;
            result.Success = true;
            result.TokensUsed = EstimateTokens(prompt) + EstimateTokens(output);

            execution.Success = true;
            execution.Output = output;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            execution.Success = false;
            execution.Error = ex.Message;
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            execution.EndTime = result.EndTime;
            execution.Duration = result.Duration;
            _executionHistory.Add(execution);
        }

        return result;
    }

    private string BuildPrompt(Agent agent, string task, Dictionary<string, object> context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are {agent.Name}: {agent.Description}");
        sb.AppendLine($"Role: {agent.Role}");
        sb.AppendLine();
        sb.AppendLine($"Task: {task}");
        sb.AppendLine();

        if (context.Any())
        {
            sb.AppendLine("Context:");
            foreach (var kvp in context)
            {
                sb.AppendLine($"- {kvp.Key}: {JsonSerializer.Serialize(kvp.Value)}");
            }
        }

        if (agent.Capabilities.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Your capabilities:");
            foreach (var capability in agent.Capabilities)
            {
                sb.AppendLine($"- {capability}");
            }
        }

        return sb.ToString();
    }

    private async Task<string> SimulateAgentThinking(Agent agent, string prompt, CancellationToken cancellationToken)
    {
        // In real implementation, call LLM API (OpenAI, Anthropic, etc.)
        // For now, return simulated response
        await Task.Delay(50, cancellationToken);
        return $"[{agent.Name}] Analyzed task and generated response based on role: {agent.Role}. " +
               $"Applied capabilities: {string.Join(", ", agent.Capabilities.Take(2))}";
    }

    #endregion

    #region Tool Management

    private void RegisterBuiltInTools()
    {
        RegisterTool(new Tool
        {
            ToolId = "web_search",
            Name = "Web Search",
            Description = "Search the web for information",
            Parameters = new() { { "query", "string" } }
        });

        RegisterTool(new Tool
        {
            ToolId = "calculator",
            Name = "Calculator",
            Description = "Perform mathematical calculations",
            Parameters = new() { { "expression", "string" } }
        });

        RegisterTool(new Tool
        {
            ToolId = "code_interpreter",
            Name = "Code Interpreter",
            Description = "Execute Python code",
            Parameters = new() { { "code", "string" } }
        });

        RegisterTool(new Tool
        {
            ToolId = "file_read",
            Name = "File Reader",
            Description = "Read contents of a file",
            Parameters = new() { { "path", "string" } }
        });

        RegisterTool(new Tool
        {
            ToolId = "api_call",
            Name = "API Caller",
            Description = "Make HTTP API requests",
            Parameters = new() { { "url", "string" }, { "method", "string" }, { "body", "object" } }
        });
    }

    public void RegisterTool(Tool tool)
    {
        _tools[tool.ToolId] = tool;
    }

    public List<Tool> GetAvailableTools()
    {
        return _tools.Values.ToList();
    }

    #endregion

    #region Analytics

    public AgentAnalytics GetAgentAnalytics(string agentId)
    {
        var executions = _executionHistory.Where(e => e.AgentId == agentId).ToList();

        return new AgentAnalytics
        {
            AgentId = agentId,
            TotalExecutions = executions.Count,
            SuccessfulExecutions = executions.Count(e => e.Success),
            FailedExecutions = executions.Count(e => !e.Success),
            AverageDuration = executions.Any() ? TimeSpan.FromMilliseconds(executions.Average(e => e.Duration.TotalMilliseconds)) : TimeSpan.Zero,
            TotalDuration = TimeSpan.FromMilliseconds(executions.Sum(e => e.Duration.TotalMilliseconds))
        };
    }

    public List<AgentExecution> GetExecutionHistory(string? agentId = null, int limit = 100)
    {
        var query = _executionHistory.AsEnumerable();

        if (!string.IsNullOrEmpty(agentId))
            query = query.Where(e => e.AgentId == agentId);

        return query.OrderByDescending(e => e.StartTime).Take(limit).ToList();
    }

    #endregion

    #region Helpers

    private double ExtractScore(string? output)
    {
        if (string.IsNullOrEmpty(output))
            return 0.5;

        // Simple extraction logic (in real implementation, parse JSON or structured output)
        if (output.Contains("0.9") || output.Contains("90%"))
            return 0.9;
        if (output.Contains("0.8") || output.Contains("80%"))
            return 0.8;
        if (output.Contains("0.7") || output.Contains("70%"))
            return 0.7;

        return 0.6; // Default
    }

    private Plan ParsePlan(string planText)
    {
        var plan = new Plan();
        var lines = planText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int stepNumber = 1;

        foreach (var line in lines)
        {
            if (line.Contains("Step") || line.Contains("step") || line.StartsWith($"{stepNumber}."))
            {
                plan.Steps.Add(new PlanStep
                {
                    StepNumber = stepNumber++,
                    Description = line.Trim(),
                    Status = StepStatus.Pending
                });
            }
        }

        return plan;
    }

    private int EstimateTokens(string text)
    {
        // Rough estimation: ~4 characters per token
        return text.Length / 4;
    }

    #endregion
}

#region Models

public class Agent
{
    public string AgentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AgentRole Role { get; set; }
    public List<string> Capabilities { get; set; } = new();
    public string Model { get; set; } = "gpt-4"; // LLM model to use
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
}

public enum AgentRole
{
    Coordinator,    // Plans and delegates
    Executor,       // Performs tasks
    Reviewer,       // Evaluates outputs
    Specialist,     // Domain expert
    Assistant       // General purpose
}

public class AgentTeam
{
    public string TeamId { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AgentIds { get; set; } = new();
    public TeamOrchestrationMode OrchestrationMode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum TeamOrchestrationMode
{
    Sequential,      // One agent after another
    Parallel,        // All agents at once
    Hierarchical,    // Coordinator + workers
    Collaborative    // Agents iterate together
}

public class AgentExecutionResult
{
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public object? Output { get; set; }
    public string? Error { get; set; }
    public int TokensUsed { get; set; }
    public double? ReflectionScore { get; set; }
    public string? ReflectionFeedback { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class TeamExecutionResult
{
    public string TeamId { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<AgentExecutionResult> AgentResults { get; set; } = new();
}

public class PlanExecutionResult
{
    public string Goal { get; set; } = string.Empty;
    public Plan Plan { get; set; } = new();
    public List<AgentExecutionResult> StepResults { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class Plan
{
    public List<PlanStep> Steps { get; set; } = new();
}

public class PlanStep
{
    public int StepNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public StepStatus Status { get; set; }
    public object? Output { get; set; }
    public string? Error { get; set; }
}

public enum StepStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public class Tool
{
    public string ToolId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();

    public async Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        // Simulate tool execution
        await Task.Delay(50, cancellationToken);
        return new { result = $"Tool {Name} executed successfully" };
    }
}

public class ToolCall
{
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class AgentExecution
{
    public string ExecutionId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public object? Output { get; set; }
    public string? Error { get; set; }
}

public class AgentAnalytics
{
    public string AgentId { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions * 100 : 0;
}

#endregion
