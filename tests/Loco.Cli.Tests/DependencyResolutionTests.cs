using System;
using System.Threading.Tasks;
using Loco.Cli;
using Loco.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Loco.Cli.Tests
{
    public class DependencyResolutionTests
    {
        [Fact]
        public async Task Host_Resolves_ILlmService_TypedClient()
        {
            using var host = Program.CreateHostBuilder(Array.Empty<string>())
                .Build();

            await host.StartAsync();

            var svc = host.Services.GetService<ILlmService>();
            Assert.NotNull(svc);

            await host.StopAsync();
        }
    }
}
