namespace Alexandria.Parser.Domain.Entities;

/// <summary>
/// Represents a chapter in an EPUB book
/// </summary>
public sealed class Chapter
{
    public Chapter(string id, string title, string content, int order, string? href = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Chapter ID cannot be empty", nameof(id));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Chapter content cannot be empty", nameof(content));

        if (order < 0)
            throw new ArgumentOutOfRangeException(nameof(order), "Chapter order must be non-negative");

        Id = id;
        Title = title ?? string.Empty;
        Content = content;
        Order = order;
        Href = href;
    }

    public string Id { get; }
    public string Title { get; }
    public string Content { get; }
    public int Order { get; }
    public string? Href { get; }

    /// <summary>
    /// Gets the content as a memory span for efficient processing
    /// </summary>
    public ReadOnlyMemory<char> GetContentMemory() => Content.AsMemory();

    /// <summary>
    /// Estimates the reading time in minutes based on average reading speed
    /// </summary>
    public int EstimateReadingTimeMinutes(int wordsPerMinute = 200)
    {
        var wordCount = Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, wordCount / wordsPerMinute);
    }
}