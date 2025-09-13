using Alexandria.Parser.Domain.Services;
using Alexandria.Parser.Domain.ValueObjects;
using System.Collections.Generic;

namespace Alexandria.Parser.Domain.Entities;

/// <summary>
/// Represents an EPUB book with immutable properties following DDD principles
/// </summary>
public sealed class Book
{
    private readonly List<Chapter> _chapters;
    private readonly List<Author> _authors;
    private readonly List<BookIdentifier> _identifiers;
    private NavigationStructure? _navigationStructure;
    private ResourceCollection? _resources;

    public Book(
        BookTitle title,
        IEnumerable<BookTitle>? alternateTitles,
        IEnumerable<Author> authors,
        IEnumerable<Chapter> chapters,
        IEnumerable<BookIdentifier> identifiers,
        Language language,
        BookMetadata metadata,
        NavigationStructure? navigationStructure = null,
        ResourceCollection? resources = null)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        AlternateTitles = alternateTitles?.ToList() ?? [];
        _authors = authors?.ToList() ?? throw new ArgumentNullException(nameof(authors));
        _chapters = chapters?.ToList() ?? throw new ArgumentNullException(nameof(chapters));
        _identifiers = identifiers?.ToList() ?? throw new ArgumentNullException(nameof(identifiers));
        Language = language ?? throw new ArgumentNullException(nameof(language));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _navigationStructure = navigationStructure;
        _resources = resources;

        if (_chapters.Count == 0)
            throw new ArgumentException("A book must have at least one chapter", nameof(chapters));

        if (_authors.Count == 0)
            throw new ArgumentException("A book must have at least one author", nameof(authors));
    }

    public BookTitle Title { get; }
    public IReadOnlyList<BookTitle> AlternateTitles { get; }
    public IReadOnlyList<Author> Authors => _authors.AsReadOnly();
    public IReadOnlyList<Chapter> Chapters => _chapters.AsReadOnly();
    public IReadOnlyList<BookIdentifier> Identifiers => _identifiers.AsReadOnly();
    public Language Language { get; }
    public BookMetadata Metadata { get; }

    public Chapter? GetChapterById(string id)
    {
        return _chapters.FirstOrDefault(c => c.Id == id);
    }

    public Chapter? GetChapterByOrder(int order)
    {
        return _chapters.FirstOrDefault(c => c.Order == order);
    }

    /// <summary>
    /// Gets the total word count across all chapters
    /// </summary>
    public int GetTotalWordCount()
    {
        return _chapters.Sum(c => c.GetWordCount());
    }

    /// <summary>
    /// Gets the estimated reading time for the entire book
    /// </summary>
    public TimeSpan GetEstimatedReadingTime(int wordsPerMinute = 250)
    {
        var totalWords = GetTotalWordCount();
        var minutes = Math.Max(1, (int)Math.Ceiling(totalWords / (double)wordsPerMinute));
        return TimeSpan.FromMinutes(minutes);
    }

    /// <summary>
    /// Gets the table of contents/navigation structure
    /// </summary>
    public NavigationStructure? GetTableOfContents()
    {
        return _navigationStructure;
    }

    /// <summary>
    /// Sets the navigation structure (can only be set once)
    /// </summary>
    public void SetNavigationStructure(NavigationStructure navigationStructure)
    {
        if (_navigationStructure != null)
            throw new InvalidOperationException("Navigation structure has already been set");

        _navigationStructure = navigationStructure ?? throw new ArgumentNullException(nameof(navigationStructure));
    }

    /// <summary>
    /// Navigate to a chapter using a TOC reference
    /// </summary>
    public Chapter? NavigateToChapter(string tocRef)
    {
        if (string.IsNullOrWhiteSpace(tocRef))
            return null;

        // First try to find by navigation structure
        if (_navigationStructure != null)
        {
            var navItem = _navigationStructure.FindByHref(tocRef);
            if (navItem?.Href != null)
            {
                // Try to match chapter by href
                var chapter = _chapters.FirstOrDefault(c => c.Href == navItem.Href);
                if (chapter != null)
                    return chapter;

                // Try to match by href without fragment
                var hrefWithoutFragment = navItem.Href.Split('#')[0];
                chapter = _chapters.FirstOrDefault(c => c.Href?.Split('#')[0] == hrefWithoutFragment);
                if (chapter != null)
                    return chapter;
            }
        }

        // Fallback to direct href matching
        return _chapters.FirstOrDefault(c => c.Href == tocRef) ??
               _chapters.FirstOrDefault(c => c.Href?.Split('#')[0] == tocRef.Split('#')[0]);
    }

    /// <summary>
    /// Gets the next chapter in the navigation order
    /// </summary>
    public Chapter? GetNextChapter(Chapter currentChapter)
    {
        if (currentChapter == null)
            return _chapters.FirstOrDefault();

        var currentIndex = _chapters.IndexOf(currentChapter);
        if (currentIndex >= 0 && currentIndex < _chapters.Count - 1)
            return _chapters[currentIndex + 1];

        return null;
    }

    /// <summary>
    /// Gets the previous chapter in the navigation order
    /// </summary>
    public Chapter? GetPreviousChapter(Chapter currentChapter)
    {
        if (currentChapter == null)
            return null;

        var currentIndex = _chapters.IndexOf(currentChapter);
        if (currentIndex > 0)
            return _chapters[currentIndex - 1];

        return null;
    }

    #region Resource Methods

    /// <summary>
    /// Gets the resource collection
    /// </summary>
    public ResourceCollection? GetResources()
    {
        return _resources;
    }

    /// <summary>
    /// Sets the resource collection (can only be set once)
    /// </summary>
    public void SetResources(ResourceCollection resources)
    {
        if (_resources != null)
            throw new InvalidOperationException("Resources have already been set");

        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    /// <summary>
    /// Gets the cover image if available
    /// </summary>
    public ImageResource? GetCoverImage()
    {
        return _resources?.CoverImage;
    }

    /// <summary>
    /// Gets all images in the book
    /// </summary>
    public IEnumerable<ImageResource> GetImages()
    {
        return _resources?.GetImages() ?? Enumerable.Empty<ImageResource>();
    }

    /// <summary>
    /// Gets a resource by its ID
    /// </summary>
    public EpubResource? GetResource(string id)
    {
        return _resources?.GetById(id);
    }

    /// <summary>
    /// Gets a resource by its href
    /// </summary>
    public EpubResource? GetResourceByHref(string href)
    {
        return _resources?.GetByHref(href);
    }

    /// <summary>
    /// Extracts all resources to a directory
    /// </summary>
    public async Task ExtractResourcesToDirectoryAsync(string directoryPath)
    {
        if (_resources == null)
            throw new InvalidOperationException("No resources available to extract");

        await _resources.ExtractAllToDirectoryAsync(directoryPath);
    }

    /// <summary>
    /// Gets all stylesheets in the book
    /// </summary>
    public IEnumerable<EpubResource> GetStylesheets()
    {
        return _resources?.GetStylesheets() ?? Enumerable.Empty<EpubResource>();
    }

    /// <summary>
    /// Gets all fonts in the book
    /// </summary>
    public IEnumerable<EpubResource> GetFonts()
    {
        return _resources?.GetFonts() ?? Enumerable.Empty<EpubResource>();
    }

    /// <summary>
    /// Checks if the book has resources
    /// </summary>
    public bool HasResources => _resources != null && _resources.Count > 0;

    /// <summary>
    /// Gets the total size of all resources in bytes
    /// </summary>
    public long GetResourcesSize()
    {
        return _resources?.GetTotalSize() ?? 0;
    }

    #endregion

    

    #region Metadata Methods

    /// <summary>
    /// Gets the primary ISBN if available
    /// </summary>
    public string? GetIsbn()
    {
        return _identifiers.FirstOrDefault(i =>
            i.Scheme?.Equals("ISBN", StringComparison.OrdinalIgnoreCase) == true ||
            i.Value.StartsWith("978") || i.Value.StartsWith("979"))?.Value;
    }

    /// <summary>
    /// Gets all ISBNs
    /// </summary>
    public IEnumerable<string> GetAllIsbns()
    {
        return _identifiers
            .Where(i => i.Scheme?.Equals("ISBN", StringComparison.OrdinalIgnoreCase) == true ||
                       i.Value.StartsWith("978") || i.Value.StartsWith("979"))
            .Select(i => i.Value);
    }

    /// <summary>
    /// Gets the primary author
    /// </summary>
    public Author GetPrimaryAuthor()
    {
        return _authors.FirstOrDefault(a =>
            string.IsNullOrEmpty(a.Role) ||
            a.Role.Equals("Author", StringComparison.OrdinalIgnoreCase) ||
            a.Role.Equals("aut", StringComparison.OrdinalIgnoreCase))
            ?? _authors[0];
    }

    /// <summary>
    /// Gets all contributors (non-author roles)
    /// </summary>
    public IEnumerable<Author> GetContributors()
    {
        return _authors.Where(a =>
            !string.IsNullOrEmpty(a.Role) &&
            !a.Role.Equals("Author", StringComparison.OrdinalIgnoreCase) &&
            !a.Role.Equals("aut", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets authors by role
    /// </summary>
    public IEnumerable<Author> GetAuthorsByRole(string role)
    {
        return _authors.Where(a =>
            a.Role?.Equals(role, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>
    /// Checks if the book has a specific identifier
    /// </summary>
    public bool HasIdentifier(string scheme)
    {
        return _identifiers.Any(i =>
            i.Scheme?.Equals(scheme, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>
    /// Gets the publication year if available
    /// </summary>
    public int? GetPublicationYear()
    {
        return Metadata.PublicationDate?.Year;
    }

    /// <summary>
    /// Gets a formatted citation string
    /// </summary>
    public string GetCitation(CitationStyle style = CitationStyle.APA)
    {
        var primaryAuthor = GetPrimaryAuthor();
        var year = GetPublicationYear()?.ToString() ?? "n.d.";
        var title = Title.Value;

        return style switch
        {
            CitationStyle.APA => $"{primaryAuthor.GetLastName()}, {primaryAuthor.GetFirstInitial()}. ({year}). {title}. {Metadata.Publisher ?? "Unknown Publisher"}.",
            CitationStyle.MLA => $"{primaryAuthor.GetLastName()}, {primaryAuthor.GetFirstName()}. {title}. {Metadata.Publisher ?? "Unknown Publisher"}, {year}.",
            CitationStyle.Chicago => $"{primaryAuthor.GetLastName()}, {primaryAuthor.GetFirstName()}. {title}. {Metadata.Publisher ?? "Unknown Publisher"}, {year}.",
            _ => $"{primaryAuthor.Name}. {title}. {year}."
        };
    }

    /// <summary>
    /// Gets all metadata as a dictionary
    /// </summary>
    public Dictionary<string, string> GetMetadataDictionary()
    {
        var dict = new Dictionary<string, string>
        {
            ["Title"] = Title.Value,
            ["Language"] = Language.Code,
            ["Authors"] = string.Join("; ", _authors.Select(a => a.Name))
        };

        if (AlternateTitles.Count > 0)
            dict["AlternateTitles"] = string.Join("; ", AlternateTitles.Select(t => t.Value));

        if (_identifiers.Count > 0)
            dict["Identifiers"] = string.Join("; ", _identifiers.Select(i => $"{i.Scheme}: {i.Value}"));

        if (!string.IsNullOrEmpty(Metadata.Publisher))
            dict["Publisher"] = Metadata.Publisher;

        if (Metadata.PublicationDate.HasValue)
            dict["PublicationDate"] = Metadata.PublicationDate.Value.ToString("yyyy-MM-dd");

        if (!string.IsNullOrEmpty(Metadata.Description))
            dict["Description"] = Metadata.Description;

        if (!string.IsNullOrEmpty(Metadata.Rights))
            dict["Rights"] = Metadata.Rights;

        if (!string.IsNullOrEmpty(Metadata.Subject))
            dict["Subject"] = Metadata.Subject;

        if (Metadata.CustomMetadata != null)
        {
            foreach (var kvp in Metadata.CustomMetadata)
            {
                dict[$"Custom:{kvp.Key}"] = kvp.Value;
            }
        }

        return dict;
    }

    /// <summary>
    /// Validates that the book has minimum required metadata
    /// </summary>
    public bool HasMinimumMetadata()
    {
        return !string.IsNullOrWhiteSpace(Title.Value) &&
               _authors.Count > 0 &&
               Language != null &&
               _chapters.Count > 0;
    }

    /// <summary>
    /// Gets a summary of the book
    /// </summary>
    public BookSummary GetSummary()
    {
        return new BookSummary(
            Title.Value,
            GetPrimaryAuthor().Name,
            _chapters.Count,
            GetTotalWordCount(),
            GetEstimatedReadingTime(),
            Metadata.PublicationDate,
            GetIsbn(),
            Language.Code,
            Metadata.Description
        );
    }

    /// <summary>
    /// Gets the EPUB version
    /// </summary>
    public string? GetEpubVersion()
    {
        return Metadata.EpubVersion;
    }

    /// <summary>
    /// Gets the series information
    /// </summary>
    public string? GetSeries()
    {
        return Metadata.Series;
    }

    /// <summary>
    /// Gets the full series information including number if available
    /// </summary>
    public string? GetFullSeriesInfo()
    {
        return Metadata.GetFullSeriesInfo();
    }

    /// <summary>
    /// Checks if the book is part of a series
    /// </summary>
    public bool IsPartOfSeries()
    {
        return Metadata.IsPartOfSeries;
    }

    /// <summary>
    /// Gets all tags/subjects associated with the book
    /// </summary>
    public IReadOnlyList<string> GetTags()
    {
        var tags = new List<string>(Metadata.Tags);

        // Add subject as a tag if not already present
        if (!string.IsNullOrWhiteSpace(Metadata.Subject) &&
            !tags.Contains(Metadata.Subject, StringComparer.OrdinalIgnoreCase))
        {
            tags.Add(Metadata.Subject);
        }

        return tags;
    }

    #endregion
}

/// <summary>
/// Citation styles for formatting
/// </summary>
public enum CitationStyle
{
    APA,
    MLA,
    Chicago,
    Simple
}

/// <summary>
/// Book summary information
/// </summary>
public sealed class BookSummary
{
    public BookSummary(
        string title,
        string primaryAuthor,
        int chapterCount,
        int wordCount,
        TimeSpan estimatedReadingTime,
        DateTime? publicationDate,
        string? isbn,
        string language,
        string? description)
    {
        Title = title;
        PrimaryAuthor = primaryAuthor;
        ChapterCount = chapterCount;
        WordCount = wordCount;
        EstimatedReadingTime = estimatedReadingTime;
        PublicationDate = publicationDate;
        Isbn = isbn;
        Language = language;
        Description = description;
    }

    public string Title { get; }
    public string PrimaryAuthor { get; }
    public int ChapterCount { get; }
    public int WordCount { get; }
    public TimeSpan EstimatedReadingTime { get; }
    public DateTime? PublicationDate { get; }
    public string? Isbn { get; }
    public string Language { get; }
    public string? Description { get; }
}