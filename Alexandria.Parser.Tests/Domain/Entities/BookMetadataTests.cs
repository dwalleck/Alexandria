using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.ValueObjects;

namespace Alexandria.Parser.Tests.Domain.Entities;

/// <summary>
/// Tests for Book entity metadata enhancement methods
/// </summary>
public class BookMetadataTests
{
    private Book CreateSampleBook()
    {
        var title = new BookTitle("Test Book");
        var alternateTitles = new[] { new BookTitle("Alternative Title") };
        var authors = new[]
        {
            new Author("John Doe", "Author"),
            new Author("Jane Smith", "Editor")
        };
        var chapters = new[]
        {
            new Chapter("ch1", "Chapter 1", "<p>Content with 10 words here for testing the word count.</p>", 0),
            new Chapter("ch2", "Chapter 2", "<p>More content with another 10 words to test counting properly.</p>", 1)
        };
        var identifiers = new[]
        {
            new BookIdentifier("isbn", "978-0-123456-78-9"),
            new BookIdentifier("uuid", "123e4567-e89b-12d3-a456-426614174000")
        };
        var language = new Language("en");
        var metadata = new BookMetadata(
            publisher: "Test Publisher",
            publicationDate: new DateTime(2024, 1, 1),
            description: "A sample test book",
            subject: "Fiction"
        );

        return new Book(
            title,
            alternateTitles,
            authors,
            chapters,
            identifiers,
            language,
            metadata
        );
    }

    [Test]
    public async Task GetMetadataDictionary_ShouldIncludeAllBasicMetadata()
    {
        // Arrange
        var book = CreateSampleBook();

        // Act
        var metadata = book.GetMetadataDictionary();

        // Assert
        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata["Title"]).IsEqualTo("Test Book");
        await Assert.That(metadata["Language"]).IsEqualTo("EN");
        await Assert.That(metadata["Authors"]).Contains("John Doe");
        await Assert.That(metadata["Authors"]).Contains("Jane Smith");
        await Assert.That(metadata["Identifiers"]).Contains("isbn: 978-0-123456-78-9");
        await Assert.That(metadata["Publisher"]).IsEqualTo("Test Publisher");
        await Assert.That(metadata["PublicationDate"]).IsEqualTo("01/01/2024");
        await Assert.That(metadata["Description"]).IsEqualTo("A sample test book");
        await Assert.That(metadata["Subject"]).IsEqualTo("Fiction");
    }

    [Test]
    public async Task GetCitation_APA_ShouldFormatCorrectly()
    {
        // Arrange
        var book = CreateSampleBook();

        // Act
        var citation = book.GetCitation(CitationStyle.APA);

        // Assert
        await Assert.That(citation).IsNotNull();
        await Assert.That(citation).Contains("Doe, J.");
        await Assert.That(citation).Contains("(2024)");
        await Assert.That(citation).Contains("Test Book");
        await Assert.That(citation).Contains("Test Publisher");
    }

    [Test]
    public async Task GetCitation_MLA_ShouldFormatCorrectly()
    {
        // Arrange
        var book = CreateSampleBook();

        // Act
        var citation = book.GetCitation(CitationStyle.MLA);

        // Assert
        await Assert.That(citation).IsNotNull();
        await Assert.That(citation).Contains("Doe, John");
        await Assert.That(citation).Contains("Test Book");
        await Assert.That(citation).Contains("Test Publisher");
        await Assert.That(citation).Contains("2024");
    }

    [Test]
    public async Task GetCitation_Chicago_ShouldFormatCorrectly()
    {
        // Arrange
        var book = CreateSampleBook();

        // Act
        var citation = book.GetCitation(CitationStyle.Chicago);

        // Assert
        await Assert.That(citation).IsNotNull();
        await Assert.That(citation).Contains("Doe, John");
        await Assert.That(citation).Contains("Test Book");
        await Assert.That(citation).Contains("Test Publisher");
        await Assert.That(citation).Contains("2024");
    }

    [Test]
    public async Task GetIsbn_ShouldReturnIsbnIdentifier()
    {
        // Arrange
        var book = CreateSampleBook();

        // Act
        var isbn = book.GetIsbn();

        // Assert
        await Assert.That(isbn).IsEqualTo("978-0-123456-78-9");
    }

    [Test]
    public async Task GetIsbn_WhenNoIsbn_ShouldReturnNull()
    {
        // Arrange
        var book = new Book(
            new BookTitle("Test Book"),
            null,
            new[] { new Author("Author") },
            new[] { new Chapter("ch1", "Chapter 1", "Content", 0) },
            new[] { new BookIdentifier("uuid", "123") },
            new Language("en"),
            BookMetadata.Empty
        );

        // Act
        var isbn = book.GetIsbn();

        // Assert
        await Assert.That(isbn).IsNull();
    }

    [Test]
    public async Task GetEstimatedReadingTime_ShouldCalculateBasedOnWordCount()
    {
        // Arrange
        var book = CreateSampleBook();

        // Act
        var readingTime = book.GetEstimatedReadingTime();

        // Assert
        // With 20 words at 250 words per minute = 0.08 minutes, rounds up to 1 minute
        await Assert.That(readingTime).IsEqualTo(TimeSpan.FromMinutes(1));
    }

    [Test]
    public async Task GetSummary_ShouldReturnCompleteBookSummary()
    {
        // Arrange
        var book = CreateSampleBook();

        // Act
        var summary = book.GetSummary();

        // Assert
        await Assert.That(summary).IsNotNull();
        await Assert.That(summary.Title).IsEqualTo("Test Book");
        await Assert.That(summary.PrimaryAuthor).IsEqualTo("John Doe");
        await Assert.That(summary.ChapterCount).IsEqualTo(2);
        await Assert.That(summary.WordCount).IsGreaterThan(0);
        await Assert.That(summary.EstimatedReadingTime).IsEqualTo(TimeSpan.FromMinutes(1));
        await Assert.That(summary.PublicationDate).IsEqualTo(new DateTime(2024, 1, 1));
        await Assert.That(summary.Isbn).IsEqualTo("978-0-123456-78-9");
        await Assert.That(summary.Language).IsEqualTo("EN");
        await Assert.That(summary.Description).IsEqualTo("A sample test book");
    }

    [Test]
    public async Task GetPrimaryAuthor_ShouldReturnFirstAuthor()
    {
        // Arrange
        var book = CreateSampleBook();

        // Act
        var primaryAuthor = book.GetPrimaryAuthor();

        // Assert
        await Assert.That(primaryAuthor.Name).IsEqualTo("John Doe");
        await Assert.That(primaryAuthor.Role).IsEqualTo("Author");
    }

    [Test]
    public async Task GetPrimaryAuthor_WhenNoAuthors_ShouldThrowException()
    {
        // Arrange & Act & Assert
        // The Book constructor throws when no authors, so we can't test this case
        // as the Book entity enforces having at least one author
        await Assert.That(() => new Book(
            new BookTitle("Test Book"),
            null,
            new Author[] { }, // Empty authors array
            new[] { new Chapter("ch1", "Chapter 1", "Content", 0) },
            new[] { new BookIdentifier("uuid", "123") },
            new Language("en"),
            BookMetadata.Empty
        )).Throws<ArgumentException>();
    }

    [Test]
    public async Task GetCitation_WithMultipleAuthors_ShouldFormatCorrectly()
    {
        // Arrange
        var book = new Book(
            new BookTitle("Test Book"),
            null,
            new[]
            {
                new Author("John Doe", "Author"),
                new Author("Jane Smith", "Author"),
                new Author("Alice Brown", "Author")
            },
            new[] { new Chapter("ch1", "Chapter 1", "Content", 0) },
            new[] { new BookIdentifier("isbn", "978-0-123456-78-9") },
            new Language("en"),
            new BookMetadata(publisher: "Test Publisher", publicationDate: new DateTime(2024, 1, 1))
        );

        // Act
        var citation = book.GetCitation(CitationStyle.APA);

        // Assert
        await Assert.That(citation).Contains("et al.");
    }

    [Test]
    public async Task GetMetadataDictionary_WithMissingOptionalFields_ShouldOnlyIncludePresent()
    {
        // Arrange
        var book = new Book(
            new BookTitle("Minimal Book"),
            null,
            new[] { new Author("Author") },
            new[] { new Chapter("ch1", "Chapter 1", "Content", 0) },
            new[] { new BookIdentifier("uuid", "123") },
            new Language("en"),
            BookMetadata.Empty
        );

        // Act
        var metadataDict = book.GetMetadataDictionary();

        // Assert
        await Assert.That(metadataDict.ContainsKey("Title")).IsTrue();
        await Assert.That(metadataDict.ContainsKey("Language")).IsTrue();
        await Assert.That(metadataDict.ContainsKey("Authors")).IsTrue();
        await Assert.That(metadataDict.ContainsKey("Publisher")).IsFalse();
        await Assert.That(metadataDict.ContainsKey("Description")).IsFalse();
    }
}