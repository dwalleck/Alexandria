using Alexandria.Parser.Domain.Entities;

namespace Alexandria.Parser.Domain.Interfaces;

/// <summary>
/// Interface for parsing EPUB content
/// </summary>
public interface IEpubParser
{
    /// <summary>
    /// Parses an EPUB file from a stream
    /// </summary>
    Task<Book> ParseAsync(Stream epubStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the EPUB structure
    /// </summary>
    Task<ValidationResult> ValidateAsync(Stream epubStream, CancellationToken cancellationToken = default);
}

public record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Valid() => new(true, Array.Empty<string>());
    public static ValidationResult Invalid(params string[] errors) => new(false, errors);
}