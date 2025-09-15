using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alexandria.Domain.Common;
using Alexandria.Domain.Entities;
using Alexandria.Domain.ValueObjects;

namespace Alexandria.Domain.Repositories;

/// <summary>
/// Repository interface for managing Book entities.
/// Provides high-performance, async operations for book persistence and retrieval.
/// </summary>
/// <remarks>
/// This interface follows repository pattern best practices:
/// - Async-first design for all I/O operations
/// - CancellationToken support for all async methods
/// - Specification pattern for complex queries
/// - Batch operations for performance
/// - Memory-efficient streaming for large result sets
/// </remarks>
public interface IBookRepository
{
    /// <summary>
    /// Retrieves a book by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the book</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The book if found, null otherwise</returns>
    ValueTask<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a book by its ISBN.
    /// </summary>
    /// <param name="isbn">The ISBN of the book</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The book if found, null otherwise</returns>
    ValueTask<Book?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves books by author name.
    /// </summary>
    /// <param name="authorName">The author name to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of books by the specified author</returns>
    Task<IReadOnlyList<Book>> GetByAuthorAsync(string authorName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all books matching the specified criteria.
    /// </summary>
    /// <param name="specification">The specification defining the query criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of books matching the criteria</returns>
    Task<IReadOnlyList<Book>> GetAllAsync(ISpecification<Book>? specification = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves books with pagination support.
    /// </summary>
    /// <param name="pageNumber">The page number (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="specification">Optional specification for filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated result containing books and metadata</returns>
    Task<PagedResult<Book>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        ISpecification<Book>? specification = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams books matching the criteria for memory-efficient processing.
    /// </summary>
    /// <param name="specification">The specification defining the query criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable for streaming results</returns>
    IAsyncEnumerable<Book> StreamAsync(ISpecification<Book>? specification = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new book to the repository.
    /// </summary>
    /// <param name="book">The book to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added book with any generated values</returns>
    Task<Book> AddAsync(Book book, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple books in a single batch operation.
    /// </summary>
    /// <param name="books">The books to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added books with any generated values</returns>
    Task<IReadOnlyList<Book>> AddRangeAsync(IEnumerable<Book> books, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing book in the repository.
    /// </summary>
    /// <param name="book">The book with updated values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated book</returns>
    Task<Book> UpdateAsync(Book book, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates multiple books in a single batch operation.
    /// </summary>
    /// <param name="books">The books with updated values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated books</returns>
    Task<IReadOnlyList<Book>> UpdateRangeAsync(IEnumerable<Book> books, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a book from the repository.
    /// </summary>
    /// <param name="book">The book to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the book was removed, false if not found</returns>
    Task<bool> RemoveAsync(Book book, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a book by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the book to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the book was removed, false if not found</returns>
    Task<bool> RemoveByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes multiple books in a single batch operation.
    /// </summary>
    /// <param name="books">The books to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of books removed</returns>
    Task<int> RemoveRangeAsync(IEnumerable<Book> books, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a book exists with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the book exists, false otherwise</returns>
    ValueTask<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a book exists with the specified ISBN.
    /// </summary>
    /// <param name="isbn">The ISBN to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the book exists, false otherwise</returns>
    ValueTask<bool> ExistsByIsbnAsync(string isbn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of books matching the criteria.
    /// </summary>
    /// <param name="specification">Optional specification for filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The count of matching books</returns>
    ValueTask<int> CountAsync(ISpecification<Book>? specification = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for books using full-text search capabilities.
    /// </summary>
    /// <param name="searchTerm">The search term</param>
    /// <param name="searchFields">Fields to search in (title, author, description, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of books matching the search</returns>
    Task<IReadOnlyList<Book>> SearchAsync(
        string searchTerm,
        SearchFields searchFields = SearchFields.All,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets books with their chapters eagerly loaded.
    /// </summary>
    /// <param name="specification">Optional specification for filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Books with chapters loaded</returns>
    Task<IReadOnlyList<Book>> GetWithChaptersAsync(
        ISpecification<Book>? specification = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recently added books.
    /// </summary>
    /// <param name="count">Number of recent books to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of recently added books</returns>
    Task<IReadOnlyList<Book>> GetRecentlyAddedAsync(int count = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets books by publication date range.
    /// </summary>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Books published within the date range</returns>
    Task<IReadOnlyList<Book>> GetByPublicationDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Flags for specifying which fields to search in.
/// </summary>
[Flags]
public enum SearchFields
{
    /// <summary>Search in title field</summary>
    Title = 1,

    /// <summary>Search in author field</summary>
    Author = 2,

    /// <summary>Search in description field</summary>
    Description = 4,

    /// <summary>Search in ISBN field</summary>
    Isbn = 8,

    /// <summary>Search in publisher field</summary>
    Publisher = 16,

    /// <summary>Search in all fields</summary>
    All = Title | Author | Description | Isbn | Publisher
}