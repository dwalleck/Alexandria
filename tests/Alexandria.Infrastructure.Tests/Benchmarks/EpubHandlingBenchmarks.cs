using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Alexandria.Infrastructure.IO;

namespace Alexandria.Infrastructure.Tests.Benchmarks;

[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class EpubHandlingBenchmarks
{
    private string _smallEpubPath = null!;
    private string _largeEpubPath = null!;
    private StreamingEpubReader _streamingReader = null!;
    private EpubResourceExtractor _resourceExtractor = null!;
    private byte[] _smallEpubBytes = null!;
    private byte[] _largeEpubBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create test EPUB files
        _smallEpubPath = Path.Combine(Path.GetTempPath(), "benchmark_small.epub");
        _largeEpubPath = Path.Combine(Path.GetTempPath(), "benchmark_large.epub");
        
        CreateBenchmarkEpub(_smallEpubPath, 10, 1024); // 10 chapters, 1KB each
        CreateBenchmarkEpub(_largeEpubPath, 100, 100 * 1024); // 100 chapters, 100KB each
        
        _smallEpubBytes = File.ReadAllBytes(_smallEpubPath);
        _largeEpubBytes = File.ReadAllBytes(_largeEpubPath);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _streamingReader?.Dispose();
        _resourceExtractor?.Dispose();
        
        if (File.Exists(_smallEpubPath))
            File.Delete(_smallEpubPath);
        if (File.Exists(_largeEpubPath))
            File.Delete(_largeEpubPath);
    }

    [Benchmark(Baseline = true)]
    public async Task<int> StreamingReader_SmallEpub_OpenAndCountChapters()
    {
        using var reader = new StreamingEpubReader(_smallEpubPath);
        await reader.OpenAsync();
        
        int count = 0;
        await foreach (var chapter in reader.StreamChaptersAsync("OEBPS/content.opf"))
        {
            count++;
        }
        
        return count;
    }

    [Benchmark]
    public async Task<int> StreamingReader_LargeEpub_OpenAndCountChapters()
    {
        using var reader = new StreamingEpubReader(_largeEpubPath);
        await reader.OpenAsync();
        
        int count = 0;
        await foreach (var chapter in reader.StreamChaptersAsync("OEBPS/content.opf"))
        {
            count++;
        }
        
        return count;
    }

    [Benchmark]
    public async Task<long> StreamingReader_LargeEpub_StreamAllContent()
    {
        using var reader = new StreamingEpubReader(_largeEpubPath);
        await reader.OpenAsync();
        
        long totalLength = 0;
        await foreach (var chapter in reader.StreamChaptersAsync("OEBPS/content.opf"))
        {
            totalLength += chapter.Content.Length;
        }
        
        return totalLength;
    }

    [Benchmark]
    public async Task<int> ResourceExtractor_ExtractWithCache()
    {
        using var extractor = new EpubResourceExtractor(_smallEpubPath);
        
        // First extraction - from file
        var result1 = await extractor.ExtractAsync("OEBPS/chapter1.xhtml");
        
        // Second extraction - from cache
        var result2 = await extractor.ExtractAsync("OEBPS/chapter1.xhtml");
        
        return result1!.Value.Length + result2!.Value.Length;
    }

    [Benchmark]
    public async Task<int> ResourceExtractor_BatchExtraction()
    {
        using var extractor = new EpubResourceExtractor(_smallEpubPath);
        
        var resources = new[]
        {
            "OEBPS/chapter1.xhtml",
            "OEBPS/chapter2.xhtml",
            "OEBPS/chapter3.xhtml",
            "OEBPS/chapter4.xhtml",
            "OEBPS/chapter5.xhtml"
        };
        
        var results = await extractor.ExtractBatchAsync(resources);
        return results.Count;
    }

    [Benchmark]
    public async Task<int> ResourceExtractor_PartialExtraction()
    {
        using var extractor = new EpubResourceExtractor(_smallEpubPath);
        
        // Extract first 100 bytes of 5 chapters
        int totalBytes = 0;
        for (int i = 1; i <= 5; i++)
        {
            var partial = await extractor.ExtractPartialAsync($"OEBPS/chapter{i}.xhtml", 0, 100);
            if (partial.HasValue)
                totalBytes += partial.Value.Length;
        }
        
        return totalBytes;
    }

    [Benchmark]
    public async Task<bool> StreamingReader_MemoryMapping_LargeFile()
    {
        // This will trigger memory mapping for files >50MB
        using var reader = new StreamingEpubReader(_largeEpubPath);
        await reader.OpenAsync();
        
        return reader.UsesMemoryMapping;
    }

    [Benchmark]
    public int Traditional_ZipArchive_SmallEpub()
    {
        using var stream = new MemoryStream(_smallEpubBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        
        int count = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith(".xhtml"))
            {
                using var entryStream = entry.Open();
                using var reader = new StreamReader(entryStream);
                var content = reader.ReadToEnd();
                count++;
            }
        }
        
        return count;
    }

    [Benchmark]
    public int SharpZipLib_SmallEpub()
    {
        using var stream = new MemoryStream(_smallEpubBytes);
        using var zipStream = new ICSharpCode.SharpZipLib.Zip.ZipInputStream(stream);
        
        int count = 0;
        ICSharpCode.SharpZipLib.Zip.ZipEntry? entry;
        
        while ((entry = zipStream.GetNextEntry()) != null)
        {
            if (entry.Name.EndsWith(".xhtml"))
            {
                var buffer = new byte[entry.Size];
                zipStream.Read(buffer, 0, buffer.Length);
                count++;
            }
        }
        
        return count;
    }

    private void CreateBenchmarkEpub(string path, int chapterCount, int chapterSize)
    {
        using var stream = new FileStream(path, FileMode.Create);
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
        
        // Add content.opf
        var opfEntry = archive.CreateEntry("OEBPS/content.opf");
        using (var writer = new StreamWriter(opfEntry.Open()))
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
            sb.AppendLine(@"<package xmlns=""http://www.idpf.org/2007/opf"" version=""3.0"">");
            sb.AppendLine(@"    <metadata xmlns:dc=""http://purl.org/dc/elements/1.1/"">");
            sb.AppendLine(@"        <dc:title>Benchmark Book</dc:title>");
            sb.AppendLine(@"        <dc:creator>Benchmark Author</dc:creator>");
            sb.AppendLine(@"        <dc:language>en</dc:language>");
            sb.AppendLine(@"    </metadata>");
            sb.AppendLine(@"    <manifest>");
            
            for (int i = 1; i <= chapterCount; i++)
            {
                sb.AppendLine($@"        <item id=""ch{i}"" href=""chapter{i}.xhtml"" media-type=""application/xhtml+xml""/>");
            }
            
            sb.AppendLine(@"    </manifest>");
            sb.AppendLine(@"    <spine>");
            
            for (int i = 1; i <= chapterCount; i++)
            {
                sb.AppendLine($@"        <itemref idref=""ch{i}""/>");
            }
            
            sb.AppendLine(@"    </spine>");
            sb.AppendLine(@"</package>");
            
            writer.Write(sb.ToString());
        }
        
        // Add chapters with specified size
        var chapterContent = GenerateContent(chapterSize);
        for (int i = 1; i <= chapterCount; i++)
        {
            var chapterEntry = archive.CreateEntry($"OEBPS/chapter{i}.xhtml");
            using var writer = new StreamWriter(chapterEntry.Open());
            writer.Write($@"<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head><title>Chapter {i}</title></head>
<body><h1>Chapter {i}</h1><p>{chapterContent}</p></body>
</html>");
        }
    }

    private string GenerateContent(int size)
    {
        const string lorem = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. ";
        var sb = new StringBuilder();
        
        while (sb.Length < size)
        {
            sb.Append(lorem);
        }
        
        return sb.ToString(0, Math.Min(sb.Length, size));
    }
}