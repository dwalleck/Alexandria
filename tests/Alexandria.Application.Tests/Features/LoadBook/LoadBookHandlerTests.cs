using Alexandria.Application.Features.LoadBook;
using Alexandria.Domain.Common;
using Alexandria.Domain.Entities;
using Alexandria.Domain.Errors;
using Alexandria.Domain.Interfaces;
using Alexandria.Domain.Services;
using Alexandria.Domain.ValueObjects;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using TUnit.Assertions;
using TUnit.Core;

namespace Alexandria.Application.Tests.Features.LoadBook;

/// <summary>
/// Unit tests for LoadBookHandler using TUnit
/// </summary>
public class LoadBookHandlerTests
{
    private readonly LoadBookHandler _handler;
    private readonly MockEpubLoader _mockEpubLoader;
    private readonly MockBookCache _mockCache;
    private readonly MockContentAnalyzer _mockContentAnalyzer;
    private readonly MockValidator _mockValidator;
    private readonly TestProgress _progress;

    public LoadBookHandlerTests()
    {
        _mockEpubLoader = new MockEpubLoader();
        _mockCache = new MockBookCache();
        _mockContentAnalyzer = new MockContentAnalyzer();
        _mockValidator = new MockValidator();
        _progress = new TestProgress();

        _handler = new LoadBookHandler(
            _mockEpubLoader,
            _mockCache,
            _mockContentAnalyzer,
            _mockValidator,
            NullLogger<LoadBookHandler>.Instance,
            _progress);
    }

    [Test]
    public async Task Handle_WithCachedBook_ReturnsCachedResult()
    {
        // Arrange
        var command = new LoadBookCommand("test.epub");
        var cachedBook = CreateTestBook();
        _mockCache.SetCachedBook("test.epub", cachedBook);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        await Assert.That(result.AsT0).IsEqualTo(cachedBook);
        await Assert.That(_mockCache.GetCallCount).IsEqualTo(1);
        await Assert.That(_mockEpubLoader.LoadCallCount).IsEqualTo(0); // Should not load from file
        await Assert.That(_progress.LastMessage).IsEqualTo("Complete");
    }

    [Test]
    public async Task Handle_WithValidFile_LoadsAndCachesBook()
    {
        // Arrange
        var command = new LoadBookCommand("valid.epub");
        var book = CreateTestBook();
        _mockEpubLoader.SetBookToReturn(book);
        _mockValidator.SetValidationResult(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        await Assert.That(result.AsT0.Title.Value).IsEqualTo("Test Book");
        await Assert.That(_mockCache.SetCallCount).IsEqualTo(1);
        await Assert.That(_mockContentAnalyzer.AnalyzeCallCount).IsEqualTo(2); // Two chapters
    }

    [Test]
    public async Task Handle_WithInvalidFile_ReturnsError()
    {
        // Arrange
        var command = new LoadBookCommand("invalid.epub");
        _mockValidator.SetValidationResult(false, "Invalid EPUB format");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        await Assert.That(result.AsT1.Type).IsEqualTo(LoadBookErrorType.InvalidFormat);
        await Assert.That(result.AsT1.Message).Contains("Invalid EPUB format");
    }

    [Test]
    public async Task Handle_WithEpubLoaderError_ReturnsParsingError()
    {
        // Arrange
        var command = new LoadBookCommand("error.epub");
        _mockValidator.SetValidationResult(true);
        _mockEpubLoader.SetErrorToReturn(new FileNotFoundError("Corrupted EPUB"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        await Assert.That(result.AsT1.Type).IsEqualTo(LoadBookErrorType.ParsingFailed);
    }

    [Test]
    public async Task Handle_ReportsProgressCorrectly()
    {
        // Arrange
        var command = new LoadBookCommand("progress.epub");
        var book = CreateTestBook();
        _mockEpubLoader.SetBookToReturn(book);
        _mockValidator.SetValidationResult(true);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(_progress.Reports.Count).IsGreaterThan(3);
        await Assert.That(_progress.Reports[0].Message).IsEqualTo("Checking cache...");
        await Assert.That(_progress.Reports.Last().Message).IsEqualTo("Complete");
    }

    [Test]
    public async Task Handle_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var command = new LoadBookCommand("cancel.epub");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _handler.Handle(command, cts.Token));
    }

    private static Book CreateTestBook()
    {
        var chapters = new List<Chapter>
        {
            new Chapter("ch1", "Chapter 1", "<p>Content 1</p>", 0),
            new Chapter("ch2", "Chapter 2", "<p>Content 2</p>", 1)
        };

        return new Book(
            new BookTitle("Test Book"),
            null, // alternateTitles
            new[] { new Author("Test Author") },
            chapters,
            new BookIdentifier[] { }, // identifiers
            Language.English,
            new BookMetadata());
    }

    // Mock implementations
    private class MockEpubLoader : IEpubLoader
    {
        private OneOf<Book, ParsingError> _result;
        public int LoadCallCount { get; private set; }

        public void SetBookToReturn(Book book) => _result = book;
        public void SetErrorToReturn(ParsingError error) => _result = error;

        public Task<OneOf<Book, ParsingError>> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            LoadCallCount++;
            return Task.FromResult(_result);
        }

        public Task<OneOf<Book, ParsingError>> LoadFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);

        public Task<OneOf<Book, ParsingError>> LoadFromBytesAsync(byte[] bytes, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);

        public Task<OneOf<Success, ValidationError>> ValidateEpubAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(OneOf<Success, ValidationError>.FromT0(new Success()));
    }

    private class MockBookCache : IBookCache
    {
        private readonly Dictionary<string, Book?> _cache = new();
        public int GetCallCount { get; private set; }
        public int SetCallCount { get; private set; }

        public void SetCachedBook(string path, Book book) => _cache[path] = book;

        public ValueTask<Book?> TryGetAsync(string filePath, CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            return new ValueTask<Book?>(_cache.TryGetValue(filePath, out var book) ? book : null);
        }

        public ValueTask SetAsync(string filePath, Book book, CancellationToken cancellationToken = default)
        {
            SetCallCount++;
            _cache[filePath] = book;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> RemoveAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return new ValueTask<bool>(_cache.Remove(filePath));
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            _cache.Clear();
            return ValueTask.CompletedTask;
        }

        public ValueTask<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            return new ValueTask<CacheStatistics>(new CacheStatistics { TotalItems = _cache.Count });
        }
    }

    private class MockContentAnalyzer : IContentAnalyzer
    {
        public int AnalyzeCallCount { get; private set; }

        public ValueTask<ContentMetrics> AnalyzeContentAsync(string htmlContent, CancellationToken cancellationToken = default)
        {
            AnalyzeCallCount++;
            return new ValueTask<ContentMetrics>(new ContentMetrics
            {
                WordCount = 100,
                CharacterCount = 500,
                SentenceCount = 10,
                EstimatedReadingTime = TimeSpan.FromMinutes(1)
            });
        }

        public string ExtractPlainText(ReadOnlySpan<char> htmlContent, char[]? buffer = null) => "Text";
        public int CountWords(ReadOnlySpan<char> text) => 100;
        public int CountSentences(ReadOnlySpan<char> text) => 10;
        public TimeSpan EstimateReadingTime(ReadOnlySpan<char> text, int wordsPerMinute = 250) => TimeSpan.FromMinutes(1);
        public string[] ExtractSentences(ReadOnlySpan<char> text, int maxSentences) => new[] { "Sentence 1." };
        public string GeneratePreview(ReadOnlySpan<char> text, int maxLength) => "Preview...";
        public string ExtractSnippet(ReadOnlySpan<char> text, string searchTerm, int contextLength = 100) => "...snippet...";
        public string HighlightTerms(string text, string[] searchTerms, string highlightStart = "**", string highlightEnd = "**") => text;
        public int CountParagraphs(ReadOnlySpan<char> htmlContent) => 5;
        public double CalculateReadabilityScore(ReadOnlySpan<char> text) => 60.0;
    }

    private class MockValidator : IValidator<LoadBookCommand>
    {
        private ValidationResult _result = new();

        public void SetValidationResult(bool isValid, string? error = null)
        {
            if (isValid)
            {
                _result = new ValidationResult();
            }
            else
            {
                _result = new ValidationResult(new[] { new ValidationFailure("FilePath", error ?? "Invalid") });
            }
        }

        public ValidationResult Validate(LoadBookCommand instance) => _result;
        public Task<ValidationResult> ValidateAsync(LoadBookCommand instance, CancellationToken cancellation = default)
            => Task.FromResult(_result);
        public ValidationResult Validate(IValidationContext context) => _result;
        public Task<ValidationResult> ValidateAsync(IValidationContext context, CancellationToken cancellation = default)
            => Task.FromResult(_result);
        public IValidatorDescriptor CreateDescriptor() => throw new NotImplementedException();
        public bool CanValidateInstancesOfType(Type type) => type == typeof(LoadBookCommand);
    }

    private class TestProgress : IProgress<LoadProgress>
    {
        public List<LoadProgress> Reports { get; } = new();
        public string? LastMessage => Reports.LastOrDefault()?.Message;

        public void Report(LoadProgress value)
        {
            Reports.Add(value);
        }
    }
}