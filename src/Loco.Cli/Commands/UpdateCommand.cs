using System;
using System.CommandLine;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Loco.Cli.Commands;

/// <summary>
/// Command for checking and managing updates
/// アップデート確認・管理コマンド
/// </summary>
public class UpdateCommand : Command
{
    private const string GITHUB_API_URL = "https://api.github.com/repos/anthropics/loco/releases/latest";
    private const string CURRENT_VERSION = "1.0.0";

    public UpdateCommand() : base("update", "Check for available updates")
    {
        var checkCommand = new Command("check", "Check for updates (default)");
        var checkVerboseOption = new Option<bool>(
            new[] { "--verbose", "-v" },
            "Show detailed version information");
        checkCommand.AddOption(checkVerboseOption);
        checkCommand.SetHandler(CheckUpdateAsync, checkVerboseOption);
        AddCommand(checkCommand);

        var infoCommand = new Command("info", "Show current version information");
        infoCommand.SetHandler(ShowVersionInfo);
        AddCommand(infoCommand);
    }

    private async Task<int> CheckUpdateAsync(bool verbose)
    {
        try
        {
            Console.WriteLine("Checking for updates...");
            Console.WriteLine();

            // Show current version
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Current Version:");
            Console.ResetColor();
            Console.WriteLine($"  Loco CLI: {CURRENT_VERSION}");
            Console.WriteLine($"  .NET:     {Environment.Version}");
            Console.WriteLine();

            // Try to check for updates from GitHub
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Checking GitHub for latest release...");
            Console.ResetColor();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Loco-CLI");
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            try
            {
                var response = await httpClient.GetStringAsync(GITHUB_API_URL);
                var release = JsonSerializer.Deserialize<GitHubRelease>(response);

                if (release != null && !string.IsNullOrEmpty(release.tag_name))
                {
                    var latestVersion = release.tag_name.TrimStart('v');
                    Console.WriteLine($"  Latest:   {latestVersion}");
                    Console.WriteLine();

                    if (latestVersion == CURRENT_VERSION)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("✓ You are running the latest version!");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"⚠ A new version is available: {latestVersion}");
                        Console.ResetColor();
                        Console.WriteLine();
                        Console.WriteLine("Update instructions:");
                        Console.WriteLine($"  1. Visit: {release.html_url}");
                        Console.WriteLine("  2. Download the latest release");
                        Console.WriteLine("  3. Replace your current installation");

                        if (verbose && !string.IsNullOrEmpty(release.body))
                        {
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("Release Notes:");
                            Console.ResetColor();
                            Console.WriteLine(release.body);
                        }
                    }
                }
                else
                {
                    ShowManualCheckMessage();
                }
            }
            catch (HttpRequestException)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ Unable to connect to GitHub API");
                Console.ResetColor();
                ShowManualCheckMessage();
            }
            catch (TaskCanceledException)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ Request timed out");
                Console.ResetColor();
                ShowManualCheckMessage();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError checking for updates: {ex.Message}");
            Console.ResetColor();
            ShowManualCheckMessage();
            return 1;
        }
    }

    private Task<int> ShowVersionInfo()
    {
        Console.WriteLine("Version Information");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Application:");
        Console.ResetColor();
        Console.WriteLine($"  Name:             Loco CLI");
        Console.WriteLine($"  Version:          {CURRENT_VERSION}");
        Console.WriteLine($"  Build:            Release");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Runtime:");
        Console.ResetColor();
        Console.WriteLine($"  .NET Version:     {Environment.Version}");
        Console.WriteLine($"  Framework:        {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"  OS:               {Environment.OSVersion}");
        Console.WriteLine($"  Architecture:     {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Environment:");
        Console.ResetColor();
        Console.WriteLine($"  Working Dir:      {Environment.CurrentDirectory}");
        Console.WriteLine($"  User:             {Environment.UserName}");
        Console.WriteLine($"  Machine:          {Environment.MachineName}");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"To check for updates, run: Loco.Cli.exe update check");
        Console.ResetColor();

        return Task.FromResult(0);
    }

    private void ShowManualCheckMessage()
    {
        Console.WriteLine();
        Console.WriteLine("Please check manually:");
        Console.WriteLine("  GitHub: https://github.com/anthropics/loco/releases");
        Console.WriteLine();
    }

    public async Task<int> InvokeAsync(string[] args)
    {
        // Default to check command if no subcommand specified
        if (args.Length == 0)
        {
            return await CheckUpdateAsync(false);
        }

        return await ((Command)this).InvokeAsync(args);
    }

    // GitHub API response model
    private class GitHubRelease
    {
        public string tag_name { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string body { get; set; } = string.Empty;
        public string html_url { get; set; } = string.Empty;
        public bool prerelease { get; set; }
        public DateTime published_at { get; set; }
    }
}
