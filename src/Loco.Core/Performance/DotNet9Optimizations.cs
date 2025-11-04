#nullable enable

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Performance;

/// <summary>
/// .NET 9 specific optimizations and features
/// Leverages latest runtime improvements for maximum performance
/// </summary>
public static class DotNet9Optimizations
{
    /// <summary>
    /// Performance monitoring for .NET 9
    /// </summary>
    public class PerformanceMetrics
    {
        public double CpuUsagePercent { get; set; }
        public long MemoryMb { get; set; }
        public long GcTotalMemory { get; set; }
        public int ThreadCount { get; set; }
        public long RequestsPerSecond { get; set; }
        public double AverageLatencyMs { get; set; }
        public long P99LatencyMs { get; set; }
        public long P95LatencyMs { get; set; }

        public override string ToString() =>
            $"CPU: {CpuUsagePercent:F2}%, Memory: {MemoryMb}MB, " +
            $"RPS: {RequestsPerSecond}, Latency (avg/p95/p99): {AverageLatencyMs:F2}ms/{P95LatencyMs}ms/{P99LatencyMs}ms";
    }
}

/// <summary>
/// SIMD (Single Instruction Multiple Data) optimizations
/// Available in .NET 9 - vectorizes common operations
/// </summary>
public static class SimdOptimizations
{
    /// <summary>
    /// Vectorized string comparison for fast pattern matching
    /// </summary>
    public static bool ContainsPattern(string text, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return true;

        // In .NET 9, string.Contains uses SIMD internally for common cases
        return text.Contains(pattern, StringComparison.Ordinal);
    }

    /// <summary>
    /// Vectorized array processing
    /// </summary>
    public static long SumLargeArray(long[] numbers)
    {
        // Modern .NET optimizes this with vectorization
        long sum = 0;
        for (int i = 0; i < numbers.Length; i++)
        {
            sum += numbers[i];
        }
        return sum;
    }

    /// <summary>
    /// Vectorized JSON parsing hint
    /// </summary>
    public static void JsonVectorizationHint()
    {
        // .NET 9's System.Text.Json uses SIMD for UTF-8 validation
        // Automatic - no code changes needed
    }
}

/// <summary>
/// Zero-copy optimizations using spans and memory
/// </summary>
public static class ZeroCopyOptimizations
{
    /// <summary>
    /// Process large buffers without allocation (using Span<T>)
    /// This is the .NET 9 recommended pattern
    /// </summary>
    public static void ProcessBuffer(ReadOnlySpan<byte> buffer)
    {
        // Zero allocation, entire operation on stack
        foreach (var item in buffer)
        {
            _ = item; // Process without copying
        }
    }

    /// <summary>
    /// Memory<T> for async zero-copy operations
    /// Allows safe passing of buffers across async boundaries
    /// </summary>
    public static async Task ProcessLargeStreamAsync(Stream stream, Memory<byte> buffer)
    {
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            ProcessBuffer(buffer.Span[..bytesRead]);
        }
    }

    /// <summary>
    /// String interning optimization
    /// </summary>
    public static void StringOptimization()
    {
        // .NET 9 improved string pool efficiency
        string a = "Hello";
        string b = "Hello";
        // a and b reference same object (interned)
        _ = ReferenceEquals(a, b); // true
    }

    /// <summary>
    /// UTF-8 string literals (C# 11+ / .NET 9)
    /// More efficient for UTF-8 workloads
    /// </summary>
    public static void Utf8StringLiterals()
    {
        // In .NET 9, UTF8 string literals are more efficient
        ReadOnlySpan<byte> utf8String = "Hello World"u8;
        _ = utf8String;
    }
}

/// <summary>
/// JIT (Just-In-Time) compilation optimizations for .NET 9
/// </summary>
public static class JitOptimizations
{
    /// <summary>
    /// Tiered compilation strategy
    /// Level 0 (Quick JIT) -> Level 1 (Full Opt) based on usage
    /// </summary>
    public static class TieredCompilation
    {
        public const string EXPLANATION = @"
TIERED COMPILATION IN .NET 9:

Tier 0 (Quick JIT):
- Fast initial compilation with minimal optimization
- 10-20% of full optimization time
- Enables app startup in milliseconds
- Suitable for methods called occasionally

Tier 1 (Optimized JIT):
- Full optimization after method becomes hot
- Replaces Tier 0 code for frequently-called methods
- 10-100x faster at runtime
- Uses CPU profiling to identify hot paths

Benefits:
- Startup time: 30-40% faster
- Throughput: 5-10% improvement after warmup
- Memory: Better working set due to smaller Tier 0 code
";
    }

    /// <summary>
    /// Method inlining hint
    /// Compiler automatically inlines small methods
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int FastOperation(int a, int b)
    {
        return a + b;
    }

    /// <summary>
    /// Loop optimization hint
    /// </summary>
    public static int LoopOptimization(int[] data)
    {
        int sum = 0;
        // JIT will optimize this loop aggressively
        for (int i = 0; i < data.Length; i++)
        {
            sum += data[i];
        }
        return sum;
    }

    /// <summary>
    /// Branch prediction optimization
    /// Keep branches predictable for CPU
    /// </summary>
    public static void BranchPredictionOptimized(int[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            // Branch is predictable (same pattern each time)
            // CPU can speculate and continue pipeline
            if (values[i] > 0)
            {
                _ = values[i] * 2;
            }
        }
    }
}

/// <summary>
/// GC (Garbage Collection) tuning for .NET 9
/// </summary>
public static class GarbageCollectionOptimizations
{
    /// <summary>
    /// GC configuration for server workloads
    /// In runtimeconfig.json:
    /// "System.GC.Server": true,
    /// "System.GC.Concurrent": true,
    /// "System.GC.HeapCount": num_cores
    /// </summary>
    public static class GcConfiguration
    {
        public const string RUNTIMECONFIG_JSON = @"{
  ""runtimeOptions"": {
    ""configProperties"": {
      ""System.GC.Server"": true,
      ""System.GC.Concurrent"": true,
      ""System.GC.HeapCount"": 8,
      ""System.GC.HeapAffinitizeMask"": 0xFF,
      ""System.GC.RetainVM"": true,
      ""System.GC.HighMemPercentage"": 90
    }
  }
}";
    }

    /// <summary>
    /// POGO (Profile Guided Optimization)
    /// Uses runtime profiling to inform JIT compilation
    /// </summary>
    public static class PogoOptimization
    {
        public const string EXPLANATION = @"
PROFILE GUIDED OPTIMIZATION:

1. Profile phase: Run app with POGO enabled
   - Runtime collects information about hot paths
   - Records branch predictions, method calls
   - Saves to .ibc file

2. Optimization phase: Rebuild using profile data
   - JIT uses profile to optimize hot paths
   - Can be 5-15% faster

Setup:
- COMPlus_TieredPGO=1 environment variable
- dotnet publish with --pogo flag
";
    }

    /// <summary>
    /// LOH (Large Object Heap) optimization
    /// </summary>
    public static void AvoidLohFragmentation()
    {
        // In .NET 9, LOH default threshold is 85KB
        // Avoid allocating many large objects separately
        // Instead: use ArrayPool<T> for reusable buffers

        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(100000);
        try
        {
            // Use buffer
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Gen 2 pinning avoidance
    /// </summary>
    public static void AvoidPinning()
    {
        // DON'T: GCHandle.Alloc(object) - pins in Gen 2
        // DO: Use stackalloc or Span for small buffers
        Span<byte> stackBuffer = stackalloc byte[256];
        _ = stackBuffer;
    }
}

/// <summary>
/// Startup performance optimizations for .NET 9
/// </summary>
public static class StartupOptimizations
{
    /// <summary>
    /// Ready-to-Run (R2R) pre-compilation
    /// Compiles IL to native code ahead-of-time
    /// </summary>
    public static class ReadyToRun
    {
        public const string EXPLANATION = @"
READY-TO-RUN COMPILATION:

Benefits:
- 30-50% faster startup time
- Reduced JIT compilation overhead
- Smaller memory footprint initially

Setup:
PublishTrimmed:
  <PublishReadyToRun>true</PublishReadyToRun>
  <PublishReadyToRunShowWarnings>true</PublishReadyToRunShowWarnings>

Tradeoff:
- Larger executable size
- Slightly lower peak throughput (JIT can still optimize further)
- Less effective for dynamic code
";
    }

    /// <summary>
    /// Native AOT (Ahead-Of-Time) compilation
    /// Fully pre-compiled to native code, no JIT at runtime
    /// </summary>
    public static class NativeAOT
    {
        public const string EXPLANATION = @"
NATIVE AOT COMPILATION (.NET 9):

Benefits:
- 10-100x faster startup (no JIT compilation)
- Deterministic performance (no JIT pauses)
- Smaller deployment size with trimming
- Works in restricted environments

Setup:
<PublishAot>true</PublishAot>
<PublishTrimmed>true</PublishTrimmed>

Limitations:
- No reflection at runtime
- No dynamic code generation
- Needs trimming-safe libraries
- Requires C++/LLVM for compilation

Best for:
- Containerized microservices
- Serverless functions
- IoT devices
- CLI tools
";
    }

    /// <summary>
    /// Reflection reduction
    /// </summary>
    public static void ReduceReflection()
    {
        // Use source generators instead of reflection where possible
        // Mark reflection-heavy assemblies with [assembly: GeneratedCode]
        // .NET 9 has better trimming analysis
    }
}

/// <summary>
/// Network and I/O optimizations for .NET 9
/// </summary>
public static class IoOptimizations
{
    /// <summary>
    /// Kernel-mode socket I/O with Kestrel
    /// .NET 9's Kestrel uses more efficient I/O patterns
    /// </summary>
    public static class KestrelOptimizations
    {
        public const string EXPLANATION = @"
KESTREL IMPROVEMENTS IN .NET 9:

1. HTTP/3 QUIC support - faster, more efficient
2. Improved memory pooling - fewer allocations
3. Better backpressure handling - prevents overload
4. Kernel-mode socket buffering on Windows

Configuration:
.UseKestrel(options =>
{
    options.Limits.Http2.MaxFrameSize = 65536;
    options.Limits.Http2.KeepAliveTimeout = TimeSpan.FromSeconds(5);
    options.AddServerHeader = false; // Saves bytes
})
";
    }

    /// <summary>
    /// PipeReader/PipeWriter for high-throughput I/O
    /// </summary>
    public static async Task ProcessStreamAsync(Stream stream)
    {
        var reader = PipeReader.Create(stream);
        try
        {
            while (true)
            {
                var result = await reader.ReadAsync().ConfigureAwait(false);
                var buffer = result.Buffer;

                // Process buffer efficiently
                if (result.IsCompleted || result.IsCanceled)
                    break;

                reader.AdvanceTo(buffer.Start, buffer.End);
            }
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Concurrency improvements in .NET 9
/// </summary>
public static class ConcurrencyOptimizations
{
    /// <summary>
    /// Lock-free synchronization using Interlocked operations
    /// </summary>
    public class AtomicCounter
    {
        private long _value;

        public long Increment()
        {
            return Interlocked.Increment(ref _value);
        }

        public long Value => Interlocked.Read(ref _value);
    }

    /// <summary>
    /// ReaderWriterLockSlim for read-heavy workloads
    /// </summary>
    public class ReadHeavyCache<T> where T : class
    {
        private readonly ReaderWriterLockSlim _lock = new();
        private T? _value;

        public bool TryGet(out T? value)
        {
            _lock.EnterReadLock();
            try
            {
                value = _value;
                return value != null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Set(T value)
        {
            _lock.EnterWriteLock();
            try
            {
                _value = value;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }

    /// <summary>
    /// Async concurrent collection for producer-consumer patterns
    /// </summary>
    public static async Task ProducerConsumerPatternAsync()
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<int>();

        // Producer
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                await channel.Writer.WriteAsync(i).ConfigureAwait(false);
            }
            channel.Writer.Complete();
        });

        // Consumer
        await foreach (var item in channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            // Process item
        }
    }
}

/// <summary>
/// Security optimizations for .NET 9
/// </summary>
public static class SecurityOptimizations
{
    /// <summary>
    /// Cryptographic operation pooling
    /// Reuse crypto providers where possible
    /// </summary>
    public static class CryptoPooling
    {
        // Pre-created SHA256 for reuse (thread-safe in .NET 9)
        private static readonly System.Security.Cryptography.SHA256 _sha256 =
            System.Security.Cryptography.SHA256.Create();

        public static byte[] HashData(ReadOnlySpan<byte> data)
        {
            lock (_sha256) // Needed if not using static HashData method
            {
                return _sha256.ComputeHash(data.ToArray());
            }
        }

        // Better: Use static HashData method (no allocation for small data)
        public static byte[] HashDataStatic(ReadOnlySpan<byte> data)
        {
            return System.Security.Cryptography.SHA256.HashData(data);
        }
    }

    /// <summary>
    /// Secure string handling
    /// </summary>
    public static void SecureStringHandling()
    {
        // Use SecureString (on Windows)
        // Or use Span<byte> for password buffers
        Span<byte> passwordBuffer = stackalloc byte[256];
        // Process without allocating on heap
    }
}

/// <summary>
/// Diagnostic and monitoring improvements
/// </summary>
public static class DiagnosticsOptimizations
{
    /// <summary>
    /// EventCounters for real-time metrics
    /// </summary>
    public class ApplicationMetrics
    {
        private readonly System.Diagnostics.Tracing.EventCounter _requestCount;
        private readonly System.Diagnostics.Tracing.PollingCounter _memoryCounter;

        public ApplicationMetrics()
        {
            // In .NET 9, these are more efficient
            _requestCount = new System.Diagnostics.Tracing.EventCounter(
                "request-count", null);

            _memoryCounter = new System.Diagnostics.Tracing.PollingCounter(
                "memory-usage",
                null,
                () => GC.GetTotalMemory(false) / 1024 / 1024);
        }

        public void RecordRequest()
        {
            _requestCount.WriteMetric(1);
        }
    }

    /// <summary>
    /// Activity/Tracing for distributed tracing
    /// </summary>
    public static void DistributedTracingExample()
    {
        var activity = new System.Diagnostics.Activity("OperationName").Start();
        try
        {
            activity?.AddTag("key", "value");
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Ok);
        }
        finally
        {
            activity?.Dispose();
        }
    }
}

/// <summary>
/// .NET 9 Performance Tuning Checklist
/// </summary>
public static class PerformanceTuningChecklist
{
    public const string CHECKLIST = @"
.NET 9 PERFORMANCE OPTIMIZATION CHECKLIST:

Startup Performance:
☐ Enable Ready-to-Run (PublishReadyToRun=true)
☐ Consider Native AOT for containerized apps
☐ Remove unnecessary assemblies during trimming
☐ Profile startup with dotnet-trace

Runtime Performance:
☐ Enable Tiered Compilation (default in .NET 9)
☐ Set GC.Server=true for multi-core servers
☐ Use ArrayPool<T> to avoid LOH fragmentation
☐ Use Span<T> and Memory<T> for zero-copy operations
☐ Enable POGO for profile-guided optimization

Memory Usage:
☐ Profile with dotnet-trace and dotnet-dump
☐ Avoid large object allocations on hot paths
☐ Use stackalloc for small temporary buffers
☐ Enable ReadyToRun for smaller working set
☐ Implement IAsyncDisposable for cleanup

I/O Performance:
☐ Use HTTP/3 (QUIC) where supported
☐ Enable kernel-mode socket buffering
☐ Use PipeReader/PipeWriter for streams
☐ Tune Kestrel thread pool size

Concurrency:
☐ Use async/await throughout
☐ Avoid blocking on async code
☐ Use SemaphoreSlim for rate limiting
☐ Implement proper cancellation handling

Monitoring:
☐ Enable EventCounters
☐ Use Application Insights or similar
☐ Monitor GC pause times
☐ Track request latency percentiles (p95, p99)
";
}
