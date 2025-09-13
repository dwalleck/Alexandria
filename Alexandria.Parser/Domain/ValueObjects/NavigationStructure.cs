namespace Alexandria.Parser.Domain.ValueObjects;

/// <summary>
/// Represents the complete navigation structure (table of contents) of an EPUB
/// </summary>
public sealed class NavigationStructure
{
    private readonly List<NavigationItem> _items;

    public NavigationStructure(
        string? title,
        IEnumerable<NavigationItem> items,
        string? tocNcxPath = null,
        string? navPath = null)
    {
        _items = items?.ToList() ?? throw new ArgumentNullException(nameof(items));

        if (_items.Count == 0)
            throw new ArgumentException("Navigation structure must have at least one item", nameof(items));

        // Validate all root items have level 0
        foreach (var item in _items)
        {
            if (item.Level != 0)
                throw new ArgumentException("Root navigation items must have level 0", nameof(items));
        }

        Title = title ?? "Table of Contents";
        TocNcxPath = tocNcxPath;
        NavPath = navPath;
    }

    /// <summary>
    /// Title of the navigation structure
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Path to toc.ncx file (EPUB 2)
    /// </summary>
    public string? TocNcxPath { get; }

    /// <summary>
    /// Path to nav.xhtml file (EPUB 3)
    /// </summary>
    public string? NavPath { get; }

    /// <summary>
    /// Root navigation items
    /// </summary>
    public IReadOnlyList<NavigationItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Total count of all navigation items (including nested)
    /// </summary>
    public int TotalItemCount => GetAllItems().Count();

    /// <summary>
    /// Maximum nesting depth in the navigation structure
    /// </summary>
    public int MaxDepth => GetAllItems().Max(item => item.Level) + 1;

    /// <summary>
    /// Find a navigation item by its ID
    /// </summary>
    public NavigationItem? FindById(string id)
    {
        foreach (var item in _items)
        {
            var found = item.FindById(id);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Find a navigation item by its href
    /// </summary>
    public NavigationItem? FindByHref(string href)
    {
        foreach (var item in _items)
        {
            var found = item.FindByHref(href);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Get all navigation items as a flat list
    /// </summary>
    public IEnumerable<NavigationItem> GetAllItems()
    {
        foreach (var item in _items)
        {
            foreach (var flatItem in item.Flatten())
            {
                yield return flatItem;
            }
        }
    }

    /// <summary>
    /// Get navigation items at a specific level
    /// </summary>
    public IEnumerable<NavigationItem> GetItemsAtLevel(int level)
    {
        if (level < 0)
            throw new ArgumentOutOfRangeException(nameof(level), "Level must be non-negative");

        return GetAllItems().Where(item => item.Level == level);
    }

    /// <summary>
    /// Get the navigation path to a specific item
    /// </summary>
    public IEnumerable<NavigationItem>? GetPathToItem(string itemId)
    {
        foreach (var root in _items)
        {
            var path = GetPathToItemRecursive(root, itemId);
            if (path != null)
                return path;
        }
        return null;
    }

    private List<NavigationItem>? GetPathToItemRecursive(NavigationItem current, string targetId)
    {
        if (current.Id == targetId)
            return [current];

        foreach (var child in current.Children)
        {
            var childPath = GetPathToItemRecursive(child, targetId);
            if (childPath != null)
            {
                childPath.Insert(0, current);
                return childPath;
            }
        }

        return null;
    }

    /// <summary>
    /// Check if the navigation structure is valid for EPUB 2
    /// </summary>
    public bool IsValidEpub2Structure()
    {
        return !string.IsNullOrEmpty(TocNcxPath) && _items.Count > 0;
    }

    /// <summary>
    /// Check if the navigation structure is valid for EPUB 3
    /// </summary>
    public bool IsValidEpub3Structure()
    {
        return !string.IsNullOrEmpty(NavPath) && _items.Count > 0;
    }

    public override bool Equals(object? obj)
    {
        return obj is NavigationStructure other &&
               Title == other.Title &&
               TocNcxPath == other.TocNcxPath &&
               NavPath == other.NavPath &&
               _items.SequenceEqual(other._items);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Title, TocNcxPath, NavPath, _items.Count);
    }
}