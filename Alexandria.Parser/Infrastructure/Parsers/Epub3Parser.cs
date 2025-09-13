using System.IO.Compression;
using System.Xml.Serialization;
using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.Exceptions;
using Alexandria.Parser.Domain.Interfaces;
using Alexandria.Parser.Domain.ValueObjects;
using Alexandria.Parser.Infrastructure.Parsers.Models.Epub3;
using Microsoft.Extensions.Logging;

namespace Alexandria.Parser.Infrastructure.Parsers;

/// <summary>
/// EPUB 3 parser implementation with support for new EPUB 3 features
/// </summary>
public sealed class Epub3Parser : IEpubParser
{
    private readonly ILogger<Epub3Parser> _logger;
    private const string ContainerPath = "META-INF/container.xml";
    private const int MaxChapterSize = 10 * 1024 * 1024; // 10MB max per chapter

    public Epub3Parser(ILogger<Epub3Parser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Book> ParseAsync(Stream epubStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(epubStream);

        try
        {
            using var archive = new ZipArchive(epubStream, ZipArchiveMode.Read, leaveOpen: true);

            // Step 1: Parse container.xml (same for EPUB 2 and 3)
            var containerEntry = archive.GetEntry(ContainerPath)
                ?? throw new InvalidEpubStructureException("container.xml", "EPUB archive");

            var container = await DeserializeXmlAsync<ContainerXml>(containerEntry, cancellationToken);

            if (container?.Rootfiles?.Length == 0)
                throw new InvalidEpubStructureException("No rootfiles found in container.xml", "EPUB archive");

            var contentPath = container!.Rootfiles![0].FullPath
                ?? throw new InvalidEpubStructureException("Rootfile path is missing", "EPUB archive");

            // Step 2: Parse content.opf (EPUB 3 version)
            var contentEntry = archive.GetEntry(contentPath)
                ?? throw new InvalidEpubStructureException($"Content file not found: {contentPath}", "EPUB archive");

            var package = await DeserializeXmlAsync<PackageXml>(contentEntry, cancellationToken);

            if (package?.Metadata == null)
                throw new InvalidEpubStructureException("Package metadata is missing", contentPath);

            // Verify it's EPUB 3
            if (!package.Version?.StartsWith("3") == true)
            {
                _logger.LogWarning("Package version {Version} is not EPUB 3, parser may not handle all features correctly",
                    package.Version);
            }

            // Step 3: Build domain model with EPUB 3 specific features
            var book = await BuildBookAsync(archive, package, Path.GetDirectoryName(contentPath), cancellationToken);

            _logger.LogInformation("Successfully parsed EPUB 3 with {ChapterCount} chapters", book.Chapters.Count);

            return book;
        }
        catch (InvalidEpubStructureException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new EpubParsingException("Failed to parse EPUB 3 stream", ex);
        }
    }

    public async Task<ValidationResult> ValidateAsync(Stream epubStream, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        try
        {
            using var archive = new ZipArchive(epubStream, ZipArchiveMode.Read, leaveOpen: true);

            // Check for container.xml
            if (archive.GetEntry(ContainerPath) == null)
            {
                errors.Add("Missing META-INF/container.xml");
                return ValidationResult.Invalid(errors.ToArray());
            }

            // Check for navigation document (EPUB 3 requirement)
            var hasNav = archive.Entries.Any(e =>
                e.Name.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) ||
                e.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase));

            if (!hasNav)
            {
                errors.Add("No navigation document found (required for EPUB 3)");
            }

            // Try to parse the EPUB
            await ParseAsync(epubStream, cancellationToken);

            return errors.Count == 0 ? ValidationResult.Valid() : ValidationResult.Invalid(errors.ToArray());
        }
        catch (InvalidEpubStructureException ex)
        {
            errors.Add(ex.Message);
        }
        catch (Exception ex)
        {
            errors.Add($"Validation failed: {ex.Message}");
        }

        return ValidationResult.Invalid(errors.ToArray());
    }

    private async Task<Book> BuildBookAsync(
        ZipArchive archive,
        PackageXml package,
        string? contentDirectory,
        CancellationToken cancellationToken)
    {
        var metadata = package.Metadata!;

        // Extract titles
        var titles = metadata.Titles?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? new List<string>();
        if (titles.Count == 0)
            throw new InvalidEpubStructureException("No title found in metadata", "content.opf");

        var mainTitle = new BookTitle(titles[0]);
        var alternateTitles = titles.Skip(1).Select(t => new BookTitle(t)).ToList();

        // Extract authors (including contributors for EPUB 3)
        var authors = new List<Author>();

        if (metadata.Creators != null)
        {
            foreach (var creator in metadata.Creators.Where(c => c?.Name != null))
            {
                // In EPUB 3, role is specified via meta refinements
                var role = GetRefinementValue(metadata.MetaItems, creator.Id, "role");
                var fileAs = GetRefinementValue(metadata.MetaItems, creator.Id, "file-as");
                authors.Add(new Author(creator.Name!, role, fileAs));
            }
        }

        if (metadata.Contributors != null)
        {
            foreach (var contributor in metadata.Contributors.Where(c => c?.Name != null))
            {
                var role = GetRefinementValue(metadata.MetaItems, contributor.Id, "role") ?? "Contributor";
                var fileAs = GetRefinementValue(metadata.MetaItems, contributor.Id, "file-as");
                authors.Add(new Author(contributor.Name!, role, fileAs));
            }
        }

        if (authors.Count == 0)
            authors.Add(new Author("Unknown"));

        // Extract identifiers
        var identifiers = metadata.Identifiers?
            .Where(i => i?.Value != null)
            .Select(i =>
            {
                // Get scheme from meta refinements in EPUB 3
                var scheme = GetRefinementValue(metadata.MetaItems, i.Id, "identifier-type") ?? "Unknown";
                return new BookIdentifier(i.Value!, scheme);
            })
            .ToList() ?? new List<BookIdentifier>();

        // Extract language
        var languageCode = metadata.Languages?.FirstOrDefault() ?? "en";
        var language = new Language(languageCode);

        // Extract EPUB 3 specific metadata
        var modifiedDate = GetMetaPropertyValue(metadata.MetaItems, "dcterms:modified");
        var publicationDate = ParseDate(metadata.Date) ??
            (modifiedDate != null ? ParseDate(modifiedDate) : null);

        // Build custom metadata from EPUB 3 meta properties
        var customMetadata = new Dictionary<string, string>();
        if (metadata.MetaItems != null)
        {
            foreach (var meta in metadata.MetaItems.Where(m => m.Property != null && m.Content != null))
            {
                customMetadata[meta.Property] = meta.Content;
            }
        }

        var bookMetadata = new BookMetadata(
            publisher: metadata.Publisher,
            publicationDate: publicationDate,
            description: metadata.Description,
            rights: metadata.Rights,
            subject: string.Join(", ", metadata.Subjects ?? Array.Empty<string>()),
            coverage: metadata.Coverage,
            customMetadata: customMetadata
        );

        // Load chapters with EPUB 3 properties support
        var chapters = await LoadChaptersAsync(archive, package, contentDirectory, cancellationToken);

        return new Book(
            mainTitle,
            alternateTitles,
            authors,
            chapters,
            identifiers,
            language,
            bookMetadata
        );
    }

    private string? GetRefinementValue(MetaXml[]? metaItems, string? refinesId, string property)
    {
        if (metaItems == null || string.IsNullOrEmpty(refinesId))
            return null;

        return metaItems
            .FirstOrDefault(m => m.Refines == $"#{refinesId}" && m.Property?.EndsWith(property) == true)
            ?.Content;
    }

    private string? GetMetaPropertyValue(MetaXml[]? metaItems, string property)
    {
        if (metaItems == null)
            return null;

        return metaItems
            .FirstOrDefault(m => m.Property == property && string.IsNullOrEmpty(m.Refines))
            ?.Content;
    }

    private async Task<List<Chapter>> LoadChaptersAsync(
        ZipArchive archive,
        PackageXml package,
        string? contentDirectory,
        CancellationToken cancellationToken)
    {
        var chapters = new List<Chapter>();
        var manifestMap = package.ManifestItems?
            .Where(m => m.Id != null)
            .ToDictionary(m => m.Id!, m => m) ?? new Dictionary<string, ManifestItemXml>();

        // Find the navigation document (EPUB 3 specific)
        var navItem = package.ManifestItems?
            .FirstOrDefault(m => m.Properties?.Contains("nav") == true);

        if (package.SpineItemRefs == null || package.SpineItemRefs.Length == 0)
        {
            _logger.LogWarning("No spine items found, using manifest items instead");
            // Fallback to manifest items if no spine
            var order = 0;
            foreach (var item in package.ManifestItems ?? Array.Empty<ManifestItemXml>())
            {
                if ((item.MediaType?.Contains("html") == true || item.MediaType?.Contains("xhtml") == true)
                    && item.Href != null
                    && item != navItem) // Skip navigation document
                {
                    var chapter = await LoadChapterAsync(archive, item.Href, item.Id ?? $"chapter_{order}",
                        order++, contentDirectory, cancellationToken);
                    if (chapter != null)
                        chapters.Add(chapter);
                }
            }
        }
        else
        {
            // Load chapters in spine order
            var order = 0;
            foreach (var spineRef in package.SpineItemRefs)
            {
                if (spineRef.IdRef != null && manifestMap.TryGetValue(spineRef.IdRef, out var manifestItem))
                {
                    // Skip non-linear items if specified
                    if (spineRef.Linear == "no")
                    {
                        _logger.LogDebug("Skipping non-linear spine item: {Id}", spineRef.IdRef);
                        continue;
                    }

                    if (manifestItem.Href != null)
                    {
                        var chapter = await LoadChapterAsync(archive, manifestItem.Href, spineRef.IdRef,
                            order++, contentDirectory, cancellationToken);
                        if (chapter != null)
                            chapters.Add(chapter);
                    }
                }
            }
        }

        return chapters;
    }

    private async Task<Chapter?> LoadChapterAsync(
        ZipArchive archive,
        string href,
        string id,
        int order,
        string? contentDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            var entryPath = string.IsNullOrEmpty(contentDirectory)
                ? href
                : Path.Combine(contentDirectory, href).Replace('\\', '/');

            // Remove fragment identifier if present
            var fragmentIndex = entryPath.IndexOf('#');
            if (fragmentIndex >= 0)
            {
                entryPath = entryPath.Substring(0, fragmentIndex);
            }

            var entry = archive.GetEntry(entryPath);
            if (entry == null)
            {
                _logger.LogWarning("Chapter file not found: {Path}", entryPath);
                return null;
            }

            if (entry.Length > MaxChapterSize)
            {
                _logger.LogWarning("Chapter {Id} exceeds maximum size limit ({Size} bytes)", id, entry.Length);
                return null;
            }

            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(cancellationToken);

            // Extract title from content if possible
            var title = ExtractTitleFromHtml(content) ?? $"Chapter {order + 1}";

            return new Chapter(id, title, content, order, href);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chapter: {Href}", href);
            return null;
        }
    }

    private static string? ExtractTitleFromHtml(string html)
    {
        // Try to extract title from <title> tag
        var titleStart = html.IndexOf("<title>", StringComparison.OrdinalIgnoreCase);
        if (titleStart >= 0)
        {
            titleStart += 7;
            var titleEnd = html.IndexOf("</title>", titleStart, StringComparison.OrdinalIgnoreCase);
            if (titleEnd > titleStart)
            {
                return html.Substring(titleStart, titleEnd - titleStart).Trim();
            }
        }

        // Try to extract from first <h1> tag
        var h1Start = html.IndexOf("<h1", StringComparison.OrdinalIgnoreCase);
        if (h1Start >= 0)
        {
            var h1ContentStart = html.IndexOf('>', h1Start) + 1;
            var h1End = html.IndexOf("</h1>", h1ContentStart, StringComparison.OrdinalIgnoreCase);
            if (h1End > h1ContentStart)
            {
                var h1Content = html.Substring(h1ContentStart, h1End - h1ContentStart);
                // Remove any nested tags
                h1Content = System.Text.RegularExpressions.Regex.Replace(h1Content, "<.*?>", "").Trim();
                if (!string.IsNullOrWhiteSpace(h1Content))
                    return h1Content;
            }
        }

        return null;
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        // EPUB 3 uses W3C date format (ISO 8601)
        if (DateTime.TryParse(dateStr, out var date))
            return date;

        // Try parsing year only
        if (int.TryParse(dateStr, out var year) && year > 1000 && year < 3000)
            return new DateTime(year, 1, 1);

        return null;
    }

    private async Task<T> DeserializeXmlAsync<T>(ZipArchiveEntry entry, CancellationToken cancellationToken)
        where T : class
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var xmlContent = await reader.ReadToEndAsync(cancellationToken);

        var serializer = new XmlSerializer(typeof(T));
        using var stringReader = new StringReader(xmlContent);

        var result = serializer.Deserialize(stringReader) as T
            ?? throw new InvalidEpubStructureException($"Failed to deserialize {typeof(T).Name}", entry.FullName);

        return result;
    }
}