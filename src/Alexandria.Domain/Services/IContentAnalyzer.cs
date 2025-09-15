using System;
using System.Threading;
using System.Threading.Tasks;
using Alexandria.Domain.ValueObjects;

namespace Alexandria.Domain.Services;

/// <summary>
/// High-performance content analysis service for EPUB chapters.
/// All text processing methods use zero-allocation techniques where possible.
/// </summary>
public interface IContentAnalyzer
{
    /// <summary>
    /// Extracts plain text from HTML content using zero-allocation techniques.
    /// </summary>
    /// <param name="htmlContent">HTML content as ReadOnlySpan for zero-copy processing</param>
    /// <param name="buffer">Optional buffer for reuse (minimum 4KB recommended)</param>
    /// <returns>Plain text content with HTML tags removed and entities decoded</returns>
    /// <remarks>
    /// Performance target: Process 1MB of HTML in under 100ms.
    /// Memory target: Zero heap allocations for text under 4KB.
    /// </remarks>
    string ExtractPlainText(ReadOnlySpan<char> htmlContent, char[]? buffer = null);

    /// <summary>
    /// Counts words in text using span-based processing for optimal performance.
    /// </summary>
    /// <param name="text">Text to analyze</param>
    /// <returns>Number of words in the text</returns>
    /// <remarks>
    /// Word counting accuracy should match Microsoft Word ±2%.
    /// Uses efficient span-based tokenization without allocations.
    /// </remarks>
    int CountWords(ReadOnlySpan<char> text);

    /// <summary>
    /// Estimates reading time based on configurable words per minute.
    /// </summary>
    /// <param name="text">Text to analyze</param>
    /// <param name="wordsPerMinute">Reading speed (default: 250 WPM for average adult)</param>
    /// <returns>Estimated reading time</returns>
    /// <remarks>
    /// Reading time estimates should be within ±10% of Medium.com calculations.
    /// </remarks>
    TimeSpan EstimateReadingTime(ReadOnlySpan<char> text, int wordsPerMinute = 250);

    /// <summary>
    /// Extracts sentences from text for preview generation or summarization.
    /// </summary>
    /// <param name="text">Source text to extract sentences from</param>
    /// <param name="maxSentences">Maximum number of sentences to extract</param>
    /// <returns>Array of extracted sentences</returns>
    /// <remarks>
    /// Handles common sentence endings (.!?) and maintains proper boundaries.
    /// Respects abbreviations and decimal numbers.
    /// </remarks>
    string[] ExtractSentences(ReadOnlySpan<char> text, int maxSentences);

    /// <summary>
    /// Generates a preview of the text, maintaining word boundaries.
    /// </summary>
    /// <param name="text">Source text to generate preview from</param>
    /// <param name="maxLength">Maximum length of the preview in characters</param>
    /// <returns>Preview text with ellipsis if truncated</returns>
    /// <remarks>
    /// Always breaks at word boundaries to avoid partial words.
    /// Adds ellipsis (...) only when text is actually truncated.
    /// </remarks>
    string GeneratePreview(ReadOnlySpan<char> text, int maxLength);

    /// <summary>
    /// Extracts a snippet of text around a search term with configurable context.
    /// </summary>
    /// <param name="text">Source text to extract snippet from</param>
    /// <param name="searchTerm">Term to center the snippet around</param>
    /// <param name="contextLength">Number of characters to include before and after the term</param>
    /// <returns>Snippet with the search term in context, or empty if term not found</returns>
    string ExtractSnippet(ReadOnlySpan<char> text, string searchTerm, int contextLength = 100);

    /// <summary>
    /// Highlights search terms in text using configurable markers.
    /// </summary>
    /// <param name="text">Text to highlight terms in</param>
    /// <param name="searchTerms">Terms to highlight</param>
    /// <param name="highlightStart">Marker to place before highlighted term (default: **)</param>
    /// <param name="highlightEnd">Marker to place after highlighted term (default: **)</param>
    /// <returns>Text with highlighted terms</returns>
    string HighlightTerms(string text, string[] searchTerms, string highlightStart = "**", string highlightEnd = "**");

    /// <summary>
    /// Analyzes HTML content and returns comprehensive metrics.
    /// </summary>
    /// <param name="htmlContent">HTML content to analyze</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Comprehensive content metrics including readability scores</returns>
    /// <remarks>
    /// This method performs full analysis including:
    /// - Word, sentence, and paragraph counting
    /// - Readability scoring (Flesch Reading Ease)
    /// - Word frequency analysis
    /// - Reading time estimation
    /// Performance target: Process 1MB HTML in under 100ms.
    /// </remarks>
    ValueTask<ContentMetrics> AnalyzeContentAsync(
        string htmlContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts sentences in the provided text.
    /// </summary>
    /// <param name="text">Text to count sentences in</param>
    /// <returns>Number of sentences detected</returns>
    int CountSentences(ReadOnlySpan<char> text);

    /// <summary>
    /// Counts paragraphs in the provided HTML content.
    /// </summary>
    /// <param name="htmlContent">HTML content to count paragraphs in</param>
    /// <returns>Number of paragraph elements detected</returns>
    int CountParagraphs(ReadOnlySpan<char> htmlContent);

    /// <summary>
    /// Calculates the Flesch Reading Ease score for the provided text.
    /// </summary>
    /// <param name="text">Text to calculate readability for</param>
    /// <returns>Flesch Reading Ease score (0-100, higher is easier)</returns>
    /// <remarks>
    /// Score interpretation:
    /// 90-100: Very Easy (5th grade)
    /// 80-90: Easy (6th grade)
    /// 70-80: Fairly Easy (7th grade)
    /// 60-70: Standard (8th-9th grade)
    /// 50-60: Fairly Difficult (10th-12th grade)
    /// 30-50: Difficult (College)
    /// 0-30: Very Difficult (College graduate)
    /// </remarks>
    double CalculateReadabilityScore(ReadOnlySpan<char> text);
}