// Rob Pike: "Simplicity is the art of hiding complexity"
// John Carmack: "Start with the simplest thing that could possibly work"

using System.Net;
using System.Text;
using System.Text.Json;

namespace Loco.Core.Practical;

/// <summary>
/// Simple HTTP server - Lightweight HTTP server without heavy frameworks
/// Fast startup, minimal dependencies, easy to use
/// </summary>
public class SimpleHttpServer
{
    private readonly HttpListener _listener;
    private readonly Dictionary<string, Func<HttpContext, Task>> _routes = new();
    private readonly List<Func<HttpContext, Func<Task>, Task>> _middleware = new();
    private readonly SimpleLogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenerTask;

    public bool IsRunning { get; private set; }

    public SimpleHttpServer(int port = 8080, SimpleLogger? logger = null)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleHttpServer));
    }

    // Add GET route
    public void Get(string path, Func<HttpContext, Task> handler)
    {
        _routes[$"GET:{path}"] = handler;
        _logger.Debug($"Registered GET {path}");
    }

    // Add POST route
    public void Post(string path, Func<HttpContext, Task> handler)
    {
        _routes[$"POST:{path}"] = handler;
        _logger.Debug($"Registered POST {path}");
    }

    // Add PUT route
    public void Put(string path, Func<HttpContext, Task> handler)
    {
        _routes[$"PUT:{path}"] = handler;
        _logger.Debug($"Registered PUT {path}");
    }

    // Add DELETE route
    public void Delete(string path, Func<HttpContext, Task> handler)
    {
        _routes[$"DELETE:{path}"] = handler;
        _logger.Debug($"Registered DELETE {path}");
    }

    // Add middleware
    public void Use(Func<HttpContext, Func<Task>, Task> middleware)
    {
        _middleware.Add(middleware);
    }

    // Start server
    public void Start()
    {
        if (IsRunning) return;

        _listener.Start();
        IsRunning = true;

        _listenerTask = Task.Run(async () =>
        {
            _logger.Info($"Server started on {string.Join(", ", _listener.Prefixes)}");

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var httpContext = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(httpContext));
                }
                catch (HttpListenerException) when (_cts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("Listener error", ex);
                }
            }
        });
    }

    // Stop server
    public void Stop()
    {
        if (!IsRunning) return;

        _cts.Cancel();
        _listener.Stop();
        IsRunning = false;

        _logger.Info("Server stopped");
    }

    private async Task HandleRequestAsync(HttpListenerContext nativeContext)
    {
        var context = new HttpContext(nativeContext);

        try
        {
            _logger.Info($"{context.Method} {context.Path}");

            // Execute middleware pipeline
            var index = -1;

            async Task Next()
            {
                index++;
                if (index < _middleware.Count)
                {
                    await _middleware[index](context, Next);
                }
                else
                {
                    // Execute route handler
                    await ExecuteRouteAsync(context);
                }
            }

            await Next();

            // Send response
            await SendResponseAsync(nativeContext.Response, context);
        }
        catch (Exception ex)
        {
            _logger.Error($"Request handling failed: {context.Method} {context.Path}", ex);
            await SendErrorAsync(nativeContext.Response, 500, "Internal Server Error");
        }
    }

    private async Task ExecuteRouteAsync(HttpContext context)
    {
        var routeKey = $"{context.Method}:{context.Path}";

        if (_routes.TryGetValue(routeKey, out var handler))
        {
            await handler(context);
        }
        else
        {
            context.Response = "Not Found";
            context.StatusCode = 404;
        }
    }

    private async Task SendResponseAsync(HttpListenerResponse response, HttpContext context)
    {
        response.StatusCode = context.StatusCode;

        foreach (var header in context.ResponseHeaders)
        {
            response.Headers[header.Key] = header.Value;
        }

        if (!response.Headers.AllKeys.Contains("Content-Type"))
        {
            response.ContentType = "text/plain; charset=utf-8";
        }

        var responseData = context.Response switch
        {
            string str => Encoding.UTF8.GetBytes(str),
            byte[] bytes => bytes,
            _ when context.Response != null => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(context.Response)),
            _ => Array.Empty<byte>()
        };

        response.ContentLength64 = responseData.Length;
        await response.OutputStream.WriteAsync(responseData, 0, responseData.Length);
        response.Close();
    }

    private async Task SendErrorAsync(HttpListenerResponse response, int statusCode, string message)
    {
        response.StatusCode = statusCode;
        response.ContentType = "text/plain";

        var bytes = Encoding.UTF8.GetBytes(message);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        response.Close();
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
        _listener.Close();
    }
}

/// <summary>
/// HTTP context
/// </summary>
public class HttpContext
{
    private readonly HttpListenerContext _nativeContext;

    public string Method { get; }
    public string Path { get; }
    public Dictionary<string, string> Query { get; } = new();
    public Dictionary<string, string> Headers { get; } = new();
    public Dictionary<string, string> ResponseHeaders { get; } = new();
    public object? Response { get; set; }
    public int StatusCode { get; set; } = 200;
    public Dictionary<string, object> Items { get; } = new(); // Request-scoped data

    public HttpContext(HttpListenerContext nativeContext)
    {
        _nativeContext = nativeContext;
        Method = nativeContext.Request.HttpMethod;
        Path = nativeContext.Request.Url?.AbsolutePath ?? "/";

        // Parse query string
        var query = nativeContext.Request.Url?.Query;
        if (!string.IsNullOrEmpty(query))
        {
            foreach (var part in query.TrimStart('?').Split('&'))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2)
                {
                    Query[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
                }
            }
        }

        // Copy headers
        foreach (var key in nativeContext.Request.Headers.AllKeys)
        {
            if (key != null)
            {
                Headers[key] = nativeContext.Request.Headers[key] ?? "";
            }
        }
    }

    public async Task<string> ReadBodyAsync()
    {
        using var reader = new StreamReader(_nativeContext.Request.InputStream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    public async Task<T?> ReadJsonAsync<T>()
    {
        var json = await ReadBodyAsync();
        return JsonSerializer.Deserialize<T>(json);
    }

    public void Json(object data)
    {
        Response = data;
        ResponseHeaders["Content-Type"] = "application/json";
    }

    public void Html(string html)
    {
        Response = html;
        ResponseHeaders["Content-Type"] = "text/html; charset=utf-8";
    }

    public void Text(string text)
    {
        Response = text;
        ResponseHeaders["Content-Type"] = "text/plain; charset=utf-8";
    }

    public void Redirect(string url, int statusCode = 302)
    {
        StatusCode = statusCode;
        ResponseHeaders["Location"] = url;
    }
}

/// <summary>
/// Router for better route organization
/// </summary>
public class SimpleRouter
{
    private readonly SimpleHttpServer _server;
    private readonly string _prefix;

    public SimpleRouter(SimpleHttpServer server, string prefix = "")
    {
        _server = server;
        _prefix = prefix.TrimEnd('/');
    }

    public void Get(string path, Func<HttpContext, Task> handler)
    {
        _server.Get(_prefix + path, handler);
    }

    public void Post(string path, Func<HttpContext, Task> handler)
    {
        _server.Post(_prefix + path, handler);
    }

    public void Put(string path, Func<HttpContext, Task> handler)
    {
        _server.Put(_prefix + path, handler);
    }

    public void Delete(string path, Func<HttpContext, Task> handler)
    {
        _server.Delete(_prefix + path, handler);
    }
}

/// <summary>
/// Common middleware
/// </summary>
public static class CommonMiddleware
{
    public static Func<HttpContext, Func<Task>, Task> Logger(SimpleLogger logger)
    {
        return async (context, next) =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await next();
            logger.Info($"{context.Method} {context.Path} - {context.StatusCode} ({sw.ElapsedMilliseconds}ms)");
        };
    }

    public static Func<HttpContext, Func<Task>, Task> Cors(string allowOrigin = "*")
    {
        return async (context, next) =>
        {
            context.ResponseHeaders["Access-Control-Allow-Origin"] = allowOrigin;
            context.ResponseHeaders["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
            context.ResponseHeaders["Access-Control-Allow-Headers"] = "Content-Type";

            if (context.Method == "OPTIONS")
            {
                context.StatusCode = 204;
                return;
            }

            await next();
        };
    }

    public static Func<HttpContext, Func<Task>, Task> ErrorHandler()
    {
        return async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                context.StatusCode = 500;
                context.Json(new { error = ex.Message });
            }
        };
    }
}

/// <summary>
/// Example API server
/// </summary>
public class ExampleApiServer
{
    public static void Run()
    {
        var server = new SimpleHttpServer(port: 8080);
        var logger = SimpleLoggerFactory.GetLogger("API");

        // Add middleware
        server.Use(CommonMiddleware.Logger(logger));
        server.Use(CommonMiddleware.Cors());
        server.Use(CommonMiddleware.ErrorHandler());

        // Routes
        server.Get("/", async ctx =>
        {
            ctx.Html("<h1>Welcome to Simple HTTP Server</h1>");
            await Task.CompletedTask;
        });

        server.Get("/api/hello", async ctx =>
        {
            var name = ctx.Query.GetValueOrDefault("name", "World");
            ctx.Json(new { message = $"Hello, {name}!" });
            await Task.CompletedTask;
        });

        server.Post("/api/echo", async ctx =>
        {
            var body = await ctx.ReadBodyAsync();
            ctx.Json(new { echo = body });
        });

        server.Get("/api/users", async ctx =>
        {
            var users = new[]
            {
                new { id = 1, name = "Alice" },
                new { id = 2, name = "Bob" }
            };
            ctx.Json(users);
            await Task.CompletedTask;
        });

        // API router
        var apiRouter = new SimpleRouter(server, "/api/v1");

        apiRouter.Get("/status", async ctx =>
        {
            ctx.Json(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                uptime = TimeSpan.FromSeconds(100)
            });
            await Task.CompletedTask;
        });

        server.Start();

        Console.WriteLine("Server is running on http://localhost:8080");
        Console.WriteLine("Press Enter to stop...");
        Console.ReadLine();

        server.Stop();
        server.Dispose();
    }
}