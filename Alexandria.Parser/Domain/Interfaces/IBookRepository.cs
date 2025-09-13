using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.Errors;
using OneOf;

namespace Alexandria.Parser.Domain.Interfaces;

/// <summary>
/// Repository interface for loading books from various sources using OneOf
/// </summary>
public interface IBookRepository
{
    /// <summary>
    /// Loads a book from a file path
    /// </summary>
    Task<OneOf<Book, ParsingError>> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a book from a stream
    /// </summary>
    Task<OneOf<Book, ParsingError>> LoadFromStreamAsync(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a book from a byte array
    /// </summary>
    Task<OneOf<Book, ParsingError>> LoadFromBytesAsync(byte[] bytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a file is a valid EPUB
    /// </summary>
    Task<OneOf<Success, ValidationError>> ValidateEpubAsync(string filePath, CancellationToken cancellationToken = default);
}