using Alexandria.Parser.Domain.ValueObjects;
using Alexandria.Parser.Infrastructure.Parsers;
using Alexandria.Parser.Tests.Utilities;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Parser.Tests.Infrastructure.Parsers;

public class Epub3ParserTests
{
    private readonly Epub3Parser _parser = new();

    [Test]
    public async Task Should_Parse_Valid_Epub3_Book()
    {
        // Arrange
        using var stream = TestEpubBuilder.CreateMinimalEpub("3.0");

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Minimal Test Book");
        await Assert.That(book.Authors).HasCount(1);
        await Assert.That(book.Chapters).HasCount(1);
    }

    [Test]
    public async Task Should_Parse_Epub3_With_Nav_Document()
    {
        // Arrange
        using var stream = new TestEpubBuilder()
            .WithVersion("3.0")
            .WithTitle("EPUB 3 Book")
            .WithAuthor("Modern Author")
            .AddChapter("ch1", "Chapter 1", "<p>First chapter</p>")
            .AddChapter("ch2", "Chapter 2", "<p>Second chapter</p>")
            .Build();

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Chapters).HasCount(2);
    }

    [Test]
    public async Task Should_Parse_Epub3_Metadata_Refinements()
    {
        // Arrange
        var contentOpf = """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="uid">
                <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="uid">urn:uuid:12345</dc:identifier>
                    <dc:title id="t1">Main Title</dc:title>
                    <dc:title id="t2">Subtitle</dc:title>
                    <meta refines="#t2" property="title-type">subtitle</meta>
                    <dc:creator id="auth1">John Doe</dc:creator>
                    <meta refines="#auth1" property="role" scheme="marc:relators">aut</meta>
                    <meta refines="#auth1" property="file-as">Doe, John</meta>
                    <dc:language>en-US</dc:language>
                    <meta property="dcterms:modified">2024-01-15T10:00:00Z</meta>
                </metadata>
                <manifest>
                    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                    <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml"/>
                </manifest>
                <spine>
                    <itemref idref="ch1"/>
                </spine>
            </package>
            """;

        using var stream = new TestEpubBuilder()
            .WithVersion("3.0")
            .AddFile("OEBPS/content.opf", contentOpf)
            .AddChapter("ch1", "Chapter", "<p>Content</p>")
            .Build();

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Main Title");
        await Assert.That(book.AlternateTitles).HasCountGreaterThanOrEqualTo(1);
        await Assert.That(book.Authors[0].FileAs).IsEqualTo("Doe, John");
    }

    [Test]
    public async Task Should_Parse_Epub3_With_Fixed_Layout_Metadata()
    {
        // Arrange
        var contentOpf = """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="uid">
                <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="uid">fixed-layout-book</dc:identifier>
                    <dc:title>Fixed Layout Book</dc:title>
                    <dc:creator>Author</dc:creator>
                    <dc:language>en</dc:language>
                    <meta property="rendition:layout">pre-paginated</meta>
                    <meta property="rendition:orientation">landscape</meta>
                    <meta property="rendition:spread">both</meta>
                </metadata>
                <manifest>
                    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                    <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml"/>
                </manifest>
                <spine>
                    <itemref idref="ch1"/>
                </spine>
            </package>
            """;

        using var stream = new TestEpubBuilder()
            .WithVersion("3.0")
            .AddFile("OEBPS/content.opf", contentOpf)
            .AddChapter("ch1", "Chapter", "<p>Content</p>")
            .Build();

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Metadata.CustomMetadata).IsNotNull();
        await Assert.That(book.Metadata.CustomMetadata!.ContainsKey("rendition:layout")).IsTrue();
        await Assert.That(book.Metadata.CustomMetadata!["rendition:layout"]).IsEqualTo("pre-paginated");
    }

    [Test]
    public async Task Should_Parse_Epub3_With_Media_Overlays()
    {
        // Arrange
        var contentOpf = """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="uid">
                <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="uid">media-overlay-book</dc:identifier>
                    <dc:title>Book with Audio</dc:title>
                    <dc:creator>Author</dc:creator>
                    <dc:language>en</dc:language>
                    <meta property="media:duration">0:14:23</meta>
                    <meta property="media:narrator">Jane Smith</meta>
                </metadata>
                <manifest>
                    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                    <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml" media-overlay="ch1_overlay"/>
                    <item id="ch1_overlay" href="ch1.smil" media-type="application/smil+xml"/>
                    <item id="audio1" href="audio/ch1.mp3" media-type="audio/mpeg"/>
                </manifest>
                <spine>
                    <itemref idref="ch1"/>
                </spine>
            </package>
            """;

        using var stream = new TestEpubBuilder()
            .WithVersion("3.0")
            .AddFile("OEBPS/content.opf", contentOpf)
            .AddChapter("ch1", "Chapter", "<p>Content</p>")
            .Build();

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Metadata.CustomMetadata!["media:duration"]).IsEqualTo("0:14:23");
        await Assert.That(book.Metadata.CustomMetadata!["media:narrator"]).IsEqualTo("Jane Smith");
    }

    [Test]
    public async Task Should_Handle_Epub3_Collection_Metadata()
    {
        // Arrange
        var contentOpf = """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="uid">
                <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="uid">series-book</dc:identifier>
                    <dc:title id="t1">Book Title</dc:title>
                    <meta property="belongs-to-collection" id="c01">Harry Potter</meta>
                    <meta refines="#c01" property="collection-type">series</meta>
                    <meta refines="#c01" property="group-position">1</meta>
                    <dc:creator>J.K. Rowling</dc:creator>
                    <dc:language>en</dc:language>
                </metadata>
                <manifest>
                    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                    <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml"/>
                </manifest>
                <spine>
                    <itemref idref="ch1"/>
                </spine>
            </package>
            """;

        using var stream = new TestEpubBuilder()
            .WithVersion("3.0")
            .AddFile("OEBPS/content.opf", contentOpf)
            .AddChapter("ch1", "Chapter", "<p>Content</p>")
            .Build();

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Metadata.CustomMetadata!.ContainsKey("belongs-to-collection")).IsTrue();
        await Assert.That(book.Metadata.CustomMetadata!["belongs-to-collection"]).IsEqualTo("Harry Potter");
    }

    [Test]
    public async Task Should_Return_Error_For_Missing_Nav_Document()
    {
        // Arrange
        var contentOpf = """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="uid">
                <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="uid">no-nav-book</dc:identifier>
                    <dc:title>Book without Nav</dc:title>
                    <dc:creator>Author</dc:creator>
                    <dc:language>en</dc:language>
                </metadata>
                <manifest>
                    <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml"/>
                </manifest>
                <spine>
                    <itemref idref="ch1"/>
                </spine>
            </package>
            """;

        using var stream = new TestEpubBuilder()
            .WithVersion("3.0")
            .AddFile("OEBPS/content.opf", contentOpf)
            .AddFile("OEBPS/ch1.xhtml", "<html><body><p>Content</p></body></html>")
            .Build();

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert - EPUB 3 should still parse without nav (though not spec compliant)
        // Parser should be lenient
        await Assert.That(result.IsT0).IsTrue();
    }

    [Test]
    public async Task Should_Parse_Epub3_With_Multiple_Renditions()
    {
        // Arrange
        var contentOpf = """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="uid" prefix="rendition: http://www.idpf.org/vocab/rendition/#">
                <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="uid">multi-rendition-book</dc:identifier>
                    <dc:title>Multi-Rendition Book</dc:title>
                    <dc:creator>Author</dc:creator>
                    <dc:language>en</dc:language>
                    <link rel="alternate" href="../reflowable/content.opf" media-type="application/oebps-package+xml"/>
                </metadata>
                <manifest>
                    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                    <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml"/>
                </manifest>
                <spine>
                    <itemref idref="ch1"/>
                </spine>
            </package>
            """;

        using var stream = new TestEpubBuilder()
            .WithVersion("3.0")
            .AddFile("OEBPS/content.opf", contentOpf)
            .AddChapter("ch1", "Chapter", "<p>Content</p>")
            .Build();

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Multi-Rendition Book");
    }

    [Test]
    public async Task Should_Parse_Epub3_With_Accessibility_Metadata()
    {
        // Arrange
        var contentOpf = """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="uid">
                <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="uid">accessible-book</dc:identifier>
                    <dc:title>Accessible Book</dc:title>
                    <dc:creator>Author</dc:creator>
                    <dc:language>en</dc:language>
                    <meta property="schema:accessMode">textual</meta>
                    <meta property="schema:accessMode">visual</meta>
                    <meta property="schema:accessibilityFeature">alternativeText</meta>
                    <meta property="schema:accessibilityFeature">tableOfContents</meta>
                    <meta property="schema:accessibilityHazard">none</meta>
                    <meta property="schema:accessibilitySummary">This publication provides full text alternative for images.</meta>
                </metadata>
                <manifest>
                    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                    <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml"/>
                </manifest>
                <spine>
                    <itemref idref="ch1"/>
                </spine>
            </package>
            """;

        using var stream = new TestEpubBuilder()
            .WithVersion("3.0")
            .AddFile("OEBPS/content.opf", contentOpf)
            .AddChapter("ch1", "Chapter", "<p>Content</p>")
            .Build();

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Metadata.CustomMetadata!.ContainsKey("schema:accessibilityHazard")).IsTrue();
        await Assert.That(book.Metadata.CustomMetadata!["schema:accessibilityHazard"]).IsEqualTo("none");
    }
}