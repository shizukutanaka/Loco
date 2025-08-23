using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Loco.Core.Models;

/// <summary>
/// Flow definition model - Clean architecture following Rob Pike's principles
/// </summary>
public class FlowDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; }
    
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
    
    [JsonPropertyName("triggers")]
    public List<TriggerDefinition> Triggers { get; set; } = new();
    
    [JsonPropertyName("conditions")]
    public List<ConditionDefinition> Conditions { get; set; } = new();
    
    [JsonPropertyName("actions")]
    public List<ActionDefinition> Actions { get; set; } = new();
    
    [JsonPropertyName("variables")]
    public Dictionary<string, object> Variables { get; set; } = new();
    
    [JsonPropertyName("permissions")]
    public PermissionSet Permissions { get; set; } = new();
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    [JsonPropertyName("downloads")]
    public int Downloads { get; set; } = 0;
}

public class TriggerDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; }
    
    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class ConditionDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; }
    
    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    [JsonPropertyName("negate")]
    public bool Negate { get; set; }
}

public class ActionDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; }
    
    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    [JsonPropertyName("continueOnError")]
    public bool ContinueOnError { get; set; }
    
    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }
    
    [JsonPropertyName("timeoutMs")]
    public int? TimeoutMs { get; set; }
}

public class PermissionSet
{
    [JsonPropertyName("network")]
    public bool Network { get; set; }
    
    [JsonPropertyName("fileSystem")]
    public bool FileSystem { get; set; }
    
    [JsonPropertyName("shell")]
    public bool Shell { get; set; }
    
    [JsonPropertyName("llm")]
    public bool Llm { get; set; }
    
    [JsonPropertyName("allowedDomains")]
    public List<string> AllowedDomains { get; set; } = new();
    
    [JsonPropertyName("allowedPaths")]
    public List<string> AllowedPaths { get; set; } = new();
}
