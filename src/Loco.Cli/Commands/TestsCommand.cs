using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Loco.Cli.Commands;

/// <summary>
/// Command for running tests
/// テスト実行コマンド
/// </summary>
public class TestsCommand : Command
{
    public TestsCommand() : base("test", "Run project tests")
    {
        var runCommand = new Command("run", "Run all tests (default)");
        var runVerboseOption = new Option<bool>(
            new[] { "--verbose", "-v" },
            "Show detailed test output");
        var runFilterOption = new Option<string?>(
            new[] { "--filter", "-f" },
            "Filter tests by name pattern");
        var runProjectOption = new Option<string?>(
            new[] { "--project", "-p" },
            "Specify test project to run");
        runCommand.AddOption(runVerboseOption);
        runCommand.AddOption(runFilterOption);
        runCommand.AddOption(runProjectOption);
        runCommand.SetHandler(RunTestsAsync, runVerboseOption, runFilterOption, runProjectOption);
        AddCommand(runCommand);

        var listCommand = new Command("list", "List available test projects");
        listCommand.SetHandler(ListTestsAsync);
        AddCommand(listCommand);

        var coverageCommand = new Command("coverage", "Run tests with code coverage");
        var coverageOutputOption = new Option<string>(
            new[] { "--output", "-o" },
            () => "coverage",
            "Coverage output directory");
        coverageCommand.AddOption(coverageOutputOption);
        coverageCommand.SetHandler(RunCoverageAsync, coverageOutputOption);
        AddCommand(coverageCommand);
    }

    private async Task<int> RunTestsAsync(bool verbose, string? filter, string? project)
    {
        try
        {
            Console.WriteLine("Running tests...");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine();

            var testProjects = FindTestProjects();
            if (testProjects.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ No test projects found");
                Console.ResetColor();
                return 1;
            }

            // Filter by project if specified
            if (!string.IsNullOrEmpty(project))
            {
                testProjects = testProjects.Where(p =>
                    Path.GetFileNameWithoutExtension(p).Contains(project, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (testProjects.Length == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ No test projects found matching: {project}");
                    Console.ResetColor();
                    return 1;
                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Test Projects ({testProjects.Length}):");
            Console.ResetColor();
            foreach (var proj in testProjects)
            {
                Console.WriteLine($"  • {Path.GetFileNameWithoutExtension(proj)}");
            }
            Console.WriteLine();

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = BuildTestArguments(verbose, filter),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine(e.Data);
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e.Data);
                    Console.ResetColor();
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            Console.WriteLine();
            if (process.ExitCode == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ All tests passed");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Some tests failed");
                Console.ResetColor();
            }

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError running tests: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private async Task<int> ListTestsAsync()
    {
        try
        {
            Console.WriteLine("Test Projects");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine();

            var testProjects = FindTestProjects();
            if (testProjects.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ No test projects found");
                Console.ResetColor();
                return 1;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Found {testProjects.Length} test project(s):");
            Console.ResetColor();
            Console.WriteLine();

            foreach (var project in testProjects)
            {
                var projectName = Path.GetFileNameWithoutExtension(project);
                var projectDir = Path.GetDirectoryName(project) ?? "";
                var relativeDir = Path.GetRelativePath(Environment.CurrentDirectory, projectDir);

                Console.WriteLine($"  • {projectName}");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"    Path: {relativeDir}");
                Console.ResetColor();

                // Count test files
                var testFiles = Directory.GetFiles(projectDir, "*Tests.cs", SearchOption.AllDirectories);
                Console.WriteLine($"    Test Files: {testFiles.Length}");
                Console.WriteLine();
            }

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("To run tests:");
            Console.WriteLine("  Loco.Cli.exe test run");
            Console.WriteLine("  Loco.Cli.exe test run --project Core");
            Console.WriteLine("  Loco.Cli.exe test run --filter MyTest");
            Console.ResetColor();

            return await Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError listing tests: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private async Task<int> RunCoverageAsync(string outputDir)
    {
        try
        {
            Console.WriteLine("Running tests with code coverage...");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine();

            var testProjects = FindTestProjects();
            if (testProjects.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ No test projects found");
                Console.ResetColor();
                return 1;
            }

            // Create output directory
            Directory.CreateDirectory(outputDir);

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test --collect:\"XPlat Code Coverage\" --results-directory {outputDir}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine(e.Data);
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e.Data);
                    Console.ResetColor();
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            Console.WriteLine();
            if (process.ExitCode == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Tests passed with coverage");
                Console.ResetColor();
                Console.WriteLine($"  Coverage results saved to: {Path.GetFullPath(outputDir)}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Tests failed");
                Console.ResetColor();
            }

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError running coverage: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private string[] FindTestProjects()
    {
        var testsDir = Path.Combine(Environment.CurrentDirectory, "tests");
        if (!Directory.Exists(testsDir))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(testsDir, "*Tests.csproj", SearchOption.AllDirectories);
    }

    private string BuildTestArguments(bool verbose, string? filter)
    {
        var args = "test";

        if (verbose)
        {
            args += " --verbosity detailed";
        }
        else
        {
            args += " --verbosity minimal";
        }

        if (!string.IsNullOrEmpty(filter))
        {
            // If the filter already contains operators like ~, =, or contains FullyQualifiedName, use it as-is
            // Otherwise, wrap it in a FullyQualifiedName~ expression
            if (filter.Contains("~") || filter.Contains("=") || filter.Contains("FullyQualifiedName"))
            {
                args += $" --filter \"{filter}\"";
            }
            else
            {
                args += $" --filter \"FullyQualifiedName~{filter}\"";
            }
        }

        args += " --no-build";

        return args;
    }

    public async Task<int> InvokeAsync(string[] args)
    {
        // Default to run command if no subcommand specified
        if (args.Length == 0)
        {
            return await RunTestsAsync(false, null, null);
        }

        return await ((Command)this).InvokeAsync(args);
    }
}
