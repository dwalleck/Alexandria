using Alexandria.Domain.Entities;

namespace Alexandria.Domain.Tests.Entities;

public class ChapterTests
{
    [Test]
    public async Task Should_Create_Valid_Chapter()
    {
        // Arrange
        const string id = "chapter-1";
        const string title = "Chapter 1: Introduction";
        const string content = "<html><body><p>This is the chapter content.</p></body></html>";
        const int order = 0;
        const string href = "chapter1.xhtml";

        // Act
        var chapter = new Chapter(id, title, content, order, href);

        // Assert
        await Assert.That(chapter.Id).IsEqualTo(id);
        await Assert.That(chapter.Title).IsEqualTo(title);
        await Assert.That(chapter.Content).IsEqualTo(content);
        await Assert.That(chapter.Order).IsEqualTo(order);
        await Assert.That(chapter.Href).IsEqualTo(href);
    }

    [Test]
    [Arguments(null, "Title", "Content", 0, "href.xhtml")]
    [Arguments("", "Title", "Content", 0, "href.xhtml")]
    [Arguments("id", null, "Content", 0, "href.xhtml")]
    [Arguments("id", "", "Content", 0, "href.xhtml")]
    [Arguments("id", "Title", null, 0, "href.xhtml")]
    [Arguments("id", "Title", "", 0, "href.xhtml")]
    [Arguments("id", "Title", "Content", -1, "href.xhtml")]
    [Arguments("id", "Title", "Content", 0, null)]
    [Arguments("id", "Title", "Content", 0, "")]
    public async Task Should_Throw_When_Parameters_Invalid(string? id, string? title, string? content, int order, string? href)
    {
        // Act & Assert
        await Assert.That(() => new Chapter(id!, title!, content!, order, href!))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Should_Calculate_Word_Count()
    {
        // Arrange
        var content = "<html><body><p>This is a test paragraph with exactly ten words here.</p></body></html>";
        var chapter = new Chapter("id", "Title", content, 0, "test.xhtml");

        // Act
        var wordCount = chapter.GetWordCount();

        // Assert
        await Assert.That(wordCount).IsEqualTo(10);
    }

    [Test]
    public async Task Should_Calculate_Word_Count_With_HTML_Tags()
    {
        // Arrange
        var content = """
            <html>
            <body>
                <h1>Chapter Title</h1>
                <p>This is the <strong>first</strong> paragraph.</p>
                <p>This is the <em>second</em> paragraph with more words.</p>
            </body>
            </html>
            """;
        var chapter = new Chapter("id", "Title", content, 0, "test.xhtml");

        // Act
        var wordCount = chapter.GetWordCount();

        // Assert
        await Assert.That(wordCount).IsEqualTo(14); // "Chapter Title This is the first paragraph This is the second paragraph with more words"
    }

    [Test]
    [Arguments(250, 1)] // 250 words = ~1 minute
    [Arguments(500, 2)] // 500 words = 2 minutes
    [Arguments(750, 3)] // 750 words = 3 minutes
    [Arguments(100, 1)] // Less than 250 = still 1 minute minimum
    public async Task Should_Calculate_Reading_Time(int wordCount, int expectedMinutes)
    {
        // Arrange
        var words = string.Join(" ", Enumerable.Repeat("word", wordCount));
        var content = $"<html><body><p>{words}</p></body></html>";
        var chapter = new Chapter("id", "Title", content, 0, "test.xhtml");

        // Act
        var readingTime = chapter.GetEstimatedReadingTime();

        // Assert
        await Assert.That(readingTime.TotalMinutes).IsEqualTo(expectedMinutes);
    }

    [Test]
    public async Task Should_Compare_Chapters_By_Order()
    {
        // Arrange
        var chapter1 = new Chapter("id1", "Chapter 1", "<p>Content 1</p>", 0, "ch1.xhtml");
        var chapter2 = new Chapter("id2", "Chapter 2", "<p>Content 2</p>", 1, "ch2.xhtml");
        var chapter3 = new Chapter("id3", "Chapter 3", "<p>Content 3</p>", 2, "ch3.xhtml");

        var chapters = new List<Chapter> { chapter3, chapter1, chapter2 };

        // Act
        var sorted = chapters.OrderBy(c => c.Order).ToList();

        // Assert
        await Assert.That(sorted[0]).IsEqualTo(chapter1);
        await Assert.That(sorted[1]).IsEqualTo(chapter2);
        await Assert.That(sorted[2]).IsEqualTo(chapter3);
    }

    [Test]
    public async Task Should_Strip_HTML_From_Content_For_WordCount()
    {
        // Arrange
        var content = """
            <html>
            <head><title>Test</title></head>
            <body>
                <script>console.log('test');</script>
                <style>body { color: red; }</style>
                <p>Only this text should be counted.</p>
            </body>
            </html>
            """;
        var chapter = new Chapter("id", "Title", content, 0, "test.xhtml");

        // Act
        var wordCount = chapter.GetWordCount();

        // Assert
        await Assert.That(wordCount).IsEqualTo(6); // "Only this text should be counted"
    }
}