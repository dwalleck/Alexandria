using System.Text.RegularExpressions;
using Alexandria.Domain.Services;
using Alexandria.Domain.ValueObjects;

namespace Alexandria.Benchmarks;

/// <summary>
/// Mock implementation for testing the benchmark infrastructure.
/// This will be replaced with AngleSharpContentAnalyzer in Phase 1.2.
/// </summary>
public class MockContentAnalyzerBenchmarks : ContentAnalyzerBenchmarks
{
    protected override void InitializeAnalyzer()
    {
        Analyzer = new MockContentAnalyzer();
    }

    /// <summary>
    /// Simple mock implementation using regex (to be replaced with AngleSharp).
    /// This demonstrates the current performance baseline.
    /// </summary>
    private class MockContentAnalyzer : IContentAnalyzer
    {
        private static readonly Regex HtmlTagRegex = new(@"<[^>]*>", RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

        public string ExtractPlainText(ReadOnlySpan<char> htmlContent, char[]? buffer = null)
        {
            var html = htmlContent.ToString();
            var text = HtmlTagRegex.Replace(html, " ");
            text = WhitespaceRegex.Replace(text, " ");
            return text.Trim();
        }

        public int CountWords(ReadOnlySpan<char> text)
        {
            var str = text.ToString();
            if (string.IsNullOrWhiteSpace(str))
                return 0;

            return str.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        public TimeSpan EstimateReadingTime(ReadOnlySpan<char> text, int wordsPerMinute = 250)
        {
            var wordCount = CountWords(text);
            var minutes = (double)wordCount / wordsPerMinute;
            return TimeSpan.FromMinutes(minutes);
        }

        public string[] ExtractSentences(ReadOnlySpan<char> text, int maxSentences)
        {
            var str = text.ToString();
            var sentences = Regex.Split(str, @"(?<=[.!?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Take(maxSentences)
                .ToArray();
            return sentences;
        }

        public string GeneratePreview(ReadOnlySpan<char> text, int maxLength)
        {
            var str = text.ToString();
            if (str.Length <= maxLength)
                return str;

            var preview = str.Substring(0, maxLength);
            var lastSpace = preview.LastIndexOf(' ');
            if (lastSpace > maxLength * 0.8)
            {
                preview = preview.Substring(0, lastSpace);
            }
            return preview.Trim() + "...";
        }

        public string ExtractSnippet(ReadOnlySpan<char> text, string searchTerm, int contextLength = 100)
        {
            var str = text.ToString();
            var index = str.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
            if (index == -1)
                return string.Empty;

            var start = Math.Max(0, index - contextLength);
            var end = Math.Min(str.Length, index + searchTerm.Length + contextLength);
            var snippet = str.Substring(start, end - start);

            if (start > 0)
                snippet = "..." + snippet;
            if (end < str.Length)
                snippet += "...";

            return snippet;
        }

        public string HighlightTerms(string text, string[] searchTerms, string highlightStart = "**", string highlightEnd = "**")
        {
            var result = text;
            foreach (var term in searchTerms.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                var pattern = $@"\b{Regex.Escape(term)}\b";
                result = Regex.Replace(result, pattern, $"{highlightStart}$0{highlightEnd}", RegexOptions.IgnoreCase);
            }
            return result;
        }

        public ValueTask<ContentMetrics> AnalyzeContentAsync(string htmlContent, CancellationToken cancellationToken = default)
        {
            var plainText = ExtractPlainText(htmlContent.AsSpan());
            var wordCount = CountWords(plainText.AsSpan());
            var sentenceCount = CountSentences(plainText.AsSpan());
            var paragraphCount = CountParagraphs(htmlContent.AsSpan());

            var metrics = new ContentMetrics
            {
                WordCount = wordCount,
                CharacterCount = plainText.Length,
                CharacterCountNoSpaces = plainText.Replace(" ", "").Length,
                SentenceCount = sentenceCount,
                ParagraphCount = paragraphCount,
                EstimatedReadingTime = EstimateReadingTime(plainText.AsSpan()),
                AverageWordsPerSentence = sentenceCount > 0 ? (double)wordCount / sentenceCount : 0,
                ReadabilityScore = CalculateReadabilityScore(plainText.AsSpan())
            };

            return new ValueTask<ContentMetrics>(metrics);
        }

        public int CountSentences(ReadOnlySpan<char> text)
        {
            var str = text.ToString();
            return Regex.Matches(str, @"[.!?]+").Count;
        }

        public int CountParagraphs(ReadOnlySpan<char> htmlContent)
        {
            var html = htmlContent.ToString();
            return Regex.Matches(html, @"<p[^>]*>", RegexOptions.IgnoreCase).Count;
        }

        public double CalculateReadabilityScore(ReadOnlySpan<char> text)
        {
            // Simplified Flesch Reading Ease calculation
            var wordCount = CountWords(text);
            var sentenceCount = CountSentences(text);

            if (wordCount == 0 || sentenceCount == 0)
                return 0;

            var avgWordsPerSentence = (double)wordCount / sentenceCount;
            var avgSyllablesPerWord = 1.5; // Simplified estimate

            // Flesch Reading Ease formula
            return 206.835 - 1.015 * avgWordsPerSentence - 84.6 * avgSyllablesPerWord;
        }
    }
}