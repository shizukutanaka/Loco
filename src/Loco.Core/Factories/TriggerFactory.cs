using System;
using System.Collections.Generic;
using System.Text.Json;
using Loco.Core.Interfaces;
using Loco.Core.Models;
using Loco.Core.Triggers;

namespace Loco.Core.Factories;

/// <summary>
/// Default implementation of the trigger factory.
/// </summary>
public class TriggerFactory : ITriggerFactory
{
    /// <inheritdoc />
    public IRuntimeTrigger? CreateTrigger(AutomationDsl.TriggerDefinition definition)
    {
        if (definition == null)
            return null;

        var triggerId = Guid.NewGuid().ToString();

        return definition.Type?.ToLower() switch
        {
            "time" => CreateTimeTrigger(triggerId, definition.Parameters),
            "filesystem" => CreateFileSystemTrigger(triggerId, definition.Parameters),
            "webhook" => CreateWebhookTrigger(triggerId, definition.Parameters),
            "application" => CreateApplicationTrigger(triggerId, definition.Parameters),
            "systemevent" => CreateSystemEventTrigger(triggerId, definition.Parameters),
            _ => null
        };
    }

    private TimeTrigger CreateTimeTrigger(string id, Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("intervalMs", out var intervalMs) && intervalMs is JsonElement intervalElem && intervalElem.TryGetInt32(out var interval))
        {
            return new TimeTrigger(id, TimeSpan.FromMilliseconds(interval));
        }

        if (parameters.TryGetValue("hour", out var hour) &&
            parameters.TryGetValue("minute", out var minute) && hour is JsonElement hElem && minute is JsonElement mElem && hElem.TryGetInt32(out var h) && mElem.TryGetInt32(out var m))
        {
            return new TimeTrigger(id, new TimeOnly(h, m));
        }

        return new TimeTrigger(id, TimeSpan.FromMinutes(5)); // Default
    }

    private FileSystemTrigger CreateFileSystemTrigger(string id, Dictionary<string, object> parameters)
    {
        var path = parameters.GetValueOrDefault("path")?.ToString() ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var filter = parameters.GetValueOrDefault("filter")?.ToString() ?? "*.*";

        return new FileSystemTrigger(id, path, filter);
    }

    private WebhookTrigger CreateWebhookTrigger(string id, Dictionary<string, object> parameters)
    {
        var port = 8080;
        if (parameters.TryGetValue("port", out var portObj) && portObj is JsonElement portElem && portElem.TryGetInt32(out var p))
        {
            port = p;
        }
        return new WebhookTrigger(id, port);
    }

    private ApplicationTrigger CreateApplicationTrigger(string id, Dictionary<string, object> parameters)
    {
        var processName = parameters.GetValueOrDefault("processName")?.ToString() ?? "notepad";
        return new ApplicationTrigger(id, processName);
    }

    private SystemEventTrigger CreateSystemEventTrigger(string id, Dictionary<string, object> parameters)
    {
        var eventTypeStr = parameters.GetValueOrDefault("eventType")?.ToString() ?? "NetworkStatus";
        var eventType = Enum.Parse<SystemEventType>(eventTypeStr, true);

        return new SystemEventTrigger(id, eventType, parameters);
    }
}
