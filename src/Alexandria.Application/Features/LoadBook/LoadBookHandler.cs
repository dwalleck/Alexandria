using Alexandria.Application.Services;
using Alexandria.Domain.Entities;
using Alexandria.Domain.Errors;
using Alexandria.Domain.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Alexandria.Application.Features.LoadBook;

/// <summary>
/// Handler for loading books using MediatR and OneOf for result handling
/// </summary>
public sealed class LoadBookHandler : IRequestHandler<LoadBookCommand, LoadBookResult>
{
    private readonly IEpubLoader _epubLoader;
    private readonly IBookCache _cache;
    private readonly IContentAnalyzer _contentAnalyzer;
    private readonly IValidator<LoadBookCommand> _validator;
    private readonly ILogger<LoadBookHandler> _logger;
    private readonly IProgress<LoadProgress>? _progress;

    public LoadBookHandler(
        IEpubLoader epubLoader,
        IBookCache cache,
        IContentAnalyzer contentAnalyzer,
        IValidator<LoadBookCommand> validator,
        ILogger<LoadBookHandler> logger,
        IProgress<LoadProgress>? progress = null)
    {
        _epubLoader = epubLoader ?? throw new ArgumentNullException(nameof(epubLoader));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _contentAnalyzer = contentAnalyzer ?? throw new ArgumentNullException(nameof(contentAnalyzer));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progress = progress;
    }

    public async Task<LoadBookResult> Handle(LoadBookCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check for cancellation immediately
            cancellationToken.ThrowIfCancellationRequested();

            // Validate the command
            _progress?.Report(LoadProgress.CheckingCache);
            var validationResult = await _validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Validation failed for {FilePath}: {Errors}", request.FilePath, errors);
                return LoadBookError.InvalidFormat(errors);
            }

            // Check cache first
            var cached = await _cache.TryGetAsync(request.FilePath, cancellationToken);
            if (cached != null)
            {
                _logger.LogDebug("Book loaded from cache: {Path}", request.FilePath);
                _progress?.Report(LoadProgress.Complete);
                return cached;
            }

            // Report progress for file opening
            _progress?.Report(LoadProgress.OpeningFile);
            _logger.LogInformation("Loading book from {FilePath}", request.FilePath);

            // Validate the EPUB structure
            var epubValidationResult = await _epubLoader.ValidateEpubAsync(request.FilePath, cancellationToken);
            if (epubValidationResult.IsT1) // ValidationError
            {
                var error = epubValidationResult.AsT1;
                _logger.LogWarning("Invalid EPUB file: {FilePath} - {Errors}", request.FilePath, error.Message);
                return LoadBookError.InvalidFormat(error.Message);
            }

            // Load metadata
            _progress?.Report(LoadProgress.ReadingMetadata);

            // Load the book
            _progress?.Report(LoadProgress.LoadingChapters);
            var loadResult = await _epubLoader.LoadFromFileAsync(request.FilePath, cancellationToken);

            if (loadResult.IsT1) // ParsingError
            {
                var error = loadResult.AsT1;
                _logger.LogError("Failed to load book from {FilePath}: {Error}", request.FilePath, error.Message);
                return LoadBookError.ParsingFailed(error.Message);
            }

            var book = loadResult.AsT0;

            // Analyze content for each chapter
            _progress?.Report(LoadProgress.AnalyzingContent);
            foreach (var chapter in book.Chapters)
            {
                if (!string.IsNullOrEmpty(chapter.Content))
                {
                    _logger.LogDebug("Analyzing chapter {Title} with content length {Length}", chapter.Title, chapter.Content.Length);
                    var metrics = await _contentAnalyzer.AnalyzeContentAsync(chapter.Content, cancellationToken);
                    _logger.LogDebug("Chapter {Title} metrics: WordCount={WordCount}, CharCount={CharCount}",
                        chapter.Title, metrics.WordCount, metrics.CharacterCount);
                    chapter.SetMetrics(metrics);
                }
                else
                {
                    _logger.LogWarning("Chapter {Title} has no content", chapter.Title);
                }
            }

            // Cache the result
            _progress?.Report(LoadProgress.Caching);
            await _cache.SetAsync(request.FilePath, book, cancellationToken);

            _logger.LogInformation("Successfully loaded book: {Title} with {ChapterCount} chapters",
                book.Title.Value, book.Chapters.Count);

            _progress?.Report(LoadProgress.Complete);
            return book;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Book loading cancelled: {Path}", request.FilePath);
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied to file: {Path}", request.FilePath);
            return LoadBookError.AccessDenied(request.FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load book: {Path}", request.FilePath);
            return LoadBookError.ParsingFailed(ex.Message, ex);
        }
    }
}