using Alexandria.Domain.Entities;
using Alexandria.Domain.Errors;
using OneOf;

namespace Alexandria.Application.Features.LoadBook;

/// <summary>
/// Result of a LoadBook operation using OneOf pattern
/// </summary>
public class LoadBookResult : OneOfBase<Book, LoadBookError>
{
    public LoadBookResult(OneOf<Book, LoadBookError> input) : base(input) { }

    public static implicit operator LoadBookResult(Book book) => new(book);
    public static implicit operator LoadBookResult(LoadBookError error) => new(error);
}

/// <summary>
/// Represents an error that occurred during book loading
/// </summary>
public record LoadBookError
{
    public LoadBookError(string message, LoadBookErrorType type, Exception? exception = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Type = type;
        Exception = exception;
    }

    public string Message { get; }
    public LoadBookErrorType Type { get; }
    public Exception? Exception { get; }

    // Factory methods for common errors
    public static LoadBookError FileNotFound(string filePath) =>
        new($"File not found: {filePath}", LoadBookErrorType.FileNotFound);

    public static LoadBookError InvalidFormat(string message) =>
        new($"Invalid EPUB format: {message}", LoadBookErrorType.InvalidFormat);

    public static LoadBookError TooLarge(long size, long maxSize) =>
        new($"File size {size} bytes exceeds maximum allowed size of {maxSize} bytes", LoadBookErrorType.TooLarge);

    public static LoadBookError AccessDenied(string filePath) =>
        new($"Access denied to file: {filePath}", LoadBookErrorType.AccessDenied);

    public static LoadBookError ParsingFailed(string message, Exception? exception = null) =>
        new($"Failed to parse EPUB: {message}", LoadBookErrorType.ParsingFailed, exception);
}

/// <summary>
/// Types of errors that can occur during book loading
/// </summary>
public enum LoadBookErrorType
{
    FileNotFound,
    InvalidFormat,
    TooLarge,
    AccessDenied,
    ParsingFailed,
    Unknown
}

/// <summary>
/// Represents the progress of a book loading operation
/// </summary>
public record LoadProgress(int Percentage, string Message)
{
    public LoadProgress(string message) : this(0, message) { }

    public static LoadProgress CheckingCache => new(0, "Checking cache...");
    public static LoadProgress OpeningFile => new(10, "Opening EPUB file...");
    public static LoadProgress ReadingMetadata => new(20, "Reading metadata...");
    public static LoadProgress LoadingChapters => new(40, "Loading chapters...");
    public static LoadProgress AnalyzingContent => new(80, "Analyzing content...");
    public static LoadProgress Caching => new(95, "Caching...");
    public static LoadProgress Complete => new(100, "Complete");
}