using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Net;

namespace Alexandria.Domain.Services;

/// <summary>
/// Provides content processing capabilities for EPUB chapters
/// </summary>
public sealed class ContentProcessor
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex ParagraphRegex = new(@"<p[^>]*>(.*?)</p>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex HeadingRegex = new(@"<h[1-6][^>]*>(.*?)</h[1-6]>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex ListItemRegex = new(@"<li[^>]*>(.*?)</li>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex BreakRegex = new(@"<br\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extracts plain text from HTML content
    /// </summary>
    public string ExtractPlainText(string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
            return string.Empty;

        // Decode HTML entities first
        var text = WebUtility.HtmlDecode(htmlContent);

        // Add line breaks for block elements
        text = ParagraphRegex.Replace(text, "$1\n\n");
        text = HeadingRegex.Replace(text, "\n$1\n\n");
        text = ListItemRegex.Replace(text, "• $1\n");
        text = BreakRegex.Replace(text, "\n");

        // Remove remaining HTML tags
        text = HtmlTagRegex.Replace(text, " ");

        // Normalize whitespace
        text = WhitespaceRegex.Replace(text, " ");

        // Clean up extra line breaks
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        return text.Trim();
    }

    /// <summary>
    /// Counts words in HTML content
    /// </summary>
    public int CountWords(string htmlContent)
    {
        var plainText = ExtractPlainText(htmlContent);
        if (string.IsNullOrWhiteSpace(plainText))
            return 0;

        // Split on whitespace and punctuation
        var words = Regex.Split(plainText, @"\W+")
            .Where(w => !string.IsNullOrWhiteSpace(w) && w.Length > 0);

        return words.Count();
    }

    /// <summary>
    /// Extracts a snippet of text around a search term
    /// </summary>
    public string ExtractSnippet(string htmlContent, string searchTerm, int contextLength = 100)
    {
        var plainText = ExtractPlainText(htmlContent);
        if (string.IsNullOrWhiteSpace(plainText) || string.IsNullOrWhiteSpace(searchTerm))
            return string.Empty;

        var index = plainText.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
        if (index == -1)
            return string.Empty;

        var start = Math.Max(0, index - contextLength);
        var end = Math.Min(plainText.Length, index + searchTerm.Length + contextLength);

        var snippet = plainText.Substring(start, end - start);

        // Add ellipsis if truncated
        if (start > 0)
            snippet = "..." + snippet;
        if (end < plainText.Length)
            snippet += "...";

        return snippet;
    }

    /// <summary>
    /// Highlights search terms in plain text
    /// </summary>
    public string HighlightTerms(string text, IEnumerable<string> searchTerms, string highlightStart = "**", string highlightEnd = "**")
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var result = text;
        foreach (var term in searchTerms.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            var pattern = $@"\b{Regex.Escape(term)}\b";
            result = Regex.Replace(result, pattern, $"{highlightStart}$0{highlightEnd}", RegexOptions.IgnoreCase);
        }

        return result;
    }

    /// <summary>
    /// Splits content into sentences
    /// </summary>
    public IEnumerable<string> ExtractSentences(string htmlContent)
    {
        var plainText = ExtractPlainText(htmlContent);
        if (string.IsNullOrWhiteSpace(plainText))
            return Enumerable.Empty<string>();

        // Split on sentence endings
        var sentences = Regex.Split(plainText, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim());

        return sentences;
    }

    /// <summary>
    /// Estimates reading time based on average reading speed
    /// </summary>
    public TimeSpan EstimateReadingTime(string htmlContent, int wordsPerMinute = 250)
    {
        var wordCount = CountWords(htmlContent);
        var minutes = (double)wordCount / wordsPerMinute;
        return TimeSpan.FromMinutes(minutes);
    }

    /// <summary>
    /// Extracts the first N characters of plain text
    /// </summary>
    public string ExtractPreview(string htmlContent, int maxLength = 200)
    {
        var plainText = ExtractPlainText(htmlContent);
        if (string.IsNullOrWhiteSpace(plainText))
            return string.Empty;

        if (plainText.Length <= maxLength)
            return plainText;

        // Try to break at a word boundary
        var preview = plainText[..maxLength];
        var lastSpace = preview.LastIndexOf(' ');
        if (lastSpace > maxLength * 0.8) // If we're close enough to the end
        {
            preview = preview[..lastSpace];
        }

        return preview.Trim() + "...";
    }
}