using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.Exceptions;
using Alexandria.Parser.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Alexandria.Parser.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for loading books from various sources
/// </summary>
public sealed class BookRepository : IBookRepository
{
    private readonly IEpubParser _epubParser;
    private readonly ILogger<BookRepository> _logger;

    public BookRepository(IEpubParser epubParser, ILogger<BookRepository> logger)
    {
        _epubParser = epubParser ?? throw new ArgumentNullException(nameof(epubParser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Book> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"EPUB file not found: {filePath}", filePath);

        try
        {
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, useAsync: true);

            return await _epubParser.ParseAsync(fileStream, cancellationToken);
        }
        catch (EpubParsingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new EpubParsingException(filePath, ex.Message);
        }
    }

    public async Task<Book> LoadFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable", nameof(stream));

        return await _epubParser.ParseAsync(stream, cancellationToken);
    }

    public async Task<Book> LoadFromBytesAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length == 0)
            throw new ArgumentException("Byte array cannot be empty", nameof(bytes));

        using var memoryStream = new MemoryStream(bytes, writable: false);
        return await _epubParser.ParseAsync(memoryStream, cancellationToken);
    }

    public async Task<bool> ValidateEpubAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
            return false;

        try
        {
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, useAsync: true);

            var result = await _epubParser.ValidateAsync(fileStream, cancellationToken);

            if (!result.IsValid)
            {
                _logger.LogWarning("EPUB validation failed for {FilePath}: {Errors}",
                    filePath, string.Join(", ", result.Errors));
            }

            return result.IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating EPUB file: {FilePath}", filePath);
            return false;
        }
    }
}