using Alexandria.Parser.Domain.ValueObjects;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Parser.Tests.Domain.ValueObjects;

public class BookIdentifierTests
{
    [Test]
    public async Task Should_Create_Valid_ISBN()
    {
        // Arrange
        const string value = "978-0-316-76948-0";
        const string scheme = "ISBN";

        // Act
        var identifier = new BookIdentifier(value, scheme);

        // Assert
        await Assert.That(identifier.Value).IsEqualTo(value);
        await Assert.That(identifier.Scheme).IsEqualTo(scheme);
    }

    [Test]
    public async Task Should_Create_Valid_UUID()
    {
        // Arrange
        const string value = "550e8400-e29b-41d4-a716-446655440000";
        const string scheme = "UUID";

        // Act
        var identifier = new BookIdentifier(value, scheme);

        // Assert
        await Assert.That(identifier.Value).IsEqualTo(value);
        await Assert.That(identifier.Scheme).IsEqualTo(scheme);
    }

    [Test]
    [Arguments(null, "ISBN")]
    [Arguments("", "ISBN")]
    [Arguments("   ", "ISBN")]
    [Arguments("123", null)]
    [Arguments("123", "")]
    [Arguments("123", "   ")]
    public async Task Should_Throw_When_Value_Or_Scheme_Invalid(string? value, string? scheme)
    {
        // Act & Assert
        await Assert.That(() => new BookIdentifier(value!, scheme!))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Should_Format_ToString_Correctly()
    {
        // Arrange
        var identifier = new BookIdentifier("978-0-316-76948-0", "ISBN");

        // Act
        var result = identifier.ToString();

        // Assert
        await Assert.That(result).IsEqualTo("ISBN: 978-0-316-76948-0");
    }

    [Test]
    public async Task Should_Compare_Equality_Correctly()
    {
        // Arrange
        var id1 = new BookIdentifier("978-0-316-76948-0", "ISBN");
        var id2 = new BookIdentifier("978-0-316-76948-0", "ISBN");
        var id3 = new BookIdentifier("978-0-316-76948-1", "ISBN");
        var id4 = new BookIdentifier("978-0-316-76948-0", "ISBN-13");

        // Assert
        await Assert.That(id1).IsEqualTo(id2);
        await Assert.That(id1).IsNotEqualTo(id3);
        await Assert.That(id1).IsNotEqualTo(id4);
    }

    [Test]
    public async Task Should_Generate_Consistent_HashCode()
    {
        // Arrange
        var id1 = new BookIdentifier("123456", "DOI");
        var id2 = new BookIdentifier("123456", "DOI");

        // Assert
        await Assert.That(id1.GetHashCode()).IsEqualTo(id2.GetHashCode());
    }

    [Test]
    public async Task Should_Trim_Whitespace()
    {
        // Arrange
        const string valueWithSpaces = "  978-0-316-76948-0  ";
        const string schemeWithSpaces = "  ISBN  ";

        // Act
        var identifier = new BookIdentifier(valueWithSpaces, schemeWithSpaces);

        // Assert
        await Assert.That(identifier.Value).IsEqualTo("978-0-316-76948-0");
        await Assert.That(identifier.Scheme).IsEqualTo("ISBN");
    }
}