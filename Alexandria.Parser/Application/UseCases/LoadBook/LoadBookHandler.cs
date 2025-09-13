using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.Errors;
using Alexandria.Parser.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Alexandria.Parser.Application.UseCases.LoadBook;

/// <summary>
/// Handler for loading books using OneOf for result handling
/// </summary>
public sealed class LoadBookHandler : ILoadBookHandler
{
    private readonly IBookRepository _bookRepository;
    private readonly ILogger<LoadBookHandler> _logger;

    public LoadBookHandler(IBookRepository bookRepository, ILogger<LoadBookHandler> logger)
    {
        _bookRepository = bookRepository ?? throw new ArgumentNullException(nameof(bookRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OneOf<Book, ParsingError>> HandleAsync(LoadBookCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading book from {FilePath}", command.FilePath);

        if (!File.Exists(command.FilePath))
        {
            _logger.LogWarning("File not found: {FilePath}", command.FilePath);
            return new FileNotFoundError(command.FilePath);
        }

        // Validate the EPUB first
        var validationResult = await _bookRepository.ValidateEpubAsync(command.FilePath, cancellationToken);

        if (validationResult.IsT1) // ValidationError
        {
            var error = validationResult.AsT1;
            _logger.LogWarning("Invalid EPUB file: {FilePath} - {Errors}", command.FilePath, error.Message);
            return error;
        }

        // Load the book
        var result = await _bookRepository.LoadFromFileAsync(command.FilePath, cancellationToken);

        return result.Match<OneOf<Book, ParsingError>>(
            book =>
            {
                _logger.LogInformation("Successfully loaded book: {Title} with {ChapterCount} chapters",
                    book.Title.Value, book.Chapters.Count);
                return book;
            },
            error =>
            {
                _logger.LogError("Failed to load book from {FilePath}: {Error}", command.FilePath, error.Message);
                return error;
            }
        );
    }
}

public interface ILoadBookHandler
{
    Task<OneOf<Book, ParsingError>> HandleAsync(LoadBookCommand command, CancellationToken cancellationToken = default);
}