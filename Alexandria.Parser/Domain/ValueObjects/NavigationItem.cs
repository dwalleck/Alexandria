namespace Alexandria.Parser.Domain.ValueObjects;

/// <summary>
/// Represents a single navigation item in the table of contents
/// </summary>
public sealed class NavigationItem
{
    private readonly List<NavigationItem> _children;

    public NavigationItem(
        string id,
        string title,
        string? href,
        int playOrder,
        int level = 0,
        IEnumerable<NavigationItem>? children = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Navigation item ID cannot be empty", nameof(id));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Navigation item title cannot be empty", nameof(title));

        if (playOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(playOrder), "Play order must be non-negative");

        if (level < 0)
            throw new ArgumentOutOfRangeException(nameof(level), "Level must be non-negative");

        Id = id;
        Title = title;
        Href = href;
        PlayOrder = playOrder;
        Level = level;
        _children = children?.ToList() ?? [];

        // Validate children don't have invalid levels
        foreach (var child in _children)
        {
            if (child.Level <= level)
                throw new ArgumentException($"Child navigation item must have a level greater than parent level {level}", nameof(children));
        }
    }

    /// <summary>
    /// Unique identifier for the navigation item
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Display title for the navigation item
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Reference to the content (usually a chapter href)
    /// </summary>
    public string? Href { get; }

    /// <summary>
    /// Order in which this item appears in navigation
    /// </summary>
    public int PlayOrder { get; }

    /// <summary>
    /// Nesting level (0 for root items)
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// Child navigation items
    /// </summary>
    public IReadOnlyList<NavigationItem> Children => _children.AsReadOnly();

    /// <summary>
    /// Indicates if this item has children
    /// </summary>
    public bool HasChildren => _children.Count > 0;

    /// <summary>
    /// Find a navigation item by its ID recursively
    /// </summary>
    public NavigationItem? FindById(string id)
    {
        if (Id == id)
            return this;

        foreach (var child in _children)
        {
            var found = child.FindById(id);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Find a navigation item by its href recursively
    /// </summary>
    public NavigationItem? FindByHref(string href)
    {
        if (Href == href)
            return this;

        foreach (var child in _children)
        {
            var found = child.FindByHref(href);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Get all navigation items as a flat list
    /// </summary>
    public IEnumerable<NavigationItem> Flatten()
    {
        yield return this;
        foreach (var child in _children)
        {
            foreach (var item in child.Flatten())
            {
                yield return item;
            }
        }
    }

    public override bool Equals(object? obj)
    {
        return obj is NavigationItem other &&
               Id == other.Id &&
               Title == other.Title &&
               Href == other.Href &&
               PlayOrder == other.PlayOrder &&
               Level == other.Level;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Title, Href, PlayOrder, Level);
    }
}