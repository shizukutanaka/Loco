using System.CommandLine;
using Loco.Core;
using Loco.Core.Configuration;

namespace Loco.Cli.Commands;

/// <summary>
/// Start the automation engine command
/// </summary>
public class StartCommand : Command
{
    public StartCommand() : base("start", "Start the automation engine")
    {
        var rulesPathOption = new Option<string?>("--rules-path", "Path to rules storage");
        AddOption(rulesPathOption);

        this.SetHandler((rulesPath) => StartEngine(rulesPath), rulesPathOption);
    }

    public async Task<int> InvokeAsync(string[] args)
    {
        return await ((Command)this).InvokeAsync(args);
    }

    private async Task<int> StartEngine(string? rulesPath)
    {
        SimpleLightEngine? engine = null;
        try
        {
            var engineResult = CreateEngine(rulesPath);
            if (engineResult == null)
            {
                return 1;
            }

            engine = engineResult.Value.engine;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Starting Loco automation engine...");
            Console.ResetColor();

            await engine.StartAsync();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Engine started successfully");
            Console.ResetColor();
            Console.WriteLine("\nPress Ctrl+C to stop the engine.");

            var tcs = new TaskCompletionSource<bool>();
            Console.CancelKeyPress += (_, e) =>
            {
                // Without Cancel = true the runtime kills the process as soon as
                // the handler returns - the graceful StopAsync/Dispose path below
                // would never actually run
                e.Cancel = true;
                tcs.TrySetResult(true);
            };
            await tcs.Task;

            Console.WriteLine("\nShutting down gracefully...");
            await engine.StopAsync();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Engine stopped successfully");
            Console.ResetColor();

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nEngine shutdown requested by user.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Engine error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
        finally
        {
            engine?.Dispose();
        }
    }

    private (SimpleLightEngine engine, object? ruleStore, LocoConfig config)? CreateEngine(string? rulesPath)
    {
        // Note: Persistent rule store not yet implemented
        var config = new LocoConfig();
        var engine = new SimpleLightEngine(null, config);
        return (engine, null, config);
    }
}
