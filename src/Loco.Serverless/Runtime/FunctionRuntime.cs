using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loco.Serverless.Runtime;

/// <summary>
/// Serverless function runtime interface
/// </summary>
public interface IFunctionRuntime
{
    string RuntimeName { get; }
    Task<FunctionExecutionResult> ExecuteAsync(
        FunctionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// .NET function runtime
/// </summary>
public class DotNetFunctionRuntime : IFunctionRuntime
{
    private readonly ILogger<DotNetFunctionRuntime> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Assembly> _loadedAssemblies;

    public string RuntimeName => "dotnet";

    public DotNetFunctionRuntime(
        ILogger<DotNetFunctionRuntime> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _loadedAssemblies = new Dictionary<string, Assembly>();
    }

    public async Task<FunctionExecutionResult> ExecuteAsync(
        FunctionContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogDebug("Executing .NET function {FunctionName}", context.FunctionName);

            // Load or compile the function
            var assembly = await LoadOrCompileAssemblyAsync(context);
            
            // Find the entry point
            var entryPoint = FindEntryPoint(assembly, context.Handler);
            
            if (entryPoint == null)
            {
                throw new InvalidOperationException($"Entry point {context.Handler} not found");
            }

            // Create instance if needed
            object? instance = null;
            if (!entryPoint.IsStatic)
            {
                var type = entryPoint.DeclaringType!;
                instance = ActivatorUtilities.CreateInstance(_serviceProvider, type);
            }

            // Prepare parameters
            var parameters = PrepareParameters(entryPoint, context);
            
            // Invoke the function
            var result = entryPoint.Invoke(instance, parameters);
            
            // Handle async methods
            if (result is Task task)
            {
                await task;
                
                // Get result from Task<T>
                var taskType = task.GetType();
                if (taskType.IsGenericType)
                {
                    var resultProperty = taskType.GetProperty("Result");
                    result = resultProperty?.GetValue(task);
                }
                else
                {
                    result = null;
                }
            }

            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogInformation("Function {FunctionName} executed successfully in {Duration}ms",
                context.FunctionName, duration.TotalMilliseconds);

            return new FunctionExecutionResult
            {
                Success = true,
                Result = result,
                Duration = duration,
                Logs = new List<string> { $"Function executed successfully" }
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogError(ex, "Function {FunctionName} failed", context.FunctionName);
            
            return new FunctionExecutionResult
            {
                Success = false,
                Error = ex.Message,
                Duration = duration,
                Logs = new List<string> { $"Function failed: {ex.Message}" }
            };
        }
    }

    private async Task<Assembly> LoadOrCompileAssemblyAsync(FunctionContext context)
    {
        // Check if already loaded
        if (_loadedAssemblies.TryGetValue(context.FunctionName, out var cached))
        {
            return cached;
        }

        Assembly assembly;
        
        if (context.CodePath.EndsWith(".dll"))
        {
            // Load compiled assembly
            assembly = await LoadAssemblyAsync(context.CodePath);
        }
        else if (context.CodePath.EndsWith(".cs"))
        {
            // Compile source code
            assembly = await CompileAssemblyAsync(context.CodePath, context.FunctionName);
        }
        else
        {
            throw new NotSupportedException($"Unsupported code format: {context.CodePath}");
        }

        _loadedAssemblies[context.FunctionName] = assembly;
        return assembly;
    }

    private async Task<Assembly> LoadAssemblyAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        return Assembly.Load(bytes);
    }

    private async Task<Assembly> CompileAssemblyAsync(string sourcePath, string assemblyName)
    {
        var sourceCode = await File.ReadAllTextAsync(sourcePath);
        
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location)
        };

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage());
            
            throw new InvalidOperationException($"Compilation failed: {string.Join(", ", errors)}");
        }

        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
    }

    private MethodInfo? FindEntryPoint(Assembly assembly, string handler)
    {
        // Handler format: Namespace.Class::Method
        var parts = handler.Split("::");
        if (parts.Length != 2)
        {
            return null;
        }

        var typeName = parts[0];
        var methodName = parts[1];

        var type = assembly.GetType(typeName);
        if (type == null)
        {
            return null;
        }

        return type.GetMethod(methodName);
    }

    private object?[] PrepareParameters(MethodInfo method, FunctionContext context)
    {
        var parameters = method.GetParameters();
        var values = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            
            if (parameter.ParameterType == typeof(FunctionContext))
            {
                values[i] = context;
            }
            else if (parameter.ParameterType == typeof(ILogger))
            {
                values[i] = _logger;
            }
            else if (parameter.ParameterType == typeof(CancellationToken))
            {
                values[i] = CancellationToken.None;
            }
            else if (context.Input != null)
            {
                // Try to deserialize input to parameter type
                var json = JsonSerializer.Serialize(context.Input);
                values[i] = JsonSerializer.Deserialize(json, parameter.ParameterType);
            }
            else
            {
                values[i] = null;
            }
        }

        return values;
    }
}

/// <summary>
/// Function context
/// </summary>
public class FunctionContext
{
    public string FunctionName { get; set; } = string.Empty;
    public string Handler { get; set; } = string.Empty;
    public string CodePath { get; set; } = string.Empty;
    public object? Input { get; set; }
    public Dictionary<string, string> Environment { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string? CorrelationId { get; set; }
    public string? RequestId { get; set; }
}

/// <summary>
/// Function execution result
/// </summary>
public class FunctionExecutionResult
{
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Logs { get; set; } = new();
    public Dictionary<string, object>? Metrics { get; set; }
}

/// <summary>
/// Function executor
/// </summary>
public class FunctionExecutor
{
    private readonly Dictionary<string, IFunctionRuntime> _runtimes;
    private readonly ILogger<FunctionExecutor> _logger;
    private readonly FunctionExecutorOptions _options;

    public FunctionExecutor(
        IEnumerable<IFunctionRuntime> runtimes,
        ILogger<FunctionExecutor> logger,
        FunctionExecutorOptions? options = null)
    {
        _runtimes = runtimes.ToDictionary(r => r.RuntimeName.ToLower());
        _logger = logger;
        _options = options ?? new FunctionExecutorOptions();
    }

    public async Task<FunctionExecutionResult> ExecuteAsync(
        string runtime,
        FunctionContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing function {FunctionName} with runtime {Runtime}",
            context.FunctionName, runtime);

        // Apply timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.DefaultTimeout);

        try
        {
            // Get runtime
            if (!_runtimes.TryGetValue(runtime.ToLower(), out var functionRuntime))
            {
                throw new NotSupportedException($"Runtime {runtime} is not supported");
            }

            // Execute with retry logic
            var attempt = 0;
            Exception? lastException = null;
            
            while (attempt < _options.MaxRetries)
            {
                try
                {
                    var result = await functionRuntime.ExecuteAsync(context, cts.Token);
                    
                    if (result.Success || !IsRetryable(result))
                    {
                        return result;
                    }
                    
                    lastException = new Exception(result.Error);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (!IsRetryable(ex))
                    {
                        throw;
                    }
                }

                attempt++;
                
                if (attempt < _options.MaxRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100);
                    _logger.LogWarning("Function execution failed, retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})",
                        delay.TotalMilliseconds, attempt, _options.MaxRetries);
                    
                    await Task.Delay(delay, cts.Token);
                }
            }

            throw new Exception($"Function execution failed after {_options.MaxRetries} attempts", lastException);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Function {FunctionName} execution timed out", context.FunctionName);
            
            return new FunctionExecutionResult
            {
                Success = false,
                Error = "Function execution timed out",
                Duration = _options.DefaultTimeout
            };
        }
    }

    private bool IsRetryable(FunctionExecutionResult result)
    {
        // Determine if error is retryable
        return result.Error?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true ||
               result.Error?.Contains("throttl", StringComparison.OrdinalIgnoreCase) == true;
    }

    private bool IsRetryable(Exception ex)
    {
        return ex is TimeoutException ||
               ex is TaskCanceledException ||
               ex.Message.Contains("throttl", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Function executor options
/// </summary>
public class FunctionExecutorOptions
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetries { get; set; } = 3;
    public bool EnableColdStartOptimization { get; set; } = true;
    public int MaxConcurrentExecutions { get; set; } = 100;
}
