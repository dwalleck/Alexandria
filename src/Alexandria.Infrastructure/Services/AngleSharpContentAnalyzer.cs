using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
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
/// Optimized for zero-allocation processing of small content and minimal allocations for large content.
/// </summary>
public sealed class AngleSharpContentAnalyzer : IContentAnalyzer
{
    private readonly IConfiguration _angleSharpConfig;
    private readonly IHtmlParser _parser;
    private readonly ArrayPool<char> _charPool;

    // Pre-allocated buffers for small content (4KB threshold)
    private const int SmallContentThreshold = 4096;
    private const int DefaultBufferSize = 8192;

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
    }

    /// <inheritdoc />
    public string ExtractPlainText(ReadOnlySpan<char> htmlContent, char[]? buffer = null)
    {
        if (htmlContent.IsEmpty)
            return string.Empty;

        // For small content, use stack-allocated or provided buffer
        if (htmlContent.Length < SmallContentThreshold)
        {
            return ExtractPlainTextOptimized(htmlContent, buffer);
        }

        // For large content, use AngleSharp with string pooling
        return ExtractPlainTextWithAngleSharp(htmlContent.ToString());
    }

    /// <inheritdoc />
    public int CountWords(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return 0;

        int wordCount = 0;
        bool inWord = false;

        // Optimized word counting using span iteration
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool isWordChar = char.IsLetterOrDigit(c) || c == '\'' || c == '-';

            if (isWordChar && !inWord)
            {
                wordCount++;
                inWord = true;
            }
            else if (!isWordChar)
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
        return TimeSpan.FromSeconds(Math.Max(30, totalSeconds)); // Minimum 30 seconds
    }

    /// <inheritdoc />
    public async ValueTask<ContentMetrics> AnalyzeContentAsync(string htmlContent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(htmlContent))
        {
            return new ContentMetrics
            {
                WordCount = 0,
                CharacterCount = 0,
                CharacterCountNoSpaces = 0,
                SentenceCount = 0,
                ParagraphCount = 0,
                AverageWordsPerSentence = 0,
                AverageSyllablesPerWord = 0,
                ReadabilityScore = 0,
                EstimatedReadingTime = TimeSpan.Zero,
                WordFrequency = new Dictionary<string, int>(),
                TopKeywords = Array.Empty<string>()
            };
        }

        // Extract plain text
        string plainText = ExtractPlainTextWithAngleSharp(htmlContent);
        var textSpan = plainText.AsSpan();

        // Calculate metrics
        int wordCount = CountWords(textSpan);
        int sentenceCount = CountSentences(textSpan);
        int paragraphCount = CountParagraphs(textSpan);
        int syllableCount = EstimateSyllables(textSpan);

        // Character counts
        int characterCount = plainText.Length;
        int characterCountWithoutSpaces = CountCharactersWithoutSpaces(textSpan);

        // Calculate averages
        double avgWordsPerSentence = sentenceCount > 0 ? (double)wordCount / sentenceCount : 0;
        double avgSyllablesPerWord = wordCount > 0 ? (double)syllableCount / wordCount : 0;

        // Calculate Flesch Reading Ease score
        double readabilityScore = CalculateFleschReadingEase(avgWordsPerSentence, avgSyllablesPerWord);

        // Estimate reading time
        TimeSpan readingTime = EstimateReadingTime(textSpan);

        // Calculate word frequency
        var wordFrequency = CalculateWordFrequency(textSpan);
        var topKeywords = ExtractTopKeywords(wordFrequency);

        return new ContentMetrics
        {
            WordCount = wordCount,
            CharacterCount = characterCount,
            CharacterCountNoSpaces = characterCountWithoutSpaces,
            SentenceCount = sentenceCount,
            ParagraphCount = paragraphCount,
            AverageWordsPerSentence = avgWordsPerSentence,
            AverageSyllablesPerWord = avgSyllablesPerWord,
            ReadabilityScore = readabilityScore,
            EstimatedReadingTime = readingTime,
            WordFrequency = wordFrequency,
            TopKeywords = topKeywords
        };
    }

    private string ExtractPlainTextOptimized(ReadOnlySpan<char> htmlContent, char[]? buffer)
    {
        // Simple HTML tag removal for small content - zero allocation approach
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

            for (int i = 0; i < htmlContent.Length; i++)
            {
                char c = htmlContent[i];

                // Check for script/style tags
                if (c == '<' && !inScript && !inStyle && i + 7 < htmlContent.Length)
                {
                    var slice = htmlContent.Slice(i, Math.Min(7, htmlContent.Length - i));
                    if (slice.StartsWith("<script", StringComparison.OrdinalIgnoreCase))
                    {
                        inScript = true;
                        // Skip to the end of the opening tag
                        while (i < htmlContent.Length && htmlContent[i] != '>')
                            i++;
                        continue;
                    }
                    else if (slice.StartsWith("<style", StringComparison.OrdinalIgnoreCase))
                    {
                        inStyle = true;
                        // Skip to the end of the opening tag
                        while (i < htmlContent.Length && htmlContent[i] != '>')
                            i++;
                        continue;
                    }
                }

                // Check for end of script/style
                if ((inScript || inStyle) && i + 9 < htmlContent.Length)
                {
                    if (inScript)
                    {
                        var slice = htmlContent.Slice(i, Math.Min(9, htmlContent.Length - i));
                        if (slice.StartsWith("</script>", StringComparison.OrdinalIgnoreCase))
                        {
                            inScript = false;
                            i += 8;
                            continue;
                        }
                    }
                    else if (inStyle)
                    {
                        var slice = htmlContent.Slice(i, Math.Min(8, htmlContent.Length - i));
                        if (slice.StartsWith("</style>", StringComparison.OrdinalIgnoreCase))
                        {
                            inStyle = false;
                            i += 7;
                            continue;
                        }
                    }
                }

                if (inScript || inStyle)
                    continue;

                if (c == '<')
                {
                    inTag = true;
                }
                else if (c == '>')
                {
                    inTag = false;
                    // Don't add spaces after inline tags
                    // We should only add spaces after block-level tags
                }
                else if (!inTag && writeIndex < buffer.Length)
                {
                    // Decode HTML entities
                    if (c == '&' && i + 3 < htmlContent.Length)
                    {
                        var entityEnd = htmlContent.Slice(i).IndexOf(';');
                        if (entityEnd > 0 && entityEnd < 10)
                        {
                            var entity = htmlContent.Slice(i, entityEnd + 1);
                            char decoded = DecodeHtmlEntity(entity);
                            if (decoded != '\0')
                            {
                                buffer[writeIndex++] = decoded;
                                i += entityEnd;
                                continue;
                            }
                        }
                    }

                    buffer[writeIndex++] = c;
                }
            }

            // Trim any trailing whitespace
            while (writeIndex > 0 && char.IsWhiteSpace(buffer[writeIndex - 1]))
            {
                writeIndex--;
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

    private string ExtractPlainTextWithAngleSharp(string htmlContent)
    {
        using var document = _parser.ParseDocument(htmlContent);

        // Remove script and style elements
        foreach (var element in document.QuerySelectorAll("script, style, noscript"))
        {
            element.Remove();
        }

        // Get text content
        var textContent = document.Body?.TextContent ?? document.DocumentElement.TextContent ?? string.Empty;

        // Normalize whitespace
        var sb = new StringBuilder(textContent.Length);
        bool lastWasSpace = false;

        foreach (char c in textContent)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }

        return sb.ToString().Trim();
    }

    private static char DecodeHtmlEntity(ReadOnlySpan<char> entity)
    {
        if (entity.Length < 3 || entity[0] != '&' || entity[entity.Length - 1] != ';')
            return '\0';

        var content = entity.Slice(1, entity.Length - 2);

        return content switch
        {
            _ when content.Equals("lt", StringComparison.Ordinal) => '<',
            _ when content.Equals("gt", StringComparison.Ordinal) => '>',
            _ when content.Equals("amp", StringComparison.Ordinal) => '&',
            _ when content.Equals("quot", StringComparison.Ordinal) => '"',
            _ when content.Equals("apos", StringComparison.Ordinal) => '\'',
            _ when content.Equals("nbsp", StringComparison.Ordinal) => ' ',
            _ => '\0'
        };
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
            else if (inSentence && (c == '.' || c == '!' || c == '?'))
            {
                // Check if it's really end of sentence (not abbreviation)
                if (i + 1 < text.Length)
                {
                    char next = text[i + 1];
                    if (char.IsWhiteSpace(next) || char.IsUpper(next))
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

        // Count last sentence if text doesn't end with punctuation
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
                if (consecutiveNewlines >= 2 && inParagraph)
                {
                    count++;
                    inParagraph = false;
                }
            }
            else if (!char.IsWhiteSpace(c))
            {
                consecutiveNewlines = 0;
                inParagraph = true;
            }
        }

        // Count last paragraph
        if (inParagraph)
            count++;

        return Math.Max(1, count);
    }

    private static int CountCharactersWithoutSpaces(ReadOnlySpan<char> text)
    {
        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
                count++;
        }
        return count;
    }

    private static int EstimateSyllables(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return 0;

        int syllableCount = 0;
        bool previousWasVowel = false;
        bool inWord = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = char.ToLowerInvariant(text[i]);

            if (char.IsLetter(c))
            {
                bool isVowel = c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u' || c == 'y';

                if (isVowel && !previousWasVowel)
                {
                    syllableCount++;
                }

                previousWasVowel = isVowel;
                inWord = true;
            }
            else
            {
                // Handle silent 'e' at end of words
                if (inWord && i > 0 && text[i - 1] == 'e' && syllableCount > 1)
                {
                    syllableCount--;
                }

                previousWasVowel = false;
                inWord = false;
            }
        }

        return Math.Max(1, syllableCount);
    }

    private static double CalculateFleschReadingEase(double avgWordsPerSentence, double avgSyllablesPerWord)
    {
        // Flesch Reading Ease formula
        double score = 206.835 - (1.015 * avgWordsPerSentence) - (84.6 * avgSyllablesPerWord);

        // Clamp between 0 and 100
        return Math.Max(0, Math.Min(100, score));
    }

    private static Dictionary<string, int> CalculateWordFrequency(ReadOnlySpan<char> text)
    {
        var frequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var currentWord = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (char.IsLetterOrDigit(c) || c == '\'' || c == '-')
            {
                currentWord.Append(char.ToLowerInvariant(c));
            }
            else if (currentWord.Length > 0)
            {
                var word = currentWord.ToString();
                if (word.Length > 2) // Skip very short words
                {
                    if (frequency.ContainsKey(word))
                        frequency[word]++;
                    else
                        frequency[word] = 1;
                }
                currentWord.Clear();
            }
        }

        // Add last word
        if (currentWord.Length > 2)
        {
            var word = currentWord.ToString();
            if (frequency.ContainsKey(word))
                frequency[word]++;
            else
                frequency[word] = 1;
        }

        // Limit to top 100 words to control memory
        return frequency
            .OrderByDescending(kvp => kvp.Value)
            .Take(100)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private static IReadOnlyList<string> ExtractTopKeywords(Dictionary<string, int> wordFrequency)
    {
        // Common English stop words to exclude
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
        };

        return wordFrequency
            .Where(kvp => !stopWords.Contains(kvp.Key))
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    public string[] ExtractSentences(ReadOnlySpan<char> text, int maxSentences)
    {
        if (text.IsEmpty || maxSentences <= 0)
            return Array.Empty<string>();

        var sentences = new List<string>(Math.Min(maxSentences, 100));
        var currentSentence = new StringBuilder();
        bool inSentence = false;

        for (int i = 0; i < text.Length && sentences.Count < maxSentences; i++)
        {
            char c = text[i];

            if (char.IsLetterOrDigit(c) || char.IsPunctuation(c) || (char.IsWhiteSpace(c) && inSentence))
            {
                currentSentence.Append(c);
                if (!char.IsWhiteSpace(c))
                    inSentence = true;
            }

            // Check for sentence ending
            if (inSentence && (c == '.' || c == '!' || c == '?'))
            {
                // Look ahead to confirm it's really end of sentence
                if (i + 1 < text.Length)
                {
                    char next = text[i + 1];
                    if (char.IsWhiteSpace(next) || char.IsUpper(next))
                    {
                        sentences.Add(currentSentence.ToString().Trim());
                        currentSentence.Clear();
                        inSentence = false;
                    }
                }
                else
                {
                    sentences.Add(currentSentence.ToString().Trim());
                    break;
                }
            }
        }

        // Add any remaining sentence
        if (currentSentence.Length > 0 && sentences.Count < maxSentences)
        {
            sentences.Add(currentSentence.ToString().Trim());
        }

        return sentences.ToArray();
    }

    public string GeneratePreview(ReadOnlySpan<char> text, int maxLength)
    {
        if (text.IsEmpty || maxLength <= 0)
            return string.Empty;

        if (text.Length <= maxLength)
            return text.ToString();

        // Find the last space before maxLength
        int cutoffPoint = maxLength;
        for (int i = maxLength - 1; i >= 0; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                cutoffPoint = i;
                break;
            }
        }

        // If no space found, just cut at maxLength
        if (cutoffPoint == maxLength && maxLength > 20)
        {
            cutoffPoint = maxLength - 3; // Leave room for ellipsis
        }

        return text.Slice(0, cutoffPoint).ToString().TrimEnd() + "...";
    }

    public string ExtractSnippet(ReadOnlySpan<char> text, string searchTerm, int contextLength = 100)
    {
        if (text.IsEmpty || string.IsNullOrEmpty(searchTerm))
            return string.Empty;

        var index = text.IndexOf(searchTerm.AsSpan(), StringComparison.OrdinalIgnoreCase);
        if (index == -1)
            return string.Empty;

        // Calculate start and end positions
        int start = Math.Max(0, index - contextLength);
        int end = Math.Min(text.Length, index + searchTerm.Length + contextLength);

        // Adjust start to word boundary
        if (start > 0)
        {
            while (start < index && !char.IsWhiteSpace(text[start]))
                start++;
            if (start < index)
                start++; // Skip the space
        }

        // Adjust end to word boundary
        if (end < text.Length)
        {
            while (end > index + searchTerm.Length && !char.IsWhiteSpace(text[end - 1]))
                end--;
        }

        var snippet = text.Slice(start, end - start).ToString().Trim();

        // Add ellipsis if needed
        if (start > 0)
            snippet = "..." + snippet;
        if (end < text.Length)
            snippet = snippet + "...";

        return snippet;
    }

    public string HighlightTerms(string text, string[] searchTerms, string highlightStart = "**", string highlightEnd = "**")
    {
        if (string.IsNullOrEmpty(text) || searchTerms == null || searchTerms.Length == 0)
            return text;

        var result = new StringBuilder(text.Length + (searchTerms.Length * 10));
        var textSpan = text.AsSpan();
        int lastIndex = 0;

        // Sort terms by length (longest first) to handle overlapping terms
        var sortedTerms = searchTerms
            .Where(t => !string.IsNullOrEmpty(t))
            .OrderByDescending(t => t.Length)
            .ToArray();

        var highlights = new List<(int start, int end)>();

        // Find all occurrences of all terms
        foreach (var term in sortedTerms)
        {
            int index = 0;
            while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                // Check if this position is already highlighted
                bool overlaps = highlights.Any(h =>
                    (index >= h.start && index < h.end) ||
                    (index + term.Length > h.start && index + term.Length <= h.end));

                if (!overlaps)
                {
                    highlights.Add((index, index + term.Length));
                }
                index += term.Length;
            }
        }

        // Sort highlights by position
        highlights.Sort((a, b) => a.start.CompareTo(b.start));

        // Build the result with highlights
        foreach (var (start, end) in highlights)
        {
            // Add text before the highlight
            if (start > lastIndex)
            {
                result.Append(textSpan.Slice(lastIndex, start - lastIndex));
            }

            // Add highlighted term
            result.Append(highlightStart);
            result.Append(textSpan.Slice(start, end - start));
            result.Append(highlightEnd);

            lastIndex = end;
        }

        // Add remaining text
        if (lastIndex < text.Length)
        {
            result.Append(textSpan.Slice(lastIndex));
        }

        return result.ToString();
    }

    public double CalculateReadabilityScore(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return 0;

        int wordCount = CountWords(text);
        int sentenceCount = CountSentences(text);
        int syllableCount = EstimateSyllables(text);

        if (wordCount == 0 || sentenceCount == 0)
            return 0;

        double avgWordsPerSentence = (double)wordCount / sentenceCount;
        double avgSyllablesPerWord = (double)syllableCount / wordCount;

        return CalculateFleschReadingEase(avgWordsPerSentence, avgSyllablesPerWord);
    }
}