using Alexandria.Parser.Application.UseCases.LoadBook;
using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.Interfaces;
using Alexandria.Parser.Infrastructure.Parsers;
using Alexandria.Parser.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Parser;

/// <summary>
/// Simplified API for reading EPUB files without dependency injection
/// </summary>
public sealed class EpubReader
{
    private readonly ILoadBookHandler _loadBookHandler;
    private readonly IBookRepository _bookRepository;

    /// <summary>
    /// Creates a new EPUB reader with default configuration
    /// </summary>
    public EpubReader() : this(NullLoggerFactory.Instance)
    {
    }

    /// <summary>
    /// Creates a new EPUB reader with custom logging
    /// </summary>
    public EpubReader(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var parserFactory = new EpubParserFactory(loggerFactory);
        var adaptiveParser = new AdaptiveEpubParser(parserFactory, loggerFactory.CreateLogger<AdaptiveEpubParser>());
        _bookRepository = new BookRepository(adaptiveParser, loggerFactory.CreateLogger<BookRepository>());
        _loadBookHandler = new LoadBookHandler(_bookRepository, loggerFactory.CreateLogger<LoadBookHandler>());
    }

    /// <summary>
    /// Loads a book from a file path
    /// </summary>
    public async Task<Book> LoadBookAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var command = new LoadBookCommand(filePath);
        var result = await _loadBookHandler.HandleAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Failed to load book");
        }

        return result.Book!;
    }

    /// <summary>
    /// Loads a book from a stream
    /// </summary>
    public async Task<Book> LoadBookAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        return await _bookRepository.LoadFromStreamAsync(stream, cancellationToken);
    }

    /// <summary>
    /// Loads a book from a byte array
    /// </summary>
    public async Task<Book> LoadBookAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        return await _bookRepository.LoadFromBytesAsync(bytes, cancellationToken);
    }

    /// <summary>
    /// Validates if a file is a valid EPUB
    /// </summary>
    public async Task<bool> ValidateAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await _bookRepository.ValidateEpubAsync(filePath, cancellationToken);
    }

    /// <summary>
    /// Static method for quick book loading (backwards compatibility)
    /// </summary>
    public static async Task<Book> OpenBookAsync(string filePath)
    {
        var reader = new EpubReader();
        return await reader.LoadBookAsync(filePath);
    }
}