using System;
using System.Collections.Generic;
using System.Linq;
using Alexandria.Domain.Entities;

namespace Alexandria.Domain.Services;

/// <summary>
/// Represents a search result for a chapter
/// </summary>
public sealed class SearchResult
{
    public Chapter Chapter { get; }
    public IReadOnlyList<SearchMatch> Matches { get; }
    public int Score { get; }
    public string Snippet { get; }

    public SearchResult(Chapter chapter, IEnumerable<SearchMatch> matches, int score, string snippet)
    {
        Chapter = chapter ?? throw new ArgumentNullException(nameof(chapter));
        Matches = matches?.ToList() ?? [];
        Score = score;
        Snippet = snippet ?? string.Empty;
    }
}

/// <summary>
/// Represents a search match within text
/// </summary>
public sealed class SearchMatch
{
    public int Position { get; }
    public int Length { get; }
    public string Text { get; }

    public SearchMatch(int position, int length, string text)
    {
        Position = position;
        Length = length;
        Text = text ?? string.Empty;
    }
}

/// <summary>
/// Options for searching
/// </summary>
public sealed class SearchOptions
{
    public bool CaseSensitive { get; set; } = false;
    public bool WholeWord { get; set; } = false;
    public int MaxMatchesPerChapter { get; set; } = 10;
    public int SnippetLength { get; set; } = 100;
}