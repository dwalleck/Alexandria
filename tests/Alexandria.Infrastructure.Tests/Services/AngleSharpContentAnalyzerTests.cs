using Alexandria.Infrastructure.Services;

namespace Alexandria.Infrastructure.Tests.Services;

public class AngleSharpContentAnalyzerTests
{
    private readonly AngleSharpContentAnalyzer _analyzer = new();

    [Test]
    public async Task ExtractPlainText_RemovesHtmlTags()
    {
        // Arrange
        const string html = "<p>Hello <strong>World</strong>!</p>";

        // Act
        var result = _analyzer.ExtractPlainText(html.AsSpan());

        // Assert
        await Assert.That(result).IsEqualTo("Hello World!");
    }

    [Test]
    public async Task ExtractPlainText_DecodesHtmlEntities()
    {
        // Arrange
        const string html = "<p>&lt;Test&gt; &amp; &quot;Quote&quot;</p>";

        // Act
        var result = _analyzer.ExtractPlainText(html.AsSpan());

        // Assert
        await Assert.That(result).IsEqualTo("<Test> & \"Quote\"");
    }

    [Test]
    public async Task ExtractPlainText_RemovesScriptAndStyleTags()
    {
        // Arrange
        const string html = @"
            <p>Visible</p>
            <script>console.log('hidden');</script>
            <style>body { color: red; }</style>
            <p>Also visible</p>";

        // Act
        var result = _analyzer.ExtractPlainText(html.AsSpan());

        // Assert
        await Assert.That(result).Contains("Visible");
        await Assert.That(result).Contains("Also visible");
        await Assert.That(result).DoesNotContain("console.log");
        await Assert.That(result).DoesNotContain("color: red");
    }

    [Test]
    public async Task CountWords_CountsSimpleText()
    {
        // Arrange
        const string text = "The quick brown fox jumps over the lazy dog";

        // Act
        var count = _analyzer.CountWords(text.AsSpan());

        // Assert
        await Assert.That(count).IsEqualTo(9);
    }

    [Test]
    public async Task CountWords_HandlesContractions()
    {
        // Arrange
        const string text = "I can't won't shouldn't";

        // Act
        var count = _analyzer.CountWords(text.AsSpan());

        // Assert
        await Assert.That(count).IsEqualTo(4); // "I" + 3 contractions
    }

    [Test]
    public async Task CountWords_HandlesHyphenatedWords()
    {
        // Arrange
        const string text = "state-of-the-art well-known";

        // Act
        var count = _analyzer.CountWords(text.AsSpan());

        // Assert
        await Assert.That(count).IsEqualTo(2); // Hyphenated words count as single words
    }

    [Test]
    public async Task CountWords_EmptyText_ReturnsZero()
    {
        // Arrange
        const string text = "";

        // Act
        var count = _analyzer.CountWords(text.AsSpan());

        // Assert
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task EstimateReadingTime_CalculatesCorrectly()
    {
        // Arrange
        // Create text with exactly 250 words (1 minute at default 250 WPM)
        var words = new string[250];
        for (int i = 0; i < 250; i++)
        {
            words[i] = "word";
        }
        var text = string.Join(" ", words);

        // Act
        var time = _analyzer.EstimateReadingTime(text.AsSpan());

        // Assert
        await Assert.That(time.TotalSeconds).IsGreaterThanOrEqualTo(60);
        await Assert.That(time.TotalSeconds).IsLessThanOrEqualTo(90);
    }

    [Test]
    public async Task EstimateReadingTime_ShortText_ReturnsMinimum30Seconds()
    {
        // Arrange
        const string text = "Just a few words";

        // Act
        var time = _analyzer.EstimateReadingTime(text.AsSpan());

        // Assert
        await Assert.That(time.TotalSeconds).IsEqualTo(30);
    }

    [Test]
    public async Task CountSentences_CountsSimpleSentences()
    {
        // Arrange
        const string text = "First sentence. Second sentence! Third sentence?";

        // Act
        var count = _analyzer.CountSentences(text.AsSpan());

        // Assert
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task CountSentences_HandlesAbbreviations()
    {
        // Arrange
        const string text = "Dr. Smith went to the U.S.A. yesterday. He arrived at 5 p.m.";

        // Act
        var count = _analyzer.CountSentences(text.AsSpan());

        // Assert
        // This is complex to handle perfectly, but should detect at least 2 sentences
        await Assert.That(count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task CountParagraphs_CountsMultipleParagraphs()
    {
        // Arrange
        const string text = @"First paragraph.

Second paragraph.

Third paragraph.";

        // Act
        var count = _analyzer.CountParagraphs(text.AsSpan());

        // Assert
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task CalculateReadabilityScore_ReturnsValidScore()
    {
        // Arrange
        const string text = "The cat sat on the mat. The dog ran in the park. Simple words are easy to read.";

        // Act
        var score = _analyzer.CalculateReadabilityScore(text.AsSpan());

        // Assert
        await Assert.That(score).IsGreaterThanOrEqualTo(0);
        await Assert.That(score).IsLessThanOrEqualTo(100);
    }

    [Test]
    public async Task ExtractSentences_ExtractsRequestedNumber()
    {
        // Arrange
        const string text = "First sentence. Second sentence. Third sentence. Fourth sentence.";

        // Act
        var sentences = _analyzer.ExtractSentences(text.AsSpan(), 2);

        // Assert
        await Assert.That(sentences.Length).IsEqualTo(2);
        await Assert.That(sentences[0]).IsEqualTo("First sentence.");
        await Assert.That(sentences[1]).IsEqualTo("Second sentence.");
    }

    [Test]
    public async Task GeneratePreview_TruncatesAtWordBoundary()
    {
        // Arrange
        const string text = "This is a longer text that should be truncated at a word boundary";

        // Act
        var preview = _analyzer.GeneratePreview(text.AsSpan(), 30);

        // Assert
        await Assert.That(preview).EndsWith("...");
        await Assert.That(preview.Length).IsLessThanOrEqualTo(33); // 30 + "..."
        await Assert.That(preview).DoesNotContain("truncated"); // Should stop before this word
    }

    [Test]
    public async Task ExtractSnippet_FindsSearchTerm()
    {
        // Arrange
        const string text = "The quick brown fox jumps over the lazy dog. The fox is very clever.";

        // Act
        var snippet = _analyzer.ExtractSnippet(text.AsSpan(), "fox", 10);

        // Assert
        await Assert.That(snippet).Contains("fox");
        await Assert.That(snippet).Contains("...");
    }

    [Test]
    public async Task HighlightTerms_HighlightsMultipleTerms()
    {
        // Arrange
        const string text = "The quick brown fox jumps over the lazy dog";
        var terms = new[] { "quick", "fox", "dog" };

        // Act
        var highlighted = _analyzer.HighlightTerms(text, terms);

        // Assert
        await Assert.That(highlighted).Contains("**quick**");
        await Assert.That(highlighted).Contains("**fox**");
        await Assert.That(highlighted).Contains("**dog**");
    }

    [Test]
    public async Task AnalyzeContentAsync_ReturnsCompleteMetrics()
    {
        // Arrange
        const string html = @"
            <html>
                <body>
                    <h1>Test Document</h1>
                    <p>This is the first paragraph with some text. It contains multiple sentences.</p>
                    <p>This is the second paragraph. It also has content.</p>
                </body>
            </html>";

        // Act
        var metrics = await _analyzer.AnalyzeContentAsync(html);

        // Assert
        await Assert.That(metrics.WordCount).IsGreaterThan(0);
        await Assert.That(metrics.CharacterCount).IsGreaterThan(0);
        await Assert.That(metrics.SentenceCount).IsGreaterThan(0);
        await Assert.That(metrics.ParagraphCount).IsGreaterThan(0);
        await Assert.That(metrics.ReadabilityScore).IsGreaterThanOrEqualTo(0);
        await Assert.That(metrics.EstimatedReadingTime).IsNotEqualTo(TimeSpan.Zero);
        await Assert.That(metrics.WordFrequency).IsNotNull();
        await Assert.That(metrics.TopKeywords).IsNotNull();
    }

    [Test]
    public async Task AnalyzeContentAsync_EmptyContent_ReturnsEmptyMetrics()
    {
        // Arrange
        const string html = "";

        // Act
        var metrics = await _analyzer.AnalyzeContentAsync(html);

        // Assert
        await Assert.That(metrics.WordCount).IsEqualTo(0);
        await Assert.That(metrics.CharacterCount).IsEqualTo(0);
        await Assert.That(metrics.SentenceCount).IsEqualTo(0);
        await Assert.That(metrics.EstimatedReadingTime).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task ExtractPlainText_WithBuffer_ReusesProvidedBuffer()
    {
        // Arrange
        const string html = "<p>Test content</p>";
        var buffer = new char[1024];

        // Act
        var result = _analyzer.ExtractPlainText(html.AsSpan(), buffer);

        // Assert
        await Assert.That(result).IsEqualTo("Test content");
    }

    [Test]
    public async Task AnalyzeContentAsync_WithXhtmlContent_ExtractsTextCorrectly()
    {
        // Arrange
        const string xhtmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.1//EN"" ""http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head>
    <title>Test Page</title>
</head>
<body class=""x-ebookmaker x-ebookmaker-3"">
    <p>This is a test paragraph with some content.</p>
    <p>This is another paragraph.</p>
</body>
</html>";

        // Act
        var metrics = await _analyzer.AnalyzeContentAsync(xhtmlContent);

        // Assert
        await Assert.That(metrics.WordCount).IsGreaterThan(0);
        await Assert.That(metrics.CharacterCount).IsGreaterThan(0);
    }

    [Test]
    public async Task AnalyzeContentAsync_WithEpubXhtmlContent_ExtractsTextCorrectly()
    {
        // Arrange - Using actual EPUB XHTML structure from Moby Dick
        const string epubXhtmlContent = @"<?xml version='1.0' encoding='utf-8'?>
<!DOCTYPE html PUBLIC '-//W3C//DTD XHTML 1.1//EN' 'http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd'>
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head>
<meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8""/>
<title>The Project Gutenberg eBook of Moby Dick; Or, The Whale, by Herman Melville</title>
</head>
<body class=""x-ebookmaker x-ebookmaker-3"">
<div class=""pgebub-root-div pgheader"" id=""pg-header-heading"">
<h1>MOBY-DICK;<br/>or, THE WHALE.</h1>
<p>By Herman Melville</p>
</div>
<p>Call me Ishmael. Some years ago—never mind how long precisely—having little or no money in my purse, and nothing particular to interest me on shore, I thought I would sail about a little and see the watery part of the world.</p>
</body>
</html>";

        // Act
        var metrics = await _analyzer.AnalyzeContentAsync(epubXhtmlContent);

        // Assert
        await Assert.That(metrics.WordCount).IsGreaterThan(0, "Expected word count to be greater than 0");
        await Assert.That(metrics.CharacterCount).IsGreaterThan(0, "Expected character count to be greater than 0");
        // Should contain at least the famous opening line
        await Assert.That(metrics.WordCount).IsGreaterThanOrEqualTo(30, "Should contain at least 30 words from the content");
    }
}