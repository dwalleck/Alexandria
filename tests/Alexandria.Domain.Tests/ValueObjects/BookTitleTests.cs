using Alexandria.Domain.ValueObjects;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Domain.Tests.ValueObjects;

public class BookTitleTests
{
    [Test]
    public async Task Should_Create_Valid_BookTitle()
    {
        // Arrange
        const string title = "The Great Gatsby";

        // Act
        var bookTitle = new BookTitle(title);

        // Assert
        await Assert.That(bookTitle.Value).IsEqualTo(title);
        await Assert.That(bookTitle.ToString()).IsEqualTo(title);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Should_Throw_When_Title_Is_NullOrEmpty(string? title)
    {
        // Act & Assert
        await Assert.That(() => new BookTitle(title!))
            .Throws<ArgumentException>()
            .WithMessage("Title cannot be empty");
    }

    [Test]
    public async Task Should_Throw_When_Title_Exceeds_MaxLength()
    {
        // Arrange
        var longTitle = new string('a', 501);

        // Act & Assert
        await Assert.That(() => new BookTitle(longTitle))
            .Throws<ArgumentException>()
            .WithMessage("Title cannot exceed 500 characters");
    }

    [Test]
    public async Task Should_Compare_Equality_Correctly()
    {
        // Arrange
        var title1 = new BookTitle("Test Book");
        var title2 = new BookTitle("Test Book");
        var title3 = new BookTitle("Different Book");

        // Assert
        await Assert.That(title1).IsEqualTo(title2);
        await Assert.That(title1).IsNotEqualTo(title3);
        await Assert.That(title1 == title2).IsTrue();
        await Assert.That(title1 != title3).IsTrue();
    }

    [Test]
    public async Task Should_Generate_Consistent_HashCode()
    {
        // Arrange
        var title1 = new BookTitle("Test Book");
        var title2 = new BookTitle("Test Book");

        // Assert
        await Assert.That(title1.GetHashCode()).IsEqualTo(title2.GetHashCode());
    }

    [Test]
    public async Task Should_Trim_Whitespace()
    {
        // Arrange
        const string titleWithSpaces = "  The Great Gatsby  ";

        // Act
        var bookTitle = new BookTitle(titleWithSpaces);

        // Assert
        await Assert.That(bookTitle.Value).IsEqualTo("The Great Gatsby");
    }
}