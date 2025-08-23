using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Loco.Core.FlowComposer;
using Loco.Core.Models;
using Microsoft.Extensions.Logging;

namespace Loco.Core.VisualFlow;

/// <summary>
/// Visual flow builder with category-based selection
/// Implements John Carmack's principle: focus on performance and practical functionality
/// </summary>
public class VisualFlowBuilder
{
    private readonly ILogger<VisualFlowBuilder> _logger;
    private readonly FlowComposerBuilder _composerBuilder;
    private readonly List<FlowProfile> _profiles;
    private FlowProfile _currentProfile;
    private FlowTask _currentTask;

    public VisualFlowBuilder(ILogger<VisualFlowBuilder> logger, FlowComposerBuilder composerBuilder)
    {
        _logger = logger;
        _composerBuilder = composerBuilder;
        _profiles = new List<FlowProfile>();
        InitializeDefaultProfiles();
    }

    /// <summary>
    /// Initialize default profiles
    /// </summary>
    private void InitializeDefaultProfiles()
    {
        // Default profile
        var defaultProfile = new FlowProfile
        {
            Id = "default",
            Name = "デフォルト",
            Icon = "🏠",
            Tasks = new List<FlowTask>()
        };
        _profiles.Add(defaultProfile);
        _currentProfile = defaultProfile;

        // Work profile
        _profiles.Add(new FlowProfile
        {
            Id = "work",
            Name = "仕事",
            Icon = "💼",
            Tasks = new List<FlowTask>()
        });

        // Home profile
        _profiles.Add(new FlowProfile
        {
            Id = "home",
            Name = "自宅",
            Icon = "🏡",
            Tasks = new List<FlowTask>()
        });
    }

    /// <summary>
    /// Get all profiles
    /// </summary>
    public List<FlowProfile> GetProfiles() => _profiles;

    /// <summary>
    /// Set active profile
    /// </summary>
    public void SetActiveProfile(string profileId)
    {
        _currentProfile = _profiles.FirstOrDefault(p => p.Id == profileId) ?? _profiles.First();
    }

    /// <summary>
    /// Create new task
    /// </summary>
    public FlowTask CreateTask(string name)
    {
        _currentTask = new FlowTask
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            CreatedAt = DateTime.UtcNow,
            Actions = new List<FlowAction>()
        };
        
        _currentProfile.Tasks.Add(_currentTask);
        return _currentTask;
    }

    /// <summary>
    /// Add component to current task
    /// </summary>
    public void AddComponent(string categoryId, string componentId, Dictionary<string, object> parameters)
    {
        if (_currentTask == null)
        {
            throw new InvalidOperationException("No active task. Create a task first.");
        }

        var component = _composerBuilder.GetComponent(componentId);
        if (component == null)
        {
            throw new ArgumentException($"Component not found: {componentId}");
        }

        var action = new FlowAction
        {
            Id = Guid.NewGuid().ToString(),
            CategoryId = categoryId,
            ComponentId = componentId,
            Name = component.Name,
            Icon = component.Icon,
            Parameters = parameters ?? new Dictionary<string, object>()
        };

        _currentTask.Actions.Add(action);
    }

    /// <summary>
    /// Get component selection menu
    /// </summary>
    public FlowMenu GetComponentMenu()
    {
        var menu = new FlowMenu
        {
            Categories = new List<FlowMenuCategory>()
        };

        foreach (var category in _composerBuilder.GetCategories())
        {
            var menuCategory = new FlowMenuCategory
            {
                Id = category.Id,
                Name = category.Name,
                Icon = category.Icon,
                Items = new List<FlowMenuItem>()
            };

            foreach (var component in category.Components)
            {
                menuCategory.Items.Add(new FlowMenuItem
                {
                    Id = component.Id,
                    Name = component.Name,
                    Description = component.Description,
                    Icon = component.Icon,
                    RequiresInput = component.Parameters.Any(p => p.Required)
                });
            }

            menu.Categories.Add(menuCategory);
        }

        return menu;
    }

    /// <summary>
    /// Build flow from task
    /// </summary>
    public FlowDefinition BuildFlow(string taskId)
    {
        var task = _currentProfile.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null)
        {
            throw new ArgumentException($"Task not found: {taskId}");
        }

        var flowBuilder = _composerBuilder.StartFlow(task.Name);

        foreach (var action in task.Actions)
        {
            var component = _composerBuilder.GetComponent(action.ComponentId);
            if (component == null) continue;

            switch (component.Type)
            {
                case ComponentType.Trigger:
                    flowBuilder.AddTrigger(action.ComponentId, action.Parameters);
                    break;
                case ComponentType.Condition:
                    flowBuilder.AddCondition(action.ComponentId, action.Parameters);
                    break;
                case ComponentType.Action:
                    flowBuilder.AddAction(action.ComponentId, action.Parameters);
                    break;
            }
        }

        return flowBuilder.Build();
    }

    /// <summary>
    /// Quick add common patterns
    /// </summary>
    public void AddQuickPattern(string patternId)
    {
        switch (patternId)
        {
            case "morning-routine":
                AddComponent("triggers", "time.schedule", new Dictionary<string, object> 
                { 
                    ["hour"] = 7, 
                    ["minute"] = 0 
                });
                AddComponent("actions", "tts.speak", new Dictionary<string, object> 
                { 
                    ["text"] = "おはようございます。今日も一日頑張りましょう。" 
                });
                break;

            case "file-organizer":
                AddComponent("triggers", "file.change", new Dictionary<string, object> 
                { 
                    ["path"] = @"C:\Downloads", 
                    ["pattern"] = "*.*" 
                });
                AddComponent("actions", "file.copy", new Dictionary<string, object> 
                { 
                    ["source"] = "${trigger.filePath}", 
                    ["destination"] = @"C:\Organized\${trigger.extension}\" 
                });
                break;

            case "system-monitor":
                AddComponent("triggers", "time.interval", new Dictionary<string, object> 
                { 
                    ["minutes"] = 5 
                });
                AddComponent("conditions", "system.memory", new Dictionary<string, object> 
                { 
                    ["threshold"] = 80, 
                    ["operator"] = ">" 
                });
                AddComponent("actions", "notification.show", new Dictionary<string, object> 
                { 
                    ["title"] = "メモリ警告", 
                    ["message"] = "メモリ使用率が80%を超えています" 
                });
                break;

            case "backup":
                AddComponent("triggers", "time.schedule", new Dictionary<string, object> 
                { 
                    ["hour"] = 23, 
                    ["minute"] = 0 
                });
                AddComponent("actions", "file.copy", new Dictionary<string, object> 
                { 
                    ["source"] = @"C:\Important", 
                    ["destination"] = @"D:\Backup\${date}" 
                });
                break;
        }
    }

    /// <summary>
    /// Export profile as JSON
    /// </summary>
    public string ExportProfile(string profileId)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null)
        {
            throw new ArgumentException($"Profile not found: {profileId}");
        }

        return JsonSerializer.Serialize(profile, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }

    /// <summary>
    /// Import profile from JSON
    /// </summary>
    public void ImportProfile(string json)
    {
        var profile = JsonSerializer.Deserialize<FlowProfile>(json);
        if (profile != null)
        {
            profile.Id = Guid.NewGuid().ToString(); // Generate new ID
            _profiles.Add(profile);
        }
    }
}

/// <summary>
/// Flow profile
/// </summary>
public class FlowProfile
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public List<FlowTask> Tasks { get; set; } = new();
    public Dictionary<string, object> Settings { get; set; } = new();
}

/// <summary>
/// Flow task
/// </summary>
public class FlowTask
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<FlowAction> Actions { get; set; } = new();
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Flow action
/// </summary>
public class FlowAction
{
    public string Id { get; set; }
    public string CategoryId { get; set; }
    public string ComponentId { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public int Order { get; set; }
}

/// <summary>
/// Component selection menu
/// </summary>
public class FlowMenu
{
    public List<FlowMenuCategory> Categories { get; set; } = new();
}

/// <summary>
/// Menu category
/// </summary>
public class FlowMenuCategory
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public List<FlowMenuItem> Items { get; set; } = new();
}

/// <summary>
/// Menu item
/// </summary>
public class FlowMenuItem
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Icon { get; set; }
    public bool RequiresInput { get; set; }
}
