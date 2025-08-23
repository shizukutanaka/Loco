using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Interfaces;

public interface ILlmService
{
    Task<string> GenerateTextAsync(
        string prompt,
        CancellationToken cancellationToken = default);

    Task<string> GenerateTextAsync(
        string prompt,
        string? modelOverride,
        int? maxTokensOverride,
        double? temperatureOverride,
        CancellationToken cancellationToken = default);
}
