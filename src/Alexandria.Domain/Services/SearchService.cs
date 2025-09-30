using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Alexandria.Domain.Entities;

namespace Alexandria.Domain.Services;

/// <summary>
/// Provides full-text search capabilities for books
/// </summary>
public sealed class SearchService
{
    private readonly ContentProcessor _contentProcessor;

    public SearchService(ContentProcessor contentProcessor)
    {
        _contentProcessor = contentProcessor ?? throw new ArgumentNullException(nameof(contentProcessor));
    }

    /// <summary>
    /// Searches for a term across all chapters in a book
    /// </summary>
    public IEnumerable<SearchResult> Search(Book book, string searchTerm, SearchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(book);

        if (string.IsNullOrWhiteSpace(searchTerm))
            return Enumerable.Empty<SearchResult>();

        options ??= new SearchOptions();
        var results = new List<SearchResult>();

        foreach (var chapter in book.Chapters)
        {
            var chapterResults = SearchInChapter(chapter, searchTerm, options);
            results.AddRange(chapterResults);
        }

        // Sort by relevance score
        return results.OrderByDescending(r => r.Score)
                     .ThenBy(r => r.Chapter.Order);
    }

    /// <summary>
    /// Searches for multiple terms (AND operation).
    /// Defaults to whole-word matching when no options are provided to prevent partial matches.
    /// Explicit options settings are respected.
    /// </summary>
    public IEnumerable<SearchResult> SearchAll(Book book, IEnumerable<string> searchTerms, SearchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(book);

        var terms = searchTerms?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (terms == null || terms.Count == 0)
            return Enumerable.Empty<SearchResult>();

        // Default to whole-word matching for multi-term AND searches when no options provided
        // This prevents partial matches (e.g., "fox" matching "foxes")
        options ??= new SearchOptions { WholeWord = true };

        var results = new List<SearchResult>();

        foreach (var chapter in book.Chapters)
        {
            var plainText = _contentProcessor.ExtractPlainText(chapter.Content);

            // Find matches for each term and check if all terms have at least one match
            var allMatches = new List<SearchMatch>();
            var termMatchCounts = new Dictionary<string, int>();
            var score = 0;

            foreach (var term in terms)
            {
                var termMatches = FindMatches(plainText, term, options);
                termMatchCounts[term] = termMatches.Count;
                allMatches.AddRange(termMatches);
                score += termMatches.Count;
            }

            // Only include result if ALL terms have at least one match
            var allTermsMatched = termMatchCounts.Values.All(count => count > 0);

            if (allTermsMatched && allMatches.Count != 0)
            {
                var snippet = _contentProcessor.ExtractSnippet(chapter.Content, terms.First(), options.SnippetLength);
                results.Add(new SearchResult(chapter, allMatches, score, snippet));
            }
        }

        return results.OrderByDescending(r => r.Score)
                     .ThenBy(r => r.Chapter.Order);
    }

    /// <summary>
    /// Searches for any of the terms (OR operation)
    /// </summary>
    public IEnumerable<SearchResult> SearchAny(Book book, IEnumerable<string> searchTerms, SearchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(book);

        var terms = searchTerms?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (terms == null || terms.Count == 0)
            return Enumerable.Empty<SearchResult>();

        options ??= new SearchOptions();
        var results = new Dictionary<string, SearchResult>(); // Use chapter ID as key

        foreach (var term in terms)
        {
            var termResults = Search(book, term, options);

            foreach (var result in termResults)
            {
                if (results.ContainsKey(result.Chapter.Id))
                {
                    // Merge results for the same chapter
                    var existing = results[result.Chapter.Id];
                    var mergedMatches = existing.Matches.Concat(result.Matches).ToList();
                    var mergedScore = existing.Score + result.Score;
                    results[result.Chapter.Id] = new SearchResult(result.Chapter, mergedMatches, mergedScore, result.Snippet);
                }
                else
                {
                    results[result.Chapter.Id] = result;
                }
            }
        }

        return results.Values.OrderByDescending(r => r.Score)
                            .ThenBy(r => r.Chapter.Order);
    }

    /// <summary>
    /// Searches using a regular expression pattern
    /// </summary>
    public IEnumerable<SearchResult> SearchRegex(Book book, string pattern, SearchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(book);

        if (string.IsNullOrWhiteSpace(pattern))
            return Enumerable.Empty<SearchResult>();

        options ??= new SearchOptions();
        var results = new List<SearchResult>();

        try
        {
            var regexOptions = options.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            var regex = new Regex(pattern, regexOptions | RegexOptions.Compiled);

            foreach (var chapter in book.Chapters)
            {
                var plainText = _contentProcessor.ExtractPlainText(chapter.Content);
                var matches = regex.Matches(plainText);

                if (matches.Count > 0)
                {
                    var searchMatches = matches.Cast<Match>()
                        .Select(m => new SearchMatch(m.Index, m.Length, m.Value))
                        .Take(options.MaxMatchesPerChapter)
                        .ToList();

                    var snippet = matches.Count > 0
                        ? _contentProcessor.ExtractSnippet(chapter.Content, matches[0].Value, options.SnippetLength)
                        : string.Empty;

                    results.Add(new SearchResult(chapter, searchMatches, matches.Count, snippet));
                }
            }
        }
        catch (ArgumentException)
        {
            // Invalid regex pattern
            return Enumerable.Empty<SearchResult>();
        }

        return results.OrderByDescending(r => r.Score)
                     .ThenBy(r => r.Chapter.Order);
    }

    private IEnumerable<SearchResult> SearchInChapter(Chapter chapter, string searchTerm, SearchOptions options)
    {
        var plainText = _contentProcessor.ExtractPlainText(chapter.Content);
        var matches = FindMatches(plainText, searchTerm, options);

        if (matches.Count != 0)
        {
            var snippet = _contentProcessor.ExtractSnippet(chapter.Content, searchTerm, options.SnippetLength);
            yield return new SearchResult(chapter, matches, matches.Count, snippet);
        }
    }

    private List<SearchMatch> FindMatches(string text, string searchTerm, SearchOptions options)
    {
        var matches = new List<SearchMatch>();
        var comparison = options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        if (options.WholeWord)
        {
            // Use regex for whole word matching
            var pattern = $@"\b{Regex.Escape(searchTerm)}\b";
            var regexOptions = options.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            var regex = new Regex(pattern, regexOptions);

            foreach (Match match in regex.Matches(text))
            {
                matches.Add(new SearchMatch(match.Index, match.Length, match.Value));
                if (matches.Count >= options.MaxMatchesPerChapter)
                    break;
            }
        }
        else
        {
            // Simple substring search
            var index = 0;
            while ((index = text.IndexOf(searchTerm, index, comparison)) != -1)
            {
                matches.Add(new SearchMatch(index, searchTerm.Length,
                    text.Substring(index, searchTerm.Length)));

                index += searchTerm.Length;

                if (matches.Count >= options.MaxMatchesPerChapter)
                    break;
            }
        }

        return matches;
    }
}

