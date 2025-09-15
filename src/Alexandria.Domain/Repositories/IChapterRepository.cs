using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alexandria.Domain.Entities;

namespace Alexandria.Domain.Repositories;

/// <summary>
/// Repository interface for managing Chapter entities.
/// Provides efficient operations for chapter persistence and retrieval with content streaming support.
/// </summary>
/// <remarks>
/// This interface is optimized for:
/// - Streaming large chapter content to avoid memory pressure
/// - Batch operations for multi-chapter updates
/// - Efficient navigation between chapters
/// - Content search within chapters
/// </remarks>
public interface IChapterRepository
{
    /// <summary>
    /// Retrieves a chapter by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the chapter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The chapter if found, null otherwise</returns>
    ValueTask<Chapter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all chapters for a specific book.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ordered collection of chapters for the book</returns>
    Task<IReadOnlyList<Chapter>> GetByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific chapter by book ID and chapter number.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="chapterNumber">The chapter number (1-based)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The chapter if found, null otherwise</returns>
    ValueTask<Chapter?> GetByBookAndNumberAsync(Guid bookId, int chapterNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a range of chapters for a book.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="startChapter">Starting chapter number (inclusive)</param>
    /// <param name="endChapter">Ending chapter number (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ordered collection of chapters in the specified range</returns>
    Task<IReadOnlyList<Chapter>> GetRangeAsync(
        Guid bookId,
        int startChapter,
        int endChapter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams chapter content for memory-efficient processing of large books.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable for streaming chapters</returns>
    IAsyncEnumerable<Chapter> StreamByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the next chapter in sequence.
    /// </summary>
    /// <param name="currentChapterId">The current chapter ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The next chapter if it exists, null otherwise</returns>
    ValueTask<Chapter?> GetNextChapterAsync(Guid currentChapterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the previous chapter in sequence.
    /// </summary>
    /// <param name="currentChapterId">The current chapter ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The previous chapter if it exists, null otherwise</returns>
    ValueTask<Chapter?> GetPreviousChapterAsync(Guid currentChapterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new chapter to the repository.
    /// </summary>
    /// <param name="chapter">The chapter to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added chapter with any generated values</returns>
    Task<Chapter> AddAsync(Chapter chapter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple chapters in a single batch operation.
    /// </summary>
    /// <param name="chapters">The chapters to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added chapters with any generated values</returns>
    Task<IReadOnlyList<Chapter>> AddRangeAsync(IEnumerable<Chapter> chapters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing chapter in the repository.
    /// </summary>
    /// <param name="chapter">The chapter with updated values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated chapter</returns>
    Task<Chapter> UpdateAsync(Chapter chapter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates multiple chapters in a single batch operation.
    /// </summary>
    /// <param name="chapters">The chapters with updated values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated chapters</returns>
    Task<IReadOnlyList<Chapter>> UpdateRangeAsync(IEnumerable<Chapter> chapters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only the content of a chapter (optimized for large content updates).
    /// </summary>
    /// <param name="chapterId">The chapter ID</param>
    /// <param name="content">The new content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if updated successfully</returns>
    Task<bool> UpdateContentAsync(Guid chapterId, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a chapter from the repository.
    /// </summary>
    /// <param name="chapter">The chapter to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the chapter was removed, false if not found</returns>
    Task<bool> RemoveAsync(Chapter chapter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a chapter by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the chapter to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the chapter was removed, false if not found</returns>
    Task<bool> RemoveByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all chapters for a specific book.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of chapters removed</returns>
    Task<int> RemoveByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes multiple chapters in a single batch operation.
    /// </summary>
    /// <param name="chapters">The chapters to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of chapters removed</returns>
    Task<int> RemoveRangeAsync(IEnumerable<Chapter> chapters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a chapter exists with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the chapter exists, false otherwise</returns>
    ValueTask<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of chapters for a book.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The count of chapters</returns>
    ValueTask<int> CountByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for chapters containing the specified text.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="searchText">The text to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Chapters containing the search text</returns>
    Task<IReadOnlyList<Chapter>> SearchInBookAsync(
        Guid bookId,
        string searchText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets chapters with content length within the specified range.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="minLength">Minimum content length</param>
    /// <param name="maxLength">Maximum content length</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Chapters within the length range</returns>
    Task<IReadOnlyList<Chapter>> GetByContentLengthRangeAsync(
        Guid bookId,
        int minLength,
        int maxLength,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reorders chapters by updating their chapter numbers.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="chapterOrder">Dictionary mapping chapter IDs to new chapter numbers</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if reordering was successful</returns>
    Task<bool> ReorderChaptersAsync(
        Guid bookId,
        Dictionary<Guid, int> chapterOrder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a summary of all chapters for a book (without full content).
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Chapter summaries with metadata but without full content</returns>
    Task<IReadOnlyList<ChapterSummary>> GetSummariesByBookIdAsync(
        Guid bookId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight representation of a chapter without full content.
/// </summary>
public record ChapterSummary
{
    /// <summary>
    /// Gets the unique identifier of the chapter.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the chapter number.
    /// </summary>
    public int ChapterNumber { get; init; }

    /// <summary>
    /// Gets the chapter title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the content length in characters.
    /// </summary>
    public int ContentLength { get; init; }

    /// <summary>
    /// Gets the word count.
    /// </summary>
    public int WordCount { get; init; }

    /// <summary>
    /// Gets the estimated reading time.
    /// </summary>
    public TimeSpan EstimatedReadingTime { get; init; }

    /// <summary>
    /// Gets a preview of the chapter content.
    /// </summary>
    public string ContentPreview { get; init; } = string.Empty;
}