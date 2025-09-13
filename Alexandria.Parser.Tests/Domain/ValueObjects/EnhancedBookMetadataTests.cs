using Alexandria.Parser.Domain.ValueObjects;

namespace Alexandria.Parser.Tests.Domain.ValueObjects;

/// <summary>
/// Tests for Phase 4 enhanced BookMetadata properties
/// </summary>
public class EnhancedBookMetadataTests
{
    [Test]
    public async Task Should_Create_With_All_Enhanced_Properties()
    {
        // Arrange & Act
        var metadata = new BookMetadata(
            publisher: "Test Publisher",
            publicationDate: new DateTime(2024, 1, 1),
            description: "Test description",
            rights: "All rights reserved",
            subject: "Fiction",
            coverage: "Global",
            isbn: "978-0-316-76948-0",
            series: "The Chronicles",
            seriesNumber: 3,
            tags: new[] { "fantasy", "adventure", "magic" },
            epubVersion: "3.0"
        );

        // Assert
        await Assert.That(metadata.Isbn).IsEqualTo("978-0-316-76948-0");
        await Assert.That(metadata.Series).IsEqualTo("The Chronicles");
        await Assert.That(metadata.SeriesNumber).IsEqualTo(3);
        await Assert.That(metadata.Tags).HasCount().EqualTo(3);
        await Assert.That(metadata.Tags).Contains("fantasy");
        await Assert.That(metadata.Tags).Contains("adventure");
        await Assert.That(metadata.Tags).Contains("magic");
        await Assert.That(metadata.EpubVersion).IsEqualTo("3.0");
    }

    [Test]
    public async Task Should_Create_With_Default_Values()
    {
        // Arrange & Act
        var metadata = BookMetadata.Empty;

        // Assert
        await Assert.That(metadata.Isbn).IsNull();
        await Assert.That(metadata.Series).IsNull();
        await Assert.That(metadata.SeriesNumber).IsNull();
        await Assert.That(metadata.Tags).IsNotNull();
        await Assert.That(metadata.Tags).HasCount().EqualTo(0);
        await Assert.That(metadata.EpubVersion).IsNull();
    }

    [Test]
    public async Task IsPartOfSeries_Should_Return_True_When_Series_Present()
    {
        // Arrange
        var metadata = new BookMetadata(series: "Harry Potter");

        // Act
        var isPartOfSeries = metadata.IsPartOfSeries;

        // Assert
        await Assert.That(isPartOfSeries).IsTrue();
    }

    [Test]
    public async Task IsPartOfSeries_Should_Return_False_When_No_Series()
    {
        // Arrange
        var metadata = new BookMetadata();

        // Act
        var isPartOfSeries = metadata.IsPartOfSeries;

        // Assert
        await Assert.That(isPartOfSeries).IsFalse();
    }

    [Test]
    public async Task GetFullSeriesInfo_Should_Include_Number_When_Present()
    {
        // Arrange
        var metadata = new BookMetadata(
            series: "The Lord of the Rings",
            seriesNumber: 2
        );

        // Act
        var seriesInfo = metadata.GetFullSeriesInfo();

        // Assert
        await Assert.That(seriesInfo).IsEqualTo("The Lord of the Rings #2");
    }

    [Test]
    public async Task GetFullSeriesInfo_Should_Return_Series_Only_When_No_Number()
    {
        // Arrange
        var metadata = new BookMetadata(series: "Standalone Series");

        // Act
        var seriesInfo = metadata.GetFullSeriesInfo();

        // Assert
        await Assert.That(seriesInfo).IsEqualTo("Standalone Series");
    }

    [Test]
    public async Task GetFullSeriesInfo_Should_Return_Null_When_No_Series()
    {
        // Arrange
        var metadata = new BookMetadata();

        // Act
        var seriesInfo = metadata.GetFullSeriesInfo();

        // Assert
        await Assert.That(seriesInfo).IsNull();
    }

    [Test]
    public async Task Should_Handle_Empty_Tags_List()
    {
        // Arrange
        var metadata = new BookMetadata(tags: new List<string>());

        // Act & Assert
        await Assert.That(metadata.Tags).IsNotNull();
        await Assert.That(metadata.Tags).HasCount().EqualTo(0);
    }

    [Test]
    public async Task Should_Accept_Null_Tags_And_Initialize_Empty_List()
    {
        // Arrange
        var metadata = new BookMetadata(tags: null);

        // Act & Assert
        await Assert.That(metadata.Tags).IsNotNull();
        await Assert.That(metadata.Tags).HasCount().EqualTo(0);
    }

    [Test]
    public async Task Should_Store_Multiple_Tags()
    {
        // Arrange
        var tags = new[] { "science fiction", "space opera", "aliens", "future" };
        var metadata = new BookMetadata(tags: tags);

        // Act & Assert
        await Assert.That(metadata.Tags).HasCount().EqualTo(4);
        await Assert.That(metadata.Tags).Contains("science fiction");
        await Assert.That(metadata.Tags).Contains("space opera");
        await Assert.That(metadata.Tags).Contains("aliens");
        await Assert.That(metadata.Tags).Contains("future");
    }

    [Test]
    public async Task Should_Store_EpubVersion_Correctly()
    {
        // Arrange & Act
        var metadata2 = new BookMetadata(epubVersion: "2.0.1");
        var metadata3 = new BookMetadata(epubVersion: "3.2");

        // Assert
        await Assert.That(metadata2.EpubVersion).IsEqualTo("2.0.1");
        await Assert.That(metadata3.EpubVersion).IsEqualTo("3.2");
    }

    [Test]
    public async Task Should_Store_ISBN_In_Various_Formats()
    {
        // Arrange & Act
        var isbn10 = new BookMetadata(isbn: "0-316-76948-7");
        var isbn13 = new BookMetadata(isbn: "978-0-316-76948-0");
        var isbnNoHyphens = new BookMetadata(isbn: "9780316769488");

        // Assert
        await Assert.That(isbn10.Isbn).IsEqualTo("0-316-76948-7");
        await Assert.That(isbn13.Isbn).IsEqualTo("978-0-316-76948-0");
        await Assert.That(isbnNoHyphens.Isbn).IsEqualTo("9780316769488");
    }
}