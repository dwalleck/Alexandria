using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alexandria.Domain.Services;
using Alexandria.Domain.ValueObjects;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Alexandria.Infrastructure.Services;

/// <summary>
/// High-performance content analyzer implementation using AngleSharp for HTML parsing.
/// Optimized following CSharp-Performance-Guide-EPUB-Parser.md guidelines.
/// </summary>
public sealed class AngleSharpContentAnalyzer : IContentAnalyzer
{
    private readonly IConfiguration _angleSharpConfig;
    private readonly IHtmlParser _parser;
    private readonly ArrayPool<char> _charPool;
    private readonly ArrayPool<string> _stringPool;

    // Pre-allocated buffers for small content (4KB threshold)
    private const int SmallContentThreshold = 4096;
    private const int DefaultBufferSize = 8192;

    // SearchValues for efficient character searching (Performance Guide lines 55-69)
    // Note: Apostrophe and hyphen handled separately for contractions and hyphenated words
    private static readonly SearchValues<char> WordBoundaries =
        SearchValues.Create(" \t\n\r.!?,;:()[]{}\"<>/");

    private static readonly SearchValues<char> SentenceEndings =
        SearchValues.Create(".!?");

    private static readonly SearchValues<char> WhitespaceChars =
        SearchValues.Create(" \t\n\r\u00A0");

    // Pre-computed vowel lookup for performance in syllable estimation
    private static readonly SearchValues<char> Vowels =
        SearchValues.Create("aeiouAEIOU");

    // FrozenSet for stop words (Performance Guide line 365-384)
    private static readonly FrozenSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "the", "be", "to", "of", "and", "a", "in", "that", "have", "i",
        "it", "for", "not", "on", "with", "he", "as", "you", "do", "at",
        "this", "but", "his", "by", "from", "they", "we", "say", "her", "she",
        "or", "an", "will", "my", "one", "all", "would", "there", "their",
        "what", "so", "up", "out", "if", "about", "who", "get", "which", "go",
        "me", "when", "make", "can", "like", "time", "no", "just", "him", "know",
        "take", "people", "into", "year", "your", "good", "some", "could", "them",
        "see", "other", "than", "then", "now", "look", "only", "come", "its", "over",
        "think", "also", "back", "after", "use", "two", "how", "our", "work"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public AngleSharpContentAnalyzer()
    {
        _angleSharpConfig = Configuration.Default;
        _parser = new HtmlParser(new HtmlParserOptions
        {
            IsScripting = false,
            IsEmbedded = false,
            IsStrictMode = false
        });
        _charPool = ArrayPool<char>.Shared;
        _stringPool = ArrayPool<string>.Shared;
    }

    /// <inheritdoc />
    public string ExtractPlainText(ReadOnlySpan<char> htmlContent, char[]? buffer = null)
    {
        if (htmlContent.IsEmpty)
            return string.Empty;

        // Always use AngleSharp for consistency in script/style removal
        // The performance difference is minimal for typical EPUB content
        return ExtractPlainTextWithAngleSharp(htmlContent.ToString());
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CountWords(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return 0;

        int wordCount = 0;
        bool inWord = false;

        // Use SearchValues for efficient boundary detection
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            bool isBoundary;

            // Special handling for apostrophes in contractions and hyphens in compound words
            if (ch == '\'' || ch == '-')
            {
                // Apostrophe/hyphen is part of word if surrounded by letters
                isBoundary = i == 0 || i == text.Length - 1 ||
                            !char.IsLetter(text[i - 1]) || !char.IsLetter(text[i + 1]);
            }
            else
            {
                isBoundary = WordBoundaries.Contains(ch);
            }

            if (!isBoundary && !inWord)
            {
                wordCount++;
                inWord = true;
            }
            else if (isBoundary)
            {
                inWord = false;
            }
        }

        return wordCount;
    }

    /// <inheritdoc />
    public TimeSpan EstimateReadingTime(ReadOnlySpan<char> text, int wordsPerMinute = 250)
    {
        if (wordsPerMinute <= 0)
            throw new ArgumentOutOfRangeException(nameof(wordsPerMinute), "Words per minute must be positive.");

        int wordCount = CountWords(text);
        double minutes = (double)wordCount / wordsPerMinute;

        // Round to nearest 30 seconds
        int totalSeconds = (int)Math.Round(minutes * 60 / 30) * 30;
        return TimeSpan.FromSeconds(Math.Max(30, totalSeconds));
    }

    /// <inheritdoc />
    /// Using ValueTask for high-frequency operations (Performance Guide lines 406-438)
    public async ValueTask<ContentMetrics> AnalyzeContentAsync(string htmlContent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(htmlContent))
        {
            return ContentMetrics.Empty;
        }

        // Extract plain text
        string plainText = ExtractPlainTextWithAngleSharp(htmlContent);
        var textSpan = plainText.AsSpan();

        // Calculate metrics using optimized methods
        var metrics = CalculateMetricsOptimized(textSpan);

        // Calculate word frequency with memory efficiency
        // We calculate synchronously since spans can't be used in async contexts
        var (wordFrequency, topKeywords) = CalculateWordFrequencyOptimized(plainText.AsSpan(), cancellationToken);

        return new ContentMetrics
        {
            WordCount = metrics.WordCount,
            CharacterCount = metrics.CharacterCount,
            CharacterCountNoSpaces = metrics.CharacterCountNoSpaces,
            SentenceCount = metrics.SentenceCount,
            ParagraphCount = metrics.ParagraphCount,
            AverageWordsPerSentence = metrics.AverageWordsPerSentence,
            AverageSyllablesPerWord = metrics.AverageSyllablesPerWord,
            ReadabilityScore = metrics.ReadabilityScore,
            EstimatedReadingTime = metrics.EstimatedReadingTime,
            WordFrequency = wordFrequency,
            TopKeywords = topKeywords
        };
    }

    private string ExtractPlainTextOptimized(ReadOnlySpan<char> htmlContent, char[]? buffer)
    {
        // Use ArrayPool for buffer management (Performance Guide lines 104-124)
        bool ownBuffer = false;
        if (buffer == null)
        {
            buffer = _charPool.Rent(htmlContent.Length);
            ownBuffer = true;
        }

        try
        {
            int writeIndex = 0;
            bool inTag = false;
            bool inScript = false;
            bool inStyle = false;

            // Process HTML with span operations
            for (int i = 0; i < htmlContent.Length; i++)
            {
                char c = htmlContent[i];

                // Fast path for tag detection
                if (c == '<')
                {
                    if (!inScript && !inStyle && i + 7 < htmlContent.Length)
                    {
                        var slice = htmlContent.Slice(i, Math.Min(8, htmlContent.Length - i));

                        // Use span comparisons to avoid allocations
                        if (slice.StartsWith("<script", StringComparison.OrdinalIgnoreCase))
                        {
                            inScript = true;
                            i = SkipToEndOfTag(htmlContent, i, "</script>");
                            continue;
                        }
                        else if (slice.StartsWith("<style", StringComparison.OrdinalIgnoreCase))
                        {
                            inStyle = true;
                            i = SkipToEndOfTag(htmlContent, i, "</style>");
                            continue;
                        }
                    }
                    inTag = true;
                }
                else if (c == '>')
                {
                    inTag = false;
                }
                else if (!inTag && !inScript && !inStyle)
                {
                    // Normalize whitespace inline
                    if (WhitespaceChars.Contains(c))
                    {
                        if (writeIndex > 0 && !WhitespaceChars.Contains(buffer[writeIndex - 1]))
                        {
                            buffer[writeIndex++] = ' ';
                        }
                    }
                    else
                    {
                        buffer[writeIndex++] = c;
                    }
                }
            }

            return new string(buffer, 0, writeIndex);
        }
        finally
        {
            if (ownBuffer)
            {
                _charPool.Return(buffer, clearArray: true);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SkipToEndOfTag(ReadOnlySpan<char> content, int startIndex, ReadOnlySpan<char> endTag)
    {
        var remaining = content.Slice(startIndex);
        var index = remaining.IndexOf(endTag, StringComparison.OrdinalIgnoreCase);
        return index == -1 ? content.Length - 1 : startIndex + index + endTag.Length;
    }

    private string ExtractPlainTextWithAngleSharp(string htmlContent)
    {
        using var document = _parser.ParseDocument(htmlContent);

        // Remove script and style elements
        foreach (var element in document.QuerySelectorAll("script, style"))
        {
            element.Remove();
        }

        var textContent = document.Body?.TextContent ?? string.Empty;

        // Use span-based normalization to avoid allocations
        return NormalizeWhitespaceOptimized(textContent.AsSpan());
    }

    private string NormalizeWhitespaceOptimized(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return string.Empty;

        // Rent buffer from pool
        var buffer = _charPool.Rent(text.Length);
        try
        {
            int writeIndex = 0;
            bool lastWasSpace = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (WhitespaceChars.Contains(text[i]))
                {
                    if (!lastWasSpace && writeIndex > 0)
                    {
                        buffer[writeIndex++] = ' ';
                        lastWasSpace = true;
                    }
                }
                else
                {
                    buffer[writeIndex++] = text[i];
                    lastWasSpace = false;
                }
            }

            // Trim end
            while (writeIndex > 0 && buffer[writeIndex - 1] == ' ')
                writeIndex--;

            return new string(buffer, 0, writeIndex);
        }
        finally
        {
            _charPool.Return(buffer, clearArray: true);
        }
    }

    private (int WordCount, int CharacterCount, int CharacterCountNoSpaces, int SentenceCount,
            int ParagraphCount, double AverageWordsPerSentence, double AverageSyllablesPerWord,
            double ReadabilityScore, TimeSpan EstimatedReadingTime)
        CalculateMetricsOptimized(ReadOnlySpan<char> text)
    {
        int wordCount = 0;
        int sentenceCount = 0;
        int paragraphCount = 0;
        int syllableCount = 0;
        int characterCountNoSpaces = 0;

        bool inWord = false;
        bool inSentence = false;
        bool inParagraph = false;
        int consecutiveNewlines = 0;

        // Single pass through text for all metrics
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // Character counting
            if (!WhitespaceChars.Contains(c))
            {
                characterCountNoSpaces++;
            }

            // Word boundaries
            bool isBoundary = WordBoundaries.Contains(c);
            if (!isBoundary && !inWord)
            {
                wordCount++;
                inWord = true;
                syllableCount += EstimateSyllablesInWord(text, i);
            }
            else if (isBoundary)
            {
                inWord = false;
            }

            // Sentence detection (use same robust logic as CountSentences)
            if (char.IsLetterOrDigit(c))
            {
                inSentence = true;
                inParagraph = true;
                consecutiveNewlines = 0;
            }
            else if (inSentence && SentenceEndings.Contains(c))
            {
                // Look ahead to confirm sentence end (matching CountSentences logic)
                if (i + 1 < text.Length)
                {
                    char next = text[i + 1];
                    if (WhitespaceChars.Contains(next) || char.IsUpper(next))
                    {
                        sentenceCount++;
                        inSentence = false;
                    }
                }
                else
                {
                    sentenceCount++;
                    inSentence = false;
                }
            }

            // Paragraph detection
            if (c == '\n' || c == '\r')
            {
                consecutiveNewlines++;
                if (consecutiveNewlines >= 2 && inParagraph)
                {
                    paragraphCount++;
                    inParagraph = false;
                }
            }
        }

        // Handle last items
        if (inSentence) sentenceCount++;
        if (inParagraph) paragraphCount++;

        // Ensure minimums
        sentenceCount = Math.Max(1, sentenceCount);
        paragraphCount = Math.Max(1, paragraphCount);

        // Calculate averages
        double avgWordsPerSentence = (double)wordCount / sentenceCount;
        double avgSyllablesPerWord = wordCount > 0 ? (double)syllableCount / wordCount : 0;

        // Flesch Reading Ease
        double readabilityScore = 206.835 - 1.015 * avgWordsPerSentence - 84.6 * avgSyllablesPerWord;
        readabilityScore = Math.Max(0, Math.Min(100, readabilityScore));

        // Calculate reading time directly with already-computed word count (avoid recalculation)
        const int wordsPerMinute = 250;
        double minutes = (double)wordCount / wordsPerMinute;
        int totalSeconds = (int)Math.Round(minutes * 60 / 30) * 30;
        TimeSpan readingTime = TimeSpan.FromSeconds(Math.Max(30, totalSeconds));

        return (wordCount, text.Length, characterCountNoSpaces, sentenceCount,
                paragraphCount, avgWordsPerSentence, avgSyllablesPerWord,
                readabilityScore, readingTime);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EstimateSyllablesInWord(ReadOnlySpan<char> text, int startIndex)
    {
        // Simple syllable estimation - count vowel groups
        int syllables = 0;
        bool inVowelGroup = false;

        for (int i = startIndex; i < text.Length && !WordBoundaries.Contains(text[i]); i++)
        {
            bool isVowel = Vowels.Contains(text[i]);
            if (isVowel && !inVowelGroup)
            {
                syllables++;
                inVowelGroup = true;
            }
            else if (!isVowel)
            {
                inVowelGroup = false;
            }
        }

        return Math.Max(1, syllables);
    }

    private (Dictionary<string, int> WordFrequency, IReadOnlyList<string> TopKeywords)
        CalculateWordFrequencyOptimized(ReadOnlySpan<char> text, CancellationToken cancellationToken)
    {
        // Use dictionary with initial capacity for better performance
        var frequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Rent buffer for word building
        var wordBuffer = _charPool.Rent(100);
        try
        {
            int wordLength = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (!WordBoundaries.Contains(c))
                {
                    if (wordLength < wordBuffer.Length)
                    {
                        wordBuffer[wordLength++] = char.ToLowerInvariant(c);
                    }
                }
                else if (wordLength > 0)
                {
                    // Only process words longer than 2 characters
                    if (wordLength > 2)
                    {
                        var word = new string(wordBuffer, 0, wordLength);

                        // Use collection expressions for cleaner code
                        frequency[word] = frequency.TryGetValue(word, out var count) ? count + 1 : 1;
                    }
                    wordLength = 0;
                }

                // Check for cancellation periodically
                if (i % 1000 == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            // Process last word
            if (wordLength > 2)
            {
                var word = new string(wordBuffer, 0, wordLength);
                frequency[word] = frequency.TryGetValue(word, out var count) ? count + 1 : 1;
            }
        }
        finally
        {
            _charPool.Return(wordBuffer, clearArray: true);
        }

        // Extract top keywords efficiently
        var topKeywords = frequency
            .Where(kvp => !StopWords.Contains(kvp.Key))
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .Select(kvp => kvp.Key)
            .ToList();

        return (frequency, topKeywords);
    }

    public int CountSentences(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return 0;

        int count = 0;
        bool inSentence = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (char.IsLetterOrDigit(c))
            {
                inSentence = true;
            }
            else if (inSentence && SentenceEndings.Contains(c))
            {
                // Look ahead to confirm sentence end
                if (i + 1 < text.Length)
                {
                    char next = text[i + 1];
                    if (WhitespaceChars.Contains(next) || char.IsUpper(next))
                    {
                        count++;
                        inSentence = false;
                    }
                }
                else
                {
                    count++;
                    inSentence = false;
                }
            }
        }

        if (inSentence)
            count++;

        return Math.Max(1, count);
    }

    public int CountParagraphs(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return 0;

        int count = 0;
        bool inParagraph = false;
        int consecutiveNewlines = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '\n' || c == '\r')
            {
                consecutiveNewlines++;
            }
            else if (!WhitespaceChars.Contains(c))
            {
                if (consecutiveNewlines >= 2 && inParagraph)
                {
                    count++;
                    inParagraph = false;
                }
                else if (!inParagraph)
                {
                    inParagraph = true;
                }
                consecutiveNewlines = 0;
            }
        }

        if (inParagraph)
            count++;

        return Math.Max(1, count);
    }

    private int CountCharactersWithoutSpaces(ReadOnlySpan<char> text)
    {
        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (!WhitespaceChars.Contains(text[i]))
                count++;
        }
        return count;
    }

    private double CalculateFleschReadingEase(double avgWordsPerSentence, double avgSyllablesPerWord)
    {
        double score = 206.835 - 1.015 * avgWordsPerSentence - 84.6 * avgSyllablesPerWord;
        return Math.Max(0, Math.Min(100, score));
    }

    public string[] ExtractSentences(ReadOnlySpan<char> text, int maxSentences)
    {
        if (text.IsEmpty || maxSentences <= 0)
            return Array.Empty<string>();

        // Use ArrayPool for result array
        var sentences = _stringPool.Rent(Math.Min(maxSentences, 100));
        var sentenceCount = 0;

        // Use pooled buffer for sentence building
        var sentenceBuffer = _charPool.Rent(1000);
        int sentenceLength = 0;
        bool inSentence = false;

        try
        {
            for (int i = 0; i < text.Length && sentenceCount < maxSentences; i++)
            {
                char c = text[i];

                if (char.IsLetterOrDigit(c) || char.IsPunctuation(c) || (WhitespaceChars.Contains(c) && inSentence))
                {
                    if (sentenceLength < sentenceBuffer.Length)
                    {
                        sentenceBuffer[sentenceLength++] = c;
                    }

                    if (!WhitespaceChars.Contains(c))
                        inSentence = true;
                }

                if (inSentence && SentenceEndings.Contains(c))
                {
                    if (i + 1 < text.Length)
                    {
                        char next = text[i + 1];
                        if (WhitespaceChars.Contains(next) || char.IsUpper(next))
                        {
                            sentences[sentenceCount++] = new string(sentenceBuffer, 0, sentenceLength).Trim();
                            sentenceLength = 0;
                            inSentence = false;
                        }
                    }
                    else
                    {
                        sentences[sentenceCount++] = new string(sentenceBuffer, 0, sentenceLength).Trim();
                        break;
                    }
                }
            }

            // Add remaining sentence if any
            if (sentenceLength > 0 && sentenceCount < maxSentences)
            {
                sentences[sentenceCount++] = new string(sentenceBuffer, 0, sentenceLength).Trim();
            }

            // Create result array
            var result = new string[sentenceCount];
            Array.Copy(sentences, result, sentenceCount);
            return result;
        }
        finally
        {
            _charPool.Return(sentenceBuffer, clearArray: true);
            _stringPool.Return(sentences, clearArray: true);
        }
    }

    public string GeneratePreview(ReadOnlySpan<char> text, int maxLength)
    {
        if (text.IsEmpty || maxLength <= 0)
            return string.Empty;

        if (text.Length <= maxLength)
            return text.ToString();

        // Find word boundary
        int cutoffPoint = maxLength;
        for (int i = maxLength - 1; i >= maxLength - 20 && i >= 0; i--)
        {
            if (WordBoundaries.Contains(text[i]))
            {
                cutoffPoint = i;
                break;
            }
        }

        return string.Concat(text.Slice(0, cutoffPoint).ToString().TrimEnd(), "...");
    }

    public string HighlightTerms(string text, string[] searchTerms, string highlightStart = "**", string highlightEnd = "**")
    {
        if (string.IsNullOrEmpty(text) || searchTerms == null || searchTerms.Length == 0)
            return text;

        // Collect all matches with their positions
        var matches = new List<(int Index, int Length, string Term)>();

        foreach (var term in searchTerms)
        {
            if (string.IsNullOrWhiteSpace(term))
                continue;

            int index = 0;
            while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                matches.Add((index, term.Length, term));
                index += term.Length;
            }
        }

        // Sort matches by position, then by length (longest first) to prioritize longer matches
        matches.Sort((a, b) =>
        {
            int posCompare = a.Index.CompareTo(b.Index);
            if (posCompare != 0) return posCompare;
            // If same position, prefer longer match
            return b.Length.CompareTo(a.Length);
        });

        // Remove overlapping matches (keep longest at each position)
        var finalMatches = new List<(int Index, int Length, string Term)>();
        if (matches.Count > 0)
        {
            finalMatches.Add(matches[0]);
            for (int i = 1; i < matches.Count; i++)
            {
                var lastMatch = finalMatches[finalMatches.Count - 1];
                // Skip if this match overlaps with the last kept match
                if (matches[i].Index >= lastMatch.Index + lastMatch.Length)
                {
                    finalMatches.Add(matches[i]);
                }
            }
        }

        // Build result with highlights
        var result = new StringBuilder(text.Length + (finalMatches.Count * (highlightStart.Length + highlightEnd.Length)));
        int lastIndex = 0;

        foreach (var match in finalMatches)
        {
            // Append text before the match
            result.Append(text.AsSpan(lastIndex, match.Index - lastIndex));

            // Append highlighted term
            result.Append(highlightStart);
            result.Append(text.AsSpan(match.Index, match.Length));
            result.Append(highlightEnd);

            lastIndex = match.Index + match.Length;
        }

        // Append remaining text
        if (lastIndex < text.Length)
            result.Append(text.AsSpan(lastIndex));

        return result.ToString();
    }

    public double CalculateReadabilityScore(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return 0;

        // Use the existing metrics calculation
        var metrics = CalculateMetricsOptimized(text);
        return metrics.ReadabilityScore;
    }

    public string ExtractSnippet(ReadOnlySpan<char> text, string searchTerm, int contextLength = 100)
    {
        if (text.IsEmpty || string.IsNullOrEmpty(searchTerm))
            return string.Empty;

        var searchSpan = searchTerm.AsSpan();
        var index = text.IndexOf(searchSpan, StringComparison.OrdinalIgnoreCase);

        if (index == -1)
            return string.Empty;

        // Calculate boundaries
        int start = Math.Max(0, index - contextLength);
        int end = Math.Min(text.Length, index + searchTerm.Length + contextLength);

        // Adjust to word boundaries
        while (start > 0 && !WordBoundaries.Contains(text[start]))
            start--;

        while (end < text.Length && !WordBoundaries.Contains(text[end]))
            end++;

        // Build result with ellipsis
        var result = new StringBuilder();

        if (start > 0)
            result.Append("...");

        result.Append(text.Slice(start, end - start).ToString().Trim());

        if (end < text.Length)
            result.Append("...");

        return result.ToString();
    }
}