namespace Alexandria.Parser.Domain.ValueObjects;

/// <summary>
/// Value object containing additional book metadata
/// </summary>
public sealed record BookMetadata
{
    public BookMetadata(
        string? publisher = null,
        DateTime? publicationDate = null,
        string? description = null,
        string? rights = null,
        string? subject = null,
        string? coverage = null,
        IReadOnlyDictionary<string, string>? customMetadata = null)
    {
        Publisher = publisher;
        PublicationDate = publicationDate;
        Description = description;
        Rights = rights;
        Subject = subject;
        Coverage = coverage;
        CustomMetadata = customMetadata ?? new Dictionary<string, string>();
    }

    public string? Publisher { get; }
    public DateTime? PublicationDate { get; }
    public string? Description { get; }
    public string? Rights { get; }
    public string? Subject { get; }
    public string? Coverage { get; }
    public IReadOnlyDictionary<string, string> CustomMetadata { get; }

    public static BookMetadata Empty => new();
}