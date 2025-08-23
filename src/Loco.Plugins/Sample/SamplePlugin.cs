using Loco.Core.Plugins;

namespace Loco.Plugins.Sample;

public class SamplePlugin : PluginBase
{
    public override string Id => "Loco.Plugins.Sample";
    public override string Name => "Sample Plugin";
    public override string Version => "1.0.0";
    public override string Description => "A sample plugin to demonstrate functionality.";

    // This would typically be loaded from the manifest.json file by the host
    public override PluginManifest Manifest => new() 
    {
        Id = Id,
        Name = Name,
        Version = Version,
        Description = Description
    };

    public override Task InitializeAsync(IPluginHostContext context)
    {
        base.InitializeAsync(context); // Important to call base

        Logger.LogInformation("SamplePlugin initializing...");

        // Registering a custom action that depends on a sandboxed service
        context.RegisterAction("logToFile", typeof(LogToFileAction));

        Logger.LogInformation("SamplePlugin initialized.");

        return Task.CompletedTask;
    }

    public override Task ShutdownAsync()
    {
        Logger.LogInformation("SamplePlugin shutting down.");
        return base.ShutdownAsync();
    }
}
