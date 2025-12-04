using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.DurableExecution;

/// <summary>
/// アクティビティインターフェース
/// </summary>
public interface IActivity
{
    string Name { get; }
    Task<object?> ExecuteAsync(object? input, CancellationToken cancellationToken);
}

/// <summary>
/// 静的アクティビティレジストリ (AOT対応)
/// </summary>
public static class StaticActivityRegistry
{
    private static readonly Dictionary<string, IActivity> _activities = new();

    public static void Register(IActivity activity)
    {
        _activities[activity.Name] = activity;
    }

    public static IActivity? GetActivity(string name)
    {
        if (_activities.TryGetValue(name, out var activity))
        {
            return activity;
        }
        return null;
    }
}
