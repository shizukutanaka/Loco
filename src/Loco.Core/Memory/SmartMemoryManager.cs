using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Memory
{
    /// <summary>
    /// Smart memory manager with advanced pooling, monitoring, and optimization
    /// Optimized for high-performance applications with minimal GC pressure
    /// </summary>
    public sealed class SmartMemoryManager : IDisposable
    {
        private readonly ILogger<SmartMemoryManager> _logger;
        private readonly ConcurrentDictionary<Type, IMemoryPool> _pools;
        private readonly MemoryMonitor _monitor;
        private readonly Timer _optimizationTimer;
        private readonly MemoryConfiguration _config;
        private bool _disposed;

        // Performance counters
        private long _totalAllocations;
        private long _totalDeallocations;
        private long _poolHits;
        private long _poolMisses;

        public SmartMemoryManager(ILogger<SmartMemoryManager> logger = null, MemoryConfiguration config = null)
        {
            _logger = logger;
            _config = config ?? new MemoryConfiguration();
            _pools = new ConcurrentDictionary<Type, IMemoryPool>();
            _monitor = new MemoryMonitor(_config);
            
            // Initialize built-in pools
            InitializePools();
            
            // Start optimization timer
            _optimizationTimer = new Timer(
                OptimizeMemoryPools, 
                null, 
                _config.OptimizationInterval, 
                _config.OptimizationInterval);
                
            _logger?.LogInformation("Smart Memory Manager initialized with config: {Config}", _config);
        }

        /// <summary>
        /// Get a managed memory block with automatic pooling
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SmartMemoryBlock<T> GetMemory<T>(int size) where T : struct
        {
            Interlocked.Increment(ref _totalAllocations);
            
            var pool = GetOrCreatePool<T>();
            var block = pool.Rent(size);
            
            if (block != null)
            {
                Interlocked.Increment(ref _poolHits);
                _monitor.RecordAllocation(typeof(T), size, true);
                return new SmartMemoryBlock<T>(block, this, size);
            }
            
            Interlocked.Increment(ref _poolMisses);
            var memory = GC.AllocateUninitializedArray<T>(size, pinned: _config.PinLargeArrays && size > _config.LargeArrayThreshold);
            _monitor.RecordAllocation(typeof(T), size, false);
            
            return new SmartMemoryBlock<T>(memory, this, size);
        }

        /// <summary>
        /// Get zero-initialized memory
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SmartMemoryBlock<T> GetZeroMemory<T>(int size) where T : struct
        {
            var block = GetMemory<T>(size);
            block.Memory.Span.Clear();
            return block;
        }

        /// <summary>
        /// Get aligned memory for SIMD operations
        /// </summary>
        public unsafe SmartMemoryBlock<T> GetAlignedMemory<T>(int size, int alignment = 64) where T : struct
        {
            if (!BitOperations.IsPow2(alignment))
                throw new ArgumentException("Alignment must be power of 2", nameof(alignment));
                
            var pool = GetOrCreateAlignedPool<T>(alignment);
            var block = pool.Rent(size);
            
            if (block != null)
            {
                Interlocked.Increment(ref _poolHits);
                return new SmartMemoryBlock<T>(block, this, size);
            }
            
            // Allocate aligned memory manually
            var totalSize = size * Unsafe.SizeOf<T>() + alignment;
            var unaligned = Marshal.AllocHGlobal(totalSize);
            var aligned = new IntPtr((unaligned.ToInt64() + alignment - 1) & ~(alignment - 1));
            
            var memory = new UnmanagedMemory<T>(aligned, size, unaligned);
            return new SmartMemoryBlock<T>(memory, this, size);
        }

        /// <summary>
        /// Create a memory-mapped region for large data sets
        /// </summary>
        public MemoryMappedRegion<T> CreateMappedRegion<T>(long size, string name = null) where T : struct
        {
            name ??= $"SmartMemory_{typeof(T).Name}_{Guid.NewGuid():N}";
            
            var elementSize = Unsafe.SizeOf<T>();
            var totalBytes = size * elementSize;
            
            var region = new MemoryMappedRegion<T>(name, totalBytes, size);
            _monitor.RecordMappedRegion(typeof(T), totalBytes);
            
            return region;
        }

        /// <summary>
        /// Bulk operations for high-performance scenarios
        /// </summary>
        public void BulkOperation<T>(ReadOnlySpan<T> source, Span<T> destination, BulkOperationType operation) where T : struct
        {
            if (source.Length != destination.Length)
                throw new ArgumentException("Source and destination must have same length");
                
            switch (operation)
            {
                case BulkOperationType.Copy:
                    source.CopyTo(destination);
                    break;
                    
                case BulkOperationType.Fill:
                    if (source.Length > 0)
                    {
                        destination.Fill(source[0]);
                    }
                    break;
                    
                case BulkOperationType.Clear:
                    destination.Clear();
                    break;
                    
                case BulkOperationType.Reverse:
                    source.CopyTo(destination);
                    destination.Reverse();
                    break;
            }
        }

        /// <summary>
        /// Copy with automatic SIMD optimization
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void OptimizedCopy<T>(ReadOnlySpan<T> source, Span<T> destination) where T : struct
        {
            if (source.Length != destination.Length)
                throw new ArgumentException("Spans must have same length");
                
            var elementSize = Unsafe.SizeOf<T>();
            var totalBytes = source.Length * elementSize;
            
            // Use hardware acceleration for large copies
            if (totalBytes >= _config.SIMDThreshold && Vector.IsHardwareAccelerated)
            {
                VectorizedCopy(source, destination);
            }
            else
            {
                source.CopyTo(destination);
            }
        }

        /// <summary>
        /// Get memory statistics
        /// </summary>
        public MemoryStatistics GetStatistics()
        {
            var gcStats = GC.GetTotalMemory(false);
            var workingSet = Process.GetCurrentProcess().WorkingSet64;
            
            return new MemoryStatistics
            {
                TotalAllocations = _totalAllocations,
                TotalDeallocations = _totalDeallocations,
                PoolHits = _poolHits,
                PoolMisses = _poolMisses,
                HitRatio = _totalAllocations > 0 ? (double)_poolHits / _totalAllocations : 0,
                GCMemory = gcStats,
                WorkingSet = workingSet,
                PoolCount = _pools.Count,
                ActivePools = CountActivePools()
            };
        }

        /// <summary>
        /// Force memory optimization
        /// </summary>
        public void OptimizeNow()
        {
            OptimizeMemoryPools(null);
            
            if (_config.EnableGCOptimization)
            {
                // Compact large object heap
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Optimized, false);
            }
        }

        /// <summary>
        /// Return memory to pool
        /// </summary>
        internal void ReturnMemory<T>(T[] memory) where T : struct
        {
            Interlocked.Increment(ref _totalDeallocations);
            
            var pool = GetOrCreatePool<T>();
            pool.Return(memory);
            
            _monitor.RecordDeallocation(typeof(T), memory.Length);
        }

        /// <summary>
        /// Return unmanaged memory
        /// </summary>
        internal void ReturnUnmanagedMemory(IntPtr memory)
        {
            Marshal.FreeHGlobal(memory);
            Interlocked.Increment(ref _totalDeallocations);
        }

        private void InitializePools()
        {
            // Pre-create common pools
            GetOrCreatePool<byte>();
            GetOrCreatePool<int>();
            GetOrCreatePool<long>();
            GetOrCreatePool<float>();
            GetOrCreatePool<double>();
            GetOrCreatePool<char>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IMemoryPool<T> GetOrCreatePool<T>() where T : struct
        {
            var type = typeof(T);
            
            if (_pools.TryGetValue(type, out var pool))
                return (IMemoryPool<T>)pool;
                
            var newPool = new SmartMemoryPool<T>(_config);
            _pools[type] = newPool;
            
            return newPool;
        }

        private IMemoryPool<T> GetOrCreateAlignedPool<T>(int alignment) where T : struct
        {
            var key = typeof(T);
            var alignedKey = new AlignedPoolKey(key, alignment);
            
            // For simplicity, return regular pool for now
            return GetOrCreatePool<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void VectorizedCopy<T>(ReadOnlySpan<T> source, Span<T> destination) where T : struct
        {
            var elementSize = Unsafe.SizeOf<T>();
            
            if (elementSize == 1) // bytes
            {
                var sourceBytes = MemoryMarshal.AsBytes(source);
                var destBytes = MemoryMarshal.AsBytes(destination);
                
                fixed (byte* srcPtr = sourceBytes)
                fixed (byte* dstPtr = destBytes)
                {
                    VectorizedCopyBytes(srcPtr, dstPtr, sourceBytes.Length);
                }
            }
            else
            {
                // Fallback for other types
                source.CopyTo(destination);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void VectorizedCopyBytes(byte* source, byte* destination, int length)
        {
            var vectorSize = Vector<byte>.Count;
            var vectorLength = length - (length % vectorSize);
            
            // Vectorized copy
            for (int i = 0; i < vectorLength; i += vectorSize)
            {
                var vector = new Vector<byte>(source + i);
                vector.CopyTo(new Span<byte>(destination + i, vectorSize));
            }
            
            // Copy remaining bytes
            for (int i = vectorLength; i < length; i++)
            {
                destination[i] = source[i];
            }
        }

        private void OptimizeMemoryPools(object state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var optimized = 0;
                
                foreach (var pool in _pools.Values)
                {
                    if (pool.TryOptimize(now))
                        optimized++;
                }
                
                _logger?.LogDebug("Optimized {Count} memory pools", optimized);
                
                // Monitor memory pressure
                _monitor.UpdateMetrics();
                
                if (_monitor.IsUnderPressure && _config.EnableAutomaticCleanup)
                {
                    PerformEmergencyCleanup();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during memory pool optimization");
            }
        }

        private void PerformEmergencyCleanup()
        {
            _logger?.LogWarning("Memory pressure detected, performing emergency cleanup");
            
            // Trim all pools aggressively
            foreach (var pool in _pools.Values)
            {
                pool.TrimExcess();
            }
            
            // Force garbage collection
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
        }

        private int CountActivePools()
        {
            var active = 0;
            foreach (var pool in _pools.Values)
            {
                if (pool.HasActiveItems)
                    active++;
            }
            return active;
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _optimizationTimer?.Dispose();
            
            foreach (var pool in _pools.Values)
            {
                pool.Dispose();
            }
            
            _pools.Clear();
            _monitor.Dispose();
            _disposed = true;
        }
    }

    // Supporting classes and interfaces
    public interface IMemoryPool : IDisposable
    {
        bool TryOptimize(DateTime now);
        void TrimExcess();
        bool HasActiveItems { get; }
    }

    public interface IMemoryPool<T> : IMemoryPool where T : struct
    {
        T[] Rent(int size);
        void Return(T[] memory);
    }

    public sealed class SmartMemoryPool<T> : IMemoryPool<T> where T : struct
    {
        private readonly ArrayPool<T> _pool;
        private readonly MemoryConfiguration _config;
        private DateTime _lastOptimization;
        private long _totalRents;
        private long _totalReturns;

        public bool HasActiveItems => _totalRents > _totalReturns;

        public SmartMemoryPool(MemoryConfiguration config)
        {
            _config = config;
            _pool = ArrayPool<T>.Create(config.MaxArrayLength, config.MaxArraysPerBucket);
            _lastOptimization = DateTime.UtcNow;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] Rent(int size)
        {
            Interlocked.Increment(ref _totalRents);
            return _pool.Rent(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(T[] memory)
        {
            Interlocked.Increment(ref _totalReturns);
            _pool.Return(memory, clearArray: _config.ClearArraysOnReturn);
        }

        public bool TryOptimize(DateTime now)
        {
            if (now - _lastOptimization < _config.OptimizationInterval)
                return false;
                
            _lastOptimization = now;
            return true;
        }

        public void TrimExcess()
        {
            // ArrayPool doesn't expose trim functionality
            // This would require custom implementation
        }

        public void Dispose()
        {
            // ArrayPool doesn't need explicit disposal
        }
    }

    public sealed class SmartMemoryBlock<T> : IDisposable where T : struct
    {
        private readonly SmartMemoryManager _manager;
        private T[] _managedMemory;
        private UnmanagedMemory<T> _unmanagedMemory;
        private bool _disposed;

        public Memory<T> Memory { get; }
        public int Size { get; }
        public bool IsUnmanaged => _unmanagedMemory != null;

        internal SmartMemoryBlock(T[] memory, SmartMemoryManager manager, int size)
        {
            _managedMemory = memory;
            _manager = manager;
            Size = size;
            Memory = new Memory<T>(memory, 0, size);
        }

        internal SmartMemoryBlock(UnmanagedMemory<T> memory, SmartMemoryManager manager, int size)
        {
            _unmanagedMemory = memory;
            _manager = manager;
            Size = size;
            Memory = memory.Memory;
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (_managedMemory != null)
            {
                _manager.ReturnMemory(_managedMemory);
                _managedMemory = null;
            }
            else if (_unmanagedMemory != null)
            {
                _unmanagedMemory.Dispose();
                _unmanagedMemory = null;
            }

            _disposed = true;
        }
    }

    public sealed unsafe class UnmanagedMemory<T> : IDisposable where T : struct
    {
        private readonly IntPtr _alignedPointer;
        private readonly IntPtr _originalPointer;
        private readonly int _size;

        public Memory<T> Memory { get; }

        internal UnmanagedMemory(IntPtr alignedPointer, int size, IntPtr originalPointer)
        {
            _alignedPointer = alignedPointer;
            _originalPointer = originalPointer;
            _size = size;
            
            var span = new Span<T>((void*)alignedPointer, size);
            Memory = new UnmanagedMemoryManager<T>(span).Memory;
        }

        public void Dispose()
        {
            if (_originalPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_originalPointer);
            }
        }
    }

    internal sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T> where T : struct
    {
        private readonly Span<T> _span;

        public UnmanagedMemoryManager(Span<T> span)
        {
            _span = span;
        }

        public override Span<T> GetSpan() => _span;

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if (elementIndex < 0 || elementIndex >= _span.Length)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));

            fixed (T* ptr = &_span[elementIndex])
            {
                return new MemoryHandle(ptr);
            }
        }

        public override void Unpin() { }

        protected override void Dispose(bool disposing) { }
    }

    public sealed class MemoryMappedRegion<T> : IDisposable where T : struct
    {
        private readonly string _name;
        private readonly long _size;
        private System.IO.MemoryMappedFiles.MemoryMappedFile _mmf;
        private System.IO.MemoryMappedFiles.MemoryMappedViewAccessor _accessor;

        public Memory<T> Memory { get; private set; }
        public long ElementCount { get; }

        internal MemoryMappedRegion(string name, long totalBytes, long elementCount)
        {
            _name = name;
            _size = totalBytes;
            ElementCount = elementCount;

            _mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateNew(name, totalBytes);
            _accessor = _mmf.CreateViewAccessor(0, totalBytes);

            // Create memory from mapped region
            unsafe
            {
                var ptr = (byte*)_accessor.SafeMemoryMappedViewHandle.DangerousGetHandle();
                var span = new Span<T>(ptr, (int)elementCount);
                Memory = new UnmanagedMemoryManager<T>(span).Memory;
            }
        }

        public void Dispose()
        {
            _accessor?.Dispose();
            _mmf?.Dispose();
        }
    }

    public class MemoryConfiguration
    {
        public int MaxArrayLength { get; set; } = 1024 * 1024; // 1MB
        public int MaxArraysPerBucket { get; set; } = 50;
        public bool ClearArraysOnReturn { get; set; } = true;
        public bool PinLargeArrays { get; set; } = true;
        public int LargeArrayThreshold { get; set; } = 85000; // LOH threshold
        public TimeSpan OptimizationInterval { get; set; } = TimeSpan.FromMinutes(5);
        public int SIMDThreshold { get; set; } = 1024; // bytes
        public bool EnableGCOptimization { get; set; } = true;
        public bool EnableAutomaticCleanup { get; set; } = true;
        public double MemoryPressureThreshold { get; set; } = 0.8; // 80% of available memory
    }

    public class MemoryStatistics
    {
        public long TotalAllocations { get; set; }
        public long TotalDeallocations { get; set; }
        public long PoolHits { get; set; }
        public long PoolMisses { get; set; }
        public double HitRatio { get; set; }
        public long GCMemory { get; set; }
        public long WorkingSet { get; set; }
        public int PoolCount { get; set; }
        public int ActivePools { get; set; }
    }

    public enum BulkOperationType
    {
        Copy,
        Fill,
        Clear,
        Reverse
    }

    internal class AlignedPoolKey
    {
        public Type Type { get; }
        public int Alignment { get; }

        public AlignedPoolKey(Type type, int alignment)
        {
            Type = type;
            Alignment = alignment;
        }

        public override bool Equals(object obj)
        {
            return obj is AlignedPoolKey other && 
                   Type == other.Type && 
                   Alignment == other.Alignment;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Alignment);
        }
    }

    internal class MemoryMonitor : IDisposable
    {
        private readonly MemoryConfiguration _config;
        private readonly PerformanceCounter _availableMemory;
        private long _totalMappedBytes;

        public bool IsUnderPressure { get; private set; }

        public MemoryMonitor(MemoryConfiguration config)
        {
            _config = config;
            
            try
            {
                _availableMemory = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch
            {
                // Performance counters may not be available
            }
        }

        public void RecordAllocation(Type type, int size, bool fromPool)
        {
            // Record allocation metrics
        }

        public void RecordDeallocation(Type type, int size)
        {
            // Record deallocation metrics
        }

        public void RecordMappedRegion(Type type, long bytes)
        {
            Interlocked.Add(ref _totalMappedBytes, bytes);
        }

        public void UpdateMetrics()
        {
            if (_availableMemory != null)
            {
                try
                {
                    var availableMB = _availableMemory.NextValue();
                    var totalMB = GC.GetTotalMemory(false) / (1024 * 1024);
                    
                    IsUnderPressure = (totalMB / (availableMB + totalMB)) > _config.MemoryPressureThreshold;
                }
                catch
                {
                    // Handle performance counter errors
                }
            }
        }

        public void Dispose()
        {
            _availableMemory?.Dispose();
        }
    }
}