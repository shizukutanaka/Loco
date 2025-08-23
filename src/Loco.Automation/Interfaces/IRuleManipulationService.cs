using Microsoft.Extensions.Logging;

namespace Loco.Automation.Interfaces;

public interface IRuleManipulationService
{
    string InjectModelId(string ruleJson, string modelId, ILogger logger);
}
