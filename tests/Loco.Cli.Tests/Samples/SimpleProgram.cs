using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core;
using Loco.Core.Models;

namespace Loco.Cli
{
    class SimpleProgram
    {
#pragma warning disable CS8892 // Sample code - not used as entry point
        static async Task<int> Main(string[] args)
#pragma warning restore CS8892
        {
            Console.WriteLine("Loco - Lightweight Automation Tool");
            Console.WriteLine("===================================");

            try
            {
                var engine = new SimpleLightEngine();
                await engine.StartAsync();

                // Create a simple test flow
                var flow = new SimpleFlow("Test Flow", "A simple test flow", "test");
                engine.AddFlow(flow);

                Console.WriteLine("Engine started successfully!");
                Console.WriteLine("Test flow created and added.");

                if (args.Length > 0 && args[0] == "test")
                {
                    Console.WriteLine("Executing test flow...");
                    var result = await engine.ExecuteFlowAsync("test");
                    Console.WriteLine($"Test flow result: {result}");
                }

                await engine.StopAsync();
                engine.Dispose();

                Console.WriteLine("Done!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }
    }
}