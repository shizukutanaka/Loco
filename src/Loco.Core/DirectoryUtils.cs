using System;
using System.IO;
using Loco.Core.Exceptions;

namespace Loco.Core;

/// <summary>
/// Utility class for directory operations with proper error handling
/// </summary>
public static class DirectoryUtils
{
    /// <summary>
    /// Ensures a directory exists with proper error handling
    /// </summary>
    public static void EnsureDirectoryExists(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));
        }

        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);

                // Verify directory was created successfully
                if (!Directory.Exists(directoryPath))
                {
                    throw new IOException($"Failed to create directory: {directoryPath}");
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException or IOException)
        {
            throw new LocoExecutionException($"Cannot create directory at {directoryPath}: {ex.Message}", ex, "DIRECTORY_CREATE_FAILED", null, null);
        }
    }
}
