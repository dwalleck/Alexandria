namespace Alexandria.Parser.Domain.Exceptions;

/// <summary>
/// Exception thrown when an EPUB file has an invalid structure
/// </summary>
public class InvalidEpubStructureException : EpubParsingException
{
    public InvalidEpubStructureException(string message) : base(message)
    {
    }

    public InvalidEpubStructureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public InvalidEpubStructureException(string missingComponent, string filePath)
        : base($"Invalid EPUB structure in '{filePath}': {missingComponent} is missing or invalid")
    {
        MissingComponent = missingComponent;
    }

    public string? MissingComponent { get; }
}