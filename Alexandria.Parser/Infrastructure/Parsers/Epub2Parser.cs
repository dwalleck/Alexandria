using System.IO.Compression;
using System.Xml.Serialization;
using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.Errors;
using Alexandria.Parser.Domain.Exceptions;
using Alexandria.Parser.Domain.Interfaces;
using Alexandria.Parser.Domain.ValueObjects;
using Alexandria.Parser.Infrastructure.Parsers.Models.Epub2;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Alexandria.Parser.Infrastructure.Parsers;

/// <summary>
/// Implementation of EPUB 2 parser with proper error handling and streaming support
/// </summary>
public sealed class Epub2Parser : IEpubParser
{
    private readonly ILogger<Epub2Parser> _logger;
    private const string ContainerPath = "META-INF/container.xml";
    private const int MaxChapterSize = 10 * 1024 * 1024; // 10MB max per chapter

    public Epub2Parser(ILogger<Epub2Parser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OneOf<Book, ParsingError>> ParseAsync(Stream epubStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(epubStream);

        try
        {
            using var archive = new ZipArchive(epubStream, ZipArchiveMode.Read, leaveOpen: true);

            // Step 1: Parse container.xml
            var containerEntry = archive.GetEntry(ContainerPath)
                ?? throw new InvalidEpubStructureException("container.xml", "EPUB archive");

            var container = await DeserializeXmlAsync<ContainerXml>(containerEntry, cancellationToken);

            if (container?.Rootfiles?.Length == 0)
                throw new InvalidEpubStructureException("No rootfiles found in container.xml", "EPUB archive");

            var contentPath = container!.Rootfiles![0].FullPath
                ?? throw new InvalidEpubStructureException("Rootfile path is missing", "EPUB archive");

            // Step 2: Parse content.opf
            var contentEntry = archive.GetEntry(contentPath)
                ?? throw new InvalidEpubStructureException($"Content file not found: {contentPath}", "EPUB archive");

            var package = await DeserializeXmlAsync<PackageXml>(contentEntry, cancellationToken);

            if (package?.Metadata == null)
                throw new InvalidEpubStructureException("Package metadata is missing", contentPath);

            // Step 3: Build domain model
            var book = await BuildBookAsync(archive, package, Path.GetDirectoryName(contentPath), cancellationToken);

            _logger.LogInformation("Successfully parsed EPUB with {ChapterCount} chapters", book.Chapters.Count);

            return book;
        }
        catch (InvalidEpubStructureException ex)
        {
            _logger.LogError(ex, "Invalid EPUB structure");
            return new InvalidStructureError(ex.MissingComponent ?? "Unknown", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse EPUB stream");
            return new ParsingFailedError(ex.Message, ex);
        }
    }

    public async Task<OneOf<Success, ValidationError>> ValidateAsync(Stream epubStream, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        try
        {
            using var archive = new ZipArchive(epubStream, ZipArchiveMode.Read, leaveOpen: true);

            // Check for container.xml
            if (archive.GetEntry(ContainerPath) == null)
            {
                errors.Add("Missing META-INF/container.xml");
                return new ValidationError(errors);
            }

            // Try to parse the EPUB
            var parseResult = await ParseAsync(epubStream, cancellationToken);

            if (parseResult.IsT1) // ParsingError
            {
                errors.Add(parseResult.AsT1.Message);
                return new ValidationError(errors);
            }

            return Success.Instance;
        }
        catch (InvalidEpubStructureException ex)
        {
            errors.Add(ex.Message);
        }
        catch (Exception ex)
        {
            errors.Add($"Validation failed: {ex.Message}");
        }

        return new ValidationError(errors);
    }

    private async Task<Book> BuildBookAsync(
        ZipArchive archive,
        PackageXml package,
        string? contentDirectory,
        CancellationToken cancellationToken)
    {
        var metadata = package.Metadata!;

        // Extract titles
        var titles = metadata.Titles?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? [];
        if (titles.Count == 0)
            throw new InvalidEpubStructureException("No title found in metadata", "content.opf");

        var mainTitle = new BookTitle(titles[0]);
        var alternateTitles = titles.Skip(1).Select(t => new BookTitle(t)).ToList();

        // Extract authors
        var authors = metadata.Authors?
            .Where(a => a?.Name != null)
            .Select(a => new Author(a.Name!, a.Role, a.FileAs))
            .ToList() ?? [new Author("Unknown")];

        // Extract identifiers
        var identifiers = metadata.Identifiers?
            .Where(i => i?.Value != null && i.Scheme != null)
            .Select(i => new BookIdentifier(i.Value!, i.Scheme!))
            .ToList() ?? [];

        // Extract language
        var languageCode = metadata.Languages?.FirstOrDefault() ?? "en";
        var language = new Language(languageCode);

        // Extract additional metadata
        var bookMetadata = new BookMetadata(
            publisher: metadata.Publisher,
            publicationDate: ParseDate(metadata.Date),
            description: metadata.Description,
            rights: metadata.Rights,
            subject: metadata.Subject,
            customMetadata: ExtractCustomMetadata(metadata.MetaItems)
        );

        // Load chapters based on spine order
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

    private async Task<List<Chapter>> LoadChaptersAsync(
        ZipArchive archive,
        PackageXml package,
        string? contentDirectory,
        CancellationToken cancellationToken)
    {
        var chapters = new List<Chapter>();
        var manifestMap = package.ManifestItems?
            .Where(m => m.Id != null)
            .ToDictionary(m => m.Id!, m => m) ?? [];

        if (package.SpineItemRefs == null || package.SpineItemRefs.Length == 0)
        {
            _logger.LogWarning("No spine items found, using manifest items instead");
            // Fallback to manifest items if no spine
            var order = 0;
            foreach (var item in package.ManifestItems ?? Array.Empty<ManifestItemXml>())
            {
                if (item.MediaType?.Contains("html") == true && item.Href != null)
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

            // Extract title from content if possible (simple approach)
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
        // Simple title extraction - could be improved with proper HTML parsing
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
        return null;
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        if (DateTime.TryParse(dateStr, out var date))
            return date;

        // Try parsing year only
        if (int.TryParse(dateStr, out var year) && year > 1000 && year < 3000)
            return new DateTime(year, 1, 1);

        return null;
    }

    private static Dictionary<string, string> ExtractCustomMetadata(MetaItemXml[]? metaItems)
    {
        var result = new Dictionary<string, string>();

        if (metaItems == null)
            return result;

        foreach (var item in metaItems)
        {
            if (!string.IsNullOrWhiteSpace(item.Name) && !string.IsNullOrWhiteSpace(item.Content))
            {
                result[item.Name] = item.Content;
            }
        }

        return result;
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