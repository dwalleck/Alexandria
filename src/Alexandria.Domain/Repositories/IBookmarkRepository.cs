using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alexandria.Domain.ValueObjects;

namespace Alexandria.Domain.Repositories;

/// <summary>
/// Repository interface for managing Bookmark value objects.
/// Provides persistence for user bookmarks across books.
/// </summary>
public interface IBookmarkRepository
{
    /// <summary>
    /// Retrieves a bookmark by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the bookmark</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The bookmark if found, null otherwise</returns>
    ValueTask<Bookmark?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all bookmarks for a specific book.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of bookmarks for the specified book</returns>
    Task<IReadOnlyList<Bookmark>> GetByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all bookmarks for a specific chapter.
    /// </summary>
    /// <param name="chapterId">The chapter identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of bookmarks for the specified chapter</returns>
    Task<IReadOnlyList<Bookmark>> GetByChapterIdAsync(string chapterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all bookmarks, optionally ordered by creation date.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of all bookmarks</returns>
    Task<IReadOnlyList<Bookmark>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new bookmark to the repository.
    /// </summary>
    /// <param name="bookmark">The bookmark to add</param>
    /// <param name="bookId">The book ID this bookmark belongs to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added bookmark</returns>
    Task<Bookmark> AddAsync(Bookmark bookmark, Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing bookmark in the repository.
    /// </summary>
    /// <param name="bookmark">The bookmark with updated values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated bookmark</returns>
    Task<Bookmark> UpdateAsync(Bookmark bookmark, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a bookmark from the repository.
    /// </summary>
    /// <param name="id">The unique identifier of the bookmark to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the bookmark was removed, false if not found</returns>
    Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all bookmarks for a specific book.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of bookmarks removed</returns>
    Task<int> RemoveByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a bookmark exists with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the bookmark exists, false otherwise</returns>
    ValueTask<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of bookmarks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The count of bookmarks</returns>
    ValueTask<int> CountAsync(CancellationToken cancellationToken = default);
}