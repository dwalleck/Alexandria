using Alexandria.Application.Features.LoadBook;
using Alexandria.Domain.Services;
using Alexandria.Infrastructure.Caching;
using Alexandria.Infrastructure.Parsers;
using Alexandria.Infrastructure.Repositories;
using Alexandria.Infrastructure.Services;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;
using TUnit.Assertions;
using TUnit.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Infrastructure.Tests.Features.LoadBook;

/// <summary>
/// Integration tests for LoadBook feature with real EPUB files
/// </summary>
public class LoadBookIntegrationTests
{
    private readonly string _sampleEpubsPath;

    public LoadBookIntegrationTests()
    {
        // Get the directory where the test assembly is located
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var testProjectDir = Path.GetDirectoryName(assemblyLocation) ?? "";
        _sampleEpubsPath = Path.Combine(testProjectDir, "sample-epubs");

        // Ensure the sample-epubs directory exists
        if (!Directory.Exists(_sampleEpubsPath))
        {
            throw new DirectoryNotFoundException(
                $"Sample EPUBs directory not found at: {_sampleEpubsPath}. " +
                "Please ensure sample-epubs folder is copied to output directory.");
        }
    }

    private LoadBookHandler CreateHandler(IBookCache? cache = null)
    {
        var nullLoggerFactory = new NullLoggerFactory();
        var parserFactory = new EpubParserFactory(nullLoggerFactory);
        var adaptiveParser = new AdaptiveEpubParser(parserFactory, new NullLogger<AdaptiveEpubParser>());
        var epubLoader = new EpubLoader(adaptiveParser, new NullLogger<EpubLoader>());
        var contentAnalyzer = new AngleSharpContentAnalyzer();
        var validator = new LoadBookValidator();

        cache ??= new BookCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 }),
            Options.Create(new BookCacheOptions { EnablePersistentCache = false }),
            new NullLogger<BookCache>());

        return new LoadBookHandler(
            epubLoader,
            cache,
            contentAnalyzer,
            validator,
            new NullLogger<LoadBookHandler>());
    }

    [Test]
    public async Task LoadBook_WithMobyDick_LoadsSuccessfully()
    {
        // Arrange
        var handler = CreateHandler();
        var epubPath = Path.Combine(_sampleEpubsPath, "pg2701-images-3.epub");

        if (!File.Exists(epubPath))
        {
            // Skip test - EPUB file not found
            return;
        }

        var command = new LoadBookCommand(epubPath);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).Contains("Moby");
        await Assert.That(book.Authors).IsNotEmpty();
        await Assert.That(book.Chapters.Count).IsGreaterThan(0);

        // Verify content analysis was performed
        // Find first chapter with actual text content (skip cover pages, etc.)
        var chapterWithContent = book.Chapters.FirstOrDefault(c =>
            c.Metrics != null && c.Metrics.WordCount > 0);

        if (chapterWithContent == null)
        {
            // If no chapters have analyzed content, debug the first few chapters
            foreach (var chapter in book.Chapters.Take(3))
            {
                Console.WriteLine($"DEBUG: Chapter '{chapter.Title}' - Content length: {chapter.Content?.Length ?? 0}, WordCount: {chapter.Metrics?.WordCount ?? 0}");
            }
        }

        await Assert.That(chapterWithContent).IsNotNull();
        await Assert.That(chapterWithContent!.Metrics).IsNotNull();
        await Assert.That(chapterWithContent.Metrics!.WordCount).IsGreaterThan(0);
    }

    [Test]
    public async Task LoadBook_WithCompleteShakespeare_LoadsLargeFile()
    {
        // Arrange
        var handler = CreateHandler();
        var epubPath = Path.Combine(_sampleEpubsPath, "pg100-images-3.epub");

        if (!File.Exists(epubPath))
        {
            // Skip test - EPUB file not found
            return;
        }

        var command = new LoadBookCommand(epubPath);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsNotEmpty();
        await Assert.That(book.Chapters.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task LoadBook_WithOathbringer_HandlesComplexStructure()
    {
        // Arrange
        var handler = CreateHandler();
        var epubPath = Path.Combine(_sampleEpubsPath, "Oathbringer (The Stormlight Arc - Brandon Sanderson.epub");

        if (!File.Exists(epubPath))
        {
            // Skip test - Oathbringer EPUB not found
            return;
        }

        var command = new LoadBookCommand(epubPath);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).Contains("Oathbringer");
        await Assert.That(book.Authors.First().Name).Contains("Sanderson");
    }

    [Test]
    public async Task LoadBook_WithCaching_ReturnsCachedOnSecondLoad()
    {
        // Arrange
        var cache = new BookCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 }),
            Options.Create(new BookCacheOptions { EnablePersistentCache = false }),
            new NullLogger<BookCache>());

        var handler = CreateHandler(cache);
        var epubPath = Path.Combine(_sampleEpubsPath, "pg345-images-3.epub");

        if (!File.Exists(epubPath))
        {
            // Skip test - EPUB file not found
            return;
        }

        var command = new LoadBookCommand(epubPath);

        // Act - First load
        var startTime1 = DateTime.UtcNow;
        var result1 = await handler.Handle(command, CancellationToken.None);
        var loadTime1 = DateTime.UtcNow - startTime1;

        // Act - Second load (should be cached)
        var startTime2 = DateTime.UtcNow;
        var result2 = await handler.Handle(command, CancellationToken.None);
        var loadTime2 = DateTime.UtcNow - startTime2;

        // Assert
        await Assert.That(result1.IsT0).IsTrue();
        await Assert.That(result2.IsT0).IsTrue();
        await Assert.That(result1.AsT0).IsEqualTo(result2.AsT0);

        // Cache hit should be much faster
        await Assert.That(loadTime2).IsLessThan(loadTime1);
        await Assert.That(loadTime2.TotalMilliseconds).IsLessThan(50); // Cache hit target <10ms
    }

    [Test]
    public async Task LoadBook_WithInvalidPath_ReturnsError()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new LoadBookCommand("/nonexistent/file.epub");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        await Assert.That(result.AsT1.Type).IsEqualTo(LoadBookErrorType.InvalidFormat);
    }

    [Test]
    public async Task LoadBook_WithProgressReporting_ReportsAllStages()
    {
        // Arrange
        var progressReports = new List<LoadProgress>();
        var progress = new Progress<LoadProgress>(p => progressReports.Add(p));

        var nullLoggerFactory = new NullLoggerFactory();
        var parserFactory = new EpubParserFactory(nullLoggerFactory);
        var adaptiveParser = new AdaptiveEpubParser(parserFactory, new NullLogger<AdaptiveEpubParser>());
        var epubLoader = new EpubLoader(adaptiveParser, new NullLogger<EpubLoader>());
        var contentAnalyzer = new AngleSharpContentAnalyzer();
        var validator = new LoadBookValidator();
        var cache = new BookCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 }),
            Options.Create(new BookCacheOptions { EnablePersistentCache = false }),
            new NullLogger<BookCache>());

        var handler = new LoadBookHandler(
            epubLoader,
            cache,
            contentAnalyzer,
            validator,
            new NullLogger<LoadBookHandler>(),
            progress);

        var epubPath = Path.Combine(_sampleEpubsPath, "pg345-images-3.epub");

        if (!File.Exists(epubPath))
        {
            // Skip test - EPUB file not found
            return;
        }

        var command = new LoadBookCommand(epubPath);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(progressReports.Count).IsGreaterThan(4);
        await Assert.That(progressReports.Any(p => p.Message.Contains("cache"))).IsTrue();
        await Assert.That(progressReports.Any(p => p.Message.Contains("metadata"))).IsTrue();
        await Assert.That(progressReports.Any(p => p.Message.Contains("chapters"))).IsTrue();
        await Assert.That(progressReports.Any(p => p.Message.Contains("content"))).IsTrue();
        await Assert.That(progressReports.Last().Message).IsEqualTo("Complete");
        await Assert.That(progressReports.Last().Percentage).IsEqualTo(100);
    }

    [Test]
    public async Task LoadBook_MultipleFilesInParallel_LoadsAllSuccessfully()
    {
        // Arrange
        var handler = CreateHandler();
        var epubFiles = new[]
        {
            "pg345-images-3.epub",
            "pg2701-images-3.epub",
            "pg100-images-3.epub"
        };

        var existingFiles = epubFiles
            .Select(f => Path.Combine(_sampleEpubsPath, f))
            .Where(File.Exists)
            .ToArray();

        if (!existingFiles.Any())
        {
            // Skip test - No EPUB files found for parallel test
            return;
        }

        // Act
        var tasks = existingFiles.Select(async path =>
        {
            var command = new LoadBookCommand(path);
            return await handler.Handle(command, CancellationToken.None);
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        await Assert.That(results.Length).IsEqualTo(existingFiles.Length);
        foreach (var result in results)
        {
            await Assert.That(result.IsT0).IsTrue();
            await Assert.That(result.AsT0.Chapters.Count).IsGreaterThan(0);
        }
    }
}