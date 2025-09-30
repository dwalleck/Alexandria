using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alexandria.Domain.ValueObjects;

namespace Alexandria.Domain.Repositories;

/// <summary>
/// Repository interface for managing Annotation value objects.
/// Provides persistence for user annotations and highlights across books.
/// </summary>
public interface IAnnotationRepository
{
    /// <summary>
    /// Retrieves an annotation by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the annotation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The annotation if found, null otherwise</returns>
    ValueTask<Annotation?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all annotations for a specific book.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of annotations for the specified book</returns>
    Task<IReadOnlyList<Annotation>> GetByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all annotations for a specific chapter.
    /// </summary>
    /// <param name="chapterId">The chapter identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of annotations for the specified chapter</returns>
    Task<IReadOnlyList<Annotation>> GetByChapterIdAsync(string chapterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves annotations by highlight color.
    /// </summary>
    /// <param name="color">The highlight color to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of annotations with the specified color</returns>
    Task<IReadOnlyList<Annotation>> GetByColorAsync(HighlightColor color, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all annotations, optionally ordered by creation date.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of all annotations</returns>
    Task<IReadOnlyList<Annotation>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new annotation to the repository.
    /// </summary>
    /// <param name="annotation">The annotation to add</param>
    /// <param name="bookId">The book ID this annotation belongs to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added annotation</returns>
    Task<Annotation> AddAsync(Annotation annotation, Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing annotation in the repository.
    /// </summary>
    /// <param name="annotation">The annotation with updated values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated annotation</returns>
    Task<Annotation> UpdateAsync(Annotation annotation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an annotation from the repository.
    /// </summary>
    /// <param name="id">The unique identifier of the annotation to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the annotation was removed, false if not found</returns>
    Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all annotations for a specific book.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of annotations removed</returns>
    Task<int> RemoveByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an annotation exists with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the annotation exists, false otherwise</returns>
    ValueTask<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of annotations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The count of annotations</returns>
    ValueTask<int> CountAsync(CancellationToken cancellationToken = default);
}