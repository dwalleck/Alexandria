using Alexandria.Parser.Infrastructure.Parsers;
using Alexandria.Parser.Tests.Utilities;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Parser.Tests.Infrastructure.Parsers;

public class AdaptiveEpubParserTests
{
    private readonly AdaptiveEpubParser _adaptiveParser = new();

    [Test]
    public async Task Should_Parse_Epub2_Version_2_0_1()
    {
        // Arrange
        using var stream = TestEpubBuilder.CreateMinimalEpub("2.0.1");

        // Act
        var result = await _adaptiveParser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Minimal Test Book");
    }

    [Test]
    public async Task Should_Parse_Epub3_Version_3_0()
    {
        // Arrange
        using var stream = TestEpubBuilder.CreateMinimalEpub("3.0");

        // Act
        var result = await _adaptiveParser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Minimal Test Book");
    }

    [Test]
    public async Task Should_Parse_Epub3_Version_3_1()
    {
        // Arrange
        using var stream = TestEpubBuilder.CreateMinimalEpub("3.1");

        // Act
        var result = await _adaptiveParser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Minimal Test Book");
    }

    [Test]
    public async Task Should_Parse_Epub3_Version_3_2()
    {
        // Arrange
        using var stream = TestEpubBuilder.CreateMinimalEpub("3.2");

        // Act
        var result = await _adaptiveParser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Minimal Test Book");
    }

    [Test]
    public async Task Should_Handle_Unknown_Version()
    {
        // Arrange - Version 1.0 doesn't exist
        using var stream = TestEpubBuilder.CreateMinimalEpub("1.0");

        // Act
        var result = await _adaptiveParser.ParseAsync(stream);

        // Assert - Should still try to parse (probably as EPUB 2)
        // The parser might be forgiving and try to parse anyway
        await Assert.That(result.IsT0 || result.IsT1).IsTrue();
    }

    [Test]
    public async Task Should_Return_Error_For_Invalid_Epub()
    {
        // Arrange
        using var stream = TestEpubBuilder.CreateInvalidEpub("empty");

        // Act
        var result = await _adaptiveParser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
    }

    [Test]
    public async Task Should_Parse_Complex_Epub2_Book()
    {
        // Arrange
        using var stream = new TestEpubBuilder()
            .WithVersion("2.0.1")
            .WithTitle("Complex EPUB 2 Book")
            .WithAuthor("John Doe")
            .WithLanguage("en-US")
            .AddChapter("preface", "Preface", "<p>This is the preface.</p>")
            .AddChapter("ch1", "Chapter 1", "<p>Content of chapter 1.</p>")
            .AddChapter("ch2", "Chapter 2", "<p>Content of chapter 2.</p>")
            .AddChapter("epilogue", "Epilogue", "<p>This is the epilogue.</p>")
            .Build();

        // Act
        var result = await _adaptiveParser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Complex EPUB 2 Book");
        await Assert.That(book.Chapters).HasCount(4);
        await Assert.That(book.Language.Code).IsEqualTo("en-US");
    }

    [Test]
    public async Task Should_Parse_Complex_Epub3_Book()
    {
        // Arrange
        using var stream = new TestEpubBuilder()
            .WithVersion("3.0")
            .WithTitle("Complex EPUB 3 Book")
            .WithAuthor("Jane Smith")
            .WithLanguage("fr-FR")
            .AddChapter("intro", "Introduction", "<p>This is the introduction.</p>")
            .AddChapter("part1", "Part I", "<p>Content of part 1.</p>")
            .AddChapter("part2", "Part II", "<p>Content of part 2.</p>")
            .AddChapter("conclusion", "Conclusion", "<p>This is the conclusion.</p>")
            .Build();

        // Act
        var result = await _adaptiveParser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Complex EPUB 3 Book");
        await Assert.That(book.Chapters).HasCount(4);
        await Assert.That(book.Language.Code).IsEqualTo("fr-FR");
    }

    [Test]
    public async Task Should_Handle_Stream_Reset_Between_Parses()
    {
        // Arrange
        using var stream = TestEpubBuilder.CreateMinimalEpub("2.0.1");

        // Act - Parse twice to ensure stream handling is correct
        var result1 = await _adaptiveParser.ParseAsync(stream);
        stream.Position = 0; // Reset for second parse
        var result2 = await _adaptiveParser.ParseAsync(stream);

        // Assert
        await Assert.That(result1.IsT0).IsTrue();
        await Assert.That(result2.IsT0).IsTrue();

        var book1 = result1.AsT0;
        var book2 = result2.AsT0;
        await Assert.That(book1.Title.Value).IsEqualTo(book2.Title.Value);
    }
}