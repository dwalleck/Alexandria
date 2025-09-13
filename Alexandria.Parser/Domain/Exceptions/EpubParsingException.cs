namespace Alexandria.Parser.Domain.Exceptions;

/// <summary>
/// Exception thrown when an EPUB file cannot be parsed
/// </summary>
public class EpubParsingException : Exception
{
    public EpubParsingException(string message) : base(message)
    {
    }

    public EpubParsingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public EpubParsingException(string filePath, string reason)
        : base($"Failed to parse EPUB file '{filePath}': {reason}")
    {
        FilePath = filePath;
        Reason = reason;
    }

    public string? FilePath { get; }
    public string? Reason { get; }
}