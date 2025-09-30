using Alexandria.Domain.ValueObjects;
using Alexandria.Infrastructure.Persistence.LiteDb.Models;

namespace Alexandria.Infrastructure.Persistence.LiteDb.Mappers;

/// <summary>
/// Maps between Bookmark value object and BookmarkDto persistence model.
/// </summary>
internal static class BookmarkMapper
{
    public static BookmarkDto ToDto(Bookmark bookmark)
    {
        return new BookmarkDto
        {
            Id = bookmark.Id,
            ChapterId = bookmark.ChapterId,
            ChapterTitle = bookmark.ChapterTitle,
            Position = bookmark.Position,
            Note = bookmark.Note,
            CreatedAt = bookmark.CreatedAt,
            ContextText = bookmark.ContextText
        };
    }

    public static Bookmark FromDto(BookmarkDto dto)
    {
        return new Bookmark(
            id: dto.Id,
            chapterId: dto.ChapterId,
            chapterTitle: dto.ChapterTitle,
            position: dto.Position,
            note: dto.Note,
            createdAt: dto.CreatedAt,
            contextText: dto.ContextText
        );
    }
}