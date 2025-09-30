using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alexandria.Domain.Repositories;
using Alexandria.Domain.ValueObjects;
using Alexandria.Infrastructure.Persistence.LiteDb.Mappers;
using Alexandria.Infrastructure.Persistence.LiteDb.Models;
using LiteDB;

namespace Alexandria.Infrastructure.Persistence.LiteDb;

/// <summary>
/// Document wrapper for storing bookmarks with book association.
/// </summary>
internal sealed class BookmarkDocument
{
    public string Id { get; set; } = string.Empty;
    public Guid BookId { get; set; }
    public BookmarkDto Bookmark { get; set; } = null!;
}

/// <summary>
/// LiteDB implementation of IBookmarkRepository.
/// Provides persistence for user bookmarks using LiteDB document database.
/// Uses DTOs internally for proper LiteDB serialization.
/// </summary>
public sealed class LiteDbBookmarkRepository : IBookmarkRepository
{
    private readonly LiteDbContext _context;
    private readonly ILiteCollection<BookmarkDocument> _bookmarks;

    public LiteDbBookmarkRepository(LiteDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _bookmarks = _context.Database.GetCollection<BookmarkDocument>("bookmarks");
    }

    public ValueTask<Bookmark?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();

        var doc = _bookmarks.FindOne(b => b.Id == id);
        var bookmark = doc != null ? BookmarkMapper.FromDto(doc.Bookmark) : null;
        return ValueTask.FromResult(bookmark);
    }

    public Task<IReadOnlyList<Bookmark>> GetByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var docs = _bookmarks
            .Query()
            .Where(b => b.BookId == bookId)
            .ToList();

        var bookmarks = docs.Select(d => BookmarkMapper.FromDto(d.Bookmark)).ToList();
        return Task.FromResult<IReadOnlyList<Bookmark>>(bookmarks);
    }

    public Task<IReadOnlyList<Bookmark>> GetByChapterIdAsync(string chapterId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chapterId);
        cancellationToken.ThrowIfCancellationRequested();

        var docs = _bookmarks
            .Query()
            .Where(b => b.Bookmark.ChapterId == chapterId)
            .ToList();

        var bookmarks = docs.Select(d => BookmarkMapper.FromDto(d.Bookmark)).ToList();
        return Task.FromResult<IReadOnlyList<Bookmark>>(bookmarks);
    }

    public Task<IReadOnlyList<Bookmark>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var docs = _bookmarks
            .Query()
            .OrderByDescending(b => b.Bookmark.CreatedAt)
            .ToList();

        var bookmarks = docs.Select(d => BookmarkMapper.FromDto(d.Bookmark)).ToList();
        return Task.FromResult<IReadOnlyList<Bookmark>>(bookmarks);
    }

    public Task<Bookmark> AddAsync(Bookmark bookmark, Guid bookId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        cancellationToken.ThrowIfCancellationRequested();

        var dto = BookmarkMapper.ToDto(bookmark);
        var doc = new BookmarkDocument
        {
            Id = bookmark.Id,
            BookId = bookId,
            Bookmark = dto
        };

        _bookmarks.Insert(doc);
        return Task.FromResult(bookmark);
    }

    public Task<Bookmark> UpdateAsync(Bookmark bookmark, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        cancellationToken.ThrowIfCancellationRequested();

        var doc = _bookmarks.FindOne(b => b.Id == bookmark.Id);
        if (doc == null)
        {
            throw new InvalidOperationException($"Bookmark with ID {bookmark.Id} not found");
        }

        doc.Bookmark = BookmarkMapper.ToDto(bookmark);
        _bookmarks.Update(doc);

        return Task.FromResult(bookmark);
    }

    public Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();

        var result = _bookmarks.DeleteMany(b => b.Id == id);
        return Task.FromResult(result > 0);
    }

    public Task<int> RemoveByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var count = _bookmarks.DeleteMany(b => b.BookId == bookId);
        return Task.FromResult(count);
    }

    public ValueTask<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();

        var exists = _bookmarks.Exists(b => b.Id == id);
        return ValueTask.FromResult(exists);
    }

    public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var count = _bookmarks.Count();
        return ValueTask.FromResult(count);
    }
}