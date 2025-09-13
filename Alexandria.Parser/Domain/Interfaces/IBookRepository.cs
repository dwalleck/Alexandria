using Alexandria.Parser.Domain.Entities;

namespace Alexandria.Parser.Domain.Interfaces;

/// <summary>
/// Repository interface for loading books from various sources
/// </summary>
public interface IBookRepository
{
    /// <summary>
    /// Loads a book from a file path
    /// </summary>
    Task<Book> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a book from a stream
    /// </summary>
    Task<Book> LoadFromStreamAsync(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a book from a byte array
    /// </summary>
    Task<Book> LoadFromBytesAsync(byte[] bytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a file is a valid EPUB
    /// </summary>
    Task<bool> ValidateEpubAsync(string filePath, CancellationToken cancellationToken = default);
}