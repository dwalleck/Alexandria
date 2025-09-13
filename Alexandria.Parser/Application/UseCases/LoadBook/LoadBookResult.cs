using Alexandria.Parser.Domain.Entities;

namespace Alexandria.Parser.Application.UseCases.LoadBook;

/// <summary>
/// Result of loading a book operation
/// </summary>
public sealed class LoadBookResult
{
    private LoadBookResult(bool isSuccess, Book? book, string? errorMessage, LoadBookErrorType? errorType)
    {
        IsSuccess = isSuccess;
        Book = book;
        ErrorMessage = errorMessage;
        ErrorType = errorType;
    }

    public bool IsSuccess { get; }
    public Book? Book { get; }
    public string? ErrorMessage { get; }
    public LoadBookErrorType? ErrorType { get; }

    public static LoadBookResult Success(Book book) =>
        new(true, book, null, null);

    public static LoadBookResult FileNotFound(string filePath) =>
        new(false, null, $"File not found: {filePath}", LoadBookErrorType.FileNotFound);

    public static LoadBookResult InvalidFormat(string filePath) =>
        new(false, null, $"Invalid EPUB format: {filePath}", LoadBookErrorType.InvalidFormat);

    public static LoadBookResult ParsingError(string message) =>
        new(false, null, message, LoadBookErrorType.ParsingError);

    public static LoadBookResult UnexpectedError(string message) =>
        new(false, null, message, LoadBookErrorType.UnexpectedError);
}

public enum LoadBookErrorType
{
    FileNotFound,
    InvalidFormat,
    ParsingError,
    UnexpectedError
}