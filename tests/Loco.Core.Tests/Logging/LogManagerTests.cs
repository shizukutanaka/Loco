using System;
using System.IO;
using System.Linq;
using Xunit;
using Loco.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Tests.Logging
{
    public class LogManagerTests : IDisposable
    {
        private readonly string _testLogDir;
        private readonly ILogger _logger;

        public LogManagerTests()
        {
            _testLogDir = Path.Combine(Path.GetTempPath(), "LocoTestLogs", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testLogDir);

            // Create test log files
            CreateTestLogFile("app.log", DateTime.Now.AddDays(-1));
            CreateTestLogFile("app.log.1", DateTime.Now.AddDays(-8)); // Old file
            CreateTestLogFile("debug.log", DateTime.Now.AddDays(-2));
            CreateTestLogFile("error.log", DateTime.Now.AddDays(-10)); // Very old file

            // Create a mock logger
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<LogManagerTests>();
        }

        private void CreateTestLogFile(string fileName, DateTime lastWriteTime)
        {
            var filePath = Path.Combine(_testLogDir, fileName);
            File.WriteAllText(filePath, $"Test log entry in {fileName}");
            File.SetLastWriteTimeUtc(filePath, lastWriteTime);
        }

        [Fact]
        public void CleanupOldLogs_WithRetentionDays_DeletesOldFiles()
        {
            // Arrange
            var initialFiles = Directory.GetFiles(_testLogDir, "*.log").Length;

            // Act
            LogManager.CleanupOldLogs(_testLogDir, 7, _logger);

            // Assert
            var remainingFiles = Directory.GetFiles(_testLogDir, "*.log").Length;
            Assert.True(remainingFiles < initialFiles, "Some old log files should have been deleted");
        }

        [Fact]
        public void GetLogStats_WithLogFiles_ReturnsCorrectStats()
        {
            // Act
            var stats = LogManager.GetLogStats(_testLogDir);

            // Assert
            Assert.True(stats.TotalFiles > 0, "Should find log files");
            Assert.True(stats.TotalSize > 0, "Should have non-zero total size");
            Assert.Contains("files", stats.GetSummary());
        }

        [Fact]
        public void GetLogStats_WithEmptyDirectory_ReturnsZeroStats()
        {
            // Arrange
            var emptyDir = Path.Combine(Path.GetTempPath(), "EmptyLogs", Guid.NewGuid().ToString());
            Directory.CreateDirectory(emptyDir);

            try
            {
                // Act
                var stats = LogManager.GetLogStats(emptyDir);

                // Assert
                Assert.Equal(0, stats.TotalFiles);
                Assert.Equal(0, stats.TotalSize);
                Assert.Equal(0, stats.OldFiles);
                Assert.Equal(0, stats.OldSize);
            }
            finally
            {
                Directory.Delete(emptyDir, true);
            }
        }

        [Fact]
        public void RotateLogFile_WithLargeFile_RotatesSuccessfully()
        {
            // Arrange
            var logFile = Path.Combine(_testLogDir, "large.log");
            var largeContent = new string('A', 2000); // Create content larger than typical rotation threshold
            File.WriteAllText(logFile, largeContent);

            // Act
            LogManager.RotateLogFile(logFile, 1000, _logger); // Rotate if > 1000 bytes

            // Assert
            var backupFile = logFile + ".1";
            Assert.True(File.Exists(backupFile), "Backup file should exist after rotation");
            Assert.True(File.Exists(logFile), "Original file should still exist (may be empty)");
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testLogDir))
                {
                    Directory.Delete(_testLogDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}
