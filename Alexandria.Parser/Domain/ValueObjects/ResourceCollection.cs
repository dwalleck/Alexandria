namespace Alexandria.Parser.Domain.ValueObjects;

/// <summary>
/// Immutable collection of EPUB resources
/// </summary>
public sealed class ResourceCollection
{
    private readonly Dictionary<string, EpubResource> _resourcesById;
    private readonly Dictionary<string, EpubResource> _resourcesByHref;
    private ImageResource? _coverImage;

    public ResourceCollection(IEnumerable<EpubResource> resources)
    {
        var resourceList = resources?.ToList() ?? throw new ArgumentNullException(nameof(resources));

        _resourcesById = new Dictionary<string, EpubResource>(StringComparer.OrdinalIgnoreCase);
        _resourcesByHref = new Dictionary<string, EpubResource>(StringComparer.OrdinalIgnoreCase);

        foreach (var resource in resourceList)
        {
            if (_resourcesById.ContainsKey(resource.Id))
                throw new ArgumentException($"Duplicate resource ID: {resource.Id}", nameof(resources));

            _resourcesById[resource.Id] = resource;
            _resourcesByHref[resource.Href] = resource;

            // Check if this is the cover image
            if (resource is ImageResource imageResource && imageResource.IsCoverImage)
            {
                if (_coverImage != null)
                    throw new ArgumentException("Multiple cover images found", nameof(resources));
                _coverImage = imageResource;
            }
        }
    }

    /// <summary>
    /// Total number of resources
    /// </summary>
    public int Count => _resourcesById.Count;

    /// <summary>
    /// All resources in the collection
    /// </summary>
    public IReadOnlyCollection<EpubResource> All => _resourcesById.Values;

    /// <summary>
    /// The cover image if available
    /// </summary>
    public ImageResource? CoverImage => _coverImage;

    /// <summary>
    /// Get a resource by its ID
    /// </summary>
    public EpubResource? GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return _resourcesById.TryGetValue(id, out var resource) ? resource : null;
    }

    /// <summary>
    /// Get a resource by its href
    /// </summary>
    public EpubResource? GetByHref(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return null;

        // Try exact match first
        if (_resourcesByHref.TryGetValue(href, out var resource))
            return resource;

        // Try without fragment
        var hrefWithoutFragment = href.Split('#')[0];
        return _resourcesByHref.TryGetValue(hrefWithoutFragment, out resource) ? resource : null;
    }

    /// <summary>
    /// Get all image resources
    /// </summary>
    public IEnumerable<ImageResource> GetImages()
    {
        return _resourcesById.Values.OfType<ImageResource>();
    }

    /// <summary>
    /// Get all resources that are images (including non-ImageResource objects)
    /// </summary>
    public IEnumerable<EpubResource> GetImageResources()
    {
        return _resourcesById.Values.Where(r => r.IsImage());
    }

    /// <summary>
    /// Get all stylesheet resources
    /// </summary>
    public IEnumerable<EpubResource> GetStylesheets()
    {
        return _resourcesById.Values.Where(r => r.IsStylesheet());
    }

    /// <summary>
    /// Get all font resources
    /// </summary>
    public IEnumerable<EpubResource> GetFonts()
    {
        return _resourcesById.Values.Where(r => r.IsFont());
    }

    /// <summary>
    /// Get resources by media type
    /// </summary>
    public IEnumerable<EpubResource> GetByMediaType(string mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
            return Enumerable.Empty<EpubResource>();

        return _resourcesById.Values.Where(r =>
            r.MediaType.Equals(mediaType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get resources by file extension
    /// </summary>
    public IEnumerable<EpubResource> GetByExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return Enumerable.Empty<EpubResource>();

        if (!extension.StartsWith("."))
            extension = "." + extension;

        return _resourcesById.Values.Where(r =>
            r.FileExtension.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extract all resources to a directory
    /// </summary>
    public async Task ExtractAllToDirectoryAsync(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        foreach (var resource in _resourcesById.Values)
        {
            var resourcePath = Path.Combine(directoryPath, resource.Href.Replace('/', Path.DirectorySeparatorChar));
            var resourceDir = Path.GetDirectoryName(resourcePath);

            if (!string.IsNullOrEmpty(resourceDir) && !Directory.Exists(resourceDir))
                Directory.CreateDirectory(resourceDir);

            await resource.SaveToFileAsync(resourcePath);
        }
    }

    /// <summary>
    /// Calculate total size of all resources
    /// </summary>
    public long GetTotalSize()
    {
        return _resourcesById.Values.Sum(r => (long)r.Size);
    }

    /// <summary>
    /// Check if a resource exists by ID
    /// </summary>
    public bool ContainsId(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _resourcesById.ContainsKey(id);
    }

    /// <summary>
    /// Check if a resource exists by href
    /// </summary>
    public bool ContainsHref(string href)
    {
        return !string.IsNullOrWhiteSpace(href) && _resourcesByHref.ContainsKey(href);
    }

    /// <summary>
    /// Create an empty resource collection
    /// </summary>
    public static ResourceCollection Empty => new(Enumerable.Empty<EpubResource>());
}