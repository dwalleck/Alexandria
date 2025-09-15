using Alexandria.Infrastructure.Services;

namespace Alexandria.Infrastructure.Tests.Services;

/// <summary>
/// Regression tests for bugs found in AngleSharpContentAnalyzer.
/// These tests ensure the bugs identified by the agent review don't reoccur.
/// </summary>
public class AngleSharpContentAnalyzerRegressionTests
{
    private readonly AngleSharpContentAnalyzer _analyzer = new();

    [Test]
    public async Task HighlightTerms_ShouldBeOrderIndependent()
    {
        // Arrange
        const string text = "The quick brown fox jumps over the lazy dog. The fox is clever.";
        var termsOrder1 = new[] { "fox", "quick", "dog" };
        var termsOrder2 = new[] { "dog", "fox", "quick" };
        var termsOrder3 = new[] { "quick", "dog", "fox" };

        // Act
        var result1 = _analyzer.HighlightTerms(text, termsOrder1);
        var result2 = _analyzer.HighlightTerms(text, termsOrder2);
        var result3 = _analyzer.HighlightTerms(text, termsOrder3);

        // Assert - All orderings should produce the same result
        await Assert.That(result1).IsEqualTo(result2);
        await Assert.That(result2).IsEqualTo(result3);

        // Verify the expected highlights are present
        await Assert.That(result1).Contains("**quick**");
        await Assert.That(result1).Contains("**fox**");
        await Assert.That(result1).Contains("**dog**");
    }

    [Test]
    public async Task HighlightTerms_ShouldHandleOverlappingTermsCorrectly()
    {
        // Arrange
        const string text = "The testing process includes test cases and testing tools.";
        var overlappingTerms = new[] { "test", "testing", "test cases" };

        // Act
        var result = _analyzer.HighlightTerms(text, overlappingTerms);

        // Assert - Algorithm now prioritizes longer matches
        // "testing" at position 4 gets highlighted (longer than "test")
        // "test cases" at position 29 gets highlighted as a unit (longer than just "test")
        // "testing" at position 45 gets highlighted (longer than "test")
        await Assert.That(result).Contains("The **testing** process");
        await Assert.That(result).Contains("includes **test cases**");
        await Assert.That(result).Contains("and **testing** tools");

        // Count the number of highlights (should be 3, not more due to overlap handling)
        var highlightCount = 0;
        var index = 0;
        while ((index = result.IndexOf("**", index, StringComparison.Ordinal)) != -1)
        {
            highlightCount++;
            index += 2;
        }
        // Each highlight has two ** markers, so we divide by 2
        await Assert.That(highlightCount / 2).IsEqualTo(3);
    }

    [Test]
    public async Task HighlightTerms_ShouldPrioritizeLongerMatches()
    {
        // Arrange
        const string text = "This is a test of testing with test cases.";
        var terms = new[] { "test", "testing", "test cases" };

        // Act
        var result = _analyzer.HighlightTerms(text, terms);

        // Assert - Longer matches should be prioritized
        await Assert.That(result).Contains("a **test** of"); // standalone "test"
        await Assert.That(result).Contains("of **testing** with"); // "testing" not "test"
        await Assert.That(result).Contains("with **test cases**."); // "test cases" as unit
    }

    [Test]
    public async Task HighlightTerms_ShouldHighlightAllOccurrencesRegardlessOfOrder()
    {
        // Arrange
        const string text = "cat dog cat dog cat";
        var terms1 = new[] { "cat", "dog" };
        var terms2 = new[] { "dog", "cat" };

        // Act
        var result1 = _analyzer.HighlightTerms(text, terms1);
        var result2 = _analyzer.HighlightTerms(text, terms2);

        // Assert
        await Assert.That(result1).IsEqualTo(result2);
        await Assert.That(result1).IsEqualTo("**cat** **dog** **cat** **dog** **cat**");
    }

    [Test]
    public async Task SentenceCounting_ShouldBeConsistentBetweenMethods()
    {
        // Arrange
        const string text = @"This is a test. Dr. Smith went to the U.S.A. yesterday.
                             He arrived at 5 p.m. The meeting was productive!
                             What do you think? I'll consider it.";

        const string html = $"<p>{text}</p>";

        // Act
        var standaloneSentenceCount = _analyzer.CountSentences(text.AsSpan());
        var metricsResult = await _analyzer.AnalyzeContentAsync(html);

        // Assert - Both methods should count sentences the same way
        await Assert.That(metricsResult.SentenceCount).IsEqualTo(standaloneSentenceCount);
    }

    [Test]
    public async Task SentenceCounting_ShouldHandleAbbreviationsConsistently()
    {
        // Arrange - Text with clear sentence boundaries
        const string text = "This is the first sentence. This is the second sentence. And this is the third one!";
        const string html = $"<p>{text}</p>";

        // Act
        var standaloneSentenceCount = _analyzer.CountSentences(text.AsSpan());
        var metricsResult = await _analyzer.AnalyzeContentAsync(html);

        // Assert - Both methods should count the same
        await Assert.That(standaloneSentenceCount).IsEqualTo(3);
        await Assert.That(metricsResult.SentenceCount).IsEqualTo(3);
    }

    [Test]
    public async Task ReadingTime_ShouldNotRecalculateWordCount()
    {
        // Arrange - Large text to make any performance difference noticeable
        var words = new string[10000];
        for (int i = 0; i < words.Length; i++)
        {
            words[i] = "word";
        }
        var text = string.Join(" ", words);
        var html = $"<p>{text}</p>";

        // Act
        var startTime = DateTime.UtcNow;
        var metrics = await _analyzer.AnalyzeContentAsync(html);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        await Assert.That(metrics.WordCount).IsEqualTo(10000);
        await Assert.That(metrics.EstimatedReadingTime.TotalMinutes).IsGreaterThan(39); // 10000/250 = 40 minutes
        await Assert.That(metrics.EstimatedReadingTime.TotalMinutes).IsLessThan(41);

        // The operation should be fast since we're not recalculating
        await Assert.That(duration.TotalMilliseconds).IsLessThan(500);
    }

    [Test]
    public async Task HighlightTerms_WithCaseInsensitiveMatching_ShouldWork()
    {
        // Arrange
        const string text = "The Fox jumped. A fox ran. FOX is clever.";
        var terms = new[] { "fox" };

        // Act
        var result = _analyzer.HighlightTerms(text, terms);

        // Assert - Should highlight all case variations
        await Assert.That(result).Contains("**Fox**");
        await Assert.That(result).Contains("**fox**");
        await Assert.That(result).Contains("**FOX**");
    }

    [Test]
    public async Task HighlightTerms_WithEmptyOrNullTerms_ShouldReturnOriginalText()
    {
        // Arrange
        const string text = "This is a test text.";
        var emptyTerms = Array.Empty<string>();
        var termsWithNulls = new[] { null, "", "  " };

        // Act
        var resultEmpty = _analyzer.HighlightTerms(text, emptyTerms);
        var resultNulls = _analyzer.HighlightTerms(text, termsWithNulls!);

        // Assert
        await Assert.That(resultEmpty).IsEqualTo(text);
        await Assert.That(resultNulls).IsEqualTo(text);
    }

    [Test]
    public async Task EstimatedReadingTime_ShouldUseConfiguredWordsPerMinute()
    {
        // Arrange
        var text = string.Join(" ", new string[500].Select(_ => "word"));

        // Act
        var defaultTime = _analyzer.EstimateReadingTime(text.AsSpan());
        var fastTime = _analyzer.EstimateReadingTime(text.AsSpan(), 500);
        var slowTime = _analyzer.EstimateReadingTime(text.AsSpan(), 100);

        // Assert
        // 500 words at 250 wpm = 2 minutes
        await Assert.That(defaultTime.TotalSeconds).IsGreaterThanOrEqualTo(120);
        await Assert.That(defaultTime.TotalSeconds).IsLessThanOrEqualTo(150);

        // 500 words at 500 wpm = 1 minute
        await Assert.That(fastTime.TotalSeconds).IsGreaterThanOrEqualTo(60);
        await Assert.That(fastTime.TotalSeconds).IsLessThanOrEqualTo(90);

        // 500 words at 100 wpm = 5 minutes
        await Assert.That(slowTime.TotalSeconds).IsGreaterThanOrEqualTo(300);
        await Assert.That(slowTime.TotalSeconds).IsLessThanOrEqualTo(330);
    }
}