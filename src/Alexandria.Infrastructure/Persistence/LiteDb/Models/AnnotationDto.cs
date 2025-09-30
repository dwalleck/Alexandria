using System;
using Alexandria.Domain.ValueObjects;

namespace Alexandria.Infrastructure.Persistence.LiteDb.Models;

/// <summary>
/// Data Transfer Object for Annotation persistence in LiteDB.
/// Mutable structure that can be serialized/deserialized by LiteDB.
/// </summary>
internal sealed class AnnotationDto
{
    public string Id { get; set; } = string.Empty;
    public string ChapterId { get; set; } = string.Empty;
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }
    public string HighlightedText { get; set; } = string.Empty;
    public string? Note { get; set; }
    public HighlightColor Color { get; set; }
    public DateTime CreatedAt { get; set; }
}