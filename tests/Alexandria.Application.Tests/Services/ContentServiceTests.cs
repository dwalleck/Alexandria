using Alexandria.Application.Services;
using Alexandria.Domain.Entities;
using Alexandria.Domain.Services;
using Alexandria.Domain.ValueObjects;

namespace Alexandria.Application.Tests.Services;

public class ContentServiceTests
{
    private readonly IContentService _contentService = new ContentService();

    private static Book CreateTestBook(int chapterCount = 3)
    {
        var chapters = new List<Chapter>();
        for (int i = 0; i < chapterCount; i++)
        {
            var content = string.Join(" ", Enumerable.Repeat("word", 250)); // 250 words per chapter
            chapters.Add(new Chapter($"ch{i}", $"Chapter {i + 1}", $"<p>{content}</p>", i, $"ch{i}.xhtml"));
        }

        return new Book(
            new BookTitle("Test Book"),
            new List<BookTitle>(),
            new List<Author> { new("Test Author") },
            chapters,
            new List<BookIdentifier>(),
            new Language("en"),
            new BookMetadata()
        );
    }

    [Test]
    public async Task Should_Search_Book_Content()
    {
        // Arrange
        var chapters = new List<Chapter>
        {
            new("ch1", "Chapter 1", "<p>The quick brown fox jumps.</p>", 0, "ch1.xhtml"),
            new("ch2", "Chapter 2", "<p>The lazy dog sleeps.</p>", 1, "ch2.xhtml")
        };
        var book = new Book(
            new BookTitle("Test"),
            new List<BookTitle>(),
            new List<Author> { new("Author") },
            chapters,
            new List<BookIdentifier>(),
            new Language("en"),
            new BookMetadata()
        );

        // Act
        var results = _contentService.Search(book, "fox").ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].Chapter.Id).IsEqualTo("ch1");
    }

    [Test]
    public async Task Should_Get_Chapter_Plain_Text()
    {
        // Arrange
        var book = CreateTestBook();
        var chapter = book.GetChapterById("ch0");

        // Act
        var plainText = chapter != null ? _contentService.GetChapterPlainText(chapter) : string.Empty;

        // Assert
        await Assert.That(plainText).Contains("word");
        await Assert.That(plainText).DoesNotContain("<p>");
        await Assert.That(plainText).DoesNotContain("</p>");
    }

    [Test]
    public async Task Should_Return_Empty_For_Invalid_Chapter()
    {
        // Arrange
        var book = CreateTestBook();
        var chapter = book.GetChapterById("invalid");

        // Act
        var plainText = chapter != null ? _contentService.GetChapterPlainText(chapter) : string.Empty;

        // Assert
        await Assert.That(plainText).IsEmpty();
    }

    [Test]
    public async Task Should_Get_Full_Plain_Text()
    {
        // Arrange
        var chapters = new List<Chapter>
        {
            new("ch1", "One", "<p>First chapter content.</p>", 0, "ch1.xhtml"),
            new("ch2", "Two", "<p>Second chapter content.</p>", 1, "ch2.xhtml")
        };
        var book = new Book(
            new BookTitle("Test"),
            new List<BookTitle>(),
            new List<Author> { new("Author") },
            chapters,
            new List<BookIdentifier>(),
            new Language("en"),
            new BookMetadata()
        );

        // Act
        var fullText = _contentService.GetFullPlainText(book);

        // Assert
        await Assert.That(fullText).Contains("First chapter");
        await Assert.That(fullText).Contains("Second chapter");
        await Assert.That(fullText).Contains("\n\n"); // Chapters separated by double newline
    }

    [Test]
    public async Task Should_Get_Book_Preview()
    {
        // Arrange
        var longContent = string.Join(" ", Enumerable.Repeat("word", 200));
        var chapters = new List<Chapter>
        {
            new("ch1", "One", $"<p>{longContent}</p>", 0, "ch1.xhtml")
        };
        var book = new Book(
            new BookTitle("Test"),
            new List<BookTitle>(),
            new List<Author> { new("Author") },
            chapters,
            new List<BookIdentifier>(),
            new Language("en"),
            new BookMetadata()
        );

        // Act
        var preview = _contentService.GetPreview(book, 100);

        // Assert
        await Assert.That(preview.Length).IsLessThanOrEqualTo(103); // 100 + "..."
        await Assert.That(preview).EndsWith("...");
    }

    [Test]
    public async Task Should_Get_Reading_Statistics()
    {
        // Arrange - 3 chapters with 250 words each
        var book = CreateTestBook(3);

        // Act
        var stats = _contentService.GetReadingStatistics(book, 250);

        // Assert
        await Assert.That(stats.TotalWords).IsEqualTo(750);
        await Assert.That(stats.ChapterStatistics).HasCount(3);
        await Assert.That(stats.AverageWordsPerChapter).IsEqualTo(250);
        await Assert.That(stats.TotalReadingTime.TotalMinutes).IsEqualTo(3.0);
    }

    [Test]
    public async Task Should_Find_Chapters_With_Term()
    {
        // Arrange
        var chapters = new List<Chapter>
        {
            new("ch1", "One", "<p>Contains special term here.</p>", 0, "ch1.xhtml"),
            new("ch2", "Two", "<p>Does not contain it.</p>", 1, "ch2.xhtml"),
            new("ch3", "Three", "<p>Has special term again.</p>", 2, "ch3.xhtml")
        };
        var book = new Book(
            new BookTitle("Test"),
            new List<BookTitle>(),
            new List<Author> { new("Author") },
            chapters,
            new List<BookIdentifier>(),
            new Language("en"),
            new BookMetadata()
        );

        // Act
        var foundChapters = _contentService.FindChaptersWithTerm(book, "special").ToList();

        // Assert
        await Assert.That(foundChapters).HasCount(2);
        await Assert.That(foundChapters[0].Id).IsEqualTo("ch1");
        await Assert.That(foundChapters[1].Id).IsEqualTo("ch3");
    }

    [Test]
    public async Task Should_Search_All_Terms_With_AND_Logic()
    {
        // Arrange
        var chapters = new List<Chapter>
        {
            new("ch1", "One", "<p>Has both cat and dog here.</p>", 0, "ch1.xhtml"),
            new("ch2", "Two", "<p>Only has cat.</p>", 1, "ch2.xhtml"),
            new("ch3", "Three", "<p>Only has dog.</p>", 2, "ch3.xhtml")
        };
        var book = new Book(
            new BookTitle("Test"),
            new List<BookTitle>(),
            new List<Author> { new("Author") },
            chapters,
            new List<BookIdentifier>(),
            new Language("en"),
            new BookMetadata()
        );

        // Act
        var results = _contentService.SearchAll(book, new[] { "cat", "dog" }).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].Chapter.Id).IsEqualTo("ch1");
    }

    [Test]
    public async Task Should_Search_Any_Terms_With_OR_Logic()
    {
        // Arrange
        var chapters = new List<Chapter>
        {
            new("ch1", "One", "<p>Has cat here.</p>", 0, "ch1.xhtml"),
            new("ch2", "Two", "<p>Has dog here.</p>", 1, "ch2.xhtml"),
            new("ch3", "Three", "<p>Has neither.</p>", 2, "ch3.xhtml")
        };
        var book = new Book(
            new BookTitle("Test"),
            new List<BookTitle>(),
            new List<Author> { new("Author") },
            chapters,
            new List<BookIdentifier>(),
            new Language("en"),
            new BookMetadata()
        );

        // Act
        var results = _contentService.SearchAny(book, new[] { "cat", "dog" }).ToList();

        // Assert
        await Assert.That(results).HasCount(2);
        await Assert.That(results.Any(r => r.Chapter.Id == "ch1")).IsTrue();
        await Assert.That(results.Any(r => r.Chapter.Id == "ch2")).IsTrue();
    }
}