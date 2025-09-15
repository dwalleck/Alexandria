using Alexandria.Domain.Services;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Application.Tests.Services;

public class ContentProcessorTests
{
    private readonly ContentProcessor _processor = new();

    [Test]
    public async Task Should_Extract_Plain_Text_From_Html()
    {
        // Arrange
        var html = "<p>This is a <strong>test</strong> paragraph.</p><p>Another paragraph here.</p>";

        // Act
        var result = _processor.ExtractPlainText(html);

        // Assert
        await Assert.That(result).IsEqualTo("This is a test paragraph.\n\nAnother paragraph here.");
    }

    [Test]
    public async Task Should_Handle_Headings_With_Line_Breaks()
    {
        // Arrange
        var html = "<h1>Title</h1><p>Content after title.</p><h2>Subtitle</h2><p>More content.</p>";

        // Act
        var result = _processor.ExtractPlainText(html);

        // Assert
        await Assert.That(result).Contains("Title");
        await Assert.That(result).Contains("Content after title");
        await Assert.That(result).Contains("Subtitle");
        await Assert.That(result).Contains("More content");
    }

    [Test]
    public async Task Should_Convert_List_Items_To_Bullets()
    {
        // Arrange
        var html = "<ul><li>First item</li><li>Second item</li><li>Third item</li></ul>";

        // Act
        var result = _processor.ExtractPlainText(html);

        // Assert
        await Assert.That(result).Contains("• First item");
        await Assert.That(result).Contains("• Second item");
        await Assert.That(result).Contains("• Third item");
    }

    [Test]
    public async Task Should_Handle_Line_Breaks()
    {
        // Arrange
        var html = "Line one<br/>Line two<br />Line three";

        // Act
        var result = _processor.ExtractPlainText(html);

        // Assert
        await Assert.That(result).Contains("Line one\nLine two\nLine three");
    }

    [Test]
    public async Task Should_Decode_Html_Entities()
    {
        // Arrange
        var html = "<p>&quot;Hello &amp; goodbye&quot; &lt;test&gt;</p>";

        // Act
        var result = _processor.ExtractPlainText(html);

        // Assert
        await Assert.That(result).IsEqualTo("\"Hello & goodbye\" <test>");
    }

    [Test]
    public async Task Should_Count_Words_Correctly()
    {
        // Arrange
        var html = "<p>This is a test paragraph with exactly ten words here.</p>";

        // Act
        var count = _processor.CountWords(html);

        // Assert
        await Assert.That(count).IsEqualTo(10);
    }

    [Test]
    public async Task Should_Count_Words_Ignoring_Html_Tags()
    {
        // Arrange
        var html = "<p>One <strong>two</strong> <em>three</em> <span>four</span> five.</p>";

        // Act
        var count = _processor.CountWords(html);

        // Assert
        await Assert.That(count).IsEqualTo(5);
    }

    [Test]
    public async Task Should_Return_Zero_For_Empty_Content()
    {
        // Arrange
        var html = "<p></p><div></div>";

        // Act
        var count = _processor.CountWords(html);

        // Assert
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Should_Extract_Snippet_With_Context()
    {
        // Arrange
        var html = "<p>This is a long paragraph with many words. The search term appears here in the middle of the content. There is more text after the term.</p>";

        // Act
        var snippet = _processor.ExtractSnippet(html, "search term", 20);

        // Assert
        await Assert.That(snippet).Contains("search term");
        await Assert.That(snippet).StartsWith("...");
        await Assert.That(snippet).EndsWith("...");
    }

    [Test]
    public async Task Should_Return_Empty_Snippet_When_Term_Not_Found()
    {
        // Arrange
        var html = "<p>This is some content without the term.</p>";

        // Act
        var snippet = _processor.ExtractSnippet(html, "missing", 20);

        // Assert
        await Assert.That(snippet).IsEmpty();
    }

    [Test]
    public async Task Should_Highlight_Search_Terms()
    {
        // Arrange
        var text = "The quick brown fox jumps over the lazy dog.";
        var terms = new[] { "quick", "fox", "dog" };

        // Act
        var result = _processor.HighlightTerms(text, terms);

        // Assert
        await Assert.That(result).IsEqualTo("The **quick** brown **fox** jumps over the lazy **dog**.");
    }

    [Test]
    public async Task Should_Highlight_Case_Insensitive()
    {
        // Arrange
        var text = "The Quick BROWN Fox jumps.";
        var terms = new[] { "quick", "brown", "fox" };

        // Act
        var result = _processor.HighlightTerms(text, terms);

        // Assert
        await Assert.That(result).Contains("**Quick**");
        await Assert.That(result).Contains("**BROWN**");
        await Assert.That(result).Contains("**Fox**");
    }

    [Test]
    public async Task Should_Extract_Sentences()
    {
        // Arrange
        var html = "<p>First sentence. Second sentence! Third sentence? Fourth.</p>";

        // Act
        var sentences = _processor.ExtractSentences(html).ToList();

        // Assert
        await Assert.That(sentences).HasCount(4);
        await Assert.That(sentences[0]).IsEqualTo("First sentence.");
        await Assert.That(sentences[1]).IsEqualTo("Second sentence!");
        await Assert.That(sentences[2]).IsEqualTo("Third sentence?");
        await Assert.That(sentences[3]).IsEqualTo("Fourth.");
    }

    [Test]
    public async Task Should_Estimate_Reading_Time()
    {
        // Arrange - 250 words at 250 WPM = 1 minute
        var words = string.Join(" ", Enumerable.Repeat("word", 250));
        var html = $"<p>{words}</p>";

        // Act
        var time = _processor.EstimateReadingTime(html, 250);

        // Assert
        await Assert.That(time.TotalMinutes).IsEqualTo(1.0);
    }

    [Test]
    public async Task Should_Estimate_Reading_Time_With_Custom_Speed()
    {
        // Arrange - 500 words at 100 WPM = 5 minutes
        var words = string.Join(" ", Enumerable.Repeat("word", 500));
        var html = $"<p>{words}</p>";

        // Act
        var time = _processor.EstimateReadingTime(html, 100);

        // Assert
        await Assert.That(time.TotalMinutes).IsEqualTo(5.0);
    }

    [Test]
    public async Task Should_Extract_Preview()
    {
        // Arrange
        var html = "<p>This is a long piece of content that should be truncated to create a preview. It has many words and goes on for quite a while.</p>";

        // Act
        var preview = _processor.ExtractPreview(html, 50);

        // Assert
        await Assert.That(preview.Length).IsLessThanOrEqualTo(53); // 50 + "..."
        await Assert.That(preview).EndsWith("...");
    }

    [Test]
    public async Task Should_Return_Full_Text_If_Under_Preview_Limit()
    {
        // Arrange
        var html = "<p>Short content.</p>";

        // Act
        var preview = _processor.ExtractPreview(html, 100);

        // Assert
        await Assert.That(preview).IsEqualTo("Short content.");
        await Assert.That(preview).DoesNotEndWith("...");
    }

    [Test]
    public async Task Should_Handle_Empty_Or_Null_Content()
    {
        // Act & Assert
        await Assert.That(_processor.ExtractPlainText(null!)).IsEmpty();
        await Assert.That(_processor.ExtractPlainText("")).IsEmpty();
        await Assert.That(_processor.ExtractPlainText("   ")).IsEmpty();
        await Assert.That(_processor.CountWords(null!)).IsEqualTo(0);
        await Assert.That(_processor.ExtractSnippet(null!, "term")).IsEmpty();
    }
}