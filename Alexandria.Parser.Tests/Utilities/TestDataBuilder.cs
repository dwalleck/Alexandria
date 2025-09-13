using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Alexandria.Parser.Tests.Utilities;

/// <summary>
/// Helper class to create test EPUB files in memory
/// </summary>
public class TestEpubBuilder
{
    private readonly List<(string path, string content)> _files = new();
    private string _version = "2.0.1";
    private string _title = "Test Book";
    private string _author = "Test Author";
    private string _language = "en";
    private readonly List<(string id, string href, string content)> _chapters = new();

    public TestEpubBuilder WithVersion(string version)
    {
        _version = version;
        return this;
    }

    public TestEpubBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public TestEpubBuilder WithAuthor(string author)
    {
        _author = author;
        return this;
    }

    public TestEpubBuilder WithLanguage(string language)
    {
        _language = language;
        return this;
    }

    public TestEpubBuilder AddChapter(string id, string title, string content)
    {
        var href = $"{id}.xhtml";
        var xhtmlContent = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml">
            <head>
                <title>{title}</title>
            </head>
            <body>
                <h1>{title}</h1>
                {content}
            </body>
            </html>
            """;
        _chapters.Add((id, href, xhtmlContent));
        return this;
    }

    public TestEpubBuilder AddFile(string path, string content)
    {
        _files.Add((path, content));
        return this;
    }

    public Stream Build()
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            // Add mimetype (must be first and uncompressed)
            var mimetypeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var writer = new StreamWriter(mimetypeEntry.Open()))
            {
                writer.Write("application/epub+zip");
            }

            // Add container.xml
            var containerEntry = archive.CreateEntry("META-INF/container.xml");
            using (var writer = new StreamWriter(containerEntry.Open()))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                        <rootfiles>
                            <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                        </rootfiles>
                    </container>
                    """);
            }

            // Add content.opf
            var contentOpfEntry = archive.CreateEntry("OEBPS/content.opf");
            using (var writer = new StreamWriter(contentOpfEntry.Open()))
            {
                writer.Write(BuildContentOpf());
            }

            // Add toc.ncx for EPUB 2
            if (_version.StartsWith("2"))
            {
                var tocEntry = archive.CreateEntry("OEBPS/toc.ncx");
                using (var writer = new StreamWriter(tocEntry.Open()))
                {
                    writer.Write(BuildTocNcx());
                }
            }

            // Add nav.xhtml for EPUB 3
            if (_version.StartsWith("3"))
            {
                var navEntry = archive.CreateEntry("OEBPS/nav.xhtml");
                using (var writer = new StreamWriter(navEntry.Open()))
                {
                    writer.Write(BuildNavXhtml());
                }
            }

            // Add chapters
            foreach (var (id, href, content) in _chapters)
            {
                var chapterEntry = archive.CreateEntry($"OEBPS/{href}");
                using (var writer = new StreamWriter(chapterEntry.Open()))
                {
                    writer.Write(content);
                }
            }

            // Add any custom files
            foreach (var (path, content) in _files)
            {
                var entry = archive.CreateEntry(path);
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write(content);
                }
            }
        }

        stream.Position = 0;
        return stream;
    }

    private string BuildContentOpf()
    {
        var manifestItems = new StringBuilder();
        var spineItems = new StringBuilder();

        // Add toc to manifest for EPUB 2
        if (_version.StartsWith("2"))
        {
            manifestItems.AppendLine("""        <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>""");
        }

        // Add nav to manifest for EPUB 3
        if (_version.StartsWith("3"))
        {
            manifestItems.AppendLine("""        <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>""");
        }

        foreach (var (id, href, _) in _chapters)
        {
            manifestItems.AppendLine($"""        <item id="{id}" href="{href}" media-type="application/xhtml+xml"/>""");
            spineItems.AppendLine($"""        <itemref idref="{id}"/>""");
        }

        var tocAttribute = _version.StartsWith("2") ? " toc=\"ncx\"" : "";

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="{_version}" unique-identifier="uid">
                <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="uid">test-book-id-12345</dc:identifier>
                    <dc:title>{_title}</dc:title>
                    <dc:creator>{_author}</dc:creator>
                    <dc:language>{_language}</dc:language>
                    <dc:date>2024-01-01</dc:date>
                    <dc:publisher>Test Publisher</dc:publisher>
                    <dc:description>This is a test EPUB book</dc:description>
                </metadata>
                <manifest>
            {manifestItems}
                </manifest>
                <spine{tocAttribute}>
            {spineItems}
                </spine>
            </package>
            """;
    }

    private string BuildTocNcx()
    {
        var navPoints = new StringBuilder();
        int playOrder = 1;

        foreach (var (id, href, _) in _chapters)
        {
            navPoints.AppendLine($"""
                    <navPoint id="navPoint-{playOrder}" playOrder="{playOrder}">
                        <navLabel>
                            <text>Chapter {playOrder}</text>
                        </navLabel>
                        <content src="{href}"/>
                    </navPoint>
            """);
            playOrder++;
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE ncx PUBLIC "-//NISO//DTD ncx 2005-1//EN" "http://www.daisy.org/z3986/2005/ncx-2005-1.dtd">
            <ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">
                <head>
                    <meta name="dtb:uid" content="test-book-id-12345"/>
                </head>
                <docTitle>
                    <text>{_title}</text>
                </docTitle>
                <navMap>
            {navPoints}
                </navMap>
            </ncx>
            """;
    }

    private string BuildNavXhtml()
    {
        var navItems = new StringBuilder();

        foreach (var (id, href, _) in _chapters)
        {
            navItems.AppendLine($"""            <li><a href="{href}">Chapter</a></li>""");
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
            <head>
                <title>Navigation</title>
            </head>
            <body>
                <nav epub:type="toc">
                    <h1>Table of Contents</h1>
                    <ol>
            {navItems}
                    </ol>
                </nav>
            </body>
            </html>
            """;
    }

    public static Stream CreateMinimalEpub(string version = "2.0.1")
    {
        return new TestEpubBuilder()
            .WithVersion(version)
            .WithTitle("Minimal Test Book")
            .AddChapter("chapter1", "Chapter 1", "<p>This is chapter 1 content.</p>")
            .Build();
    }

    public static Stream CreateInvalidEpub(string errorType)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            switch (errorType)
            {
                case "missing-container":
                    // Add mimetype but no container.xml
                    var mimetypeEntry = archive.CreateEntry("mimetype");
                    using (var writer = new StreamWriter(mimetypeEntry.Open()))
                    {
                        writer.Write("application/epub+zip");
                    }
                    break;

                case "invalid-xml":
                    // Add container with invalid XML
                    var containerEntry = archive.CreateEntry("META-INF/container.xml");
                    using (var writer = new StreamWriter(containerEntry.Open()))
                    {
                        writer.Write("This is not valid XML!");
                    }
                    break;

                case "empty":
                    // Completely empty archive
                    break;
            }
        }

        stream.Position = 0;
        return stream;
    }
}