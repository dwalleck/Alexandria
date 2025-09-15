using Alexandria.Application.Features.LoadBook;
using Alexandria.Domain.Entities;
using Alexandria.Domain.Services;
using Alexandria.Domain.ValueObjects;
using Alexandria.Infrastructure.Caching;
using Alexandria.Infrastructure.Parsers;
using Alexandria.Infrastructure.Repositories;
using Alexandria.Infrastructure.Services;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Alexandria.Benchmarks.Benchmarks;

/// <summary>
/// Performance benchmarks for LoadBook caching behavior
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[Config(typeof(Config))]
public class LoadBookCachingBenchmarks
{
    private LoadBookHandler _handler = null!;
    private LoadBookHandler _handlerWithCache = null!;
    private LoadBookHandler _handlerWithTwoTierCache = null!;
    private LoadBookCommand _command = null!;
    private string _epubPath = null!;
    private IBookCache _memoryOnlyCache = null!;
    private IBookCache _twoTierCache = null!;

    private class Config : ManualConfig
    {
        public Config()
        {
            AddLogger(ConsoleLogger.Default);
            AddColumn(StatisticColumn.Mean);
            AddColumn(StatisticColumn.Median);
            AddColumn(StatisticColumn.Min);
            AddColumn(StatisticColumn.Max);
            AddColumn(StatisticColumn.P95);
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        // Find the smallest EPUB file for consistent benchmarking
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var testProjectDir = Path.GetDirectoryName(assemblyLocation) ?? "";

        // Look for sample EPUBs in various possible locations
        var possiblePaths = new[]
        {
            Path.Combine(testProjectDir, "sample-epubs"),
            Path.Combine(testProjectDir, "..", "..", "..", "Alexandria.Infrastructure.Tests", "sample-epubs"),
            Path.Combine(testProjectDir, "..", "..", "..", "..", "..", "sample-epubs")
        };

        string? sampleEpubsPath = null;
        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                sampleEpubsPath = path;
                break;
            }
        }

        if (sampleEpubsPath == null)
        {
            throw new DirectoryNotFoundException("Sample EPUBs directory not found for benchmarking");
        }

        _epubPath = Path.Combine(sampleEpubsPath, "pg345-images-3.epub");
        if (!File.Exists(_epubPath))
        {
            // Fallback to any available EPUB
            _epubPath = Directory.GetFiles(sampleEpubsPath, "*.epub").FirstOrDefault()
                ?? throw new FileNotFoundException("No EPUB files found for benchmarking");
        }

        _command = new LoadBookCommand(_epubPath);

        var nullLoggerFactory = new NullLoggerFactory();
        var parserFactory = new EpubParserFactory(nullLoggerFactory);
        var adaptiveParser = new AdaptiveEpubParser(parserFactory, new NullLogger<AdaptiveEpubParser>());
        var epubLoader = new EpubLoader(adaptiveParser, new NullLogger<EpubLoader>());
        var contentAnalyzer = new AngleSharpContentAnalyzer();
        var validator = new LoadBookValidator();

        // Handler without cache
        var noCache = new NoOpCache();
        _handler = new LoadBookHandler(
            epubLoader,
            noCache,
            contentAnalyzer,
            validator,
            new NullLogger<LoadBookHandler>());

        // Handler with memory-only cache
        _memoryOnlyCache = new BookCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }),
            Options.Create(new BookCacheOptions { EnablePersistentCache = false }),
            new NullLogger<BookCache>());

        _handlerWithCache = new LoadBookHandler(
            epubLoader,
            _memoryOnlyCache,
            contentAnalyzer,
            validator,
            new NullLogger<LoadBookHandler>());

        // Handler with two-tier cache
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"benchmark-cache-{Guid.NewGuid()}.db");
        _twoTierCache = new BookCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }),
            Options.Create(new BookCacheOptions
            {
                EnablePersistentCache = true,
                PersistentCachePath = tempDbPath
            }),
            new NullLogger<BookCache>());

        _handlerWithTwoTierCache = new LoadBookHandler(
            epubLoader,
            _twoTierCache,
            contentAnalyzer,
            validator,
            new NullLogger<LoadBookHandler>());

        // Pre-warm the caches
        _handlerWithCache.Handle(_command, CancellationToken.None).GetAwaiter().GetResult();
        _handlerWithTwoTierCache.Handle(_command, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public async Task<LoadBookResult> LoadBook_NoCache()
    {
        return await _handler.Handle(_command, CancellationToken.None);
    }

    [Benchmark]
    public async Task<LoadBookResult> LoadBook_MemoryCacheHit()
    {
        return await _handlerWithCache.Handle(_command, CancellationToken.None);
    }

    [Benchmark]
    public async Task<LoadBookResult> LoadBook_TwoTierCacheHit()
    {
        return await _handlerWithTwoTierCache.Handle(_command, CancellationToken.None);
    }

    [Benchmark]
    public async ValueTask<Book?> DirectCacheAccess_MemoryHit()
    {
        return await _memoryOnlyCache.TryGetAsync(_epubPath);
    }

    [Benchmark]
    public async ValueTask<Book?> DirectCacheAccess_TwoTierHit()
    {
        return await _twoTierCache.TryGetAsync(_epubPath);
    }

    private class NoOpCache : IBookCache
    {
        public ValueTask<Book?> TryGetAsync(string filePath, CancellationToken cancellationToken = default)
            => new((Book?)null);

        public ValueTask SetAsync(string filePath, Book book, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<bool> RemoveAsync(string filePath, CancellationToken cancellationToken = default)
            => new(false);

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
            => new(new CacheStatistics());
    }
}

/// <summary>
/// Benchmarks for cache operations at different scales
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90, warmupCount: 3, iterationCount: 10)]
public class CacheScalabilityBenchmarks
{
    private IBookCache _cache = null!;
    private readonly List<string> _filePaths = new();
    private readonly List<Book> _books = new();

    [Params(10, 100, 1000)]
    public int CacheSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var nullLoggerFactory = new NullLoggerFactory();
        _cache = new BookCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = CacheSize }),
            Options.Create(new BookCacheOptions { EnablePersistentCache = false }),
            new NullLogger<BookCache>());

        // Generate test data
        for (int i = 0; i < CacheSize; i++)
        {
            _filePaths.Add($"book_{i}.epub");
            _books.Add(CreateTestBook($"Book {i}"));
        }

        // Pre-populate cache
        for (int i = 0; i < CacheSize; i++)
        {
            _cache.SetAsync(_filePaths[i], _books[i]).GetAwaiter().GetResult();
        }
    }

    [Benchmark]
    public async ValueTask<Book?> RandomCacheAccess()
    {
        var index = Random.Shared.Next(0, CacheSize);
        return await _cache.TryGetAsync(_filePaths[index]);
    }

    [Benchmark]
    public async ValueTask CacheWrite()
    {
        var index = Random.Shared.Next(0, CacheSize);
        await _cache.SetAsync($"new_{index}.epub", _books[index]);
    }

    [Benchmark]
    public async ValueTask<CacheStatistics> GetStatistics()
    {
        return await _cache.GetStatisticsAsync();
    }

    private static Book CreateTestBook(string title)
    {
        var chapters = new List<Chapter>
        {
            new Chapter("ch1", "Chapter 1", "<p>Content</p>", 0)
        };

        return new Book(
            new BookTitle(title),
            null, // alternateTitles
            new[] { new Author("Test Author") },
            chapters,
            new BookIdentifier[] { }, // identifiers
            Language.English,
            new BookMetadata());
    }
}