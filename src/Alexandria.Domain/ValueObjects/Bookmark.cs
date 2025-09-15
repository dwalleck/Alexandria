using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Alexandria.Domain.ValueObjects;

/// <summary>
/// Represents a bookmark in a book
/// </summary>
public sealed class Bookmark
{
    public Bookmark(
        string id,
        string chapterId,
        string chapterTitle,
        int position,
        string? note,
        DateTime createdAt,
        string? contextText = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        ChapterId = chapterId ?? throw new ArgumentNullException(nameof(chapterId));
        ChapterTitle = chapterTitle ?? throw new ArgumentNullException(nameof(chapterTitle));
        Position = Math.Max(0, position);
        Note = note;
        CreatedAt = createdAt;
        ContextText = contextText;
    }

    public string Id { get; }
    public string ChapterId { get; }
    public string ChapterTitle { get; }
    public int Position { get; }
    public string? Note { get; }
    public DateTime CreatedAt { get; }
    public string? ContextText { get; }

    /// <summary>
    /// Updates the note for this bookmark
    /// </summary>
    public Bookmark UpdateNote(string? newNote)
    {
        return new Bookmark(Id, ChapterId, ChapterTitle, Position, newNote, CreatedAt, ContextText);
    }

    /// <summary>
    /// Creates a new bookmark
    /// </summary>
    public static Bookmark Create(string chapterId, string chapterTitle, int position, string? note = null, string? contextText = null)
    {
        return new Bookmark(
            Guid.NewGuid().ToString(),
            chapterId,
            chapterTitle,
            position,
            note,
            DateTime.UtcNow,
            contextText
        );
    }

    public override string ToString()
    {
        var result = $"{ChapterTitle} (Position: {Position})";
        if (!string.IsNullOrWhiteSpace(Note))
            result += $" - {Note}";
        return result;
    }
}

/// <summary>
/// Represents an annotation/highlight in a book
/// </summary>
public sealed class Annotation
{
    public Annotation(
        string id,
        string chapterId,
        int startPosition,
        int endPosition,
        string highlightedText,
        string? note,
        HighlightColor color,
        DateTime createdAt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        ChapterId = chapterId ?? throw new ArgumentNullException(nameof(chapterId));
        HighlightedText = highlightedText ?? throw new ArgumentNullException(nameof(highlightedText));
        Note = note;
        Color = color;
        CreatedAt = createdAt;

        if (startPosition < 0)
            throw new ArgumentOutOfRangeException(nameof(startPosition), "Start position must be non-negative");

        if (endPosition <= startPosition)
            throw new ArgumentException("End position must be greater than start position");

        StartPosition = startPosition;
        EndPosition = endPosition;
    }

    public string Id { get; }
    public string ChapterId { get; }
    public int StartPosition { get; }
    public int EndPosition { get; }
    public string HighlightedText { get; }
    public string? Note { get; }
    public HighlightColor Color { get; }
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the length of the highlighted text
    /// </summary>
    public int Length => EndPosition - StartPosition;

    /// <summary>
    /// Updates the note for this annotation
    /// </summary>
    public Annotation UpdateNote(string? newNote)
    {
        return new Annotation(Id, ChapterId, StartPosition, EndPosition, HighlightedText, newNote, Color, CreatedAt);
    }

    /// <summary>
    /// Changes the highlight color
    /// </summary>
    public Annotation ChangeColor(HighlightColor newColor)
    {
        return new Annotation(Id, ChapterId, StartPosition, EndPosition, HighlightedText, Note, newColor, CreatedAt);
    }

    /// <summary>
    /// Creates a new annotation
    /// </summary>
    public static Annotation Create(
        string chapterId,
        int startPosition,
        int endPosition,
        string highlightedText,
        HighlightColor color = HighlightColor.Yellow,
        string? note = null)
    {
        return new Annotation(
            Guid.NewGuid().ToString(),
            chapterId,
            startPosition,
            endPosition,
            highlightedText,
            note,
            color,
            DateTime.UtcNow
        );
    }
}

/// <summary>
/// Highlight colors for annotations
/// </summary>
public enum HighlightColor
{
    Yellow,
    Green,
    Blue,
    Pink,
    Orange,
    Purple
}