namespace Alexandria.Parser.Domain.ValueObjects;

/// <summary>
/// Value object representing a book identifier (ISBN, UUID, etc.)
/// </summary>
public sealed record BookIdentifier
{
    public BookIdentifier(string value, string scheme)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Identifier value cannot be empty", nameof(value));

        if (string.IsNullOrWhiteSpace(scheme))
            throw new ArgumentException("Identifier scheme cannot be empty", nameof(scheme));

        Value = value.Trim();
        Scheme = scheme.Trim().ToUpperInvariant();
    }

    public string Value { get; }
    public string Scheme { get; }

    public bool IsIsbn => Scheme == "ISBN";
    public bool IsUuid => Scheme == "UUID";
    public bool IsDoi => Scheme == "DOI";

    public override string ToString() => $"{Scheme}:{Value}";
}