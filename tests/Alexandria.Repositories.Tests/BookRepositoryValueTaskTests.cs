using Alexandria.Domain.Entities;
using Alexandria.Domain.Repositories;
using Alexandria.Domain.ValueObjects;
using Moq;

namespace Alexandria.Repositories.Tests;

/// <summary>
/// Tests demonstrating proper handling of ValueTask returns from repository methods.
/// Following Testing_Guidelines.md best practices with TUnit and Moq.
/// </summary>
public class BookRepositoryValueTaskTests
{
    private readonly Mock<IBookRepository> _mockRepository;

    public BookRepositoryValueTaskTests()
    {
        _mockRepository = new Mock<IBookRepository>();
    }

    /// <summary>
    /// Tests for GetByIdAsync which returns ValueTask<Book?>
    /// </summary>
    public class GetByIdAsyncTests : BookRepositoryValueTaskTests
    {
        [Test]
        public async Task GetByIdAsync_ShouldReturnBook_WhenBookExists()
        {
            // Arrange
            var bookId = Guid.NewGuid();
            var expectedBook = CreateTestBook();

            _mockRepository
                .Setup(r => r.GetByIdAsync(bookId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedBook);

            // Act
            // IMPORTANT: ValueTask should be awaited directly, not stored in a variable
            var result = await _mockRepository.Object.GetByIdAsync(bookId);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Title.Value).IsEqualTo("Test Book");
        }

        [Test]
        public async Task GetByIdAsync_ShouldReturnNull_WhenBookDoesNotExist()
        {
            // Arrange
            var bookId = Guid.NewGuid();

            _mockRepository
                .Setup(r => r.GetByIdAsync(bookId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Book?)null);

            // Act
            var result = await _mockRepository.Object.GetByIdAsync(bookId);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task GetByIdAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            var bookId = Guid.NewGuid();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            _mockRepository
                .Setup(r => r.GetByIdAsync(bookId, cts.Token))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await _mockRepository.Object.GetByIdAsync(bookId, cts.Token);
            });
        }
    }

    /// <summary>
    /// Tests for GetByIsbnAsync which returns ValueTask<Book?>
    /// </summary>
    public class GetByIsbnAsyncTests : BookRepositoryValueTaskTests
    {
        [Test]
        public async Task GetByIsbnAsync_ShouldReturnBook_WhenIsbnExists()
        {
            // Arrange
            const string isbn = "978-0-123456-78-9";
            var expectedBook = CreateTestBook(isbn);

            _mockRepository
                .Setup(r => r.GetByIsbnAsync(isbn, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedBook);

            // Act
            var result = await _mockRepository.Object.GetByIsbnAsync(isbn);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Metadata.Isbn).IsEqualTo(isbn);
        }

        [Test]
        public async Task GetByIsbnAsync_ShouldReturnNull_WhenIsbnDoesNotExist()
        {
            // Arrange
            const string isbn = "978-0-123456-78-9";

            _mockRepository
                .Setup(r => r.GetByIsbnAsync(isbn, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Book?)null);

            // Act
            var result = await _mockRepository.Object.GetByIsbnAsync(isbn);

            // Assert
            await Assert.That(result).IsNull();
        }
    }

    /// <summary>
    /// Tests for ExistsAsync which returns ValueTask<bool>
    /// </summary>
    public class ExistsAsyncTests : BookRepositoryValueTaskTests
    {
        [Test]
        public async Task ExistsAsync_ShouldReturnTrue_WhenBookExists()
        {
            // Arrange
            var bookId = Guid.NewGuid();

            _mockRepository
                .Setup(r => r.ExistsAsync(bookId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var exists = await _mockRepository.Object.ExistsAsync(bookId);

            // Assert
            await Assert.That(exists).IsTrue();
        }

        [Test]
        public async Task ExistsAsync_ShouldReturnFalse_WhenBookDoesNotExist()
        {
            // Arrange
            var bookId = Guid.NewGuid();

            _mockRepository
                .Setup(r => r.ExistsAsync(bookId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var exists = await _mockRepository.Object.ExistsAsync(bookId);

            // Assert
            await Assert.That(exists).IsFalse();
        }
    }

    /// <summary>
    /// Tests for ExistsByIsbnAsync which returns ValueTask<bool>
    /// </summary>
    public class ExistsByIsbnAsyncTests : BookRepositoryValueTaskTests
    {
        [Test]
        public async Task ExistsByIsbnAsync_ShouldReturnTrue_WhenIsbnExists()
        {
            // Arrange
            const string isbn = "978-0-123456-78-9";

            _mockRepository
                .Setup(r => r.ExistsByIsbnAsync(isbn, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var exists = await _mockRepository.Object.ExistsByIsbnAsync(isbn);

            // Assert
            await Assert.That(exists).IsTrue();
        }

        [Test]
        public async Task ExistsByIsbnAsync_ShouldReturnFalse_WhenIsbnDoesNotExist()
        {
            // Arrange
            const string isbn = "978-0-123456-78-9";

            _mockRepository
                .Setup(r => r.ExistsByIsbnAsync(isbn, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var exists = await _mockRepository.Object.ExistsByIsbnAsync(isbn);

            // Assert
            await Assert.That(exists).IsFalse();
        }
    }

    /// <summary>
    /// Tests for CountAsync which returns ValueTask<int>
    /// </summary>
    public class CountAsyncTests : BookRepositoryValueTaskTests
    {
        [Test]
        public async Task CountAsync_ShouldReturnCorrectCount_WithoutSpecification()
        {
            // Arrange
            const int expectedCount = 42;

            _mockRepository
                .Setup(r => r.CountAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedCount);

            // Act
            var count = await _mockRepository.Object.CountAsync();

            // Assert
            await Assert.That(count).IsEqualTo(expectedCount);
        }

        [Test]
        public async Task CountAsync_ShouldReturnZero_WhenNoBooks()
        {
            // Arrange
            _mockRepository
                .Setup(r => r.CountAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            // Act
            var count = await _mockRepository.Object.CountAsync();

            // Assert
            await Assert.That(count).IsEqualTo(0);
        }
    }

    /// <summary>
    /// Integration test demonstrating multiple ValueTask operations
    /// </summary>
    public class IntegrationTests : BookRepositoryValueTaskTests
    {
        [Test]
        public async Task MultipleValueTaskOperations_ShouldWorkCorrectly()
        {
            // Arrange
            var bookId = Guid.NewGuid();
            var isbn = "978-0-123456-78-9";
            var book = CreateTestBook(isbn);

            _mockRepository
                .Setup(r => r.ExistsAsync(bookId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockRepository
                .Setup(r => r.GetByIdAsync(bookId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(book);

            _mockRepository
                .Setup(r => r.CountAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act - Multiple ValueTask operations in sequence
            var exists = await _mockRepository.Object.ExistsAsync(bookId);
            var retrievedBook = await _mockRepository.Object.GetByIdAsync(bookId);
            var count = await _mockRepository.Object.CountAsync();

            // Assert
            await Assert.That(exists).IsTrue();
            await Assert.That(retrievedBook).IsNotNull();
            await Assert.That(retrievedBook!.Title.Value).IsEqualTo("Test Book");
            await Assert.That(count).IsEqualTo(1);
        }
    }

    /// <summary>
    /// Demonstrates what NOT to do with ValueTask
    /// </summary>
    public class AntiPatternExamples : BookRepositoryValueTaskTests
    {
        [Test]
        [Skip("Demonstration of incorrect ValueTask usage")]
        public async Task IncorrectValueTaskUsage_DoNotDoThis()
        {
            // DON'T: Store ValueTask in a variable and await multiple times
            // This would cause runtime errors with actual ValueTask implementation

            // var valueTask = _repository.GetByIdAsync(bookId);
            // var book1 = await valueTask; // First await
            // var book2 = await valueTask; // Second await - WRONG! ValueTask can only be consumed once

            // DO: Await immediately
            var bookId = Guid.NewGuid();
            _mockRepository
                .Setup(r => r.GetByIdAsync(bookId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTestBook());

            var book = await _mockRepository.Object.GetByIdAsync(bookId);

            // If you need the result multiple times, store the result, not the ValueTask
            var anotherReference = book;

            await Assert.That(book).IsNotNull();
            await Assert.That(anotherReference).IsNotNull();
        }
    }

    #region Helper Methods

    private static Book CreateTestBook(string? isbn = null)
    {
        var title = new BookTitle("Test Book");
        var authors = new List<Author> { new Author("Test Author") };
        var chapters = new List<Chapter>
        {
            new Chapter(
                id: "chapter1",
                title: "Chapter 1",
                content: "Test content",
                order: 0,
                href: "chapter1.html"
            )
        };
        var identifiers = new List<BookIdentifier>
        {
            new BookIdentifier("isbn", isbn ?? "978-0-123456-78-9")
        };
        var language = new Language("en");
        var metadata = new BookMetadata(
            publisher: "Test Publisher",
            publicationDate: DateTime.UtcNow,
            description: "Test Description",
            rights: null,
            subject: null,
            coverage: null,
            isbn: isbn ?? "978-0-123456-78-9",
            series: null,
            seriesNumber: null,
            tags: null,
            epubVersion: "3.0",
            customMetadata: null
        );

        return new Book(
            title: title,
            alternateTitles: null,
            authors: authors,
            chapters: chapters,
            identifiers: identifiers,
            language: language,
            metadata: metadata
        );
    }

    #endregion
}