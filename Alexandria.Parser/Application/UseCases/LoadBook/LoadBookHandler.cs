using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.Interfaces;
using Alexandria.Parser.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Alexandria.Parser.Application.UseCases.LoadBook;

/// <summary>
/// Handler for loading books with proper error handling and logging
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

    public async Task<LoadBookResult> HandleAsync(LoadBookCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Loading book from {FilePath}", command.FilePath);

            if (!File.Exists(command.FilePath))
            {
                _logger.LogWarning("File not found: {FilePath}", command.FilePath);
                return LoadBookResult.FileNotFound(command.FilePath);
            }

            var isValid = await _bookRepository.ValidateEpubAsync(command.FilePath, cancellationToken);
            if (!isValid)
            {
                _logger.LogWarning("Invalid EPUB file: {FilePath}", command.FilePath);
                return LoadBookResult.InvalidFormat(command.FilePath);
            }

            var book = await _bookRepository.LoadFromFileAsync(command.FilePath, cancellationToken);

            _logger.LogInformation("Successfully loaded book: {Title} with {ChapterCount} chapters",
                book.Title.Value, book.Chapters.Count);

            return LoadBookResult.Success(book);
        }
        catch (EpubParsingException ex)
        {
            _logger.LogError(ex, "Failed to parse EPUB file: {FilePath}", command.FilePath);
            return LoadBookResult.ParsingError(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading book from {FilePath}", command.FilePath);
            return LoadBookResult.UnexpectedError(ex.Message);
        }
    }
}

public interface ILoadBookHandler
{
    Task<LoadBookResult> HandleAsync(LoadBookCommand command, CancellationToken cancellationToken = default);
}