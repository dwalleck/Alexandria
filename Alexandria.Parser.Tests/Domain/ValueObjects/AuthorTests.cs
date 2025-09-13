using Alexandria.Parser.Domain.ValueObjects;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Parser.Tests.Domain.ValueObjects;

public class AuthorTests
{
    [Test]
    public async Task Should_Create_Author_With_Name_Only()
    {
        // Arrange
        const string name = "F. Scott Fitzgerald";

        // Act
        var author = new Author(name);

        // Assert
        await Assert.That(author.Name).IsEqualTo(name);
        await Assert.That(author.Role).IsNull();
        await Assert.That(author.FileAs).IsNull();
        await Assert.That(author.ToString()).IsEqualTo(name);
    }

    [Test]
    public async Task Should_Create_Author_With_Role_And_FileAs()
    {
        // Arrange
        const string name = "F. Scott Fitzgerald";
        const string role = "Author";
        const string fileAs = "Fitzgerald, F. Scott";

        // Act
        var author = new Author(name, role, fileAs);

        // Assert
        await Assert.That(author.Name).IsEqualTo(name);
        await Assert.That(author.Role).IsEqualTo(role);
        await Assert.That(author.FileAs).IsEqualTo(fileAs);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Should_Throw_When_Name_Is_Invalid(string? name)
    {
        // Act & Assert
        await Assert.That(() => new Author(name!))
            .Throws<ArgumentException>()
            .WithMessage("Author name cannot be empty (Parameter 'name')");
    }

    [Test]
    public async Task Should_Compare_Authors_Correctly()
    {
        // Arrange
        var author1 = new Author("John Doe", "Author");
        var author2 = new Author("John Doe", "Author");
        var author3 = new Author("Jane Doe", "Author");
        var author4 = new Author("John Doe", "Editor");

        // Assert
        await Assert.That(author1).IsEqualTo(author2);
        await Assert.That(author1).IsNotEqualTo(author3);
        await Assert.That(author1).IsNotEqualTo(author4);
    }

    [Test]
    public async Task Should_Generate_Consistent_HashCode()
    {
        // Arrange
        var author1 = new Author("John Doe", "Author", "Doe, John");
        var author2 = new Author("John Doe", "Author", "Doe, John");

        // Assert
        await Assert.That(author1.GetHashCode()).IsEqualTo(author2.GetHashCode());
    }

    [Test]
    public async Task Should_Format_ToString_With_Role()
    {
        // Arrange
        var author = new Author("John Doe", "Editor");

        // Act
        var result = author.ToString();

        // Assert
        await Assert.That(result).IsEqualTo("John Doe (Editor)");
    }

    [Test]
    public async Task Should_Trim_Whitespace_From_Name()
    {
        // Arrange
        const string nameWithSpaces = "  John Doe  ";

        // Act
        var author = new Author(nameWithSpaces);

        // Assert
        await Assert.That(author.Name).IsEqualTo("John Doe");
    }
}