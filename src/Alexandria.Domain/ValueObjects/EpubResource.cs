using System;
using System.Threading.Tasks;

using System.IO;
namespace Alexandria.Domain.ValueObjects;

/// <summary>
/// Represents a resource (file) within an EPUB archive
/// </summary>
public class EpubResource
{
    private byte[]? _content;
    private readonly Lazy<byte[]> _lazyContent;

    public EpubResource(
        string id,
        string href,
        string mediaType,
        byte[]? content = null,
        Func<byte[]>? contentLoader = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Resource ID cannot be empty", nameof(id));

        if (string.IsNullOrWhiteSpace(href))
            throw new ArgumentException("Resource href cannot be empty", nameof(href));

        if (string.IsNullOrWhiteSpace(mediaType))
            throw new ArgumentException("Resource media type cannot be empty", nameof(mediaType));

        if (content == null && contentLoader == null)
            throw new ArgumentException("Either content or contentLoader must be provided");

        Id = id;
        Href = href;
        MediaType = mediaType;
        _content = content;

        // Use lazy loading if a loader is provided
        _lazyContent = contentLoader != null
            ? new Lazy<byte[]>(contentLoader)
            : new Lazy<byte[]>(() => _content ?? Array.Empty<byte>());
    }

    /// <summary>
    /// Unique identifier for the resource (from manifest)
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Relative path to the resource within the EPUB
    /// </summary>
    public string Href { get; }

    /// <summary>
    /// MIME type of the resource
    /// </summary>
    public string MediaType { get; }

    /// <summary>
    /// File name extracted from href
    /// </summary>
    public string FileName => Path.GetFileName(Href);

    /// <summary>
    /// File extension extracted from href
    /// </summary>
    public string FileExtension => Path.GetExtension(Href);

    /// <summary>
    /// Size of the resource in bytes
    /// </summary>
    public int Size => Content.Length;

    /// <summary>
    /// The actual content of the resource (loaded on demand if using lazy loading)
    /// </summary>
    public byte[] Content
    {
        get
        {
            if (_content != null)
                return _content;

            _content = _lazyContent.Value;
            return _content;
        }
    }

    /// <summary>
    /// Indicates if the content has been loaded
    /// </summary>
    public bool IsContentLoaded => _content != null || _lazyContent.IsValueCreated;

    /// <summary>
    /// Checks if this resource is an image based on media type
    /// </summary>
    public bool IsImage()
    {
        return MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if this resource is a stylesheet
    /// </summary>
    public bool IsStylesheet()
    {
        return MediaType.Equals("text/css", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if this resource is a font
    /// </summary>
    public bool IsFont()
    {
        return MediaType.StartsWith("font/", StringComparison.OrdinalIgnoreCase) ||
               MediaType.StartsWith("application/font", StringComparison.OrdinalIgnoreCase) ||
               MediaType.Equals("application/vnd.ms-opentype", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if this resource is HTML/XHTML content
    /// </summary>
    public bool IsHtmlContent()
    {
        return MediaType.Contains("html", StringComparison.OrdinalIgnoreCase) ||
               MediaType.Contains("xml", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Save the resource to a file
    /// </summary>
    public async Task SaveToFileAsync(string filePath)
    {
        await File.WriteAllBytesAsync(filePath, Content);
    }

    /// <summary>
    /// Save the resource to a stream
    /// </summary>
    public async Task SaveToStreamAsync(Stream stream)
    {
        await stream.WriteAsync(Content, 0, Content.Length);
    }

    /// <summary>
    /// Get the content as a string (assumes UTF-8 encoding)
    /// </summary>
    public string GetContentAsString()
    {
        return System.Text.Encoding.UTF8.GetString(Content);
    }

    public override bool Equals(object? obj)
    {
        return obj is EpubResource other &&
               Id == other.Id &&
               Href == other.Href &&
               MediaType == other.MediaType;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Href, MediaType);
    }

    public override string ToString()
    {
        return $"{FileName} ({MediaType}) [{Size:N0} bytes]";
    }
}