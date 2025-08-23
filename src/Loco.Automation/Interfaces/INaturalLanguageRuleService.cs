using System.Threading;
using System.Threading.Tasks;

namespace Loco.Automation.Interfaces;

public interface INaturalLanguageRuleService
{
    /// <summary>
    /// Converts a natural language text instruction into a rule defined in JSON format.
    /// The returned JSON should conform to the Automation DSL schema, containing at a minimum an 'id' and 'name'.
    /// </summary>
    /// <param name="text">The natural language text to convert.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the JSON string of the converted rule, or an empty string if conversion fails.</returns>
    Task<string> ConvertTextToRuleJsonAsync(string text, CancellationToken cancellationToken = default);
}
