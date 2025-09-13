using Alexandria.Parser.Domain.ValueObjects;
using Alexandria.Parser.Infrastructure.Parsers;
using Alexandria.Parser.Tests.Utilities;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Parser.Tests.Infrastructure.Parsers;

public class Epub2ParserTests
{
    private readonly Epub2Parser _parser = new();

    [Test]
    public async Task Should_Parse_Valid_Epub2_Book()
    {
        // Arrange
        using var stream = TestEpubBuilder.CreateMinimalEpub("2.0.1");

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Minimal Test Book");
        await Assert.That(book.Authors).HasCount(1);
        await Assert.That(book.Authors[0].Name).IsEqualTo("Test Author");
        await Assert.That(book.Chapters).HasCount(1);
        await Assert.That(book.Language.Code).IsEqualTo("en");
    }

    [Test]
    public async Task Should_Parse_Epub2_With_Multiple_Chapters()
    {
        // Arrange
        using var stream = new TestEpubBuilder()
            .WithVersion("2.0.1")
            .WithTitle("Multi-Chapter Book")
            .WithAuthor("Jane Doe")
            .AddChapter("ch1", "Introduction", "<p>This is the introduction.</p>")
            .AddChapter("ch2", "Chapter 1", "<p>Content of chapter 1.</p>")
            .AddChapter("ch3", "Chapter 2", "<p>Content of chapter 2.</p>")
            .AddChapter("ch4", "Conclusion", "<p>This is the conclusion.</p>")
            .Build();

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Chapters).HasCount(4);
        await Assert.That(book.Chapters[0].Title).IsEqualTo("Introduction");
        await Assert.That(book.Chapters[1].Title).IsEqualTo("Chapter 1");
        await Assert.That(book.Chapters[2].Title).IsEqualTo("Chapter 2");
        await Assert.That(book.Chapters[3].Title).IsEqualTo("Conclusion");
    }

    [Test]
    public async Task Should_Parse_Metadata_Correctly()
    {
        // Arrange
        using var stream = new TestEpubBuilder()
            .WithVersion("2.0.1")
            .WithTitle("Book with Metadata")
            .WithAuthor("John Smith")
            .WithLanguage("fr")
            .AddChapter("ch1", "Chapter", "<p>Content</p>")
            .Build();

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Book with Metadata");
        await Assert.That(book.Authors[0].Name).IsEqualTo("John Smith");
        await Assert.That(book.Language.Code).IsEqualTo("fr");
    }

    [Test]
    public async Task Should_Return_Error_For_Missing_Container()
    {
        // Arrange
        using var stream = TestEpubBuilder.CreateInvalidEpub("missing-container");

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        var error = result.AsT1;
        await Assert.That(error.Message).Contains("container.xml");
    }

    [Test]
    public async Task Should_Return_Error_For_Invalid_XML()
    {
        // Arrange
        using var stream = TestEpubBuilder.CreateInvalidEpub("invalid-xml");

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        var error = result.AsT1;
        await Assert.That(error.Message).Contains("XML");
    }

    [Test]
    public async Task Should_Return_Error_For_Empty_Archive()
    {
        // Arrange
        using var stream = TestEpubBuilder.CreateInvalidEpub("empty");

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        var error = result.AsT1;
        await Assert.That(error.Message).IsNotNull();
    }

    [Test]
    public async Task Should_Parse_Book_With_Multiple_Authors()
    {
        // Arrange
        var contentOpf = """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="uid">
                <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="uid">test-book-id</dc:identifier>
                    <dc:title>Multi-Author Book</dc:title>
                    <dc:creator>First Author</dc:creator>
                    <dc:creator>Second Author</dc:creator>
                    <dc:creator>Third Author</dc:creator>
                    <dc:language>en</dc:language>
                </metadata>
                <manifest>
                    <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
                    <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml"/>
                </manifest>
                <spine toc="ncx">
                    <itemref idref="ch1"/>
                </spine>
            </package>
            """;

        using var stream = new TestEpubBuilder()
            .WithVersion("2.0.1")
            .AddChapter("ch1", "Chapter", "<p>Content</p>")
            .AddFile("OEBPS/content.opf", contentOpf)
            .Build();

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Authors).HasCount(3);
        await Assert.That(book.Authors[0].Name).IsEqualTo("First Author");
        await Assert.That(book.Authors[1].Name).IsEqualTo("Second Author");
        await Assert.That(book.Authors[2].Name).IsEqualTo("Third Author");
    }

    [Test]
    public async Task Should_Parse_Book_With_Multiple_Identifiers()
    {
        // Arrange
        var contentOpf = """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="uid">
                <metadata xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:opf="http://www.idpf.org/2007/opf">
                    <dc:identifier id="uid" opf:scheme="ISBN">978-0-123456-78-9</dc:identifier>
                    <dc:identifier opf:scheme="UUID">550e8400-e29b-41d4-a716-446655440000</dc:identifier>
                    <dc:identifier opf:scheme="DOI">10.1234/example</dc:identifier>
                    <dc:title>Book with Identifiers</dc:title>
                    <dc:creator>Author</dc:creator>
                    <dc:language>en</dc:language>
                </metadata>
                <manifest>
                    <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
                    <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml"/>
                </manifest>
                <spine toc="ncx">
                    <itemref idref="ch1"/>
                </spine>
            </package>
            """;

        using var stream = new TestEpubBuilder()
            .WithVersion("2.0.1")
            .AddChapter("ch1", "Chapter", "<p>Content</p>")
            .AddFile("OEBPS/content.opf", contentOpf)
            .Build();

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Identifiers).HasCount(3);

        var isbnId = book.Identifiers.FirstOrDefault(i => i.Scheme == "ISBN");
        await Assert.That(isbnId).IsNotNull();
        await Assert.That(isbnId!.Value).IsEqualTo("978-0-123456-78-9");

        var uuidId = book.Identifiers.FirstOrDefault(i => i.Scheme == "UUID");
        await Assert.That(uuidId).IsNotNull();
        await Assert.That(uuidId!.Value).IsEqualTo("550e8400-e29b-41d4-a716-446655440000");
    }

    [Test]
    public async Task Should_Preserve_Chapter_Order()
    {
        // Arrange
        var contentOpf = """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="uid">
                <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="uid">test-book-id</dc:identifier>
                    <dc:title>Ordered Book</dc:title>
                    <dc:creator>Author</dc:creator>
                    <dc:language>en</dc:language>
                </metadata>
                <manifest>
                    <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
                    <item id="ch3" href="ch3.xhtml" media-type="application/xhtml+xml"/>
                    <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml"/>
                    <item id="ch2" href="ch2.xhtml" media-type="application/xhtml+xml"/>
                </manifest>
                <spine toc="ncx">
                    <itemref idref="ch2"/>
                    <itemref idref="ch1"/>
                    <itemref idref="ch3"/>
                </spine>
            </package>
            """;

        using var stream = new TestEpubBuilder()
            .WithVersion("2.0.1")
            .AddFile("OEBPS/content.opf", contentOpf)
            .AddFile("OEBPS/ch1.xhtml", """
                <html><body><h1>First</h1></body></html>
                """)
            .AddFile("OEBPS/ch2.xhtml", """
                <html><body><h1>Second</h1></body></html>
                """)
            .AddFile("OEBPS/ch3.xhtml", """
                <html><body><h1>Third</h1></body></html>
                """)
            .Build();

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Chapters).HasCount(3);
        // Order should follow spine order, not manifest order
        await Assert.That(book.Chapters[0].Href).IsEqualTo("ch2.xhtml");
        await Assert.That(book.Chapters[1].Href).IsEqualTo("ch1.xhtml");
        await Assert.That(book.Chapters[2].Href).IsEqualTo("ch3.xhtml");
    }

    [Test]
    public async Task Should_Handle_Missing_Optional_Metadata()
    {
        // Arrange
        var contentOpf = """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="uid">
                <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="uid">minimal-id</dc:identifier>
                    <dc:title>Minimal Book</dc:title>
                    <dc:creator>Author</dc:creator>
                    <dc:language>en</dc:language>
                </metadata>
                <manifest>
                    <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
                    <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml"/>
                </manifest>
                <spine toc="ncx">
                    <itemref idref="ch1"/>
                </spine>
            </package>
            """;

        using var stream = new TestEpubBuilder()
            .WithVersion("2.0.1")
            .AddChapter("ch1", "Chapter", "<p>Content</p>")
            .AddFile("OEBPS/content.opf", contentOpf)
            .Build();

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Metadata.Publisher).IsNull();
        await Assert.That(book.Metadata.PublicationDate).IsNull();
        await Assert.That(book.Metadata.Description).IsNull();
        await Assert.That(book.Metadata.Rights).IsNull();
    }
}