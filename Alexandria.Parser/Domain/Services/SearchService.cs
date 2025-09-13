using System.Text.RegularExpressions;
using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.ValueObjects;

namespace Alexandria.Parser.Domain.Services;

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
        if (book == null)
            throw new ArgumentNullException(nameof(book));

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
    /// Searches for multiple terms (AND operation)
    /// </summary>
    public IEnumerable<SearchResult> SearchAll(Book book, IEnumerable<string> searchTerms, SearchOptions? options = null)
    {
        if (book == null)
            throw new ArgumentNullException(nameof(book));

        var terms = searchTerms?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (terms == null || !terms.Any())
            return Enumerable.Empty<SearchResult>();

        options ??= new SearchOptions();
        var results = new List<SearchResult>();

        foreach (var chapter in book.Chapters)
        {
            var plainText = _contentProcessor.ExtractPlainText(chapter.Content);

            // Check if all terms are present
            var allTermsPresent = terms.All(term =>
                plainText.Contains(term, options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));

            if (allTermsPresent)
            {
                // Calculate combined score
                var score = 0;
                var matches = new List<SearchMatch>();

                foreach (var term in terms)
                {
                    var termMatches = FindMatches(plainText, term, options);
                    matches.AddRange(termMatches);
                    score += termMatches.Count;
                }

                if (matches.Any())
                {
                    var snippet = _contentProcessor.ExtractSnippet(chapter.Content, terms.First(), options.SnippetLength);
                    results.Add(new SearchResult(chapter, matches, score, snippet));
                }
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
        if (book == null)
            throw new ArgumentNullException(nameof(book));

        var terms = searchTerms?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (terms == null || !terms.Any())
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
        if (book == null)
            throw new ArgumentNullException(nameof(book));

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

        if (matches.Any())
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
        Matches = matches?.ToList() ?? new List<SearchMatch>();
        Score = score;
        Snippet = snippet ?? string.Empty;
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