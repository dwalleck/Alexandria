using System;

namespace Alexandria.Domain.ValueObjects;

/// <summary>
/// Value object representing a book title
/// </summary>
public sealed record BookTitle
{
    public BookTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Book title cannot be empty", nameof(value));

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;

    public static implicit operator string(BookTitle title) => title.Value;
    public static explicit operator BookTitle(string value) => new(value);
}