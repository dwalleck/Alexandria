using Alexandria.Domain.Entities;
using Alexandria.Domain.Services;
using Alexandria.Domain.ValueObjects;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Application.Tests.Services;

public class SearchServiceTests
{
    private readonly ContentProcessor _contentProcessor = new();
    private readonly SearchService _searchService;

    public SearchServiceTests()
    {
        _searchService = new SearchService(_contentProcessor);
    }

    private static Book CreateTestBook()
    {
        var chapters = new List<Chapter>
        {
            new("ch1", "Chapter 1", "<p>The quick brown fox jumps over the lazy dog.</p>", 0, "ch1.xhtml"),
            new("ch2", "Chapter 2", "<p>This is a test chapter with some content about foxes and dogs.</p>", 1, "ch2.xhtml"),
            new("ch3", "Chapter 3", "<p>No animals here, just some other text content.</p>", 2, "ch3.xhtml")
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
    public async Task Should_Find_Search_Term_In_Chapters()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var results = _searchService.Search(book, "fox").ToList();

        // Assert
        await Assert.That(results).HasCount(2);
        await Assert.That(results[0].Chapter.Id).IsEqualTo("ch1");
        await Assert.That(results[1].Chapter.Id).IsEqualTo("ch2");
    }

    [Test]
    public async Task Should_Return_Empty_For_Not_Found_Term()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var results = _searchService.Search(book, "elephant").ToList();

        // Assert
        await Assert.That(results).HasCount(0);
    }

    [Test]
    public async Task Should_Search_Case_Insensitive_By_Default()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var results = _searchService.Search(book, "FOX").ToList();

        // Assert
        await Assert.That(results).HasCount(2);
    }

    [Test]
    public async Task Should_Search_Case_Sensitive_When_Specified()
    {
        // Arrange
        var book = CreateTestBook();
        var options = new SearchOptions { CaseSensitive = true };

        // Act
        var results = _searchService.Search(book, "FOX", options).ToList();

        // Assert
        await Assert.That(results).HasCount(0);
    }

    [Test]
    public async Task Should_Find_Whole_Words_Only_When_Specified()
    {
        // Arrange
        var chapters = new List<Chapter>
        {
            new("ch1", "Test", "<p>The testing tester tests the test.</p>", 0, "ch1.xhtml")
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

        var options = new SearchOptions { WholeWord = true };

        // Act
        var results = _searchService.Search(book, "test", options).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].Matches).HasCount(1); // Only "test", not "testing", "tester", "tests"
    }

    [Test]
    public async Task Should_Search_All_Terms_With_AND_Logic()
    {
        // Arrange
        var book = CreateTestBook();
        var terms = new[] { "fox", "dog" };

        // Act
        var results = _searchService.SearchAll(book, terms).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].Chapter.Id).IsEqualTo("ch1");
    }

    [Test]
    public async Task Should_Search_Any_Terms_With_OR_Logic()
    {
        // Arrange
        var book = CreateTestBook();
        var terms = new[] { "fox", "animals" };

        // Act
        var results = _searchService.SearchAny(book, terms).ToList();

        // Assert
        await Assert.That(results).HasCount(3); // ch1 and ch2 have "fox", ch3 has "animals"
    }

    [Test]
    public async Task Should_Search_With_Regex_Pattern()
    {
        // Arrange
        var book = CreateTestBook();
        var pattern = @"\b\w+ox\b"; // Words ending with "ox"

        // Act
        var results = _searchService.SearchRegex(book, pattern).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].Chapter.Id).IsEqualTo("ch1");
        await Assert.That(results[0].Matches[0].Text).IsEqualTo("fox");
    }

    [Test]
    public async Task Should_Return_Empty_For_Invalid_Regex()
    {
        // Arrange
        var book = CreateTestBook();
        var invalidPattern = "["; // Invalid regex

        // Act
        var results = _searchService.SearchRegex(book, invalidPattern).ToList();

        // Assert
        await Assert.That(results).HasCount(0);
    }

    [Test]
    public async Task Should_Include_Snippet_In_Results()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var results = _searchService.Search(book, "fox").ToList();

        // Assert
        await Assert.That(results[0].Snippet).IsNotEmpty();
        await Assert.That(results[0].Snippet).Contains("fox");
    }

    [Test]
    public async Task Should_Respect_Max_Matches_Per_Chapter()
    {
        // Arrange
        var content = string.Join(" ", Enumerable.Repeat("test", 20));
        var chapters = new List<Chapter>
        {
            new("ch1", "Test", $"<p>{content}</p>", 0, "ch1.xhtml")
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

        var options = new SearchOptions { MaxMatchesPerChapter = 5 };

        // Act
        var results = _searchService.Search(book, "test", options).ToList();

        // Assert
        await Assert.That(results[0].Matches).HasCount(5);
    }

    [Test]
    public async Task Should_Calculate_Score_Based_On_Match_Count()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var results = _searchService.Search(book, "o").ToList(); // 'o' appears multiple times

        // Assert
        await Assert.That(results[0].Score).IsGreaterThan(0);
    }

    [Test]
    public async Task Should_Order_Results_By_Score_And_Chapter_Order()
    {
        // Arrange
        var chapters = new List<Chapter>
        {
            new("ch1", "One", "<p>test</p>", 2, "ch1.xhtml"),
            new("ch2", "Two", "<p>test test test</p>", 0, "ch2.xhtml"),
            new("ch3", "Three", "<p>test test</p>", 1, "ch3.xhtml")
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
        var results = _searchService.Search(book, "test").ToList();

        // Assert
        await Assert.That(results[0].Score).IsEqualTo(3); // Most matches
        await Assert.That(results[0].Chapter.Title).IsEqualTo("Two");
        await Assert.That(results[1].Score).IsEqualTo(2);
        await Assert.That(results[2].Score).IsEqualTo(1);
    }

    [Test]
    public async Task Should_Handle_Empty_Search_Term()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var results = _searchService.Search(book, "").ToList();

        // Assert
        await Assert.That(results).HasCount(0);
    }

    [Test]
    public async Task Should_Handle_Null_Search_Term()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var results = _searchService.Search(book, null!).ToList();

        // Assert
        await Assert.That(results).HasCount(0);
    }

    [Test]
    public async Task Should_Throw_For_Null_Book()
    {
        // Act & Assert
        await Assert.That(() => _searchService.Search(null!, "test"))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Should_Merge_Results_In_SearchAny()
    {
        // Arrange
        var chapters = new List<Chapter>
        {
            new("ch1", "Test", "<p>cat and dog together</p>", 0, "ch1.xhtml")
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
        var results = _searchService.SearchAny(book, new[] { "cat", "dog" }).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].Score).IsEqualTo(2); // Found both terms
    }
}