using Alexandria.Domain.Entities;
using Alexandria.Domain.ValueObjects;
using Alexandria.Infrastructure.Persistence.LiteDb;
using Microsoft.Extensions.Options;
using TUnit.Core;

namespace Alexandria.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for LiteDbBookRepository.
/// Tests CRUD operations, queries, and FileStorage for cover images.
/// </summary>
public class LiteDbBookRepositoryTests : IDisposable
{
    private readonly LiteDbContext _context;
    private readonly LiteDbBookRepository _repository;
    private readonly string _testDbPath;

    public LiteDbBookRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"alexandria_test_{Guid.NewGuid()}.db");
        var options = Options.Create(new LiteDbOptions { DatabasePath = _testDbPath });
        _context = new LiteDbContext(options);
        _repository = new LiteDbBookRepository(_context);
    }

    [Test]
    public async Task AddAsync_ShouldAddBookToDatabase()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var result = await _repository.AddAsync(book);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Id).IsEqualTo(book.Id);

        var retrieved = await _repository.GetByIdAsync(book.Id);
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Title.Value).IsEqualTo(book.Title.Value);
    }

    [Test]
    public async Task GetByIdAsync_ShouldReturnBook_WhenExists()
    {
        // Arrange
        var book = CreateTestBook();
        await _repository.AddAsync(book);

        // Act
        var result = await _repository.GetByIdAsync(book.Id);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Title.Value).IsEqualTo("Test Book");
    }

    [Test]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetByIsbnAsync_ShouldReturnBook_WhenExists()
    {
        // Arrange
        const string isbn = "978-0-123456-78-9";
        var book = CreateTestBook(isbn: isbn);
        await _repository.AddAsync(book);

        // Act
        var result = await _repository.GetByIsbnAsync(isbn);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Metadata.Isbn).IsEqualTo(isbn);
    }

    [Test]
    public async Task GetByAuthorAsync_ShouldReturnBooks_WhenAuthorMatches()
    {
        // Arrange
        var book1 = CreateTestBook(authorName: "John Doe");
        var book2 = CreateTestBook(authorName: "John Smith");
        var book3 = CreateTestBook(authorName: "Jane Doe");

        await _repository.AddAsync(book1);
        await _repository.AddAsync(book2);
        await _repository.AddAsync(book3);

        // Act
        var results = await _repository.GetByAuthorAsync("John");

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);
    }

    [Test]
    public async Task UpdateAsync_ShouldUpdateBook()
    {
        // Arrange
        var book = CreateTestBook();
        await _repository.AddAsync(book);

        // Act - Create a new book instance with same ID but different title
        var updatedBook = CreateTestBook(id: book.Id, title: "Updated Title");
        await _repository.UpdateAsync(updatedBook);

        // Assert
        var retrieved = await _repository.GetByIdAsync(book.Id);
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Title.Value).IsEqualTo("Updated Title");
    }

    [Test]
    public async Task RemoveAsync_ShouldRemoveBook()
    {
        // Arrange
        var book = CreateTestBook();
        await _repository.AddAsync(book);

        // Act
        var result = await _repository.RemoveAsync(book);

        // Assert
        await Assert.That(result).IsTrue();

        var retrieved = await _repository.GetByIdAsync(book.Id);
        await Assert.That(retrieved).IsNull();
    }

    [Test]
    public async Task RemoveByIdAsync_ShouldRemoveBook()
    {
        // Arrange
        var book = CreateTestBook();
        await _repository.AddAsync(book);

        // Act
        var result = await _repository.RemoveByIdAsync(book.Id);

        // Assert
        await Assert.That(result).IsTrue();

        var retrieved = await _repository.GetByIdAsync(book.Id);
        await Assert.That(retrieved).IsNull();
    }

    [Test]
    public async Task ExistsAsync_ShouldReturnTrue_WhenBookExists()
    {
        // Arrange
        var book = CreateTestBook();
        await _repository.AddAsync(book);

        // Act
        var exists = await _repository.ExistsAsync(book.Id);

        // Assert
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task ExistsAsync_ShouldReturnFalse_WhenBookDoesNotExist()
    {
        // Act
        var exists = await _repository.ExistsAsync(Guid.NewGuid());

        // Assert
        await Assert.That(exists).IsFalse();
    }

    [Test]
    public async Task CountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        await _repository.AddAsync(CreateTestBook());
        await _repository.AddAsync(CreateTestBook());
        await _repository.AddAsync(CreateTestBook());

        // Act
        var count = await _repository.CountAsync();

        // Assert
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnAllBooks()
    {
        // Arrange
        await _repository.AddAsync(CreateTestBook());
        await _repository.AddAsync(CreateTestBook());
        await _repository.AddAsync(CreateTestBook());

        // Act
        var books = await _repository.GetAllAsync();

        // Assert
        await Assert.That(books.Count).IsEqualTo(3);
    }

    [Test]
    public async Task SearchAsync_ShouldFindBooksByTitle()
    {
        // Arrange
        var book1 = CreateTestBook(title: "The Great Gatsby");
        var book2 = CreateTestBook(title: "The Catcher in the Rye");
        var book3 = CreateTestBook(title: "To Kill a Mockingbird");

        await _repository.AddAsync(book1);
        await _repository.AddAsync(book2);
        await _repository.AddAsync(book3);

        // Act
        var results = await _repository.SearchAsync("The", Alexandria.Domain.Repositories.SearchFields.Title);

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);
    }

    [Test]
    public async Task AddRangeAsync_ShouldAddMultipleBooks()
    {
        // Arrange
        var books = new[]
        {
            CreateTestBook(),
            CreateTestBook(),
            CreateTestBook()
        };

        // Act
        await _repository.AddRangeAsync(books);

        // Assert
        var count = await _repository.CountAsync();
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task GetPagedAsync_ShouldReturnCorrectPage()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _repository.AddAsync(CreateTestBook());
        }

        // Act
        var page = await _repository.GetPagedAsync(pageNumber: 2, pageSize: 3);

        // Assert
        await Assert.That(page.Items.Count).IsEqualTo(3);
        await Assert.That(page.TotalCount).IsEqualTo(10);
        await Assert.That(page.PageNumber).IsEqualTo(2);
        await Assert.That(page.TotalPages).IsEqualTo(4);
    }

    #region Helper Methods

    private Book CreateTestBook(
        Guid? id = null,
        string title = "Test Book",
        string authorName = "Test Author",
        string isbn = "978-0-123456-78-9")
    {
        var bookTitle = new BookTitle(title);
        var authors = new List<Author> { new Author(authorName) };
        var chapters = new List<Chapter>
        {
            new Chapter(
                id: "chapter1",
                title: "Chapter 1",
                content: "Test content for chapter 1",
                order: 0,
                href: "chapter1.html"
            )
        };
        var identifiers = new List<BookIdentifier>
        {
            new BookIdentifier("isbn", isbn)
        };
        var language = new Language("en");
        var metadata = new BookMetadata(
            publisher: "Test Publisher",
            publicationDate: DateTime.UtcNow,
            description: "Test Description",
            rights: null,
            subject: null,
            coverage: null,
            isbn: isbn,
            series: null,
            seriesNumber: null,
            tags: null,
            epubVersion: "3.0",
            customMetadata: null
        );

        return new Book(
            title: bookTitle,
            alternateTitles: null,
            authors: authors,
            chapters: chapters,
            identifiers: identifiers,
            language: language,
            metadata: metadata,
            id: id
        );
    }

    #endregion

    public void Dispose()
    {
        _context?.Dispose();

        if (File.Exists(_testDbPath))
        {
            try
            {
                File.Delete(_testDbPath);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }
}