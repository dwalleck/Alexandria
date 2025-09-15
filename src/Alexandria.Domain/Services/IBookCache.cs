using System;
using System.Threading;
using System.Threading.Tasks;
using Alexandria.Domain.Entities;

namespace Alexandria.Domain.Services;

/// <summary>
/// Interface for caching loaded books with high-performance patterns
/// </summary>
public interface IBookCache
{
    /// <summary>
    /// Try to get a book from cache
    /// Uses ValueTask for sync path optimization when cache hit
    /// </summary>
    ValueTask<Book?> TryGetAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set a book in cache
    /// </summary>
    ValueTask SetAsync(string filePath, Book book, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a book from cache
    /// </summary>
    ValueTask<bool> RemoveAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all cached books
    /// </summary>
    ValueTask ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache statistics
    /// </summary>
    ValueTask<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cache statistics for monitoring and optimization
/// </summary>
public record CacheStatistics
{
    public int TotalItems { get; init; }
    public long TotalSizeBytes { get; init; }
    public int HitCount { get; init; }
    public int MissCount { get; init; }
    public double HitRate => TotalRequests > 0 ? (double)HitCount / TotalRequests : 0;
    public int TotalRequests => HitCount + MissCount;
    public DateTime LastAccessed { get; init; }
    public TimeSpan AverageLoadTime { get; init; }
}