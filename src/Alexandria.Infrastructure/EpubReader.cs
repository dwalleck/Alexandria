using Alexandria.Application.Features.LoadBook;
using Alexandria.Domain.Entities;
using Alexandria.Domain.Errors;
using Alexandria.Domain.Interfaces;
using Alexandria.Infrastructure.Parsers;
using Alexandria.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OneOf;

namespace Alexandria.Infrastructure;

/// <summary>
/// Simplified API for reading EPUB files without dependency injection
/// </summary>
public sealed class EpubReader
{
    private readonly ILoadBookHandler _loadBookHandler;
    private readonly IEpubLoader _epubLoader;

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
        _epubLoader = new EpubLoader(adaptiveParser, loggerFactory.CreateLogger<EpubLoader>());
        _loadBookHandler = new LoadBookHandler(_epubLoader, loggerFactory.CreateLogger<LoadBookHandler>());
    }

    /// <summary>
    /// Loads a book from a file path
    /// </summary>
    public async Task<Book> LoadBookAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var command = new LoadBookCommand(filePath);
        var result = await _loadBookHandler.HandleAsync(command, cancellationToken);

        return result.Match<Book>(
            book => book,
            error => throw new InvalidOperationException(error.Message)
        );
    }

    /// <summary>
    /// Loads a book from a stream
    /// </summary>
    public async Task<Book> LoadBookAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var result = await _epubLoader.LoadFromStreamAsync(stream, cancellationToken);

        return result.Match<Book>(
            book => book,
            error => throw new InvalidOperationException(error.Message)
        );
    }

    /// <summary>
    /// Loads a book from a byte array
    /// </summary>
    public async Task<Book> LoadBookAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        var result = await _epubLoader.LoadFromBytesAsync(bytes, cancellationToken);

        return result.Match<Book>(
            book => book,
            error => throw new InvalidOperationException(error.Message)
        );
    }

    /// <summary>
    /// Validates if a file is a valid EPUB
    /// </summary>
    public async Task<bool> ValidateAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = await _epubLoader.ValidateEpubAsync(filePath, cancellationToken);

        return result.Match(
            success => true,
            error => false
        );
    }

    /// <summary>
    /// Loads a book from a file path with OneOf result
    /// </summary>
    public async Task<OneOf<Book, ParsingError>> TryLoadBookAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var command = new LoadBookCommand(filePath);
        return await _loadBookHandler.HandleAsync(command, cancellationToken);
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