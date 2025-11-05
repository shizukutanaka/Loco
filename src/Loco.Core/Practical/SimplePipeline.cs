// Rob Pike: "Concurrency is not parallelism, but it enables parallelism"
// John Carmack: "The best code is simple, direct, and obviously correct"

namespace Loco.Core.Practical;

/// <summary>
/// Simple pipeline - Chain operations, transform data, handle errors
/// Composable, testable, zero dependencies
/// </summary>
public class SimplePipeline<TInput, TOutput>
{
    private readonly List<IStage> _stages = new();
    private readonly SimpleLogger _logger;

    public SimplePipeline(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimplePipeline<TInput, TOutput>));
    }

    private interface IStage
    {
        Task<object?> ExecuteAsync(object? input);
        bool CanExecute(object? input);
    }

    private class Stage<TIn, TOut> : IStage
    {
        private readonly Func<TIn, Task<TOut>> _operation;
        private readonly Func<TIn, bool>? _condition;
        private readonly string _name;

        public Stage(string name, Func<TIn, Task<TOut>> operation, Func<TIn, bool>? condition = null)
        {
            _name = name;
            _operation = operation;
            _condition = condition;
        }

        public async Task<object?> ExecuteAsync(object? input)
        {
            if (input is TIn typedInput)
            {
                var result = await _operation(typedInput);
                return result;
            }
            throw new InvalidOperationException($"Stage {_name} expected {typeof(TIn).Name} but got {input?.GetType().Name ?? "null"}");
        }

        public bool CanExecute(object? input)
        {
            if (input is TIn typedInput)
            {
                return _condition?.Invoke(typedInput) ?? true;
            }
            return false;
        }
    }

    // Add synchronous stage
    public SimplePipeline<TInput, TNewOutput> AddStage<TCurrentOutput, TNewOutput>(
        string name,
        Func<TCurrentOutput, TNewOutput> operation,
        Func<TCurrentOutput, bool>? condition = null)
    {
        _stages.Add(new Stage<TCurrentOutput, TNewOutput>(
            name,
            input => Task.FromResult(operation(input)),
            condition));

        return new SimplePipeline<TInput, TNewOutput> { _stages = this._stages };
    }

    // Add async stage
    public SimplePipeline<TInput, TNewOutput> AddStageAsync<TCurrentOutput, TNewOutput>(
        string name,
        Func<TCurrentOutput, Task<TNewOutput>> operation,
        Func<TCurrentOutput, bool>? condition = null)
    {
        _stages.Add(new Stage<TCurrentOutput, TNewOutput>(name, operation, condition));
        return new SimplePipeline<TInput, TNewOutput> { _stages = this._stages };
    }

    // Execute pipeline
    public async Task<TOutput?> ExecuteAsync(TInput input)
    {
        object? current = input;

        foreach (var stage in _stages)
        {
            if (!stage.CanExecute(current))
            {
                _logger.Warning($"Stage skipped due to condition");
                continue;
            }

            try
            {
                current = await stage.ExecuteAsync(current);
            }
            catch (Exception ex)
            {
                _logger.Error($"Pipeline stage failed", ex);
                throw;
            }
        }

        return current is TOutput output ? output : default;
    }
}

/// <summary>
/// Pipeline builder - Fluent API for building pipelines
/// </summary>
public static class Pipeline
{
    public static SimplePipeline<T, T> Create<T>(SimpleLogger? logger = null)
    {
        return new SimplePipeline<T, T>(logger);
    }

    public static SimplePipeline<TInput, TOutput> Create<TInput, TOutput>(
        Func<TInput, TOutput> initialTransform,
        SimpleLogger? logger = null)
    {
        var pipeline = new SimplePipeline<TInput, TOutput>(logger);
        return pipeline.AddStage("Initial", initialTransform);
    }
}

/// <summary>
/// Parallel pipeline - Process items in parallel
/// </summary>
public class ParallelPipeline<T>
{
    private readonly Func<T, Task<T>> _process;
    private readonly int _maxDegreeOfParallelism;
    private readonly SimpleLogger _logger;

    public ParallelPipeline(
        Func<T, Task<T>> process,
        int maxDegreeOfParallelism = 4,
        SimpleLogger? logger = null)
    {
        _process = process;
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(ParallelPipeline<T>));
    }

    public async Task<List<T>> ProcessAsync(IEnumerable<T> items)
    {
        var semaphore = new SemaphoreSlim(_maxDegreeOfParallelism);
        var tasks = new List<Task<T>>();

        foreach (var item in items)
        {
            await semaphore.WaitAsync();

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    return await _process(item);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        return (await Task.WhenAll(tasks)).ToList();
    }
}

/// <summary>
/// Stream pipeline - Process data streams
/// </summary>
public class StreamPipeline<T>
{
    private readonly List<Func<T, Task<T>>> _transforms = new();
    private readonly SimpleLogger _logger;

    public StreamPipeline(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(StreamPipeline<T>));
    }

    public StreamPipeline<T> Transform(Func<T, T> transform)
    {
        _transforms.Add(async item => transform(item));
        return this;
    }

    public StreamPipeline<T> TransformAsync(Func<T, Task<T>> transform)
    {
        _transforms.Add(transform);
        return this;
    }

    public StreamPipeline<T> Filter(Func<T, bool> predicate)
    {
        _transforms.Add(async item => predicate(item) ? item : default!);
        return this;
    }

    public async IAsyncEnumerable<T> ProcessAsync(IAsyncEnumerable<T> source)
    {
        await foreach (var item in source)
        {
            var current = item;

            foreach (var transform in _transforms)
            {
                current = await transform(current);
                if (current == null) break; // Filtered out
            }

            if (current != null)
            {
                yield return current;
            }
        }
    }
}

/// <summary>
/// Example: Data processing pipeline
/// </summary>
public class DataProcessingPipeline
{
    public async Task<ProcessedData> ProcessDataAsync(RawData input)
    {
        var pipeline = Pipeline.Create<RawData, ProcessedData>()
            .AddStage("Validate", (RawData data) =>
            {
                if (string.IsNullOrEmpty(data.Content))
                    throw new ArgumentException("Content is required");
                return data;
            })
            .AddStage("Clean", (RawData data) => new CleanedData
            {
                Content = data.Content.Trim().ToLowerInvariant(),
                Timestamp = data.Timestamp
            })
            .AddStage("Transform", (CleanedData data) => new TransformedData
            {
                Words = data.Content.Split(' '),
                Timestamp = data.Timestamp
            })
            .AddStage("Analyze", (TransformedData data) => new ProcessedData
            {
                WordCount = data.Words.Length,
                UniqueWords = data.Words.Distinct().Count(),
                Timestamp = data.Timestamp
            });

        return await pipeline.ExecuteAsync(input) ?? new ProcessedData();
    }
}

// Example data types
public record RawData(string Content, DateTime Timestamp);
public record CleanedData { public string Content { get; init; } = ""; public DateTime Timestamp { get; init; } }
public record TransformedData { public string[] Words { get; init; } = Array.Empty<string>(); public DateTime Timestamp { get; init; } }
public record ProcessedData { public int WordCount { get; init; } public int UniqueWords { get; init; } public DateTime Timestamp { get; init; } }

/// <summary>
/// Example: Image processing pipeline
/// </summary>
public class ImagePipeline
{
    public static async Task<byte[]> ProcessImageAsync(byte[] imageData)
    {
        var pipeline = Pipeline.Create<byte[], byte[]>()
            .AddStageAsync("Resize", async (byte[] data) =>
            {
                // Simulate resize
                await Task.Delay(10);
                return data;
            })
            .AddStage("Compress", (byte[] data) =>
            {
                // Simulate compression
                return data.Take(data.Length / 2).ToArray();
            })
            .AddStage("Optimize", (byte[] data) =>
            {
                // Simulate optimization
                return data;
            },
            condition: data => data.Length > 1024); // Only optimize large images

        return await pipeline.ExecuteAsync(imageData) ?? Array.Empty<byte>();
    }
}

/// <summary>
/// Example: ETL pipeline
/// </summary>
public class EtlPipeline<TSource, TDestination>
{
    private readonly Func<TSource, TDestination> _transform;
    private readonly Action<TDestination> _load;
    private readonly SimpleMetrics _metrics;

    public EtlPipeline(
        Func<TSource, TDestination> transform,
        Action<TDestination> load,
        SimpleMetrics? metrics = null)
    {
        _transform = transform;
        _load = load;
        _metrics = metrics ?? new SimpleMetrics();
    }

    public async Task ProcessBatchAsync(IEnumerable<TSource> batch)
    {
        var pipeline = new ParallelPipeline<TSource>(
            async item =>
            {
                _metrics.IncrementCounter("etl.extracted");
                var transformed = _transform(item);
                await Task.CompletedTask;
                return item;
            },
            maxDegreeOfParallelism: 8);

        var results = await pipeline.ProcessAsync(batch);

        foreach (var item in results)
        {
            var transformed = _transform(item);
            _load(transformed);
            _metrics.IncrementCounter("etl.loaded");
        }
    }
}

/// <summary>
/// Example: Middleware pipeline
/// </summary>
public class MiddlewarePipeline<TContext>
{
    private readonly List<Func<TContext, Func<Task>, Task>> _middleware = new();

    public MiddlewarePipeline<TContext> Use(Func<TContext, Func<Task>, Task> middleware)
    {
        _middleware.Add(middleware);
        return this;
    }

    public async Task ExecuteAsync(TContext context)
    {
        var index = -1;

        Task Next()
        {
            index++;
            if (index < _middleware.Count)
            {
                return _middleware[index](context, Next);
            }
            return Task.CompletedTask;
        }

        await Next();
    }
}

// Example middleware
public class HttpContext
{
    public string Path { get; set; } = "";
    public Dictionary<string, string> Headers { get; } = new();
    public object? Response { get; set; }
}

public class ApiPipeline
{
    public static MiddlewarePipeline<HttpContext> CreatePipeline()
    {
        return new MiddlewarePipeline<HttpContext>()
            .Use(async (context, next) =>
            {
                // Logging middleware
                Console.WriteLine($"Request: {context.Path}");
                await next();
                Console.WriteLine($"Response: {context.Response}");
            })
            .Use(async (context, next) =>
            {
                // Authentication middleware
                if (!context.Headers.ContainsKey("Authorization"))
                {
                    context.Response = "Unauthorized";
                    return;
                }
                await next();
            })
            .Use(async (context, next) =>
            {
                // Route handler
                context.Response = $"Handled: {context.Path}";
                await Task.CompletedTask;
            });
    }
}