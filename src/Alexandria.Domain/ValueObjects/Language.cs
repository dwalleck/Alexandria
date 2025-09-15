using System;
using System.Collections.Generic;

namespace Alexandria.Domain.ValueObjects;

/// <summary>
/// Value object representing a language code
/// </summary>
public sealed record Language
{
    private static readonly HashSet<string> CommonLanguageCodes =
    [
        "EN", "ES", "FR", "DE", "IT", "PT", "RU", "ZH", "JA", "KO", "AR", "HI"
    ];

    public Language(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Language code cannot be empty", nameof(code));

        if (code.Length < 2)
            throw new ArgumentException("Language code must be at least 2 characters", nameof(code));

        Code = code.Trim().ToUpperInvariant();
    }

    public string Code { get; }

    public bool IsCommonLanguage => CommonLanguageCodes.Contains(Code.Substring(0, 2));

    public override string ToString() => Code;

    public static Language English => new("EN");
    public static Language Spanish => new("ES");
    public static Language French => new("FR");
}