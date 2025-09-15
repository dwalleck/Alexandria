using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alexandria.Infrastructure.IO;
using TUnit.Assertions;
using TUnit.Core;

namespace Alexandria.Infrastructure.Tests.IO;

public class EpubResourceExtractorTests : IAsyncDisposable
{
    private string _testEpubPath;
    private EpubResourceExtractor? _extractor;

    public EpubResourceExtractorTests()
    {
        _testEpubPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.epub");
    }

    [Before(HookType.Test)]
    public void Setup()
    {
        CreateTestEpubWithResources();
    }

    [After(HookType.Test)]
    public async Task Cleanup()
    {
        _extractor?.Dispose();
        _extractor = null;

        if (File.Exists(_testEpubPath))
        {
            File.Delete(_testEpubPath);
        }
        
        await Task.CompletedTask;
    }

    [Test]
    public async Task Constructor_WithInvalidPath_ThrowsFileNotFoundException()
    {
        var invalidPath = "/path/that/does/not/exist.epub";
        
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            Task.FromResult(new EpubResourceExtractor(invalidPath)));
    }

    [Test]
    public async Task ExtractAsync_CachedResource_ReturnsFromCacheWithoutExtraction()
    {
        _extractor = new EpubResourceExtractor(_testEpubPath);
        
        // First extraction - from file
        var firstResult = await _extractor.ExtractAsync("OEBPS/styles.css");
        await Assert.That(firstResult.HasValue).IsTrue();
        
        // Second extraction - should be from cache (instant)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cachedResult = await _extractor.ExtractAsync("OEBPS/styles.css");
        sw.Stop();
        
        await Assert.That(cachedResult.HasValue).IsTrue();
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(10); // Cache hit should be very fast
        
        // Content should be identical
        await Assert.That(cachedResult.Value.Span.SequenceEqual(firstResult.Value.Span)).IsTrue();
    }

    [Test]
    public async Task ExtractAsync_NonExistentResource_ReturnsNull()
    {
        _extractor = new EpubResourceExtractor(_testEpubPath);
        
        var result = await _extractor.ExtractAsync("nonexistent.file");
        
        await Assert.That(result.HasValue).IsFalse();
    }

    [Test]
    public async Task ExtractPartialAsync_ValidRange_ReturnsPartialContent()
    {
        _extractor = new EpubResourceExtractor(_testEpubPath);
        
        // Extract first 10 bytes of CSS
        var partial = await _extractor.ExtractPartialAsync("OEBPS/styles.css", 0, 10);
        
        await Assert.That(partial.HasValue).IsTrue();
        await Assert.That(partial.Value.Length).IsEqualTo(10);
        
        var content = Encoding.UTF8.GetString(partial.Value.Span);
        await Assert.That(content).StartsWith("body { fon"); // "body { font-family: serif; }"
    }

    [Test]
    public async Task ExtractPartialAsync_OffsetBeyondFile_ReturnsNull()
    {
        _extractor = new EpubResourceExtractor(_testEpubPath);
        
        var partial = await _extractor.ExtractPartialAsync("OEBPS/styles.css", 10000, 10);
        
        await Assert.That(partial.HasValue).IsFalse();
    }

    [Test]
    public async Task ExtractBatchAsync_MultipleResources_ReturnsAll()
    {
        _extractor = new EpubResourceExtractor(_testEpubPath);
        
        var resources = new[] { "OEBPS/styles.css", "OEBPS/image.png", "META-INF/container.xml" };
        var results = await _extractor.ExtractBatchAsync(resources);
        
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results.ContainsKey("OEBPS/styles.css")).IsTrue();
        await Assert.That(results.ContainsKey("OEBPS/image.png")).IsTrue();
        await Assert.That(results.ContainsKey("META-INF/container.xml")).IsTrue();
    }

    [Test]
    public async Task ExtractBatchAsync_MixedValidInvalid_ReturnsOnlyValid()
    {
        _extractor = new EpubResourceExtractor(_testEpubPath);
        
        var resources = new[] { "OEBPS/styles.css", "nonexistent.file", "META-INF/container.xml" };
        var results = await _extractor.ExtractBatchAsync(resources);
        
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results.ContainsKey("OEBPS/styles.css")).IsTrue();
        await Assert.That(results.ContainsKey("META-INF/container.xml")).IsTrue();
        await Assert.That(results.ContainsKey("nonexistent.file")).IsFalse();
    }

    [Test]
    public async Task PreloadAsync_LoadsResourcesIntoCache()
    {
        _extractor = new EpubResourceExtractor(_testEpubPath);
        
        var resources = new[] { "OEBPS/styles.css", "OEBPS/image.png" };
        await _extractor.PreloadAsync(resources);
        
        // Now extraction should be instant from cache
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result1 = await _extractor.ExtractAsync("OEBPS/styles.css");
        var result2 = await _extractor.ExtractAsync("OEBPS/image.png");
        sw.Stop();
        
        await Assert.That(result1.HasValue).IsTrue();
        await Assert.That(result2.HasValue).IsTrue();
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(10);
    }

    [Test]
    public async Task GetResourceInfoAsync_ValidResource_ReturnsInfo()
    {
        _extractor = new EpubResourceExtractor(_testEpubPath);
        
        var info = await _extractor.GetResourceInfoAsync("OEBPS/styles.css");
        
        await Assert.That(info).IsNotNull();
        await Assert.That(info!.Path).IsEqualTo("OEBPS/styles.css");
        await Assert.That(info.UncompressedSize).IsGreaterThan(0);
        await Assert.That(info.IsCompressed).IsTrue();
    }

    [Test]
    public async Task GetResourceInfoAsync_InvalidResource_ReturnsNull()
    {
        _extractor = new EpubResourceExtractor(_testEpubPath);
        
        var info = await _extractor.GetResourceInfoAsync("nonexistent.file");
        
        await Assert.That(info).IsNull();
    }

    [Test]
    public async Task GetCacheStatistics_ReflectsCurrentUsage()
    {
        var config = new EpubResourceExtractor.Configuration
        {
            MaxCacheSizeBytes = 1024 * 1024 // 1MB
        };
        _extractor = new EpubResourceExtractor(_testEpubPath, config);
        
        // Initial stats
        var stats1 = _extractor.GetCacheStatistics();
        await Assert.That(stats1.CurrentSizeBytes).IsEqualTo(0);
        await Assert.That(stats1.UtilizationPercentage).IsEqualTo(0.0);
        
        // Extract something to populate cache
        await _extractor.ExtractAsync("OEBPS/styles.css");
        
        // Stats should reflect cached content
        var stats2 = _extractor.GetCacheStatistics();
        await Assert.That(stats2.CurrentSizeBytes).IsGreaterThan(0);
        await Assert.That(stats2.UtilizationPercentage).IsGreaterThan(0.0);
    }

    [Test]
    public async Task ClearCache_RemovesAllCachedItems()
    {
        _extractor = new EpubResourceExtractor(_testEpubPath);
        
        // Cache some items
        await _extractor.ExtractAsync("OEBPS/styles.css");
        await _extractor.ExtractAsync("OEBPS/image.png");
        
        var statsBeforeClear = _extractor.GetCacheStatistics();
        await Assert.That(statsBeforeClear.CurrentSizeBytes).IsGreaterThan(0);
        
        // Clear cache
        _extractor.ClearCache();
        
        var statsAfterClear = _extractor.GetCacheStatistics();
        await Assert.That(statsAfterClear.CurrentSizeBytes).IsEqualTo(0);
    }

    [Test]
    public async Task CachePriority_SmallFilesGetHigherPriority()
    {
        var config = new EpubResourceExtractor.Configuration
        {
            MaxCacheSizeBytes = 50 * 1024 // 50KB - small cache to test eviction
        };
        _extractor = new EpubResourceExtractor(_testEpubPath, config);
        
        // Extract small file first (should get high priority)
        await _extractor.ExtractAsync("OEBPS/styles.css");
        
        // Extract large file (should get lower priority)
        await _extractor.ExtractAsync("OEBPS/large.txt");
        
        // Small file should still be in cache due to higher priority
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var smallFile = await _extractor.ExtractAsync("OEBPS/styles.css");
        sw.Stop();
        
        await Assert.That(smallFile.HasValue).IsTrue();
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(10); // Should be from cache
    }

    [Test]
    public async Task Configuration_MaxConcurrentExtractions_Works()
    {
        var config = new EpubResourceExtractor.Configuration
        {
            MaxConcurrentExtractions = 2 // Allow 2 concurrent extractions
        };
        _extractor = new EpubResourceExtractor(_testEpubPath, config);

        // Simply verify that multiple concurrent extractions work without errors
        var tasks = new List<Task<ReadOnlyMemory<byte>?>>();

        // Start 5 extraction tasks (more than MaxConcurrentExtractions)
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(_extractor.ExtractAsync("OEBPS/styles.css").AsTask());
        }

        // All should complete successfully even with concurrency limit
        var results = await Task.WhenAll(tasks);

        // Verify all extractions succeeded
        foreach (var result in results)
        {
            await Assert.That(result.HasValue).IsTrue();
        }
    }

    [Test]
    public async Task Dispose_MultipleCalls_DoesNotThrow()
    {
        _extractor = new EpubResourceExtractor(_testEpubPath);
        await _extractor.ExtractAsync("OEBPS/styles.css");
        
        _extractor.Dispose();
        _extractor.Dispose(); // Should not throw
        
        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task ExtractAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        _extractor = new EpubResourceExtractor(_testEpubPath);
        _extractor.Dispose();
        
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _extractor.ExtractAsync("OEBPS/styles.css"));
    }

    private void CreateTestEpubWithResources()
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
            writer.Write(@"<?xml version=""1.0""?>
<container xmlns=""urn:oasis:names:tc:opendocument:xmlns:container"" version=""1.0"">
<rootfiles><rootfile full-path=""OEBPS/content.opf"" media-type=""application/oebps-package+xml""/></rootfiles>
</container>");
        }
        
        // Add CSS
        var cssEntry = archive.CreateEntry("OEBPS/styles.css");
        using (var writer = new StreamWriter(cssEntry.Open()))
        {
            writer.Write("body { font-family: serif; }\np { margin: 1em 0; }");
        }
        
        // Add a fake image (just some bytes)
        var imageEntry = archive.CreateEntry("OEBPS/image.png");
        using (var stream2 = imageEntry.Open())
        {
            var fakeImageData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            stream2.Write(fakeImageData, 0, fakeImageData.Length);
        }
        
        // Add a large text file for testing cache eviction
        var largeEntry = archive.CreateEntry("OEBPS/large.txt");
        using (var writer = new StreamWriter(largeEntry.Open()))
        {
            var largeContent = new string('X', 100 * 1024); // 100KB
            writer.Write(largeContent);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Cleanup();
    }
}