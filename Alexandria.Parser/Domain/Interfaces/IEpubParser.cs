using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.Errors;
using OneOf;

namespace Alexandria.Parser.Domain.Interfaces;

/// <summary>
/// Interface for parsing EPUB content using OneOf for result types
/// </summary>
public interface IEpubParser
{
    /// <summary>
    /// Parses an EPUB file from a stream
    /// </summary>
    /// <returns>Either a Book or a ParsingError</returns>
    Task<OneOf<Book, ParsingError>> ParseAsync(Stream epubStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the EPUB structure
    /// </summary>
    /// <returns>Either Success (true) or ValidationError</returns>
    Task<OneOf<Success, ValidationError>> ValidateAsync(Stream epubStream, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a successful operation with no return value
/// </summary>
public readonly struct Success
{
    public static Success Instance => default;
}