using System.IO.Compression;
using System.Text;
using Alexandria.Infrastructure.IO;

namespace Alexandria.Infrastructure.Tests.IO;

public class StreamingEpubReaderTests : IAsyncDisposable
{
    private string _testEpubPath;
    private StreamingEpubReader? _reader;

    public StreamingEpubReaderTests()
    {
        _testEpubPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.epub");
    }

    [Before(HookType.Test)]
    public void Setup()
    {
        CreateTestEpub();
    }

    [After(HookType.Test)]
    public async Task Cleanup()
    {
        if (_reader != null)
        {
            await _reader.DisposeAsync();
            _reader = null;
        }

        if (File.Exists(_testEpubPath))
        {
            File.Delete(_testEpubPath);
        }
    }

    [Test]
    public async Task Constructor_WithInvalidPath_ThrowsFileNotFoundException()
    {
        var invalidPath = "/path/that/does/not/exist.epub";
        
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            Task.FromResult(new StreamingEpubReader(invalidPath)));
    }

    [Test]
    public async Task OpenAsync_ValidEpub_ReturnsMetadata()
    {
        _reader = new StreamingEpubReader(_testEpubPath);
        
        var metadata = await _reader.OpenAsync();
        
        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata.Title).IsEqualTo("Test Book");
        await Assert.That(metadata.Creator).IsEqualTo("Test Author");
        await Assert.That(metadata.Language).IsEqualTo("en");
    }

    [Test]
    public async Task OpenAsync_LargeFile_UsesMemoryMapping()
    {
        // Create a large test EPUB (>50MB)
        var largePath = Path.Combine(Path.GetTempPath(), $"large_{Guid.NewGuid()}.epub");
        CreateLargeTestEpub(largePath, 51 * 1024 * 1024); // 51MB
        
        try
        {
            using var reader = new StreamingEpubReader(largePath);
            await reader.OpenAsync();
            
            await Assert.That(reader.UsesMemoryMapping).IsTrue();
            await Assert.That(reader.FileSize).IsGreaterThan(50 * 1024 * 1024);
        }
        finally
        {
            if (File.Exists(largePath))
                File.Delete(largePath);
        }
    }

    [Test]
    public async Task StreamChaptersAsync_ReturnsChaptersLazily()
    {
        _reader = new StreamingEpubReader(_testEpubPath);
        await _reader.OpenAsync();
        
        var chapters = new List<Domain.Entities.Chapter>();
        var opfPath = "OEBPS/content.opf";
        
        await foreach (var chapter in _reader.StreamChaptersAsync(opfPath))
        {
            chapters.Add(chapter);
        }
        
        await Assert.That(chapters.Count).IsEqualTo(2);
        await Assert.That(chapters[0].Order).IsEqualTo(1);
        await Assert.That(chapters[1].Order).IsEqualTo(2);
    }

    [Test]
    public async Task ReadChapterContentAsync_ValidHref_ReturnsContent()
    {
        _reader = new StreamingEpubReader(_testEpubPath);
        await _reader.OpenAsync();
        
        var content = await _reader.ReadChapterContentAsync("chapter1.xhtml");
        
        await Assert.That(content).IsNotNull();
        await Assert.That(content).Contains("Chapter 1 Content");
    }

    [Test]
    public async Task ReadChapterContentAsync_InvalidHref_ThrowsFileNotFoundException()
    {
        _reader = new StreamingEpubReader(_testEpubPath);
        await _reader.OpenAsync();
        
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await _reader.ReadChapterContentAsync("nonexistent.xhtml"));
    }

    [Test]
    public async Task ExtractResourceAsync_ValidResource_ReturnsBytes()
    {
        _reader = new StreamingEpubReader(_testEpubPath);
        await _reader.OpenAsync();
        
        var resource = await _reader.ExtractResourceAsync("styles.css");
        
        await Assert.That(resource.Length).IsGreaterThan(0);
        
        var content = Encoding.UTF8.GetString(resource.Span);
        await Assert.That(content).Contains("body");
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        _reader = new StreamingEpubReader(_testEpubPath);
        await _reader.OpenAsync();
        
        _reader.Dispose();
        _reader.Dispose(); // Should not throw
        
        await Assert.That(true).IsTrue(); // Test passed if we got here
    }

    [Test]
    public async Task DisposeAsync_CalledMultipleTimes_DoesNotThrow()
    {
        _reader = new StreamingEpubReader(_testEpubPath);
        await _reader.OpenAsync();
        
        await _reader.DisposeAsync();
        await _reader.DisposeAsync(); // Should not throw
        
        await Assert.That(true).IsTrue(); // Test passed if we got here
    }

    [Test]
    public async Task OpenAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        _reader = new StreamingEpubReader(_testEpubPath);
        _reader.Dispose();
        
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _reader.OpenAsync());
    }

    private void CreateTestEpub()
    {
        using var stream = new FileStream(_testEpubPath, FileMode.Create);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        
        // Add mimetype
        var mimetypeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var writer = new StreamWriter(mimetypeEntry.Open()))
        {
            writer.Write("application/epub+zip");
        }
        
        // Add container.xml
        var containerEntry = archive.CreateEntry("META-INF/container.xml");
        using (var writer = new StreamWriter(containerEntry.Open()))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<container version=""1.0"" xmlns=""urn:oasis:names:tc:opendocument:xmlns:container"">
    <rootfiles>
        <rootfile full-path=""OEBPS/content.opf"" media-type=""application/oebps-package+xml""/>
    </rootfiles>
</container>");
        }
        
        // Add content.opf
        var opfEntry = archive.CreateEntry("OEBPS/content.opf");
        using (var writer = new StreamWriter(opfEntry.Open()))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<package xmlns=""http://www.idpf.org/2007/opf"" version=""3.0"">
    <metadata xmlns:dc=""http://purl.org/dc/elements/1.1/"">
        <dc:title>Test Book</dc:title>
        <dc:creator>Test Author</dc:creator>
        <dc:language>en</dc:language>
        <dc:identifier>urn:uuid:test-123</dc:identifier>
    </metadata>
    <manifest>
        <item id=""ch1"" href=""chapter1.xhtml"" media-type=""application/xhtml+xml""/>
        <item id=""ch2"" href=""chapter2.xhtml"" media-type=""application/xhtml+xml""/>
        <item id=""css"" href=""styles.css"" media-type=""text/css""/>
    </manifest>
    <spine>
        <itemref idref=""ch1""/>
        <itemref idref=""ch2""/>
    </spine>
</package>");
        }
        
        // Add chapters
        var chapter1Entry = archive.CreateEntry("OEBPS/chapter1.xhtml");
        using (var writer = new StreamWriter(chapter1Entry.Open()))
        {
            writer.Write(@"<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head><title>Chapter 1</title></head>
<body><h1>Chapter 1</h1><p>Chapter 1 Content</p></body>
</html>");
        }
        
        var chapter2Entry = archive.CreateEntry("OEBPS/chapter2.xhtml");
        using (var writer = new StreamWriter(chapter2Entry.Open()))
        {
            writer.Write(@"<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head><title>Chapter 2</title></head>
<body><h1>Chapter 2</h1><p>Chapter 2 Content</p></body>
</html>");
        }
        
        // Add CSS
        var cssEntry = archive.CreateEntry("OEBPS/styles.css");
        using (var writer = new StreamWriter(cssEntry.Open()))
        {
            writer.Write("body { font-family: serif; }");
        }
    }

    private void CreateLargeTestEpub(string path, int targetSize)
    {
        using var stream = new FileStream(path, FileMode.Create);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        
        // Add minimal EPUB structure
        var mimetypeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var writer = new StreamWriter(mimetypeEntry.Open()))
        {
            writer.Write("application/epub+zip");
        }
        
        // Add container.xml
        var containerEntry = archive.CreateEntry("META-INF/container.xml");
        using (var writer = new StreamWriter(containerEntry.Open()))
        {
            writer.Write(@"<?xml version=""1.0""?>
<container xmlns=""urn:oasis:names:tc:opendocument:xmlns:container"" version=""1.0"">
<rootfiles><rootfile full-path=""content.opf"" media-type=""application/oebps-package+xml""/></rootfiles>
</container>");
        }

        // Add content.opf
        var opfEntry = archive.CreateEntry("content.opf");
        using (var writer = new StreamWriter(opfEntry.Open()))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<package xmlns=""http://www.idpf.org/2007/opf"" version=""3.0"">
    <metadata xmlns:dc=""http://purl.org/dc/elements/1.1/"">
        <dc:title>Large Test Book</dc:title>
        <dc:creator>Test Author</dc:creator>
        <dc:language>en</dc:language>
    </metadata>
    <manifest>
        <item id=""ch1"" href=""chapter1.xhtml"" media-type=""application/xhtml+xml""/>
    </manifest>
    <spine>
        <itemref idref=""ch1""/>
    </spine>
</package>");
        }

        // Add large content to reach target size
        // Use NoCompression to ensure the archive size matches our target
        var largeEntry = archive.CreateEntry("large.bin", CompressionLevel.NoCompression);
        using (var entryStream = largeEntry.Open())
        {
            // Write random data that won't compress well
            var random = new Random(42);
            var buffer = new byte[1024 * 1024]; // 1MB buffer
            var bytesWritten = 0;

            while (bytesWritten < targetSize)
            {
                random.NextBytes(buffer);
                var toWrite = Math.Min(buffer.Length, targetSize - bytesWritten);
                entryStream.Write(buffer, 0, toWrite);
                bytesWritten += toWrite;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Cleanup();
    }
}