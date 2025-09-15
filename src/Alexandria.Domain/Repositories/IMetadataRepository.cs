using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alexandria.Domain.ValueObjects;

namespace Alexandria.Domain.Repositories;

/// <summary>
/// Repository interface for managing book metadata.
/// Provides efficient operations for metadata storage, retrieval, and search.
/// </summary>
/// <remarks>
/// This repository handles metadata as value objects, supporting:
/// - Efficient metadata indexing for search
/// - Batch metadata updates
/// - Metadata versioning and history
/// - Custom metadata fields
/// </remarks>
public interface IMetadataRepository
{
    /// <summary>
    /// Retrieves metadata for a specific book.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The book metadata if found, null otherwise</returns>
    ValueTask<BookMetadata?> GetByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves metadata for multiple books.
    /// </summary>
    /// <param name="bookIds">The unique identifiers of the books</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping book IDs to their metadata</returns>
    Task<IReadOnlyDictionary<Guid, BookMetadata>> GetByBookIdsAsync(
        IEnumerable<Guid> bookIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves metadata by ISBN.
    /// </summary>
    /// <param name="isbn">The ISBN to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The book metadata if found, null otherwise</returns>
    ValueTask<BookMetadata?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches metadata by title.
    /// </summary>
    /// <param name="title">The title to search for (supports partial matching)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of matching metadata</returns>
    Task<IReadOnlyList<BookMetadata>> SearchByTitleAsync(
        string title,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches metadata by author.
    /// </summary>
    /// <param name="author">The author name to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of matching metadata</returns>
    Task<IReadOnlyList<BookMetadata>> SearchByAuthorAsync(
        string author,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches metadata by publisher.
    /// </summary>
    /// <param name="publisher">The publisher name to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of matching metadata</returns>
    Task<IReadOnlyList<BookMetadata>> SearchByPublisherAsync(
        string publisher,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches metadata by language.
    /// </summary>
    /// <param name="languageCode">The ISO language code (e.g., "en", "fr")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of matching metadata</returns>
    Task<IReadOnlyList<BookMetadata>> GetByLanguageAsync(
        string languageCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches metadata by publication year range.
    /// </summary>
    /// <param name="startYear">Start year (inclusive)</param>
    /// <param name="endYear">End year (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of matching metadata</returns>
    Task<IReadOnlyList<BookMetadata>> GetByPublicationYearRangeAsync(
        int startYear,
        int endYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches metadata by genre/subject.
    /// </summary>
    /// <param name="genre">The genre or subject to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of matching metadata</returns>
    Task<IReadOnlyList<BookMetadata>> GetByGenreAsync(
        string genre,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a full-text search across all metadata fields.
    /// </summary>
    /// <param name="searchTerm">The search term</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of matching metadata ordered by relevance</returns>
    Task<IReadOnlyList<BookMetadata>> FullTextSearchAsync(
        string searchTerm,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates metadata for a book.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="metadata">The metadata to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The saved metadata</returns>
    Task<BookMetadata> SaveAsync(
        Guid bookId,
        BookMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates metadata for multiple books in batch.
    /// </summary>
    /// <param name="metadataEntries">Dictionary mapping book IDs to their metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The saved metadata entries</returns>
    Task<IReadOnlyDictionary<Guid, BookMetadata>> SaveBatchAsync(
        IDictionary<Guid, BookMetadata> metadataEntries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates specific metadata fields without replacing the entire object.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="updates">Dictionary of field names and their new values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated metadata</returns>
    Task<BookMetadata> UpdateFieldsAsync(
        Guid bookId,
        IDictionary<string, object> updates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes metadata for a specific book.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if metadata was removed, false if not found</returns>
    Task<bool> RemoveAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes metadata for multiple books.
    /// </summary>
    /// <param name="bookIds">The unique identifiers of the books</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of metadata entries removed</returns>
    Task<int> RemoveBatchAsync(
        IEnumerable<Guid> bookIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if metadata exists for a book.
    /// </summary>
    /// <param name="bookId">The unique identifier of the book</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if metadata exists, false otherwise</returns>
    ValueTask<bool> ExistsAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all unique authors in the metadata repository.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sorted list of unique author names</returns>
    Task<IReadOnlyList<string>> GetAllAuthorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all unique publishers in the metadata repository.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sorted list of unique publisher names</returns>
    Task<IReadOnlyList<string>> GetAllPublishersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all unique genres/subjects in the metadata repository.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sorted list of unique genres</returns>
    Task<IReadOnlyList<string>> GetAllGenresAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all unique languages in the metadata repository.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sorted list of unique language codes</returns>
    Task<IReadOnlyList<string>> GetAllLanguagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata statistics for the repository.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Statistics about the metadata collection</returns>
    Task<MetadataStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and repairs metadata integrity.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Report of validation results and repairs made</returns>
    Task<MetadataValidationReport> ValidateAndRepairAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports metadata to a standard format (e.g., ONIX, MARC).
    /// </summary>
    /// <param name="bookIds">Optional list of book IDs to export (null for all)</param>
    /// <param name="format">The export format</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exported metadata as a string in the specified format</returns>
    Task<string> ExportAsync(
        IEnumerable<Guid>? bookIds,
        MetadataExportFormat format,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports metadata from a standard format.
    /// </summary>
    /// <param name="data">The metadata to import</param>
    /// <param name="format">The format of the imported data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Report of the import operation</returns>
    Task<MetadataImportReport> ImportAsync(
        string data,
        MetadataExportFormat format,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about the metadata collection.
/// </summary>
public record MetadataStatistics
{
    /// <summary>Total number of metadata entries.</summary>
    public int TotalCount { get; init; }

    /// <summary>Number of unique authors.</summary>
    public int UniqueAuthors { get; init; }

    /// <summary>Number of unique publishers.</summary>
    public int UniquePublishers { get; init; }

    /// <summary>Number of unique languages.</summary>
    public int UniqueLanguages { get; init; }

    /// <summary>Number of unique genres.</summary>
    public int UniqueGenres { get; init; }

    /// <summary>Average publication year.</summary>
    public int AveragePublicationYear { get; init; }

    /// <summary>Most common language.</summary>
    public string MostCommonLanguage { get; init; } = string.Empty;

    /// <summary>Most prolific author.</summary>
    public string MostProlificAuthor { get; init; } = string.Empty;
}

/// <summary>
/// Report of metadata validation and repair operations.
/// </summary>
public record MetadataValidationReport
{
    /// <summary>Number of entries validated.</summary>
    public int EntriesValidated { get; init; }

    /// <summary>Number of errors found.</summary>
    public int ErrorsFound { get; init; }

    /// <summary>Number of errors repaired.</summary>
    public int ErrorsRepaired { get; init; }

    /// <summary>List of validation errors.</summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>List of repairs made.</summary>
    public IReadOnlyList<string> Repairs { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Report of metadata import operations.
/// </summary>
public record MetadataImportReport
{
    /// <summary>Number of entries processed.</summary>
    public int EntriesProcessed { get; init; }

    /// <summary>Number of entries imported successfully.</summary>
    public int EntriesImported { get; init; }

    /// <summary>Number of entries updated.</summary>
    public int EntriesUpdated { get; init; }

    /// <summary>Number of entries skipped.</summary>
    public int EntriesSkipped { get; init; }

    /// <summary>List of import errors.</summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Supported metadata export formats.
/// </summary>
public enum MetadataExportFormat
{
    /// <summary>JSON format.</summary>
    Json,

    /// <summary>XML format.</summary>
    Xml,

    /// <summary>ONIX for Books format.</summary>
    Onix,

    /// <summary>MARC 21 format.</summary>
    Marc21,

    /// <summary>Dublin Core format.</summary>
    DublinCore,

    /// <summary>CSV format.</summary>
    Csv
}