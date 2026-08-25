// Stand-in for Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<T>.
//
// The real package starts the application in-process behind a TestServer, by
// hooking the host builder the entry point creates. Reproducing that hook
// faithfully is a lot of framework internals. This does something simpler and,
// for these tests, stricter: it launches the API as a REAL PROCESS on a
// loopback port and talks to it over REAL HTTP.
//
// So what runs is the actual application - actual Kestrel, actual middleware
// pipeline, actual model binding and JSON serialization, actual authentication
// and authorization. A test that asserts a secret never appears in a response
// body is reading bytes that genuinely crossed a socket. That is a stronger
// claim than TestServer makes, not a weaker one.
//
// Configuration reaches the child through environment variables, which is why
// the settings recorded by ConfigureWebHost work at all: WebApplication's
// default configuration includes AddEnvironmentVariables, where a "__" stands
// in for the ":" in a configuration key. Each factory gets its own process and
// therefore its own environment - four fixtures cannot tread on each other.
//
// What it does NOT do: it cannot reach into the host's service collection, so
// WebApplicationFactory's WithWebHostBuilder/ConfigureServices overloads for
// swapping services are not provided. Nothing in this repository uses them.
//
// The API assembly's path arrives in LOCO_TEST_API_DLL, set by
// scripts/run-tests-offline.sh, which builds it alongside the tests.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Mvc.Testing
{
    public class WebApplicationFactory<TEntryPoint> : IDisposable where TEntryPoint : class
    {
        private readonly Dictionary<string, string> _settings = new(StringComparer.Ordinal);
        private readonly List<HttpClient> _clients = new();
        private readonly object _lock = new();

        private string _environment = "Development";
        private Process? _process;
        private string? _baseAddress;
        private bool _disposed;

        /// <summary>
        /// Override to record settings, exactly as with the real factory. The
        /// builder handed in only captures them; it does not build anything.
        /// </summary>
        protected virtual void ConfigureWebHost(IWebHostBuilder builder) { }

        public HttpClient CreateClient()
        {
            EnsureStarted();

            var client = new HttpClient
            {
                BaseAddress = new Uri(_baseAddress!),
                Timeout = TimeSpan.FromSeconds(30),
            };

            lock (_lock) { _clients.Add(client); }
            return client;
        }

        private void EnsureStarted()
        {
            lock (_lock)
            {
                if (_process is not null) return;

                ConfigureWebHost(new RecordingWebHostBuilder(_settings, value => _environment = value));

                var apiDll = Environment.GetEnvironmentVariable("LOCO_TEST_API_DLL");
                if (string.IsNullOrWhiteSpace(apiDll) || !System.IO.File.Exists(apiDll))
                {
                    throw new InvalidOperationException(
                        "LOCO_TEST_API_DLL is not set to a built API assembly. " +
                        "Run these tests through scripts/run-tests-offline.sh, which builds it.");
                }

                var port = FreePort();
                _baseAddress = $"http://127.0.0.1:{port}";

                var start = new ProcessStartInfo("dotnet", $"\"{apiDll}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };

                start.Environment["ASPNETCORE_URLS"] = _baseAddress;
                start.Environment["ASPNETCORE_ENVIRONMENT"] = _environment;
                start.Environment["DOTNET_ENVIRONMENT"] = _environment;

                // "Auth:Users:0:Username" is "Auth__Users__0__Username" to the
                // configuration provider that reads the environment.
                foreach (var (key, value) in _settings)
                {
                    start.Environment[key.Replace(":", "__")] = value;
                }

                _process = Process.Start(start)
                    ?? throw new InvalidOperationException("Could not start the API process.");

                // Drained so a chatty log cannot fill the pipe buffer and block
                // the child, which would look exactly like a hung test.
                _process.OutputDataReceived += (_, _) => { };
                _process.ErrorDataReceived += (_, _) => { };
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                WaitUntilReady();
            }
        }

        /// <summary>
        /// Asks the kernel for an unused port and immediately gives it back. A
        /// port could in principle be taken in the gap; the alternative is
        /// parsing the child's startup log, which is far more fragile.
        /// </summary>
        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private void WaitUntilReady()
        {
            using var probe = new HttpClient
            {
                BaseAddress = new Uri(_baseAddress!),
                Timeout = TimeSpan.FromSeconds(2),
            };

            var deadline = DateTime.UtcNow.AddSeconds(30);
            Exception? last = null;

            while (DateTime.UtcNow < deadline)
            {
                if (_process!.HasExited)
                {
                    throw new InvalidOperationException(
                        $"The API process exited with code {_process.ExitCode} before answering. " +
                        "Run scripts/run-tests-offline.sh --verbose to see its output.");
                }

                try
                {
                    var response = probe.GetAsync("/health/live").GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode) return;
                }
                catch (Exception ex)
                {
                    last = ex;
                }

                Thread.Sleep(100);
            }

            throw new TimeoutException(
                "The API did not become ready within 30s." +
                (last is null ? "" : $" Last probe error: {last.Message}"));
        }

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Subclasses clean up their own state (LocoApiFactory deletes its
            // temp data directory) before the process holding it goes away.
            Dispose(true);

            lock (_lock)
            {
                foreach (var client in _clients) client.Dispose();
                _clients.Clear();

                if (_process is not null)
                {
                    try
                    {
                        if (!_process.HasExited)
                        {
                            _process.Kill(entireProcessTree: true);
                            _process.WaitForExit(5000);
                        }
                    }
                    catch { /* the process is going away either way */ }

                    _process.Dispose();
                    _process = null;
                }
            }

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// An IWebHostBuilder that only remembers. ConfigureWebHost overrides call
    /// UseSetting and UseEnvironment on it; everything else is accepted and
    /// ignored, because this builder never builds a host - the child process
    /// does that from its own entry point.
    /// </summary>
    internal sealed class RecordingWebHostBuilder : IWebHostBuilder
    {
        private readonly Dictionary<string, string> _settings;
        private readonly Action<string> _onEnvironment;

        public RecordingWebHostBuilder(Dictionary<string, string> settings, Action<string> onEnvironment)
        {
            _settings = settings;
            _onEnvironment = onEnvironment;
        }

        public IWebHostBuilder UseSetting(string key, string? value)
        {
            if (value is null) _settings.Remove(key);
            else if (string.Equals(key, WebHostDefaults.EnvironmentKey, StringComparison.Ordinal))
                _onEnvironment(value);
            else _settings[key] = value;

            return this;
        }

        public string? GetSetting(string key) => _settings.TryGetValue(key, out var v) ? v : null;

        public IWebHostBuilder ConfigureAppConfiguration(
            Action<WebHostBuilderContext, IConfigurationBuilder> configureDelegate) => this;

        public IWebHostBuilder ConfigureServices(Action<IServiceCollection> configureServices) => this;

        public IWebHostBuilder ConfigureServices(
            Action<WebHostBuilderContext, IServiceCollection> configureServices) => this;

        public IWebHost Build() => throw new NotSupportedException(
            "This builder records configuration; the API is built by its own entry point.");
    }
}

/// <summary>
/// The marker type LocoApiFactory names as its type parameter.
///
/// The application declares its own `public partial class Program { }` at the
/// end of Program.cs, for exactly this purpose - but Program.cs cannot be
/// compiled into the test assembly, because its top-level statements are an
/// entry point and csc refuses -main: alongside one. The type parameter is
/// never used to build anything here: the API is launched by assembly path.
/// </summary>
public partial class Program { }
