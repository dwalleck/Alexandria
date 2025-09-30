using System;

namespace Alexandria.Infrastructure.Persistence.LiteDb.Models;

/// <summary>
/// Data Transfer Object for Bookmark persistence in LiteDB.
/// Mutable structure that can be serialized/deserialized by LiteDB.
/// </summary>
internal sealed class BookmarkDto
{
    public string Id { get; set; } = string.Empty;
    public string ChapterId { get; set; } = string.Empty;
    public string ChapterTitle { get; set; } = string.Empty;
    public int Position { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ContextText { get; set; }
}