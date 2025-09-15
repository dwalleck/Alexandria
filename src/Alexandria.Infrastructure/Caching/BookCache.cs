using System.Collections.Concurrent;
using System.Diagnostics;
using Alexandria.Domain.Entities;
using Alexandria.Domain.Services;
using LiteDB;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Infrastructure.Caching;

/// <summary>
/// High-performance two-tier book cache using Memory and LiteDB
/// </summary>
public sealed class BookCache : IBookCache, IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILiteDatabase? _persistentCache;
    private readonly ILogger<BookCache> _logger;
    private readonly BookCacheOptions _options;
    private readonly ConcurrentDictionary<string, CacheEntry> _cacheMetadata = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    // Statistics tracking
    private int _hitCount;
    private int _missCount;
    private readonly Stopwatch _stopwatch = new();

    public BookCache(
        IMemoryCache memoryCache,
        IOptions<BookCacheOptions> options,
        ILogger<BookCache> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _options = options?.Value ?? new BookCacheOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize persistent cache if enabled
        if (_options.EnablePersistentCache && !string.IsNullOrEmpty(_options.PersistentCachePath))
        {
            try
            {
                var directory = Path.GetDirectoryName(_options.PersistentCachePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _persistentCache = new LiteDatabase(_options.PersistentCachePath);
                _logger.LogInformation("Persistent cache initialized at {Path}", _options.PersistentCachePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize persistent cache");
            }
        }

        _stopwatch.Start();
    }

    public ValueTask<Book?> TryGetAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new ValueTask<Book?>((Book?)null);
        }

        var cacheKey = GetCacheKey(filePath);

        // Try memory cache first (synchronous path - no allocation when cache hit)
        if (_memoryCache.TryGetValue<Book>(cacheKey, out var book))
        {
            Interlocked.Increment(ref _hitCount);
            UpdateCacheMetadata(filePath, true);
            _logger.LogDebug("Memory cache hit for {FilePath}", filePath);
            return new ValueTask<Book?>(book);
        }

        // Fall back to persistent cache if available
        if (_persistentCache != null)
        {
            return LoadFromPersistentCacheAsync(filePath, cacheKey, cancellationToken);
        }

        Interlocked.Increment(ref _missCount);
        UpdateCacheMetadata(filePath, false);
        return new ValueTask<Book?>((Book?)null);
    }

    private async ValueTask<Book?> LoadFromPersistentCacheAsync(
        string filePath,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await _cacheLock.WaitAsync(cancellationToken);

            var collection = _persistentCache!.GetCollection<CachedBook>("books");
            var cachedBook = collection.FindById(cacheKey);

            if (cachedBook != null && !IsExpired(cachedBook))
            {
                // Deserialize and add to memory cache
                var book = cachedBook.ToBook();

                var memoryCacheOptions = new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(_options.MemoryCacheSlidingExpirationMinutes),
                    Size = 1,
                    PostEvictionCallbacks =
                    {
                        new PostEvictionCallbackRegistration
                        {
                            EvictionCallback = OnMemoryCacheEviction,
                            State = this
                        }
                    }
                };

                _memoryCache.Set(cacheKey, book, memoryCacheOptions);

                Interlocked.Increment(ref _hitCount);
                UpdateCacheMetadata(filePath, true);
                _logger.LogDebug("Persistent cache hit for {FilePath}", filePath);

                return book;
            }

            Interlocked.Increment(ref _missCount);
            UpdateCacheMetadata(filePath, false);
            return null;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async ValueTask SetAsync(string filePath, Book book, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || book == null)
        {
            return;
        }

        var cacheKey = GetCacheKey(filePath);

        // Add to memory cache
        var memoryCacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(_options.MemoryCacheSlidingExpirationMinutes),
            Size = 1,
            PostEvictionCallbacks =
            {
                new PostEvictionCallbackRegistration
                {
                    EvictionCallback = OnMemoryCacheEviction,
                    State = this
                }
            }
        };

        _memoryCache.Set(cacheKey, book, memoryCacheOptions);

        // Add to persistent cache if available
        if (_persistentCache != null)
        {
            await SaveToPersistentCacheAsync(cacheKey, book, cancellationToken);
        }

        UpdateCacheMetadata(filePath, true);
        _logger.LogDebug("Cached book {Title} at {FilePath}", book.Title.Value, filePath);
    }

    private async ValueTask SaveToPersistentCacheAsync(
        string cacheKey,
        Book book,
        CancellationToken cancellationToken)
    {
        try
        {
            await _cacheLock.WaitAsync(cancellationToken);

            var collection = _persistentCache!.GetCollection<CachedBook>("books");
            var cachedBook = CachedBook.FromBook(cacheKey, book);
            collection.Upsert(cachedBook);

            _logger.LogDebug("Saved book to persistent cache: {Key}", cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save book to persistent cache");
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async ValueTask<bool> RemoveAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var cacheKey = GetCacheKey(filePath);

        // Remove from memory cache
        _memoryCache.Remove(cacheKey);

        // Remove from persistent cache if available
        if (_persistentCache != null)
        {
            try
            {
                await _cacheLock.WaitAsync(cancellationToken);
                var collection = _persistentCache.GetCollection<CachedBook>("books");
                return collection.Delete(cacheKey);
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        _cacheMetadata.TryRemove(filePath, out _);
        return true;
    }

    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        // Clear memory cache (no direct clear method, so we track keys)
        foreach (var entry in _cacheMetadata)
        {
            _memoryCache.Remove(GetCacheKey(entry.Key));
        }

        // Clear persistent cache if available
        if (_persistentCache != null)
        {
            try
            {
                await _cacheLock.WaitAsync(cancellationToken);
                var collection = _persistentCache.GetCollection<CachedBook>("books");
                collection.DeleteAll();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        _cacheMetadata.Clear();
        Interlocked.Exchange(ref _hitCount, 0);
        Interlocked.Exchange(ref _missCount, 0);
    }

    public ValueTask<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new CacheStatistics
        {
            TotalItems = _cacheMetadata.Count,
            HitCount = _hitCount,
            MissCount = _missCount,
            LastAccessed = _cacheMetadata.Values
                .Where(e => e.LastAccessed.HasValue)
                .Select(e => e.LastAccessed!.Value)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max(),
            TotalSizeBytes = _cacheMetadata.Values.Sum(e => e.SizeBytes),
            AverageLoadTime = TimeSpan.FromMilliseconds(
                _cacheMetadata.Values
                    .Where(e => e.LoadTimeMs > 0)
                    .Select(e => e.LoadTimeMs)
                    .DefaultIfEmpty(0)
                    .Average())
        };

        return new ValueTask<CacheStatistics>(stats);
    }

    private static string GetCacheKey(string filePath)
    {
        // Normalize path for consistent caching
        return $"book:{Path.GetFullPath(filePath).ToLowerInvariant()}";
    }

    private void UpdateCacheMetadata(string filePath, bool isHit)
    {
        _cacheMetadata.AddOrUpdate(filePath,
            new CacheEntry
            {
                FilePath = filePath,
                LastAccessed = DateTime.UtcNow,
                HitCount = isHit ? 1 : 0
            },
            (_, existing) =>
            {
                existing.LastAccessed = DateTime.UtcNow;
                if (isHit) existing.HitCount++;
                return existing;
            });
    }

    private bool IsExpired(CachedBook cachedBook)
    {
        var age = DateTime.UtcNow - cachedBook.CachedAt;
        return age > TimeSpan.FromMinutes(_options.PersistentCacheExpirationMinutes);
    }

    private static void OnMemoryCacheEviction(object key, object? value, EvictionReason reason, object? state)
    {
        if (state is BookCache cache && value is Book book)
        {
            cache._logger.LogDebug("Book evicted from memory cache: {Key}, Reason: {Reason}",
                key, reason);
        }
    }

    public void Dispose()
    {
        _persistentCache?.Dispose();
        _cacheLock.Dispose();
        _stopwatch.Stop();
    }

    private class CacheEntry
    {
        public string FilePath { get; init; } = string.Empty;
        public DateTime? LastAccessed { get; set; }
        public int HitCount { get; set; }
        public long SizeBytes { get; set; }
        public double LoadTimeMs { get; set; }
    }

    /// <summary>
    /// LiteDB-compatible cached book model
    /// </summary>
    private class CachedBook
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<string> Authors { get; set; } = new();
        public string SerializedContent { get; set; } = string.Empty;
        public DateTime CachedAt { get; set; }

        public static CachedBook FromBook(string id, Book book)
        {
            return new CachedBook
            {
                Id = id,
                Title = book.Title.Value,
                Authors = book.Authors.Select(a => a.Name).ToList(),
                SerializedContent = System.Text.Json.JsonSerializer.Serialize(book),
                CachedAt = DateTime.UtcNow
            };
        }

        public Book ToBook()
        {
            return System.Text.Json.JsonSerializer.Deserialize<Book>(SerializedContent)!;
        }
    }
}

/// <summary>
/// Configuration options for BookCache
/// </summary>
public class BookCacheOptions
{
    public bool EnablePersistentCache { get; set; } = true;
    public string PersistentCachePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Alexandria",
        "cache.db");
    public int MemoryCacheSlidingExpirationMinutes { get; set; } = 30;
    public int PersistentCacheExpirationMinutes { get; set; } = 1440; // 24 hours
    public int MaxMemoryCacheSize { get; set; } = 100; // Maximum number of books in memory
}