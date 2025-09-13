using Alexandria.Parser;
using Alexandria.Parser.Tests.Utilities;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Parser.Tests.EndToEnd;

public class EpubReaderTests
{
    private readonly EpubReader _reader = new();

    [Test]
    public async Task Should_Read_Epub2_Book_End_To_End()
    {
        // Arrange
        using var stream = new TestEpubBuilder()
            .WithVersion("2.0.1")
            .WithTitle("End-to-End EPUB 2 Test")
            .WithAuthor("Test Author")
            .WithLanguage("en")
            .AddChapter("ch1", "Introduction", "<p>This is the introduction with some content.</p>")
            .AddChapter("ch2", "Chapter 1", "<p>The main content of chapter 1 goes here.</p>")
            .AddChapter("ch3", "Conclusion", "<p>The conclusion wraps everything up.</p>")
            .Build();

        // Act
        var result = await _reader.LoadBookAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("End-to-End EPUB 2 Test");
        await Assert.That(book.Authors).HasCount(1);
        await Assert.That(book.Authors[0].Name).IsEqualTo("Test Author");
        await Assert.That(book.Chapters).HasCount(3);
        await Assert.That(book.Language.Code).IsEqualTo("en");

        // Verify chapters are in correct order
        await Assert.That(book.Chapters[0].Title).IsEqualTo("Introduction");
        await Assert.That(book.Chapters[1].Title).IsEqualTo("Chapter 1");
        await Assert.That(book.Chapters[2].Title).IsEqualTo("Conclusion");

        // Verify word count functionality
        var totalWords = book.GetTotalWordCount();
        await Assert.That(totalWords).IsGreaterThan(0);

        // Verify reading time estimation
        var readingTime = book.GetEstimatedReadingTime();
        await Assert.That(readingTime.TotalMinutes).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Should_Read_Epub3_Book_End_To_End()
    {
        // Arrange
        using var stream = new TestEpubBuilder()
            .WithVersion("3.0")
            .WithTitle("End-to-End EPUB 3 Test")
            .WithAuthor("Modern Author")
            .WithLanguage("en-US")
            .AddChapter("preface", "Preface", "<p>A modern EPUB 3.0 book with advanced features.</p>")
            .AddChapter("part1", "Part I: Basics", "<p>Introduction to the basics of the subject.</p>")
            .AddChapter("part2", "Part II: Advanced", "<p>Advanced topics and concepts are covered here.</p>")
            .AddChapter("appendix", "Appendix", "<p>Additional reference material.</p>")
            .Build();

        // Act
        var result = await _reader.LoadBookAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("End-to-End EPUB 3 Test");
        await Assert.That(book.Authors[0].Name).IsEqualTo("Modern Author");
        await Assert.That(book.Language.Code).IsEqualTo("en-US");
        await Assert.That(book.Chapters).HasCount(4);

        // Verify chapter content
        await Assert.That(book.Chapters[0].Title).IsEqualTo("Preface");
        await Assert.That(book.Chapters[0].Content).Contains("modern EPUB 3.0");
    }

    [Test]
    public async Task Should_Handle_Invalid_Epub_Gracefully()
    {
        // Arrange
        using var stream = TestEpubBuilder.CreateInvalidEpub("invalid-xml");

        // Act
        var result = await _reader.LoadBookAsync(stream);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        var error = result.AsT1;
        await Assert.That(error.Message).IsNotNull();
        await Assert.That(error.Message).IsNotEmpty();
    }

    [Test]
    public async Task Should_Read_Complex_Book_With_Metadata()
    {
        // Arrange
        var contentOpf = """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="uid">
                <metadata xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:opf="http://www.idpf.org/2007/opf">
                    <dc:identifier id="uid" opf:scheme="ISBN">978-0-123456-78-9</dc:identifier>
                    <dc:identifier opf:scheme="UUID">550e8400-e29b-41d4-a716-446655440000</dc:identifier>
                    <dc:title>Complex Book with Full Metadata</dc:title>
                    <dc:creator opf:role="aut" opf:file-as="Doe, John">John Doe</dc:creator>
                    <dc:creator opf:role="edt">Jane Smith</dc:creator>
                    <dc:language>en-GB</dc:language>
                    <dc:publisher>Test Publisher Inc.</dc:publisher>
                    <dc:date>2024-01-15</dc:date>
                    <dc:description>A comprehensive test book with all metadata fields.</dc:description>
                    <dc:rights>Copyright © 2024 Test Publisher</dc:rights>
                    <dc:subject>Fiction</dc:subject>
                    <dc:coverage>Worldwide</dc:coverage>
                </metadata>
                <manifest>
                    <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
                    <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml"/>
                    <item id="ch2" href="ch2.xhtml" media-type="application/xhtml+xml"/>
                </manifest>
                <spine toc="ncx">
                    <itemref idref="ch1"/>
                    <itemref idref="ch2"/>
                </spine>
            </package>
            """;

        using var stream = new TestEpubBuilder()
            .WithVersion("2.0.1")
            .AddFile("OEBPS/content.opf", contentOpf)
            .AddFile("OEBPS/ch1.xhtml", """
                <html><body>
                    <h1>Chapter 1: The Beginning</h1>
                    <p>This is a longer chapter with multiple paragraphs to test word counting.</p>
                    <p>The story begins on a dark and stormy night. Our protagonist was sitting by the fireplace, reading an old book.</p>
                    <p>Suddenly, there was a knock at the door. Who could it be at this hour?</p>
                </body></html>
                """)
            .AddFile("OEBPS/ch2.xhtml", """
                <html><body>
                    <h1>Chapter 2: The Mystery Deepens</h1>
                    <p>The visitor turned out to be an old friend with urgent news.</p>
                    <p>They spoke in hushed tones about events that had transpired in the neighboring village.</p>
                    <p>It seemed that strange things were happening, and help was desperately needed.</p>
                </body></html>
                """)
            .Build();

        // Act
        var result = await _reader.LoadBookAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;

        // Verify basic info
        await Assert.That(book.Title.Value).IsEqualTo("Complex Book with Full Metadata");
        await Assert.That(book.Language.Code).IsEqualTo("en-GB");

        // Verify authors
        await Assert.That(book.Authors).HasCount(2);
        await Assert.That(book.Authors[0].Name).IsEqualTo("John Doe");
        await Assert.That(book.Authors[0].Role).IsEqualTo("aut");
        await Assert.That(book.Authors[0].FileAs).IsEqualTo("Doe, John");
        await Assert.That(book.Authors[1].Name).IsEqualTo("Jane Smith");
        await Assert.That(book.Authors[1].Role).IsEqualTo("edt");

        // Verify identifiers
        await Assert.That(book.Identifiers).HasCount(2);
        var isbn = book.Identifiers.FirstOrDefault(i => i.Scheme == "ISBN");
        await Assert.That(isbn).IsNotNull();
        await Assert.That(isbn!.Value).IsEqualTo("978-0-123456-78-9");

        // Verify metadata
        await Assert.That(book.Metadata.Publisher).IsEqualTo("Test Publisher Inc.");
        await Assert.That(book.Metadata.PublicationDate).IsNotNull();
        await Assert.That(book.Metadata.Description).IsEqualTo("A comprehensive test book with all metadata fields.");
        await Assert.That(book.Metadata.Rights).IsEqualTo("Copyright © 2024 Test Publisher");
        await Assert.That(book.Metadata.Subject).IsEqualTo("Fiction");
        await Assert.That(book.Metadata.Coverage).IsEqualTo("Worldwide");

        // Verify chapters
        await Assert.That(book.Chapters).HasCount(2);
        await Assert.That(book.Chapters[0].Content).Contains("dark and stormy night");
        await Assert.That(book.Chapters[1].Content).Contains("Mystery Deepens");

        // Verify word count is reasonable
        var totalWords = book.GetTotalWordCount();
        await Assert.That(totalWords).IsGreaterThan(50);
        await Assert.That(totalWords).IsLessThan(200);
    }

    [Test]
    public async Task Should_Load_Book_From_File_Path()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".epub";
        using (var fileStream = File.Create(tempFile))
        {
            using var epubStream = TestEpubBuilder.CreateMinimalEpub("2.0.1");
            await epubStream.CopyToAsync(fileStream);
        }

        try
        {
            // Act
            var result = await _reader.LoadBookAsync(tempFile);

            // Assert
            await Assert.That(result.IsT0).IsTrue();
            var book = result.AsT0;
            await Assert.That(book.Title.Value).IsEqualTo("Minimal Test Book");
        }
        finally
        {
            // Clean up
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Test]
    public async Task Should_Return_Error_For_Non_Existent_File()
    {
        // Arrange
        var nonExistentFile = "/this/file/does/not/exist.epub";

        // Act
        var result = await _reader.LoadBookAsync(nonExistentFile);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        var error = result.AsT1;
        await Assert.That(error.Message).Contains("not found");
    }

    [Test]
    public async Task Should_Return_Error_For_Non_Epub_File()
    {
        // Arrange
        var textFile = Path.GetTempFileName() + ".txt";
        await File.WriteAllTextAsync(textFile, "This is not an EPUB file");

        try
        {
            // Act
            var result = await _reader.LoadBookAsync(textFile);

            // Assert
            await Assert.That(result.IsT1).IsTrue();
            var error = result.AsT1;
            await Assert.That(error.Message).Contains("EPUB");
        }
        finally
        {
            // Clean up
            if (File.Exists(textFile))
                File.Delete(textFile);
        }
    }

    [Test]
    public async Task Should_Parse_Book_With_Special_Characters()
    {
        // Arrange
        using var stream = new TestEpubBuilder()
            .WithVersion("2.0.1")
            .WithTitle("Book with Special Characters: & < > \" '")
            .WithAuthor("Ñoño García & José María")
            .WithLanguage("es")
            .AddChapter("ch1", "Capítulo 1: Introducción", "<p>Este es un párrafo con caracteres especiales: ñ, á, é, í, ó, ú, ü, ¿, ¡</p>")
            .Build();

        // Act
        var result = await _reader.LoadBookAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).Contains("&");
        await Assert.That(book.Authors[0].Name).Contains("Ñoño");
        await Assert.That(book.Chapters[0].Title).Contains("Capítulo");
        await Assert.That(book.Chapters[0].Content).Contains("párrafo");
    }

    [Test]
    public async Task Should_Handle_Large_Book_Efficiently()
    {
        // Arrange
        var builder = new TestEpubBuilder()
            .WithVersion("2.0.1")
            .WithTitle("Large Book Test")
            .WithAuthor("Test Author");

        // Add 100 chapters
        for (int i = 0; i < 100; i++)
        {
            var content = string.Join(" ", Enumerable.Repeat($"Word{i}", 100));
            builder.AddChapter($"ch{i}", $"Chapter {i + 1}", $"<p>{content}</p>");
        }

        using var stream = builder.Build();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _reader.LoadBookAsync(stream);
        stopwatch.Stop();

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Chapters).HasCount(100);

        // Should parse in reasonable time (less than 5 seconds)
        await Assert.That(stopwatch.Elapsed.TotalSeconds).IsLessThan(5);

        // Verify word count calculation works
        var totalWords = book.GetTotalWordCount();
        await Assert.That(totalWords).IsEqualTo(10000); // 100 chapters * 100 words each
    }
}