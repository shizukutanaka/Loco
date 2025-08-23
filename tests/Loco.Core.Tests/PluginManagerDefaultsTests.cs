using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Loco.Core.Plugins;
using Xunit;

namespace Loco.Core.Tests
{
    public class PluginManagerDefaultsTests
    {
        private static string GetPrivatePluginsDirectory(PluginManager manager)
        {
            var field = typeof(PluginManager).GetField("_pluginsDirectory", BindingFlags.NonPublic | BindingFlags.Instance);
            field.Should().NotBeNull("_pluginsDirectory field should exist");
            return (string)field!.GetValue(manager)!;
        }

        [Fact]
        public void Defaults_To_AppData_Loco_Plugins_When_Null_Path()
        {
            // Arrange
            var expected = PluginPaths.GetDefaultPluginsDirectory();
            var old = Environment.GetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName, null);

                // Act
                var manager = new PluginManager(logger: null, pluginsDirectory: null, configuration: null);
                var actual = GetPrivatePluginsDirectory(manager);

                // Assert
                actual.Should().Be(expected);
                Directory.Exists(actual).Should().BeTrue();
            }
            finally
            {
                Environment.SetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName, old);
            }
        }

        [Fact]
        public void Uses_Custom_Path_When_Provided()
        {
            // Arrange
            var custom = Path.Combine(Path.GetTempPath(), "Loco_Test_Plugins_" + Guid.NewGuid().ToString("N"));

            try
            {
                // Act
                var manager = new PluginManager(logger: null, pluginsDirectory: custom, configuration: null);
                var actual = GetPrivatePluginsDirectory(manager);

                // Assert
                actual.Should().Be(custom);
                Directory.Exists(actual).Should().BeTrue();
            }
            finally
            {
                try { if (Directory.Exists(custom)) Directory.Delete(custom, true); } catch { /* ignore */ }
            }
        }

        [Fact]
        public void Defaults_To_Env_When_Set_And_Null_Path()
        {
            var old = Environment.GetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName);
            var envPath = Path.Combine(Path.GetTempPath(), "Loco_Test_Env_Manager_" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName, envPath);
                var manager = new PluginManager(logger: null, pluginsDirectory: null, configuration: null);
                var actual = GetPrivatePluginsDirectory(manager);
                actual.Should().Be(envPath);
                Directory.Exists(actual).Should().BeTrue();
            }
            finally
            {
                Environment.SetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName, old);
                try { if (Directory.Exists(envPath)) Directory.Delete(envPath, true); } catch { /* ignore */ }
            }
        }
    }
}
