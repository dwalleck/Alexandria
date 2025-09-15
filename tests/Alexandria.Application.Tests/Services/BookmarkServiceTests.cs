using Alexandria.Application.Services;
using Alexandria.Domain.Entities;
using Alexandria.Domain.ValueObjects;

namespace Alexandria.Application.Tests.Services;

public class BookmarkServiceTests
{
    private readonly BookmarkService _service = new();

    private static Book CreateTestBook()
    {
        var chapters = new List<Chapter>
        {
            new("ch1", "Chapter 1", "<p>First chapter content here.</p>", 0, "ch1.xhtml"),
            new("ch2", "Chapter 2", "<p>Second chapter content here.</p>", 1, "ch2.xhtml"),
            new("ch3", "Chapter 3", "<p>Third chapter content here.</p>", 2, "ch3.xhtml")
        };

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
    public async Task Should_Add_Bookmark()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var bookmark = _service.AddBookmark(book, "ch1", 100, "Important section");

        // Assert
        await Assert.That(bookmark).IsNotNull();
        await Assert.That(bookmark.ChapterId).IsEqualTo("ch1");
        await Assert.That(bookmark.ChapterTitle).IsEqualTo("Chapter 1");
        await Assert.That(bookmark.Position).IsEqualTo(100);
        await Assert.That(bookmark.Note).IsEqualTo("Important section");
    }

    [Test]
    public async Task Should_Throw_When_Chapter_Not_Found()
    {
        // Arrange
        var book = CreateTestBook();

        // Act & Assert
        await Assert.That(() => _service.AddBookmark(book, "invalid", 100))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Should_Get_All_Bookmarks()
    {
        // Arrange
        var book = CreateTestBook();
        _service.AddBookmark(book, "ch1", 100);
        _service.AddBookmark(book, "ch2", 200);
        _service.AddBookmark(book, "ch3", 300);

        // Act
        var bookmarks = _service.GetBookmarks(book).ToList();

        // Assert
        await Assert.That(bookmarks).HasCount(3);
    }

    [Test]
    public async Task Should_Get_Chapter_Bookmarks()
    {
        // Arrange
        var book = CreateTestBook();
        _service.AddBookmark(book, "ch1", 100);
        _service.AddBookmark(book, "ch1", 200);
        _service.AddBookmark(book, "ch2", 300);

        // Act
        var ch1Bookmarks = _service.GetChapterBookmarks(book, "ch1").ToList();

        // Assert
        await Assert.That(ch1Bookmarks).HasCount(2);
        await Assert.That(ch1Bookmarks.All(b => b.ChapterId == "ch1")).IsTrue();
    }

    [Test]
    public async Task Should_Remove_Bookmark()
    {
        // Arrange
        var book = CreateTestBook();
        var bookmark = _service.AddBookmark(book, "ch1", 100);

        // Act
        var removed = _service.RemoveBookmark(book, bookmark.Id);
        var bookmarks = _service.GetBookmarks(book).ToList();

        // Assert
        await Assert.That(removed).IsTrue();
        await Assert.That(bookmarks).HasCount(0);
    }

    [Test]
    public async Task Should_Return_False_When_Removing_NonExistent_Bookmark()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var removed = _service.RemoveBookmark(book, "non-existent");

        // Assert
        await Assert.That(removed).IsFalse();
    }

    [Test]
    public async Task Should_Add_Annotation()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var annotation = _service.AddAnnotation(book, "ch1", 10, 20, "highlighted", HighlightColor.Yellow, "Note");

        // Assert
        await Assert.That(annotation).IsNotNull();
        await Assert.That(annotation.ChapterId).IsEqualTo("ch1");
        await Assert.That(annotation.StartPosition).IsEqualTo(10);
        await Assert.That(annotation.EndPosition).IsEqualTo(20);
        await Assert.That(annotation.HighlightedText).IsEqualTo("highlighted");
        await Assert.That(annotation.Color).IsEqualTo(HighlightColor.Yellow);
        await Assert.That(annotation.Note).IsEqualTo("Note");
    }

    [Test]
    public async Task Should_Get_All_Annotations()
    {
        // Arrange
        var book = CreateTestBook();
        _service.AddAnnotation(book, "ch1", 10, 20, "text1");
        _service.AddAnnotation(book, "ch2", 30, 40, "text2");

        // Act
        var annotations = _service.GetAnnotations(book).ToList();

        // Assert
        await Assert.That(annotations).HasCount(2);
    }

    [Test]
    public async Task Should_Get_Chapter_Annotations()
    {
        // Arrange
        var book = CreateTestBook();
        _service.AddAnnotation(book, "ch1", 10, 20, "text1");
        _service.AddAnnotation(book, "ch1", 30, 40, "text2");
        _service.AddAnnotation(book, "ch2", 50, 60, "text3");

        // Act
        var ch1Annotations = _service.GetChapterAnnotations(book, "ch1").ToList();

        // Assert
        await Assert.That(ch1Annotations).HasCount(2);
        await Assert.That(ch1Annotations.All(a => a.ChapterId == "ch1")).IsTrue();
    }

    [Test]
    public async Task Should_Remove_Annotation()
    {
        // Arrange
        var book = CreateTestBook();
        var annotation = _service.AddAnnotation(book, "ch1", 10, 20, "text");

        // Act
        var removed = _service.RemoveAnnotation(book, annotation.Id);
        var annotations = _service.GetAnnotations(book).ToList();

        // Assert
        await Assert.That(removed).IsTrue();
        await Assert.That(annotations).HasCount(0);
    }

    [Test]
    public async Task Should_Update_Reading_Progress()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var progress = _service.UpdateReadingProgress(book, "ch2", 100);

        // Assert
        await Assert.That(progress).IsNotNull();
        await Assert.That(progress.BookId).IsEqualTo("Test Book");
        await Assert.That(progress.ChapterId).IsEqualTo("ch2");
        await Assert.That(progress.PositionInChapter).IsEqualTo(100);
    }

    [Test]
    public async Task Should_Get_Reading_Progress()
    {
        // Arrange
        var book = CreateTestBook();
        _service.UpdateReadingProgress(book, "ch2", 100);

        // Act
        var progress = _service.GetReadingProgress(book);

        // Assert
        await Assert.That(progress).IsNotNull();
        await Assert.That(progress!.ChapterId).IsEqualTo("ch2");
    }

    [Test]
    public async Task Should_Return_Null_When_No_Reading_Progress()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var progress = _service.GetReadingProgress(book);

        // Assert
        await Assert.That(progress).IsNull();
    }

    [Test]
    public async Task Should_Clear_All_Book_Data()
    {
        // Arrange
        var book = CreateTestBook();
        _service.AddBookmark(book, "ch1", 100);
        _service.AddAnnotation(book, "ch1", 10, 20, "text");
        _service.UpdateReadingProgress(book, "ch1", 50);

        // Act
        _service.ClearBookData(book);

        // Assert
        await Assert.That(_service.GetBookmarks(book)).HasCount(0);
        await Assert.That(_service.GetAnnotations(book)).HasCount(0);
        await Assert.That(_service.GetReadingProgress(book)).IsNull();
    }

    [Test]
    public async Task Should_Export_Bookmarks()
    {
        // Arrange
        var book = CreateTestBook();
        _service.AddBookmark(book, "ch1", 100, "Note 1");
        _service.AddBookmark(book, "ch2", 200, "Note 2");
        _service.AddAnnotation(book, "ch1", 10, 20, "text1");
        _service.UpdateReadingProgress(book, "ch2", 150);

        // Act
        var export = _service.ExportBookmarks(book);

        // Assert
        await Assert.That(export.BookTitle).IsEqualTo("Test Book");
        await Assert.That(export.TotalBookmarks).IsEqualTo(2);
        await Assert.That(export.TotalAnnotations).IsEqualTo(1);
        await Assert.That(export.ReadingProgress).IsNotNull();
        await Assert.That(export.ReadingProgress!.ChapterId).IsEqualTo("ch2");
    }
}