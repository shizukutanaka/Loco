using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Performance;

/// <summary>
/// High-performance parallel execution engine
/// Inspired by John Carmack's optimization techniques
/// </summary>
public sealed class ParallelExecutionEngine : IDisposable
{
    private readonly ILogger<ParallelExecutionEngine> _logger;
    private readonly int _workerCount;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Channel<WorkItem> _workQueue;
    private readonly Task[] _workers;
    private readonly ThreadLocal<Stopwatch> _threadLocalStopwatch;
    
    // Performance metrics
    private long _tasksProcessed;
    private long _totalProcessingTime;
    private long _queuedTasks;
    private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics;
    
    private class WorkItem
    {
        public Func<Task> Work { get; set; }
        public TaskCompletionSource<object> CompletionSource { get; set; }
        public string MetricKey { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }
    
    private class PerformanceMetric
    {
        public long Count { get; set; }
        public long TotalTimeMs { get; set; }
        public long MinTimeMs { get; set; } = long.MaxValue;
        public long MaxTimeMs { get; set; }
        public double AverageTimeMs => Count > 0 ? (double)TotalTimeMs / Count : 0;
    }
    
    public ParallelExecutionEngine(ILogger<ParallelExecutionEngine> logger, int? workerCount = null)
    {
        _logger = logger;
        _workerCount = workerCount ?? Math.Min(Environment.ProcessorCount * 2, 32);
        _cancellationTokenSource = new CancellationTokenSource();
        _metrics = new ConcurrentDictionary<string, PerformanceMetric>();
        _threadLocalStopwatch = new ThreadLocal<Stopwatch>(() => new Stopwatch());
        
        // Create unbounded channel for work items
        _workQueue = Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        
        // Start worker tasks
        _workers = new Task[_workerCount];
        for (int i = 0; i < _workerCount; i++)
        {
            var workerId = i;
            _workers[i] = Task.Run(() => WorkerLoop(workerId), _cancellationTokenSource.Token);
        }
        
        _logger.LogInformation("Parallel execution engine started with {WorkerCount} workers", _workerCount);
    }
    
    /// <summary>
    /// Execute a task asynchronously with optimal scheduling
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ExecuteAsync(Func<Task> work, string metricKey = null, CancellationToken cancellationToken = default)
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            return Task.FromException(new ObjectDisposedException(nameof(ParallelExecutionEngine)));
        }
        
        var workItem = new WorkItem
        {
            Work = work,
            CompletionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously),
            MetricKey = metricKey ?? "default",
            CancellationToken = cancellationToken
        };
        
        if (!_workQueue.Writer.TryWrite(workItem))
        {
            return Task.FromException(new InvalidOperationException("Work queue is full"));
        }
        
        Interlocked.Increment(ref _queuedTasks);
        return workItem.CompletionSource.Task;
    }
    
    /// <summary>
    /// Execute multiple tasks in parallel with batching
    /// </summary>
    public async Task<TResult[]> ExecuteBatchAsync<TResult>(
        IEnumerable<Func<Task<TResult>>> tasks,
        int maxConcurrency = 0,
        CancellationToken cancellationToken = default)
    {
        var taskList = tasks.ToList();
        if (taskList.Count == 0)
        {
            return Array.Empty<TResult>();
        }
        
        maxConcurrency = maxConcurrency > 0 ? maxConcurrency : _workerCount;
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var results = new ConcurrentBag<(int Index, TResult Result)>();
        
        var executionTasks = taskList.Select(async (task, index) =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await task();
                results.Add((index, result));
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        await Task.WhenAll(executionTasks);
        
        // Return results in original order
        return results.OrderBy(r => r.Index).Select(r => r.Result).ToArray();
    }
    
    /// <summary>
    /// Execute tasks with pipeline pattern for maximum throughput
    /// </summary>
    public async Task<IAsyncEnumerable<TResult>> ExecutePipelineAsync<TInput, TResult>(
        IAsyncEnumerable<TInput> inputs,
        Func<TInput, Task<TResult>> processor,
        int bufferSize = 100,
        CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<TResult>(new BoundedChannelOptions(bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        
        // Producer task
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var input in inputs.WithCancellation(cancellationToken))
                {
                    var task = ExecuteAsync(async () =>
                    {
                        var result = await processor(input);
                        await channel.Writer.WriteAsync(result, cancellationToken);
                    }, "pipeline", cancellationToken);
                    
                    // Don't await - let it process in parallel
                    _ = task.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            _logger.LogError(t.Exception, "Pipeline processing error");
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);
        
        // Return async enumerable from channel
        return channel.Reader.ReadAllAsync(cancellationToken);
    }
    
    /// <summary>
    /// Map-reduce pattern for data processing
    /// </summary>
    public async Task<TResult> MapReduceAsync<TInput, TIntermediate, TResult>(
        IEnumerable<TInput> inputs,
        Func<TInput, Task<TIntermediate>> mapFunc,
        Func<IEnumerable<TIntermediate>, TResult> reduceFunc,
        CancellationToken cancellationToken = default)
    {
        var intermediateResults = await ExecuteBatchAsync(
            inputs.Select(input => () => mapFunc(input)),
            cancellationToken: cancellationToken);
        
        return reduceFunc(intermediateResults);
    }
    
    private async Task WorkerLoop(int workerId)
    {
        _logger.LogDebug("Worker {WorkerId} started", workerId);
        var stopwatch = _threadLocalStopwatch.Value;
        
        try
        {
            await foreach (var workItem in _workQueue.Reader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                if (workItem.CancellationToken.IsCancellationRequested)
                {
                    workItem.CompletionSource.SetCanceled(workItem.CancellationToken);
                    continue;
                }
                
                stopwatch.Restart();
                
                try
                {
                    await workItem.Work();
                    workItem.CompletionSource.SetResult(null);
                    
                    stopwatch.Stop();
                    UpdateMetrics(workItem.MetricKey, stopwatch.ElapsedMilliseconds);
                }
                catch (OperationCanceledException ex)
                {
                    workItem.CompletionSource.SetCanceled(ex.CancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker {WorkerId} encountered error", workerId);
                    workItem.CompletionSource.SetException(ex);
                }
                finally
                {
                    Interlocked.Decrement(ref _queuedTasks);
                    Interlocked.Increment(ref _tasksProcessed);
                    
                    if (stopwatch.IsRunning)
                    {
                        stopwatch.Stop();
                        Interlocked.Add(ref _totalProcessingTime, stopwatch.ElapsedMilliseconds);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker {WorkerId} crashed", workerId);
        }
        
        _logger.LogDebug("Worker {WorkerId} stopped", workerId);
    }
    
    private void UpdateMetrics(string key, long elapsedMs)
    {
        _metrics.AddOrUpdate(key,
            k => new PerformanceMetric
            {
                Count = 1,
                TotalTimeMs = elapsedMs,
                MinTimeMs = elapsedMs,
                MaxTimeMs = elapsedMs
            },
            (k, existing) =>
            {
                Interlocked.Increment(ref existing.Count);
                Interlocked.Add(ref existing.TotalTimeMs, elapsedMs);
                
                // Update min/max (not perfectly thread-safe but good enough)
                if (elapsedMs < existing.MinTimeMs)
                    existing.MinTimeMs = elapsedMs;
                if (elapsedMs > existing.MaxTimeMs)
                    existing.MaxTimeMs = elapsedMs;
                
                return existing;
            });
    }
    
    public ExecutionStatistics GetStatistics()
    {
        return new ExecutionStatistics
        {
            TasksProcessed = _tasksProcessed,
            QueuedTasks = _queuedTasks,
            TotalProcessingTimeMs = _totalProcessingTime,
            AverageProcessingTimeMs = _tasksProcessed > 0 ? (double)_totalProcessingTime / _tasksProcessed : 0,
            WorkerCount = _workerCount,
            Metrics = _metrics.ToDictionary(
                kvp => kvp.Key,
                kvp => new ExecutionMetricSummary
                {
                    Count = kvp.Value.Count,
                    TotalTimeMs = kvp.Value.TotalTimeMs,
                    AverageTimeMs = kvp.Value.AverageTimeMs,
                    MinTimeMs = kvp.Value.MinTimeMs,
                    MaxTimeMs = kvp.Value.MaxTimeMs
                })
        };
    }
    
    public async Task ShutdownAsync(TimeSpan timeout = default)
    {
        if (timeout == default)
            timeout = TimeSpan.FromSeconds(30);
        
        _logger.LogInformation("Shutting down parallel execution engine");
        
        // Stop accepting new work
        _workQueue.Writer.TryComplete();
        
        // Wait for workers to complete or timeout
        var shutdownTask = Task.WhenAll(_workers);
        var timeoutTask = Task.Delay(timeout);
        
        if (await Task.WhenAny(shutdownTask, timeoutTask) == timeoutTask)
        {
            _logger.LogWarning("Shutdown timeout exceeded, forcing cancellation");
            _cancellationTokenSource.Cancel();
            
            try
            {
                await Task.WhenAll(_workers);
            }
            catch
            {
                // Ignore cancellation exceptions during shutdown
            }
        }
        
        _logger.LogInformation("Parallel execution engine shutdown complete. Processed {Tasks} tasks", _tasksProcessed);
    }
    
    public void Dispose()
    {
        ShutdownAsync().GetAwaiter().GetResult();
        _cancellationTokenSource?.Dispose();
        _threadLocalStopwatch?.Dispose();
    }
}

public class ExecutionStatistics
{
    public long TasksProcessed { get; set; }
    public long QueuedTasks { get; set; }
    public long TotalProcessingTimeMs { get; set; }
    public double AverageProcessingTimeMs { get; set; }
    public int WorkerCount { get; set; }
    public Dictionary<string, ExecutionMetricSummary> Metrics { get; set; }
}

public class ExecutionMetricSummary
{
    public long Count { get; set; }
    public long TotalTimeMs { get; set; }
    public double AverageTimeMs { get; set; }
    public long MinTimeMs { get; set; }
    public long MaxTimeMs { get; set; }
}

/// <summary>
/// Task scheduler optimized for high throughput
/// </summary>
public sealed class OptimizedTaskScheduler : TaskScheduler, IDisposable
{
    private readonly BlockingCollection<Task> _tasks;
    private readonly Thread[] _threads;
    private readonly CancellationTokenSource _cancellationTokenSource;
    
    public OptimizedTaskScheduler(int concurrency = 0)
    {
        concurrency = concurrency > 0 ? concurrency : Environment.ProcessorCount;
        _tasks = new BlockingCollection<Task>();
        _cancellationTokenSource = new CancellationTokenSource();
        _threads = new Thread[concurrency];
        
        for (int i = 0; i < concurrency; i++)
        {
            _threads[i] = new Thread(WorkerThread)
            {
                IsBackground = true,
                Name = $"OptimizedScheduler-{i}"
            };
            _threads[i].Start();
        }
    }
    
    protected override IEnumerable<Task> GetScheduledTasks()
    {
        return _tasks.ToArray();
    }
    
    protected override void QueueTask(Task task)
    {
        _tasks.Add(task);
    }
    
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        // Allow inlining for better performance
        if (Thread.CurrentThread.Name?.StartsWith("OptimizedScheduler") == true)
        {
            return TryExecuteTask(task);
        }
        return false;
    }
    
    private void WorkerThread()
    {
        foreach (var task in _tasks.GetConsumingEnumerable(_cancellationTokenSource.Token))
        {
            TryExecuteTask(task);
        }
    }
    
    public void Dispose()
    {
        _tasks.CompleteAdding();
        _cancellationTokenSource.Cancel();
        
        foreach (var thread in _threads)
        {
            thread.Join(5000);
        }
        
        _tasks.Dispose();
        _cancellationTokenSource.Dispose();
    }
}
