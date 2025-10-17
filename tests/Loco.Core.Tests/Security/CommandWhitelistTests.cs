using Xunit;
using Loco.Core.Security;

namespace Loco.Core.Tests.Security
{
    public class CommandWhitelistTests
    {
        [Fact]
        public void IsAllowed_DefaultCommands_ReturnsTrue()
        {
            // Arrange & Act & Assert
            Assert.True(CommandWhitelist.IsAllowed("cmd.exe"));
            Assert.True(CommandWhitelist.IsAllowed("powershell.exe"));
            Assert.True(CommandWhitelist.IsAllowed("dotnet.exe"));
            Assert.True(CommandWhitelist.IsAllowed("git.exe"));
        }

        [Fact]
        public void IsAllowed_UnknownCommand_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.False(CommandWhitelist.IsAllowed("malware.exe"));
            Assert.False(CommandWhitelist.IsAllowed("hack.exe"));
        }

        [Fact]
        public void IsAllowed_CaseInsensitive_ReturnsTrue()
        {
            // Arrange & Act & Assert
            Assert.True(CommandWhitelist.IsAllowed("CMD.EXE"));
            Assert.True(CommandWhitelist.IsAllowed("PowerShell.exe"));
            Assert.True(CommandWhitelist.IsAllowed("DOTNET.EXE"));
        }

        [Fact]
        public void IsAllowed_WithPath_ChecksFilenameOnly()
        {
            // Arrange & Act & Assert
            Assert.True(CommandWhitelist.IsAllowed(@"C:\Windows\System32\cmd.exe"));
            Assert.True(CommandWhitelist.IsAllowed(@"/usr/bin/git.exe"));
        }

        [Fact]
        public void IsAllowed_NullOrEmpty_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.False(CommandWhitelist.IsAllowed(null));
            Assert.False(CommandWhitelist.IsAllowed(""));
            Assert.False(CommandWhitelist.IsAllowed("   "));
        }

        [Fact]
        public void ResetToDefaults_RestoresDefaultCommands()
        {
            // Arrange
            CommandWhitelist.ResetToDefaults();

            // Act & Assert
            Assert.True(CommandWhitelist.IsAllowed("cmd.exe"));
            Assert.False(CommandWhitelist.IsAllowed("custom.exe"));
        }
    }
}
