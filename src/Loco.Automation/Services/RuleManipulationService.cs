using System.Text.Json;
using System.Text.Json.Nodes;
using Loco.Automation.Interfaces;
using Microsoft.Extensions.Logging;

namespace Loco.Automation.Services;

public class RuleManipulationService : IRuleManipulationService
{
    public string InjectModelId(string ruleJson, string modelId, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(ruleJson) || string.IsNullOrWhiteSpace(modelId))
        {
            return ruleJson;
        }

        try
        {
            var jsonNode = JsonNode.Parse(ruleJson);
            if (jsonNode is null) return ruleJson;

            var actions = jsonNode["actions"]?.AsArray();
            if (actions is null) return ruleJson;

            bool modified = false;
            foreach (var action in actions)
            {
                if (action is not JsonObject actionObj) continue;

                if (actionObj.TryGetPropertyValue("type", out var typeNode) && typeNode?.GetValue<string>() == "llmQuery")
                {
                    if (!actionObj.ContainsKey("modelId") || string.IsNullOrWhiteSpace(actionObj["modelId"]?.GetValue<string>()))
                    {
                        actionObj["modelId"] = modelId;
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                logger.LogInformation("Injected stable model ID '{ModelId}' into rule.", modelId);
                return jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            }

            return ruleJson;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse and inject model ID into rule JSON.");
            return ruleJson;
        }
    }
}
