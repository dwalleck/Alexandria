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

    public override string ToString() => Name;
}