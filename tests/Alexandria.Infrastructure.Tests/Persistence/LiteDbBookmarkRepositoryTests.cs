using Alexandria.Domain.ValueObjects;
using Alexandria.Infrastructure.Persistence.LiteDb;
using Microsoft.Extensions.Options;
using TUnit.Core;

namespace Alexandria.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for LiteDbBookmarkRepository.
/// Tests CRUD operations for bookmark persistence.
/// </summary>
public class LiteDbBookmarkRepositoryTests : IDisposable
{
    private readonly LiteDbContext _context;
    private readonly LiteDbBookmarkRepository _repository;
    private readonly string _testDbPath;

    public LiteDbBookmarkRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"alexandria_bookmarks_test_{Guid.NewGuid()}.db");
        var options = Options.Create(new LiteDbOptions { DatabasePath = _testDbPath });
        _context = new LiteDbContext(options);
        _repository = new LiteDbBookmarkRepository(_context);
    }

    [Test]
    public async Task AddAsync_ShouldAddBookmark()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var bookmark = Bookmark.Create("chapter1", "Chapter 1", 100, "Test note");

        // Act
        var result = await _repository.AddAsync(bookmark, bookId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Position).IsEqualTo(100);

        var retrieved = await _repository.GetByIdAsync(bookmark.Id);
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Note).IsEqualTo("Test note");
    }

    [Test]
    public async Task GetByIdAsync_ShouldReturnBookmark_WhenExists()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var bookmark = Bookmark.Create("chapter1", "Chapter 1", 100);
        await _repository.AddAsync(bookmark, bookId);

        // Act
        var result = await _repository.GetByIdAsync(bookmark.Id);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ChapterId).IsEqualTo("chapter1");
    }

    [Test]
    public async Task GetByBookIdAsync_ShouldReturnAllBookmarksForBook()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var otherBookId = Guid.NewGuid();

        var bookmark1 = Bookmark.Create("chapter1", "Chapter 1", 100);
        var bookmark2 = Bookmark.Create("chapter2", "Chapter 2", 200);
        var bookmark3 = Bookmark.Create("chapter1", "Chapter 1", 300);

        await _repository.AddAsync(bookmark1, bookId);
        await _repository.AddAsync(bookmark2, bookId);
        await _repository.AddAsync(bookmark3, otherBookId);

        // Act
        var results = await _repository.GetByBookIdAsync(bookId);

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetByChapterIdAsync_ShouldReturnAllBookmarksForChapter()
    {
        // Arrange
        var bookId = Guid.NewGuid();

        var bookmark1 = Bookmark.Create("chapter1", "Chapter 1", 100);
        var bookmark2 = Bookmark.Create("chapter1", "Chapter 1", 200);
        var bookmark3 = Bookmark.Create("chapter2", "Chapter 2", 300);

        await _repository.AddAsync(bookmark1, bookId);
        await _repository.AddAsync(bookmark2, bookId);
        await _repository.AddAsync(bookmark3, bookId);

        // Act
        var results = await _repository.GetByChapterIdAsync("chapter1");

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);
    }

    [Test]
    public async Task UpdateAsync_ShouldUpdateBookmark()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var bookmark = Bookmark.Create("chapter1", "Chapter 1", 100, "Original note");
        await _repository.AddAsync(bookmark, bookId);

        // Act
        var updated = bookmark.UpdateNote("Updated note");
        await _repository.UpdateAsync(updated);

        // Assert
        var retrieved = await _repository.GetByIdAsync(bookmark.Id);
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Note).IsEqualTo("Updated note");
    }

    [Test]
    public async Task RemoveAsync_ShouldRemoveBookmark()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var bookmark = Bookmark.Create("chapter1", "Chapter 1", 100);
        await _repository.AddAsync(bookmark, bookId);

        // Act
        var result = await _repository.RemoveAsync(bookmark.Id);

        // Assert
        await Assert.That(result).IsTrue();

        var retrieved = await _repository.GetByIdAsync(bookmark.Id);
        await Assert.That(retrieved).IsNull();
    }

    [Test]
    public async Task RemoveByBookIdAsync_ShouldRemoveAllBookmarksForBook()
    {
        // Arrange
        var bookId = Guid.NewGuid();

        var bookmark1 = Bookmark.Create("chapter1", "Chapter 1", 100);
        var bookmark2 = Bookmark.Create("chapter2", "Chapter 2", 200);
        var bookmark3 = Bookmark.Create("chapter3", "Chapter 3", 300);

        await _repository.AddAsync(bookmark1, bookId);
        await _repository.AddAsync(bookmark2, bookId);
        await _repository.AddAsync(bookmark3, bookId);

        // Act
        var count = await _repository.RemoveByBookIdAsync(bookId);

        // Assert
        await Assert.That(count).IsEqualTo(3);

        var remaining = await _repository.GetByBookIdAsync(bookId);
        await Assert.That(remaining.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ExistsAsync_ShouldReturnTrue_WhenBookmarkExists()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var bookmark = Bookmark.Create("chapter1", "Chapter 1", 100);
        await _repository.AddAsync(bookmark, bookId);

        // Act
        var exists = await _repository.ExistsAsync(bookmark.Id);

        // Assert
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task ExistsAsync_ShouldReturnFalse_WhenBookmarkDoesNotExist()
    {
        // Act
        var exists = await _repository.ExistsAsync("nonexistent");

        // Assert
        await Assert.That(exists).IsFalse();
    }

    [Test]
    public async Task CountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var bookId = Guid.NewGuid();

        await _repository.AddAsync(Bookmark.Create("chapter1", "Chapter 1", 100), bookId);
        await _repository.AddAsync(Bookmark.Create("chapter2", "Chapter 2", 200), bookId);
        await _repository.AddAsync(Bookmark.Create("chapter3", "Chapter 3", 300), bookId);

        // Act
        var count = await _repository.CountAsync();

        // Assert
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnAllBookmarks()
    {
        // Arrange
        var bookId = Guid.NewGuid();

        await _repository.AddAsync(Bookmark.Create("chapter1", "Chapter 1", 100), bookId);
        await _repository.AddAsync(Bookmark.Create("chapter2", "Chapter 2", 200), bookId);

        // Act
        var bookmarks = await _repository.GetAllAsync();

        // Assert
        await Assert.That(bookmarks.Count).IsEqualTo(2);
    }

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