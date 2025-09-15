using System.Threading.Tasks;

using System.IO;
using System.Threading;
using Alexandria.Domain.Entities;
using Alexandria.Domain.Errors;
using OneOf;

namespace Alexandria.Domain.Interfaces;

/// <summary>
/// Interface for loading EPUB books from various sources using OneOf
/// </summary>
public interface IEpubLoader
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