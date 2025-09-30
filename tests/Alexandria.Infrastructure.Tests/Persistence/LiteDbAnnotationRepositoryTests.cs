using Alexandria.Domain.ValueObjects;
using Alexandria.Infrastructure.Persistence.LiteDb;
using Microsoft.Extensions.Options;
using TUnit.Core;

namespace Alexandria.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for LiteDbAnnotationRepository.
/// Tests CRUD operations for annotation persistence.
/// </summary>
public class LiteDbAnnotationRepositoryTests : IDisposable
{
    private readonly LiteDbContext _context;
    private readonly LiteDbAnnotationRepository _repository;
    private readonly string _testDbPath;

    public LiteDbAnnotationRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"alexandria_annotations_test_{Guid.NewGuid()}.db");
        var options = Options.Create(new LiteDbOptions { DatabasePath = _testDbPath });
        _context = new LiteDbContext(options);
        _repository = new LiteDbAnnotationRepository(_context);
    }

    [Test]
    public async Task AddAsync_ShouldAddAnnotation()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var annotation = Annotation.Create(
            chapterId: "chapter1",
            startPosition: 100,
            endPosition: 150,
            highlightedText: "Important text",
            color: HighlightColor.Yellow,
            note: "Test note"
        );

        // Act
        var result = await _repository.AddAsync(annotation, bookId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.HighlightedText).IsEqualTo("Important text");

        var retrieved = await _repository.GetByIdAsync(annotation.Id);
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Note).IsEqualTo("Test note");
    }

    [Test]
    public async Task GetByIdAsync_ShouldReturnAnnotation_WhenExists()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var annotation = Annotation.Create("chapter1", 100, 150, "Test text");
        await _repository.AddAsync(annotation, bookId);

        // Act
        var result = await _repository.GetByIdAsync(annotation.Id);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ChapterId).IsEqualTo("chapter1");
    }

    [Test]
    public async Task GetByBookIdAsync_ShouldReturnAllAnnotationsForBook()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var otherBookId = Guid.NewGuid();

        var annotation1 = Annotation.Create("chapter1", 100, 150, "Text 1");
        var annotation2 = Annotation.Create("chapter2", 200, 250, "Text 2");
        var annotation3 = Annotation.Create("chapter1", 300, 350, "Text 3");

        await _repository.AddAsync(annotation1, bookId);
        await _repository.AddAsync(annotation2, bookId);
        await _repository.AddAsync(annotation3, otherBookId);

        // Act
        var results = await _repository.GetByBookIdAsync(bookId);

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetByChapterIdAsync_ShouldReturnAllAnnotationsForChapter()
    {
        // Arrange
        var bookId = Guid.NewGuid();

        var annotation1 = Annotation.Create("chapter1", 100, 150, "Text 1");
        var annotation2 = Annotation.Create("chapter1", 200, 250, "Text 2");
        var annotation3 = Annotation.Create("chapter2", 300, 350, "Text 3");

        await _repository.AddAsync(annotation1, bookId);
        await _repository.AddAsync(annotation2, bookId);
        await _repository.AddAsync(annotation3, bookId);

        // Act
        var results = await _repository.GetByChapterIdAsync("chapter1");

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetByColorAsync_ShouldReturnAnnotationsWithColor()
    {
        // Arrange
        var bookId = Guid.NewGuid();

        var annotation1 = Annotation.Create("chapter1", 100, 150, "Text 1", HighlightColor.Yellow);
        var annotation2 = Annotation.Create("chapter2", 200, 250, "Text 2", HighlightColor.Green);
        var annotation3 = Annotation.Create("chapter3", 300, 350, "Text 3", HighlightColor.Yellow);

        await _repository.AddAsync(annotation1, bookId);
        await _repository.AddAsync(annotation2, bookId);
        await _repository.AddAsync(annotation3, bookId);

        // Act
        var results = await _repository.GetByColorAsync(HighlightColor.Yellow);

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);
    }

    [Test]
    public async Task UpdateAsync_ShouldUpdateAnnotation()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var annotation = Annotation.Create("chapter1", 100, 150, "Text", HighlightColor.Yellow, "Original note");
        await _repository.AddAsync(annotation, bookId);

        // Act
        var updated = annotation.UpdateNote("Updated note");
        await _repository.UpdateAsync(updated);

        // Assert
        var retrieved = await _repository.GetByIdAsync(annotation.Id);
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Note).IsEqualTo("Updated note");
    }

    [Test]
    public async Task ChangeColor_ShouldUpdateAnnotationColor()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var annotation = Annotation.Create("chapter1", 100, 150, "Text", HighlightColor.Yellow);
        await _repository.AddAsync(annotation, bookId);

        // Act
        var updated = annotation.ChangeColor(HighlightColor.Green);
        await _repository.UpdateAsync(updated);

        // Assert
        var retrieved = await _repository.GetByIdAsync(annotation.Id);
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Color).IsEqualTo(HighlightColor.Green);
    }

    [Test]
    public async Task RemoveAsync_ShouldRemoveAnnotation()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var annotation = Annotation.Create("chapter1", 100, 150, "Text");
        await _repository.AddAsync(annotation, bookId);

        // Act
        var result = await _repository.RemoveAsync(annotation.Id);

        // Assert
        await Assert.That(result).IsTrue();

        var retrieved = await _repository.GetByIdAsync(annotation.Id);
        await Assert.That(retrieved).IsNull();
    }

    [Test]
    public async Task RemoveByBookIdAsync_ShouldRemoveAllAnnotationsForBook()
    {
        // Arrange
        var bookId = Guid.NewGuid();

        var annotation1 = Annotation.Create("chapter1", 100, 150, "Text 1");
        var annotation2 = Annotation.Create("chapter2", 200, 250, "Text 2");
        var annotation3 = Annotation.Create("chapter3", 300, 350, "Text 3");

        await _repository.AddAsync(annotation1, bookId);
        await _repository.AddAsync(annotation2, bookId);
        await _repository.AddAsync(annotation3, bookId);

        // Act
        var count = await _repository.RemoveByBookIdAsync(bookId);

        // Assert
        await Assert.That(count).IsEqualTo(3);

        var remaining = await _repository.GetByBookIdAsync(bookId);
        await Assert.That(remaining.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ExistsAsync_ShouldReturnTrue_WhenAnnotationExists()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var annotation = Annotation.Create("chapter1", 100, 150, "Text");
        await _repository.AddAsync(annotation, bookId);

        // Act
        var exists = await _repository.ExistsAsync(annotation.Id);

        // Assert
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task ExistsAsync_ShouldReturnFalse_WhenAnnotationDoesNotExist()
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

        await _repository.AddAsync(Annotation.Create("chapter1", 100, 150, "Text 1"), bookId);
        await _repository.AddAsync(Annotation.Create("chapter2", 200, 250, "Text 2"), bookId);
        await _repository.AddAsync(Annotation.Create("chapter3", 300, 350, "Text 3"), bookId);

        // Act
        var count = await _repository.CountAsync();

        // Assert
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnAllAnnotations()
    {
        // Arrange
        var bookId = Guid.NewGuid();

        await _repository.AddAsync(Annotation.Create("chapter1", 100, 150, "Text 1"), bookId);
        await _repository.AddAsync(Annotation.Create("chapter2", 200, 250, "Text 2"), bookId);

        // Act
        var annotations = await _repository.GetAllAsync();

        // Assert
        await Assert.That(annotations.Count).IsEqualTo(2);
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