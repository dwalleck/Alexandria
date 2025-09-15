using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Alexandria.Domain.ValueObjects;

/// <summary>
/// Value object containing additional book metadata
/// </summary>
public sealed record BookMetadata
{
    public BookMetadata(
        string? publisher = null,
        DateTime? publicationDate = null,
        string? description = null,
        string? rights = null,
        string? subject = null,
        string? coverage = null,
        string? isbn = null,
        string? series = null,
        int? seriesNumber = null,
        IReadOnlyList<string>? tags = null,
        string? epubVersion = null,
        IReadOnlyDictionary<string, string>? customMetadata = null)
    {
        Publisher = publisher;
        PublicationDate = publicationDate;
        Description = description;
        Rights = rights;
        Subject = subject;
        Coverage = coverage;
        Isbn = isbn;
        Series = series;
        SeriesNumber = seriesNumber;
        Tags = tags ?? new List<string>();
        EpubVersion = epubVersion;
        CustomMetadata = customMetadata ?? new Dictionary<string, string>();
    }

    public string? Publisher { get; }
    public DateTime? PublicationDate { get; }
    public string? Description { get; }
    public string? Rights { get; }
    public string? Subject { get; }
    public string? Coverage { get; }

    // Phase 4 Enhanced Metadata Properties
    public string? Isbn { get; }
    public string? Series { get; }
    public int? SeriesNumber { get; }
    public IReadOnlyList<string> Tags { get; }
    public string? EpubVersion { get; }

    public IReadOnlyDictionary<string, string> CustomMetadata { get; }

    public static BookMetadata Empty => new();

    /// <summary>
    /// Checks if this book is part of a series
    /// </summary>
    public bool IsPartOfSeries => !string.IsNullOrWhiteSpace(Series);

    /// <summary>
    /// Gets the full series information if available
    /// </summary>
    public string? GetFullSeriesInfo()
    {
        if (!IsPartOfSeries)
            return null;

        if (SeriesNumber.HasValue)
            return $"{Series} #{SeriesNumber}";

        return Series;
    }
}