using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Caching.Memory;

namespace Alexandria.Infrastructure.IO;

/// <summary>
/// Efficient resource extractor for EPUB files with LRU caching.
/// </summary>
public sealed class EpubResourceExtractor : IDisposable
{
    private readonly string _epubPath;
    private readonly IMemoryCache _cache;
    private readonly ArrayPool<byte> _bytePool;
    private readonly SemaphoreSlim _extractionSemaphore;
    private readonly int _maxConcurrentExtractions;
    private bool _disposed;

    /// <summary>
    /// Configuration for the resource extractor.
    /// </summary>
    public sealed class Configuration
    {
        /// <summary>
        /// Maximum cache size in bytes. Default: 50MB.
        /// </summary>
        public long MaxCacheSizeBytes { get; set; } = 50 * 1024 * 1024;

        /// <summary>
        /// Cache entry expiration time. Default: 5 minutes.
        /// </summary>
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Maximum concurrent resource extractions. Default: 4.
        /// </summary>
        public int MaxConcurrentExtractions { get; set; } = 4;

        /// <summary>
        /// Buffer size for extraction operations. Default: 8KB.
        /// </summary>
        public int BufferSize { get; set; } = 8192;
    }

    private readonly Configuration _config;
    private long _currentCacheSize;

    public EpubResourceExtractor(string epubPath, Configuration? config = null)
    {
        _epubPath = epubPath ?? throw new ArgumentNullException(nameof(epubPath));
        _config = config ?? new Configuration();
        
        if (!File.Exists(_epubPath))
            throw new FileNotFoundException($"EPUB file not found: {_epubPath}");

        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = _config.MaxCacheSizeBytes,
            CompactionPercentage = 0.25
        });
        
        _bytePool = ArrayPool<byte>.Shared;
        _maxConcurrentExtractions = _config.MaxConcurrentExtractions;
        _extractionSemaphore = new SemaphoreSlim(_maxConcurrentExtractions, _maxConcurrentExtractions);
    }

    /// <summary>
    /// Extracts a resource from the EPUB file, using cache when possible.
    /// </summary>
    /// <param name="resourcePath">Path to the resource within the EPUB</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The resource data, or null if not found</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<ReadOnlyMemory<byte>?> ExtractAsync(
        string resourcePath,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EpubResourceExtractor));

        // Try to get from cache first
        if (_cache.TryGetValue<byte[]>(resourcePath, out var cached))
        {
            return new ValueTask<ReadOnlyMemory<byte>?>(cached.AsMemory());
        }

        // Not in cache, extract from file
        return ExtractFromFileAsync(resourcePath, cancellationToken);
    }

    /// <summary>
    /// Extracts a resource with partial content support (byte ranges).
    /// </summary>
    public async ValueTask<ReadOnlyMemory<byte>?> ExtractPartialAsync(
        string resourcePath,
        long offset,
        int length,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EpubResourceExtractor));

        var cacheKey = $"{resourcePath}:{offset}:{length}";
        
        if (_cache.TryGetValue<byte[]>(cacheKey, out var cached))
        {
            return cached.AsMemory();
        }

        await _extractionSemaphore.WaitAsync(cancellationToken);
        try
        {
            using var fileStream = new FileStream(
                _epubPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1,
                useAsync: true);
            
            using var zipStream = new ZipInputStream(fileStream);
            
            ZipEntry? entry;
            while ((entry = zipStream.GetNextEntry()) != null)
            {
                if (entry.Name.Equals(resourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    // Skip to the requested offset
                    var skipBuffer = _bytePool.Rent(_config.BufferSize);
                    try
                    {
                        long skipped = 0;
                        while (skipped < offset)
                        {
                            var toSkip = (int)Math.Min(skipBuffer.Length, offset - skipped);
                            var read = await zipStream.ReadAsync(
                                skipBuffer.AsMemory(0, toSkip),
                                cancellationToken);
                            
                            if (read == 0)
                                return null;
                            
                            skipped += read;
                        }
                    }
                    finally
                    {
                        _bytePool.Return(skipBuffer, clearArray: true);
                    }

                    // Read the requested length
                    var buffer = new byte[length];
                    var totalRead = 0;
                    
                    while (totalRead < length)
                    {
                        var read = await zipStream.ReadAsync(
                            buffer.AsMemory(totalRead, length - totalRead),
                            cancellationToken);
                        
                        if (read == 0)
                            break;
                        
                        totalRead += read;
                    }

                    if (totalRead > 0)
                    {
                        var result = new byte[totalRead];
                        Buffer.BlockCopy(buffer, 0, result, 0, totalRead);
                        
                        // Cache the partial content
                        CacheResource(cacheKey, result);
                        
                        return result.AsMemory();
                    }
                    
                    return null;
                }
            }
            
            return null;
        }
        finally
        {
            _extractionSemaphore.Release();
        }
    }

    /// <summary>
    /// Extracts multiple resources in parallel for efficiency.
    /// </summary>
    public async Task<Dictionary<string, ReadOnlyMemory<byte>>> ExtractBatchAsync(
        IEnumerable<string> resourcePaths,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EpubResourceExtractor));

        var results = new ConcurrentDictionary<string, ReadOnlyMemory<byte>>();
        var tasks = new List<Task>();

        foreach (var path in resourcePaths)
        {
            tasks.Add(Task.Run(async () =>
            {
                var data = await ExtractAsync(path, cancellationToken);
                if (data.HasValue)
                {
                    results.TryAdd(path, data.Value);
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
        
        return new Dictionary<string, ReadOnlyMemory<byte>>(results);
    }

    /// <summary>
    /// Pre-loads commonly accessed resources into cache.
    /// </summary>
    public async Task PreloadAsync(
        IEnumerable<string> resourcePaths,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EpubResourceExtractor));

        foreach (var path in resourcePaths)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            // This will load into cache if not already present
            await ExtractAsync(path, cancellationToken);
        }
    }

    /// <summary>
    /// Gets information about a resource without extracting it.
    /// </summary>
    public async Task<ResourceInfo?> GetResourceInfoAsync(
        string resourcePath,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EpubResourceExtractor));

        await _extractionSemaphore.WaitAsync(cancellationToken);
        try
        {
            using var fileStream = new FileStream(
                _epubPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1,
                useAsync: true);
            
            using var zipStream = new ZipInputStream(fileStream);
            
            ZipEntry? entry;
            while ((entry = zipStream.GetNextEntry()) != null)
            {
                if (entry.Name.Equals(resourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    return new ResourceInfo
                    {
                        Path = entry.Name,
                        CompressedSize = entry.CompressedSize,
                        UncompressedSize = entry.Size,
                        LastModified = entry.DateTime,
                        IsCompressed = entry.CompressionMethod != CompressionMethod.Stored
                    };
                }
            }
            
            return null;
        }
        finally
        {
            _extractionSemaphore.Release();
        }
    }

    /// <summary>
    /// Clears the resource cache.
    /// </summary>
    public void ClearCache()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EpubResourceExtractor));

        _cache.Dispose();
        Interlocked.Exchange(ref _currentCacheSize, 0);
    }

    /// <summary>
    /// Gets current cache statistics.
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EpubResourceExtractor));

        return new CacheStatistics
        {
            CurrentSizeBytes = Interlocked.Read(ref _currentCacheSize),
            MaxSizeBytes = _config.MaxCacheSizeBytes,
            UtilizationPercentage = (double)_currentCacheSize / _config.MaxCacheSizeBytes * 100
        };
    }

    private async ValueTask<ReadOnlyMemory<byte>?> ExtractFromFileAsync(
        string resourcePath,
        CancellationToken cancellationToken)
    {
        await _extractionSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check cache after acquiring semaphore
            if (_cache.TryGetValue<byte[]>(resourcePath, out var cached))
            {
                return cached.AsMemory();
            }

            using var fileStream = new FileStream(
                _epubPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: _config.BufferSize,
                useAsync: true);
            
            using var zipStream = new ZipInputStream(fileStream);
            
            ZipEntry? entry;
            while ((entry = zipStream.GetNextEntry()) != null)
            {
                if (entry.Name.Equals(resourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    var buffer = new byte[entry.Size];
                    var totalRead = 0;
                    
                    while (totalRead < buffer.Length)
                    {
                        var read = await zipStream.ReadAsync(
                            buffer.AsMemory(totalRead, buffer.Length - totalRead),
                            cancellationToken);
                        
                        if (read == 0)
                            break;
                        
                        totalRead += read;
                    }

                    if (totalRead > 0)
                    {
                        var result = new byte[totalRead];
                        Buffer.BlockCopy(buffer, 0, result, 0, totalRead);
                        
                        // Add to cache
                        CacheResource(resourcePath, result);
                        
                        return result.AsMemory();
                    }
                    
                    return null;
                }
            }
            
            return null;
        }
        finally
        {
            _extractionSemaphore.Release();
        }
    }

    private void CacheResource(string key, byte[] data)
    {
        var entryOptions = new MemoryCacheEntryOptions()
            .SetSize(data.Length)
            .SetSlidingExpiration(_config.CacheExpiration)
            .RegisterPostEvictionCallback((evictedKey, evictedValue, reason, state) =>
            {
                if (evictedValue is byte[] evictedData)
                {
                    Interlocked.Add(ref _currentCacheSize, -evictedData.Length);
                }
            });

        if (_cache.TryGetValue(key, out _))
            return; // Already cached

        // Check if adding this would exceed cache size
        var currentSize = Interlocked.Read(ref _currentCacheSize);

        // If this item is larger than max cache size, don't cache it
        if (data.Length > _config.MaxCacheSizeBytes)
            return;

        // If adding would exceed limit, evict based on priority
        if (currentSize + data.Length > _config.MaxCacheSizeBytes)
        {
            // Calculate how much space we need
            var spaceNeeded = (currentSize + data.Length) - _config.MaxCacheSizeBytes;

            // Try to make room by setting a lower size limit
            // This will trigger the MemoryCache to evict LRU items
            var targetSize = _config.MaxCacheSizeBytes - data.Length;

            // Set priority for this item based on size
            // Smaller items get higher priority (more likely to stay cached)
            var priority = data.Length < 10240 ? CacheItemPriority.High :
                          data.Length < 102400 ? CacheItemPriority.Normal :
                          CacheItemPriority.Low;

            entryOptions.SetPriority(priority);
        }

        // Update size tracking and cache the item
        Interlocked.Add(ref _currentCacheSize, data.Length);
        _cache.Set(key, data, entryOptions);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cache?.Dispose();
        _extractionSemaphore?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Information about a resource in the EPUB file.
/// </summary>
public sealed class ResourceInfo
{
    public string Path { get; init; } = string.Empty;
    public long CompressedSize { get; init; }
    public long UncompressedSize { get; init; }
    public DateTime LastModified { get; init; }
    public bool IsCompressed { get; init; }
}

/// <summary>
/// Cache statistics for monitoring.
/// </summary>
public sealed class CacheStatistics
{
    public long CurrentSizeBytes { get; init; }
    public long MaxSizeBytes { get; init; }
    public double UtilizationPercentage { get; init; }
}