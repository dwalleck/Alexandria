namespace Alexandria.Parser.Domain.ValueObjects;

/// <summary>
/// Value object representing a book author
/// </summary>
public sealed record Author
{
    public Author(string name, string? role = null, string? fileAs = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Author name cannot be empty", nameof(name));

        Name = name.Trim();
        Role = role?.Trim();
        FileAs = fileAs?.Trim();
    }

    public string Name { get; }
    public string? Role { get; }
    public string? FileAs { get; }

    public override string ToString() => !string.IsNullOrEmpty(Role) ? $"{Name} ({Role})" : Name;

    /// <summary>
    /// Gets the last name (assumes last word is last name)
    /// </summary>
    public string GetLastName()
    {
        var parts = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : Name;
    }

    /// <summary>
    /// Gets the first name (all but last word)
    /// </summary>
    public string GetFirstName()
    {
        var parts = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
            return Name;
        return string.Join(" ", parts.Take(parts.Length - 1));
    }

    /// <summary>
    /// Gets the first initial
    /// </summary>
    public string GetFirstInitial()
    {
        var firstName = GetFirstName();
        return firstName.Length > 0 ? firstName[0].ToString().ToUpper() : "";
    }
}