using Alexandria.Domain.ValueObjects;
using Alexandria.Infrastructure.Persistence.LiteDb.Models;

namespace Alexandria.Infrastructure.Persistence.LiteDb.Mappers;

/// <summary>
/// Maps between Annotation value object and AnnotationDto persistence model.
/// </summary>
internal static class AnnotationMapper
{
    public static AnnotationDto ToDto(Annotation annotation)
    {
        return new AnnotationDto
        {
            Id = annotation.Id,
            ChapterId = annotation.ChapterId,
            StartPosition = annotation.StartPosition,
            EndPosition = annotation.EndPosition,
            HighlightedText = annotation.HighlightedText,
            Note = annotation.Note,
            Color = annotation.Color,
            CreatedAt = annotation.CreatedAt
        };
    }

    public static Annotation FromDto(AnnotationDto dto)
    {
        return new Annotation(
            id: dto.Id,
            chapterId: dto.ChapterId,
            startPosition: dto.StartPosition,
            endPosition: dto.EndPosition,
            highlightedText: dto.HighlightedText,
            note: dto.Note,
            color: dto.Color,
            createdAt: dto.CreatedAt
        );
    }
}