using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.Errors;
using Alexandria.Parser.Domain.Exceptions;
using Alexandria.Parser.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using OneOf;

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

    public async Task<OneOf<Book, ParsingError>> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File not found: {FilePath}", filePath);
            return new FileNotFoundError(filePath);
        }

        try
        {
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, useAsync: true);

            return await _epubParser.ParseAsync(fileStream, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load book from file: {FilePath}", filePath);
            return new ParsingFailedError($"Failed to load from {filePath}", ex);
        }
    }

    public async Task<OneOf<Book, ParsingError>> LoadFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            _logger.LogError("Stream is not readable");
            return new InvalidFormatError("Invalid Stream", "Stream must be readable");
        }

        return await _epubParser.ParseAsync(stream, cancellationToken);
    }

    public async Task<OneOf<Book, ParsingError>> LoadFromBytesAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length == 0)
        {
            _logger.LogError("Byte array is empty");
            return new InvalidFormatError("Invalid Input", "Byte array cannot be empty");
        }

        using var memoryStream = new MemoryStream(bytes, writable: false);
        return await _epubParser.ParseAsync(memoryStream, cancellationToken);
    }

    public async Task<OneOf<Success, ValidationError>> ValidateEpubAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return new ValidationError(new[] { $"File not found: {filePath}" });
        }

        try
        {
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, useAsync: true);

            var result = await _epubParser.ValidateAsync(fileStream, cancellationToken);

            if (result.IsT1) // ValidationError
            {
                var error = result.AsT1;
                _logger.LogWarning("EPUB validation failed for {FilePath}: {Errors}",
                    filePath, error.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating EPUB file: {FilePath}", filePath);
            return new ValidationError(new[] { $"Validation failed: {ex.Message}" });
        }
    }
}