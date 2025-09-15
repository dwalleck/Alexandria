using Alexandria.Domain.Entities;
using Alexandria.Domain.ValueObjects;

namespace Alexandria.Domain.Tests.Entities;

/// <summary>
/// Tests for Phase 4 Book entity convenience methods
/// </summary>
public class BookPhase4MetadataTests
{
    private Book CreateBookWithFullMetadata()
    {
        var metadata = new BookMetadata(
            publisher: "Test Publisher",
            publicationDate: new DateTime(2024, 1, 1),
            description: "A test book",
            isbn: "978-0-316-76948-0",
            series: "Test Series",
            seriesNumber: 2,
            tags: new[] { "fiction", "adventure" },
            epubVersion: "3.0",
            subject: "Fiction"
        );

        return new Book(
            new BookTitle("Test Book"),
            null,
            new[] { new Author("John Doe") },
            new[] { new Chapter("ch1", "Chapter 1", "Content", 0) },
            new[] { new BookIdentifier("978-0-316-76948-0", "isbn") },
            new Language("en"),
            metadata
        );
    }

    private Book CreateBookWithMinimalMetadata()
    {
        return new Book(
            new BookTitle("Minimal Book"),
            null,
            new[] { new Author("Jane Doe") },
            new[] { new Chapter("ch1", "Chapter 1", "Content", 0) },
            new[] { new BookIdentifier("123", "uuid") },
            new Language("en"),
            BookMetadata.Empty
        );
    }

    [Test]
    public async Task GetEpubVersion_Should_Return_Version_When_Present()
    {
        // Arrange
        var book = CreateBookWithFullMetadata();

        // Act
        var version = book.GetEpubVersion();

        // Assert
        await Assert.That(version).IsEqualTo("3.0");
    }

    [Test]
    public async Task GetEpubVersion_Should_Return_Null_When_Not_Present()
    {
        // Arrange
        var book = CreateBookWithMinimalMetadata();

        // Act
        var version = book.GetEpubVersion();

        // Assert
        await Assert.That(version).IsNull();
    }

    [Test]
    public async Task GetSeries_Should_Return_Series_Name()
    {
        // Arrange
        var book = CreateBookWithFullMetadata();

        // Act
        var series = book.GetSeries();

        // Assert
        await Assert.That(series).IsEqualTo("Test Series");
    }

    [Test]
    public async Task GetSeries_Should_Return_Null_When_Not_Part_Of_Series()
    {
        // Arrange
        var book = CreateBookWithMinimalMetadata();

        // Act
        var series = book.GetSeries();

        // Assert
        await Assert.That(series).IsNull();
    }

    [Test]
    public async Task GetFullSeriesInfo_Should_Include_Series_Number()
    {
        // Arrange
        var book = CreateBookWithFullMetadata();

        // Act
        var seriesInfo = book.GetFullSeriesInfo();

        // Assert
        await Assert.That(seriesInfo).IsEqualTo("Test Series #2");
    }

    [Test]
    public async Task IsPartOfSeries_Should_Return_True_When_Series_Present()
    {
        // Arrange
        var book = CreateBookWithFullMetadata();

        // Act
        var isPartOfSeries = book.IsPartOfSeries();

        // Assert
        await Assert.That(isPartOfSeries).IsTrue();
    }

    [Test]
    public async Task IsPartOfSeries_Should_Return_False_When_No_Series()
    {
        // Arrange
        var book = CreateBookWithMinimalMetadata();

        // Act
        var isPartOfSeries = book.IsPartOfSeries();

        // Assert
        await Assert.That(isPartOfSeries).IsFalse();
    }

    [Test]
    public async Task GetTags_Should_Return_All_Tags()
    {
        // Arrange
        var book = CreateBookWithFullMetadata();

        // Act
        var tags = book.GetTags();

        // Assert
        await Assert.That(tags).HasCount().EqualTo(3); // fiction, adventure, plus Fiction from subject
        await Assert.That(tags).Contains("fiction");
        await Assert.That(tags).Contains("adventure");
        await Assert.That(tags).Contains("Fiction");
    }

    [Test]
    public async Task GetTags_Should_Include_Subject_When_Not_Already_In_Tags()
    {
        // Arrange
        var metadata = new BookMetadata(
            tags: new[] { "adventure", "fantasy" },
            subject: "Young Adult"
        );
        var book = new Book(
            new BookTitle("Test Book"),
            null,
            new[] { new Author("Author") },
            new[] { new Chapter("ch1", "Chapter 1", "Content", 0) },
            new[] { new BookIdentifier("123", "uuid") },
            new Language("en"),
            metadata
        );

        // Act
        var tags = book.GetTags();

        // Assert
        await Assert.That(tags).HasCount().EqualTo(3);
        await Assert.That(tags).Contains("adventure");
        await Assert.That(tags).Contains("fantasy");
        await Assert.That(tags).Contains("Young Adult");
    }

    [Test]
    public async Task GetTags_Should_Not_Duplicate_Subject_If_Already_In_Tags()
    {
        // Arrange
        var metadata = new BookMetadata(
            tags: new[] { "Fiction", "Adventure" },
            subject: "fiction" // Different case
        );
        var book = new Book(
            new BookTitle("Test Book"),
            null,
            new[] { new Author("Author") },
            new[] { new Chapter("ch1", "Chapter 1", "Content", 0) },
            new[] { new BookIdentifier("123", "uuid") },
            new Language("en"),
            metadata
        );

        // Act
        var tags = book.GetTags();

        // Assert
        await Assert.That(tags).HasCount().EqualTo(2); // Should not add duplicate
        await Assert.That(tags).Contains("Fiction");
        await Assert.That(tags).Contains("Adventure");
    }

    [Test]
    public async Task GetTags_Should_Return_Empty_List_When_No_Tags()
    {
        // Arrange
        var book = CreateBookWithMinimalMetadata();

        // Act
        var tags = book.GetTags();

        // Assert
        await Assert.That(tags).IsNotNull();
        await Assert.That(tags).HasCount().EqualTo(0);
    }

    [Test]
    public async Task GetIsbn_Should_Return_ISBN_From_Metadata()
    {
        // Arrange
        var metadata = new BookMetadata(isbn: "978-0-316-76948-0");
        var book = new Book(
            new BookTitle("Test Book"),
            null,
            new[] { new Author("Author") },
            new[] { new Chapter("ch1", "Chapter 1", "Content", 0) },
            new[] { new BookIdentifier("978-0-316-76948-0", "isbn") },
            new Language("en"),
            metadata
        );

        // Act
        var isbn = book.GetIsbn();

        // Assert
        await Assert.That(isbn).IsEqualTo("978-0-316-76948-0");
    }

    [Test]
    public async Task Should_Create_Book_With_Series_Without_Number()
    {
        // Arrange
        var metadata = new BookMetadata(
            series: "Standalone Series"
            // No seriesNumber
        );
        var book = new Book(
            new BookTitle("Test Book"),
            null,
            new[] { new Author("Author") },
            new[] { new Chapter("ch1", "Chapter 1", "Content", 0) },
            new[] { new BookIdentifier("123", "uuid") },
            new Language("en"),
            metadata
        );

        // Act
        var series = book.GetSeries();
        var fullInfo = book.GetFullSeriesInfo();

        // Assert
        await Assert.That(series).IsEqualTo("Standalone Series");
        await Assert.That(fullInfo).IsEqualTo("Standalone Series");
    }
}