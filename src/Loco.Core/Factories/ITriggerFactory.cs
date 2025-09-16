using Loco.Core.Interfaces;
using Loco.Core.Models;
using Loco.Core.Triggers;

namespace Loco.Core.Factories;

/// <summary>
/// Factory for creating trigger instances from DSL definitions.
/// </summary>
public interface ITriggerFactory
{
    /// <summary>
    /// Creates a trigger instance based on its DSL definition.
    /// </summary>
    /// <param name="definition">The trigger definition from the DSL.</param>
    /// <returns>An instance of IRuntimeTrigger, or null if the type is unknown.</returns>
    IRuntimeTrigger? CreateTrigger(AutomationDsl.TriggerDefinition definition);
}
