using System;
using System.IO;
using FluentAssertions;
using Loco.Core.Plugins;
using Xunit;

namespace Loco.Core.Tests
{
    public class PluginPathsTests
    {
        [Fact]
        public void GetDefaultPluginsDirectory_Returns_AppData_Loco_Plugins()
        {
            // Arrange
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Loco", "Plugins");

            // Act
            var actual = PluginPaths.GetDefaultPluginsDirectory();

            // Assert
            actual.Should().Be(expected);
            Path.IsPathRooted(actual).Should().BeTrue();
        }

        [Fact]
        public void EnsureDirectory_CreatesDirectory_And_IsIdempotent()
        {
            // Arrange
            var tempRoot = Path.Combine(Path.GetTempPath(), "Loco_Test_PluginPaths_" + Guid.NewGuid().ToString("N"));
            var dir = Path.Combine(tempRoot, "Plugins");

            try
            {
                Directory.Exists(dir).Should().BeFalse();

                // Act
                var ensured1 = PluginPaths.EnsureDirectory(dir);
                var ensured2 = PluginPaths.EnsureDirectory(dir); // idempotent

                // Assert
                ensured1.Should().Be(dir);
                ensured2.Should().Be(dir);
                Directory.Exists(dir).Should().BeTrue();
            }
            finally
            {
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { /* ignore */ }
            }
        }

        [Fact]
        public void GetEffective_Uses_Provided_When_NotEmpty()
        {
            var provided = Path.Combine(Path.GetTempPath(), "Loco_Test_Provided_" + Guid.NewGuid().ToString("N"));
            var actual = PluginPaths.GetEffectivePluginsDirectory(provided);
            actual.Should().Be(provided);
        }

        [Fact]
        public void GetEffective_Uses_Env_When_No_Provided()
        {
            var old = Environment.GetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName);
            var envPath = Path.Combine(Path.GetTempPath(), "Loco_Test_Env_" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName, envPath);
                var actual = PluginPaths.GetEffectivePluginsDirectory(null);
                actual.Should().Be(envPath);
            }
            finally
            {
                Environment.SetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName, old);
            }
        }

        [Fact]
        public void GetEffective_Provided_Takes_Precedence_Over_Env()
        {
            var old = Environment.GetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName);
            var envPath = Path.Combine(Path.GetTempPath(), "Loco_Test_Env2_" + Guid.NewGuid().ToString("N"));
            var provided = Path.Combine(Path.GetTempPath(), "Loco_Test_Provided2_" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName, envPath);
                var actual = PluginPaths.GetEffectivePluginsDirectory(provided);
                actual.Should().Be(provided);
            }
            finally
            {
                Environment.SetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName, old);
            }
        }
        
        [Fact]
        public void GetEffectiveWithSource_Returns_Explicit_When_Provided()
        {
            var provided = Path.Combine(Path.GetTempPath(), "Loco_Test_WithSource_Provided_" + Guid.NewGuid().ToString("N"));
            var (path, source) = PluginPaths.GetEffectivePluginsDirectoryWithSource(provided);
            path.Should().Be(provided);
            source.Should().Be("explicit");
        }

        [Fact]
        public void GetEffectiveWithSource_Returns_Env_When_Set_And_No_Provided()
        {
            var old = Environment.GetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName);
            var envPath = Path.Combine(Path.GetTempPath(), "Loco_Test_WithSource_Env_" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName, envPath);
                var (path, source) = PluginPaths.GetEffectivePluginsDirectoryWithSource(null);
                path.Should().Be(envPath);
                source.Should().Be($"env:{PluginPaths.PluginsPathEnvVarName}");
            }
            finally
            {
                Environment.SetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName, old);
            }
        }

        [Fact]
        public void GetEffectiveWithSource_Returns_Default_When_No_Provided_And_No_Env()
        {
            var old = Environment.GetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName, null);
                var (path, source) = PluginPaths.GetEffectivePluginsDirectoryWithSource(null);
                path.Should().Be(PluginPaths.GetDefaultPluginsDirectory());
                source.Should().Be("default");
            }
            finally
            {
                Environment.SetEnvironmentVariable(PluginPaths.PluginsPathEnvVarName, old);
            }
        }
    }
}
