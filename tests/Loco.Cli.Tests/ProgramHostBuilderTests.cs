using System;
using System.IO;
using Loco.Core.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Loco.Cli.Tests;

public class ProgramHostBuilderTests
{
    [Fact]
    public void CreateHostBuilder_UsesExplicitPluginsPath_FromArgs()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        var explicitPlugins = Path.Combine(tempRoot, "explicitPlugins");

        var args = new[] { "--plugins-path", explicitPlugins };

        // Act
        using var host = Loco.Cli.Program.CreateHostBuilder(args).Build();
        var pm = host.Services.GetRequiredService<PluginManager>();

        // Assert
        Assert.Equal(explicitPlugins, pm.PluginsDirectory);
    }

    [Fact]
    public void CreateHostBuilder_UsesEnvPluginsPath_WhenNoArg()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        var envPlugins = Path.Combine(tempRoot, "envPlugins");
        var envName = PluginPaths.PluginsPathEnvVarName; // LOCO_PLUGINS_PATH
        var prev = Environment.GetEnvironmentVariable(envName);

        try
        {
            Environment.SetEnvironmentVariable(envName, envPlugins);

            // Act
            using var host = Loco.Cli.Program.CreateHostBuilder(Array.Empty<string>()).Build();
            var pm = host.Services.GetRequiredService<PluginManager>();

            // Assert
            Assert.Equal(envPlugins, pm.PluginsDirectory);
        }
        finally
        {
            // Restore
            Environment.SetEnvironmentVariable(envName, prev);
        }
    }

    [Fact]
    public void CreateHostBuilder_Defaults_ToAppData_WhenNoArgNoEnv()
    {
        // Arrange
        var envName = PluginPaths.PluginsPathEnvVarName; // LOCO_PLUGINS_PATH
        var prev = Environment.GetEnvironmentVariable(envName);
        try
        {
            Environment.SetEnvironmentVariable(envName, null);

            // Act
            using var host = Loco.Cli.Program.CreateHostBuilder(Array.Empty<string>()).Build();
            var pm = host.Services.GetRequiredService<PluginManager>();

            // Assert
            Assert.Equal(PluginPaths.GetDefaultPluginsDirectory(), pm.PluginsDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, prev);
        }
    }

    [Fact]
    public void CreateHostBuilder_UsesShortAlias_p()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        var explicitPlugins = Path.Combine(tempRoot, "explicitPluginsShort");

        var args = new[] { "-p", explicitPlugins };

        // Act
        using var host = Loco.Cli.Program.CreateHostBuilder(args).Build();
        var pm = host.Services.GetRequiredService<PluginManager>();

        // Assert
        Assert.Equal(explicitPlugins, pm.PluginsDirectory);
    }
}
