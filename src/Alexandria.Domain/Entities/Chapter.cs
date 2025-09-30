using System;
using System.Text.RegularExpressions;
using Alexandria.Domain.ValueObjects;

namespace Alexandria.Domain.Entities;

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
    /// Content analysis metrics for this chapter
    /// </summary>
    public ContentMetrics? Metrics { get; private set; }

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

    /// <summary>
    /// Gets the word count of the chapter, stripping HTML tags
    /// </summary>
    public int GetWordCount()
    {
        var textOnly = Content;
        // Strip head, script, and style content first (before removing tags)
        textOnly = Regex.Replace(textOnly, @"<head[^>]*>[\s\S]*?</head>", " ", RegexOptions.IgnoreCase);
        textOnly = Regex.Replace(textOnly, @"<script[^>]*>[\s\S]*?</script>", " ", RegexOptions.IgnoreCase);
        textOnly = Regex.Replace(textOnly, @"<style[^>]*>[\s\S]*?</style>", " ", RegexOptions.IgnoreCase);
        // Then strip remaining HTML tags
        textOnly = Regex.Replace(textOnly, @"<[^>]+>", " ");
        // Decode HTML entities
        textOnly = System.Net.WebUtility.HtmlDecode(textOnly);
        // Split by whitespace and count non-empty strings
        var words = textOnly.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        return words.Length;
    }

    /// <summary>
    /// Gets the estimated reading time as a TimeSpan
    /// </summary>
    public TimeSpan GetEstimatedReadingTime(int wordsPerMinute = 250)
    {
        var wordCount = GetWordCount();
        var minutes = Math.Max(1, (int)Math.Ceiling(wordCount / (double)wordsPerMinute));
        return TimeSpan.FromMinutes(minutes);
    }

    /// <summary>
    /// Sets the content metrics for this chapter
    /// </summary>
    public void SetMetrics(ContentMetrics metrics)
    {
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }
}