using Alexandria.Domain.ValueObjects;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Domain.Tests.ValueObjects;

public class BookmarkTests
{
    [Test]
    public async Task Should_Create_Bookmark()
    {
        // Arrange & Act
        var bookmark = new Bookmark(
            "id1",
            "ch1",
            "Chapter 1",
            100,
            "Important section",
            DateTime.UtcNow,
            "This is the context..."
        );

        // Assert
        await Assert.That(bookmark.Id).IsEqualTo("id1");
        await Assert.That(bookmark.ChapterId).IsEqualTo("ch1");
        await Assert.That(bookmark.ChapterTitle).IsEqualTo("Chapter 1");
        await Assert.That(bookmark.Position).IsEqualTo(100);
        await Assert.That(bookmark.Note).IsEqualTo("Important section");
        await Assert.That(bookmark.ContextText).IsEqualTo("This is the context...");
    }

    [Test]
    public async Task Should_Throw_When_Required_Fields_Null()
    {
        // Act & Assert
        await Assert.That(() => new Bookmark(null!, "ch1", "Title", 0, null, DateTime.UtcNow))
            .Throws<ArgumentNullException>();

        await Assert.That(() => new Bookmark("id1", null!, "Title", 0, null, DateTime.UtcNow))
            .Throws<ArgumentNullException>();

        await Assert.That(() => new Bookmark("id1", "ch1", null!, 0, null, DateTime.UtcNow))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Should_Normalize_Negative_Position()
    {
        // Arrange & Act
        var bookmark = new Bookmark("id1", "ch1", "Title", -10, null, DateTime.UtcNow);

        // Assert
        await Assert.That(bookmark.Position).IsEqualTo(0);
    }

    [Test]
    public async Task Should_Update_Note()
    {
        // Arrange
        var bookmark = new Bookmark("id1", "ch1", "Title", 100, "Old note", DateTime.UtcNow);

        // Act
        var updated = bookmark.UpdateNote("New note");

        // Assert
        await Assert.That(updated.Note).IsEqualTo("New note");
        await Assert.That(updated.Id).IsEqualTo(bookmark.Id);
        await Assert.That(updated.Position).IsEqualTo(bookmark.Position);
    }

    [Test]
    public async Task Should_Create_New_Bookmark_With_Factory()
    {
        // Act
        var bookmark = Bookmark.Create("ch1", "Chapter 1", 100, "Note", "Context");

        // Assert
        await Assert.That(bookmark.Id).IsNotEmpty();
        await Assert.That(bookmark.ChapterId).IsEqualTo("ch1");
        await Assert.That(bookmark.ChapterTitle).IsEqualTo("Chapter 1");
        await Assert.That(bookmark.Position).IsEqualTo(100);
        await Assert.That(bookmark.Note).IsEqualTo("Note");
        await Assert.That(bookmark.ContextText).IsEqualTo("Context");
    }

    [Test]
    public async Task Should_Format_ToString()
    {
        // Arrange
        var bookmarkWithNote = new Bookmark("id1", "ch1", "Chapter 1", 100, "My note", DateTime.UtcNow);
        var bookmarkWithoutNote = new Bookmark("id2", "ch2", "Chapter 2", 200, null, DateTime.UtcNow);

        // Act
        var withNote = bookmarkWithNote.ToString();
        var withoutNote = bookmarkWithoutNote.ToString();

        // Assert
        await Assert.That(withNote).IsEqualTo("Chapter 1 (Position: 100) - My note");
        await Assert.That(withoutNote).IsEqualTo("Chapter 2 (Position: 200)");
    }
}

public class AnnotationTests
{
    [Test]
    public async Task Should_Create_Annotation()
    {
        // Arrange & Act
        var annotation = new Annotation(
            "id1",
            "ch1",
            100,
            150,
            "Highlighted text",
            "My note",
            HighlightColor.Yellow,
            DateTime.UtcNow
        );

        // Assert
        await Assert.That(annotation.Id).IsEqualTo("id1");
        await Assert.That(annotation.ChapterId).IsEqualTo("ch1");
        await Assert.That(annotation.StartPosition).IsEqualTo(100);
        await Assert.That(annotation.EndPosition).IsEqualTo(150);
        await Assert.That(annotation.HighlightedText).IsEqualTo("Highlighted text");
        await Assert.That(annotation.Note).IsEqualTo("My note");
        await Assert.That(annotation.Color).IsEqualTo(HighlightColor.Yellow);
        await Assert.That(annotation.Length).IsEqualTo(50);
    }

    [Test]
    public async Task Should_Throw_When_Required_Fields_Null()
    {
        // Act & Assert
        await Assert.That(() => new Annotation(null!, "ch1", 0, 10, "text", null, HighlightColor.Yellow, DateTime.UtcNow))
            .Throws<ArgumentNullException>();

        await Assert.That(() => new Annotation("id1", null!, 0, 10, "text", null, HighlightColor.Yellow, DateTime.UtcNow))
            .Throws<ArgumentNullException>();

        await Assert.That(() => new Annotation("id1", "ch1", 0, 10, null!, null, HighlightColor.Yellow, DateTime.UtcNow))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Should_Throw_When_Invalid_Positions()
    {
        // Act & Assert
        await Assert.That(() => new Annotation("id1", "ch1", -1, 10, "text", null, HighlightColor.Yellow, DateTime.UtcNow))
            .Throws<ArgumentOutOfRangeException>();

        await Assert.That(() => new Annotation("id1", "ch1", 10, 10, "text", null, HighlightColor.Yellow, DateTime.UtcNow))
            .Throws<ArgumentException>();

        await Assert.That(() => new Annotation("id1", "ch1", 10, 5, "text", null, HighlightColor.Yellow, DateTime.UtcNow))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Should_Update_Note()
    {
        // Arrange
        var annotation = Annotation.Create("ch1", 0, 10, "text", HighlightColor.Yellow, "Old note");

        // Act
        var updated = annotation.UpdateNote("New note");

        // Assert
        await Assert.That(updated.Note).IsEqualTo("New note");
        await Assert.That(updated.Id).IsEqualTo(annotation.Id);
        await Assert.That(updated.StartPosition).IsEqualTo(annotation.StartPosition);
    }

    [Test]
    public async Task Should_Change_Color()
    {
        // Arrange
        var annotation = Annotation.Create("ch1", 0, 10, "text", HighlightColor.Yellow);

        // Act
        var updated = annotation.ChangeColor(HighlightColor.Green);

        // Assert
        await Assert.That(updated.Color).IsEqualTo(HighlightColor.Green);
        await Assert.That(updated.Id).IsEqualTo(annotation.Id);
    }

    [Test]
    public async Task Should_Create_New_Annotation_With_Factory()
    {
        // Act
        var annotation = Annotation.Create("ch1", 100, 200, "Highlighted", HighlightColor.Blue, "Note");

        // Assert
        await Assert.That(annotation.Id).IsNotEmpty();
        await Assert.That(annotation.ChapterId).IsEqualTo("ch1");
        await Assert.That(annotation.StartPosition).IsEqualTo(100);
        await Assert.That(annotation.EndPosition).IsEqualTo(200);
        await Assert.That(annotation.HighlightedText).IsEqualTo("Highlighted");
        await Assert.That(annotation.Color).IsEqualTo(HighlightColor.Blue);
        await Assert.That(annotation.Note).IsEqualTo("Note");
    }

    [Test]
    public async Task Should_Have_All_Highlight_Colors()
    {
        // Assert
        await Assert.That(Enum.GetValues<HighlightColor>()).Contains(HighlightColor.Yellow);
        await Assert.That(Enum.GetValues<HighlightColor>()).Contains(HighlightColor.Green);
        await Assert.That(Enum.GetValues<HighlightColor>()).Contains(HighlightColor.Blue);
        await Assert.That(Enum.GetValues<HighlightColor>()).Contains(HighlightColor.Pink);
        await Assert.That(Enum.GetValues<HighlightColor>()).Contains(HighlightColor.Orange);
        await Assert.That(Enum.GetValues<HighlightColor>()).Contains(HighlightColor.Purple);
    }
}