using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Interfaces;
using Loco.Core.Models;
using Loco.Core.Models.Actions;

namespace Loco.Plugins.Sample;

public class LogToFileAction : IAction
{
    private readonly IPluginFileSystem _fileSystem;

    public string Id { get; } = Guid.NewGuid().ToString();

    public LogToFileAction(IPluginFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        try
        {
                        // Get parameters from context
            var message = context.Parameters.GetValueOrDefault("Message", "");
            var fileName = context.Parameters.GetValueOrDefault("FileName", "plugin.log");

            var logPath = Path.Combine("logs", fileName);
            var content = $"{DateTime.UtcNow:O} - {message}{Environment.NewLine}";
            
            if (await _fileSystem.FileExists(logPath))
            {
                var existingContent = await _fileSystem.ReadAllTextAsync(logPath);
                content = existingContent + content;
            }

            await _fileSystem.WriteAllTextAsync(logPath, content);

            return new ActionResult(true, $"Successfully wrote to {logPath}");
        }
        catch (Exception ex)
        {
            return new ActionResult(false, $"Failed to write to log file: {ex.Message}");
        }
    }
}
