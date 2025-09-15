# Alexandria EPUB Parser - Enhanced Development Plan

## Overview

This enhanced development plan provides detailed implementation specifications, performance requirements, and code templates for migrating Alexandria EPUB Parser to a clean Vertical Slice Architecture with Domain-Driven Design principles. Each phase includes specific guidance from our architecture and performance documents.

## Table of Contents

1. [Phase 1: Foundation & Core Domain](#phase-1-foundation--core-domain)
2. [Phase 2: Vertical Slice Features](#phase-2-vertical-slice-architecture---core-features)
3. [Performance Testing Framework](#performance-testing-framework)
4. [Migration Checklist](#migration-checklist)
5. [Code Review Guidelines](#code-review-guidelines)

---

## Phase 1: Foundation & Core Domain 🏗️

### 1.1 Domain Service Interfaces - IContentAnalyzer

#### Implementation Requirements

Create `Domain/Services/IContentAnalyzer.cs` with performance-aware signatures:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alexandria.Domain.Services;

/// <summary>
/// High-performance content analysis service for EPUB chapters
/// </summary>
public interface IContentAnalyzer
{
    /// <summary>
    /// Extracts plain text from HTML content using zero-allocation techniques
    /// </summary>
    /// <param name="htmlContent">HTML content as ReadOnlySpan for zero-copy processing</param>
    /// <param name="buffer">Optional buffer for reuse (min 4KB)</param>
    /// <returns>Plain text content</returns>
    string ExtractPlainText(ReadOnlySpan<char> htmlContent, char[]? buffer = null);

    /// <summary>
    /// Counts words using span-based processing
    /// </summary>
    /// <param name="text">Text to analyze</param>
    /// <returns>Word count</returns>
    int CountWords(ReadOnlySpan<char> text);

    /// <summary>
    /// Estimates reading time based on configurable WPM
    /// </summary>
    /// <param name="text">Text to analyze</param>
    /// <param name="wordsPerMinute">Reading speed (default: 250)</param>
    /// <returns>Estimated reading time</returns>
    TimeSpan EstimateReadingTime(ReadOnlySpan<char> text, int wordsPerMinute = 250);

    /// <summary>
    /// Extracts sentences for preview generation
    /// </summary>
    /// <param name="text">Source text</param>
    /// <param name="maxSentences">Maximum sentences to extract</param>
    /// <returns>Extracted sentences</returns>
    string[] ExtractSentences(ReadOnlySpan<char> text, int maxSentences);

    /// <summary>
    /// Generates a preview maintaining word boundaries
    /// </summary>
    /// <param name="text">Source text</param>
    /// <param name="maxLength">Maximum preview length</param>
    /// <returns>Preview text with ellipsis if truncated</returns>
    string GeneratePreview(ReadOnlySpan<char> text, int maxLength);

    /// <summary>
    /// Analyzes content and returns comprehensive metrics
    /// </summary>
    /// <param name="htmlContent">HTML content to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Content metrics</returns>
    ValueTask<ContentMetrics> AnalyzeContentAsync(
        string htmlContent,
        CancellationToken cancellationToken = default);
}
```

#### ContentMetrics Value Object

Create `Domain/ValueObjects/ContentMetrics.cs`:

```csharp
namespace Alexandria.Domain.ValueObjects;

/// <summary>
/// Immutable value object containing content analysis metrics
/// </summary>
public sealed record ContentMetrics
{
    public int WordCount { get; init; }
    public int CharacterCount { get; init; }
    public int CharacterCountNoSpaces { get; init; }
    public int SentenceCount { get; init; }
    public int ParagraphCount { get; init; }
    public TimeSpan EstimatedReadingTime { get; init; }
    public double AverageWordsPerSentence { get; init; }
    public double ReadabilityScore { get; init; } // Flesch Reading Ease
    public Dictionary<string, int> WordFrequency { get; init; } = new();

    /// <summary>
    /// Calculates reading difficulty level based on readability score
    /// </summary>
    public ReadingDifficulty GetDifficulty() => ReadabilityScore switch
    {
        >= 90 => ReadingDifficulty.VeryEasy,
        >= 80 => ReadingDifficulty.Easy,
        >= 70 => ReadingDifficulty.FairlyEasy,
        >= 60 => ReadingDifficulty.Standard,
        >= 50 => ReadingDifficulty.FairlyDifficult,
        >= 30 => ReadingDifficulty.Difficult,
        _ => ReadingDifficulty.VeryDifficult
    };
}

public enum ReadingDifficulty
{
    VeryEasy,
    Easy,
    FairlyEasy,
    Standard,
    FairlyDifficult,
    Difficult,
    VeryDifficult
}
```

#### Performance Patterns Required

- Use `ReadOnlySpan<char>` for all text processing
- Implement `SearchValues<char>` for character set operations
- Use `ArrayPool<char>` for temporary buffers over 4KB
- Stack allocate buffers under 4KB using `stackalloc`
- Avoid regex for simple operations; use spans instead

#### Acceptance Criteria

- [ ] Process 1MB of HTML content in under 100ms
- [ ] Zero heap allocations for text under 4KB
- [ ] Word counting accuracy matches Microsoft Word ±2%
- [ ] Reading time estimates within ±10% of Medium.com
- [ ] All methods have XML documentation
- [ ] Unit tests achieve 100% code coverage

#### Testing Requirements

```csharp
[Benchmark]
public class ContentAnalyzerBenchmarks
{
    private IContentAnalyzer _analyzer;
    private string _smallHtml; // 1KB
    private string _mediumHtml; // 100KB
    private string _largeHtml; // 1MB

    [Benchmark]
    public int CountWords_Small() =>
        _analyzer.CountWords(_smallHtml.AsSpan());

    [Benchmark]
    public int CountWords_Large() =>
        _analyzer.CountWords(_largeHtml.AsSpan());

    // Memory allocation should be 0 for small content
    [Benchmark]
    [MemoryDiagnoser]
    public string ExtractPlainText_Small() =>
        _analyzer.ExtractPlainText(_smallHtml.AsSpan());
}
```

---

### 1.2 Core Package Integration - AngleSharpContentAnalyzer

#### Implementation Requirements

Create `Infrastructure/ContentProcessing/AngleSharpContentAnalyzer.cs`:

```csharp
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Alexandria.Infrastructure.ContentProcessing;

/// <summary>
/// High-performance content analyzer using AngleSharp for HTML parsing
/// </summary>
public sealed class AngleSharpContentAnalyzer : IContentAnalyzer
{
    // Pre-compiled SearchValues for efficient character searching (NET 8+)
    private static readonly SearchValues<char> WordBoundaries =
        SearchValues.Create(" \t\n\r.!?,;:()[]{}\"'""''—–");
    private static readonly SearchValues<char> SentenceEndings =
        SearchValues.Create(".!?");
    private static readonly SearchValues<char> Whitespace =
        SearchValues.Create(" \t\n\r\u00A0");

    // AngleSharp configuration (reused for performance)
    private readonly IConfiguration _angleSharpConfig;
    private readonly IHtmlParser _parser;

    // Object pools for buffer reuse
    private readonly ArrayPool<char> _charPool = ArrayPool<char>.Shared;
    private readonly ArrayPool<byte> _bytePool = ArrayPool<byte>.Shared;

    public AngleSharpContentAnalyzer()
    {
        _angleSharpConfig = Configuration.Default;
        _parser = new HtmlParser(new HtmlParserOptions
        {
            IsKeepingSourceReferences = false, // Save memory
            IsScripting = false // We don't need script execution
        });
    }

    public string ExtractPlainText(ReadOnlySpan<char> htmlContent, char[]? buffer = null)
    {
        // Fast path for small content - use stack allocation
        if (htmlContent.Length < 4096)
        {
            return ExtractPlainTextSmall(htmlContent);
        }

        // Large content - use ArrayPool
        return ExtractPlainTextLarge(htmlContent, buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ExtractPlainTextSmall(ReadOnlySpan<char> htmlContent)
    {
        // Stack allocate buffer for small content
        Span<char> buffer = stackalloc char[htmlContent.Length];
        int written = 0;

        // Parse with AngleSharp
        using var document = _parser.ParseDocument(htmlContent.ToString());

        // Extract text from body
        var textContent = document.Body?.TextContent ?? string.Empty;

        // Normalize whitespace using spans
        ReadOnlySpan<char> source = textContent.AsSpan();
        bool lastWasWhitespace = false;

        for (int i = 0; i < source.Length; i++)
        {
            if (source[i..].ContainsAny(Whitespace))
            {
                if (!lastWasWhitespace && written > 0)
                {
                    buffer[written++] = ' ';
                    lastWasWhitespace = true;
                }
            }
            else
            {
                buffer[written++] = source[i];
                lastWasWhitespace = false;
            }
        }

        return new string(buffer[..written]);
    }

    private string ExtractPlainTextLarge(ReadOnlySpan<char> htmlContent, char[]? providedBuffer)
    {
        // Rent buffer from pool
        var buffer = providedBuffer ?? _charPool.Rent(htmlContent.Length);
        try
        {
            int written = 0;

            // Parse HTML
            using var document = _parser.ParseDocument(htmlContent.ToString());

            // Process block elements to maintain structure
            ProcessNode(document.Body, buffer, ref written);

            return new string(buffer, 0, written);
        }
        finally
        {
            if (providedBuffer == null)
            {
                _charPool.Return(buffer, clearArray: true);
            }
        }
    }

    private void ProcessNode(INode? node, char[] buffer, ref int written)
    {
        if (node == null) return;

        switch (node.NodeType)
        {
            case NodeType.Text:
                var text = node.TextContent.AsSpan();
                if (!text.IsWhiteSpace())
                {
                    text.CopyTo(buffer.AsSpan(written));
                    written += text.Length;
                }
                break;

            case NodeType.Element:
                var element = (IElement)node;

                // Add line breaks for block elements
                if (IsBlockElement(element.TagName))
                {
                    if (written > 0 && buffer[written - 1] != '\n')
                    {
                        buffer[written++] = '\n';
                        if (element.TagName.Equals("P", StringComparison.OrdinalIgnoreCase))
                        {
                            buffer[written++] = '\n'; // Double line break for paragraphs
                        }
                    }
                }

                // Process children
                foreach (var child in element.ChildNodes)
                {
                    ProcessNode(child, buffer, ref written);
                }

                // Add line break after block element
                if (IsBlockElement(element.TagName) && written > 0)
                {
                    if (buffer[written - 1] != '\n')
                    {
                        buffer[written++] = '\n';
                    }
                }
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBlockElement(string tagName)
    {
        return tagName switch
        {
            "P" or "DIV" or "H1" or "H2" or "H3" or "H4" or "H5" or "H6"
            or "LI" or "TR" or "BR" or "HR" or "BLOCKQUOTE" => true,
            _ => false
        };
    }

    public int CountWords(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) return 0;

        int count = 0;
        bool inWord = false;

        for (int i = 0; i < text.Length; i++)
        {
            bool isBoundary = WordBoundaries.Contains(text[i]);

            if (!isBoundary && !inWord)
            {
                count++;
                inWord = true;
            }
            else if (isBoundary)
            {
                inWord = false;
            }
        }

        return count;
    }

    public TimeSpan EstimateReadingTime(ReadOnlySpan<char> text, int wordsPerMinute = 250)
    {
        if (wordsPerMinute <= 0)
            throw new ArgumentException("Words per minute must be positive", nameof(wordsPerMinute));

        int wordCount = CountWords(text);
        double minutes = (double)wordCount / wordsPerMinute;
        return TimeSpan.FromMinutes(minutes);
    }

    public string[] ExtractSentences(ReadOnlySpan<char> text, int maxSentences)
    {
        if (maxSentences <= 0) return Array.Empty<string>();

        var sentences = new List<string>(maxSentences);
        int start = 0;

        for (int i = 0; i < text.Length && sentences.Count < maxSentences; i++)
        {
            if (SentenceEndings.Contains(text[i]))
            {
                // Check for abbreviations (e.g., "Mr.", "Dr.")
                if (i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1]))
                    continue;

                var sentence = text[start..(i + 1)].Trim();
                if (!sentence.IsEmpty)
                {
                    sentences.Add(sentence.ToString());
                }
                start = i + 1;
            }
        }

        return sentences.ToArray();
    }

    public string GeneratePreview(ReadOnlySpan<char> text, int maxLength)
    {
        if (text.IsEmpty || maxLength <= 0) return string.Empty;

        if (text.Length <= maxLength)
            return text.ToString();

        // Find last complete word within limit
        int cutoff = Math.Min(maxLength, text.Length);

        // Back up to last word boundary
        while (cutoff > 0 && !WordBoundaries.Contains(text[cutoff - 1]))
        {
            cutoff--;
        }

        // Trim whitespace
        while (cutoff > 0 && char.IsWhiteSpace(text[cutoff - 1]))
        {
            cutoff--;
        }

        if (cutoff == 0)
            cutoff = Math.Min(maxLength, text.Length);

        return text[..cutoff].ToString() + "...";
    }

    public async ValueTask<ContentMetrics> AnalyzeContentAsync(
        string htmlContent,
        CancellationToken cancellationToken = default)
    {
        // Extract plain text first
        var plainText = ExtractPlainText(htmlContent.AsSpan());
        var textSpan = plainText.AsSpan();

        // Count words
        int wordCount = CountWords(textSpan);

        // Count sentences
        var sentences = ExtractSentences(textSpan, int.MaxValue);
        int sentenceCount = sentences.Length;

        // Character counts
        int charCount = textSpan.Length;
        int charCountNoSpaces = 0;
        for (int i = 0; i < textSpan.Length; i++)
        {
            if (!char.IsWhiteSpace(textSpan[i]))
                charCountNoSpaces++;
        }

        // Parse HTML for paragraph count
        using var document = await _parser.ParseDocumentAsync(htmlContent, cancellationToken);
        int paragraphCount = document.QuerySelectorAll("p").Length;

        // Calculate reading time
        var readingTime = EstimateReadingTime(textSpan);

        // Calculate averages
        double avgWordsPerSentence = sentenceCount > 0 ? (double)wordCount / sentenceCount : 0;

        // Calculate readability (simplified Flesch Reading Ease)
        double readability = CalculateReadability(wordCount, sentenceCount, textSpan);

        // Word frequency (top 100 words)
        var wordFrequency = CalculateWordFrequency(textSpan, 100);

        return new ContentMetrics
        {
            WordCount = wordCount,
            CharacterCount = charCount,
            CharacterCountNoSpaces = charCountNoSpaces,
            SentenceCount = sentenceCount,
            ParagraphCount = paragraphCount,
            EstimatedReadingTime = readingTime,
            AverageWordsPerSentence = avgWordsPerSentence,
            ReadabilityScore = readability,
            WordFrequency = wordFrequency
        };
    }

    private double CalculateReadability(int words, int sentences, ReadOnlySpan<char> text)
    {
        if (sentences == 0 || words == 0) return 0;

        // Simplified Flesch Reading Ease
        // Score = 206.835 - 1.015 * (words/sentences) - 84.6 * (syllables/words)

        double avgWordsPerSentence = (double)words / sentences;

        // Simplified syllable counting (not perfect but fast)
        int syllables = EstimateSyllables(text);
        double avgSyllablesPerWord = (double)syllables / words;

        double score = 206.835 - (1.015 * avgWordsPerSentence) - (84.6 * avgSyllablesPerWord);
        return Math.Max(0, Math.Min(100, score));
    }

    private int EstimateSyllables(ReadOnlySpan<char> text)
    {
        // Simple vowel-based estimation (not linguistically perfect but fast)
        int count = 0;
        bool lastWasVowel = false;

        for (int i = 0; i < text.Length; i++)
        {
            bool isVowel = "aeiouAEIOU".Contains(text[i]);
            if (isVowel && !lastWasVowel)
            {
                count++;
            }
            lastWasVowel = isVowel;
        }

        return Math.Max(count, 1);
    }

    private Dictionary<string, int> CalculateWordFrequency(ReadOnlySpan<char> text, int topN)
    {
        var frequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int wordStart = -1;

        for (int i = 0; i <= text.Length; i++)
        {
            bool isBoundary = i == text.Length || WordBoundaries.Contains(text[i]);

            if (!isBoundary && wordStart == -1)
            {
                wordStart = i;
            }
            else if (isBoundary && wordStart != -1)
            {
                var word = text[wordStart..i].ToString().ToLowerInvariant();
                if (word.Length > 2) // Skip very short words
                {
                    frequency[word] = frequency.GetValueOrDefault(word) + 1;
                }
                wordStart = -1;
            }
        }

        // Return top N words
        return frequency.OrderByDescending(kvp => kvp.Value)
                       .Take(topN)
                       .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}
```

#### Package Configuration

Add to `Alexandria.Infrastructure.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="AngleSharp" Version="0.17.1" />
  <PackageReference Include="HtmlSanitizer" Version="8.0.746" />
  <PackageReference Include="ExCSS" Version="4.2.3" />
  <PackageReference Include="SharpZipLib" Version="1.4.2" />
  <PackageReference Include="LiteDB" Version="5.0.17" />
  <PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="8.0.0" />
</ItemGroup>
```

#### Performance Anti-Patterns to Avoid

❌ **DON'T**:
- Use `string.Split()` for word counting
- Create intermediate string arrays
- Use regex for simple character operations
- Allocate new buffers for each operation
- Parse HTML multiple times

✅ **DO**:
- Use `ReadOnlySpan<char>` for text processing
- Reuse buffers from ArrayPool
- Stack allocate small buffers
- Use SearchValues for character set operations
- Cache parsed documents when analyzing multiple metrics

#### Acceptance Criteria

- [ ] Passes all unit tests from Phase 1.1
- [ ] Memory allocation under 1KB for 4KB HTML
- [ ] Handles malformed HTML gracefully
- [ ] Thread-safe implementation
- [ ] Supports cancellation tokens
- [ ] Benchmark results meet performance targets

---

### 1.4 Efficient EPUB File Handling - StreamingEpubReader

#### Implementation Requirements

Create `Infrastructure/IO/StreamingEpubReader.cs`:

```csharp
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;

namespace Alexandria.Infrastructure.IO;

/// <summary>
/// High-performance EPUB reader using streaming and memory-mapped files
/// </summary>
public sealed class StreamingEpubReader : IDisposable
{
    private readonly string _epubPath;
    private readonly long _fileSize;
    private ZipFile? _zipFile;
    private MemoryMappedFile? _memoryMappedFile;
    private readonly Dictionary<string, WeakReference<byte[]>> _cache = new();
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

    // Thresholds
    private const long MemoryMapThreshold = 10 * 1024 * 1024; // 10MB
    private const int StreamBufferSize = 81920; // Just under LOH threshold
    private const int CacheMaxSize = 50 * 1024 * 1024; // 50MB cache limit

    public StreamingEpubReader(string epubPath)
    {
        _epubPath = epubPath ?? throw new ArgumentNullException(nameof(epubPath));

        if (!File.Exists(epubPath))
            throw new FileNotFoundException("EPUB file not found", epubPath);

        _fileSize = new FileInfo(epubPath).Length;
        InitializeReader();
    }

    private void InitializeReader()
    {
        // Use memory-mapped files for large EPUBs
        if (_fileSize > MemoryMapThreshold)
        {
            _memoryMappedFile = MemoryMappedFile.CreateFromFile(
                _epubPath,
                FileMode.Open,
                null,
                _fileSize,
                MemoryMappedFileAccess.Read);

            // Create ZipFile from memory-mapped stream
            var stream = _memoryMappedFile.CreateViewStream(
                0, _fileSize, MemoryMappedFileAccess.Read);
            _zipFile = new ZipFile(stream);
        }
        else
        {
            // Direct file access for smaller EPUBs
            _zipFile = new ZipFile(_epubPath);
        }
    }

    /// <summary>
    /// Reads container.xml without extracting the entire EPUB
    /// </summary>
    public async ValueTask<string> ReadContainerXmlAsync(CancellationToken cancellationToken = default)
    {
        const string containerPath = "META-INF/container.xml";
        return await ReadEntryAsync(containerPath, cancellationToken);
    }

    /// <summary>
    /// Reads an entry from the EPUB as a string
    /// </summary>
    public async ValueTask<string> ReadEntryAsync(string entryPath, CancellationToken cancellationToken = default)
    {
        var bytes = await ReadEntryBytesAsync(entryPath, cancellationToken);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Reads an entry from the EPUB as bytes with caching
    /// </summary>
    public async ValueTask<byte[]> ReadEntryBytesAsync(string entryPath, CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cache.TryGetValue(entryPath, out var weakRef) && weakRef.TryGetTarget(out var cached))
        {
            return cached;
        }

        // Find entry
        var entry = _zipFile?.GetEntry(entryPath);
        if (entry == null)
            throw new InvalidOperationException($"Entry not found: {entryPath}");

        // Read entry
        byte[] data = await ReadEntryInternalAsync(entry, cancellationToken);

        // Cache if small enough
        if (data.Length < 1024 * 1024) // Cache entries under 1MB
        {
            _cache[entryPath] = new WeakReference<byte[]>(data);
        }

        return data;
    }

    private async ValueTask<byte[]> ReadEntryInternalAsync(ZipEntry entry, CancellationToken cancellationToken)
    {
        using var stream = _zipFile!.GetInputStream(entry);

        // For small entries, read directly
        if (entry.Size < StreamBufferSize)
        {
            var buffer = new byte[entry.Size];
            await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            return buffer;
        }

        // For large entries, use pooled buffers
        var result = new byte[entry.Size];
        var tempBuffer = _bufferPool.Rent(StreamBufferSize);
        try
        {
            int totalRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(
                tempBuffer, 0, StreamBufferSize, cancellationToken)) > 0)
            {
                Buffer.BlockCopy(tempBuffer, 0, result, totalRead, bytesRead);
                totalRead += bytesRead;

                cancellationToken.ThrowIfCancellationRequested();
            }

            return result;
        }
        finally
        {
            _bufferPool.Return(tempBuffer, clearArray: true);
        }
    }

    /// <summary>
    /// Streams a chapter without loading it entirely into memory
    /// </summary>
    public async IAsyncEnumerable<ReadOnlyMemory<char>> StreamChapterAsync(
        string chapterPath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var entry = _zipFile?.GetEntry(chapterPath);
        if (entry == null)
            throw new InvalidOperationException($"Chapter not found: {chapterPath}");

        using var stream = _zipFile.GetInputStream(entry);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var buffer = ArrayPool<char>.Shared.Rent(4096);
        try
        {
            int charsRead;
            while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                yield return buffer.AsMemory(0, charsRead);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Gets metadata about entries without reading them
    /// </summary>
    public IEnumerable<EpubEntryInfo> GetEntries()
    {
        if (_zipFile == null) yield break;

        foreach (ZipEntry entry in _zipFile)
        {
            yield return new EpubEntryInfo
            {
                Path = entry.Name,
                CompressedSize = entry.CompressedSize,
                UncompressedSize = entry.Size,
                LastModified = entry.DateTime,
                IsDirectory = entry.IsDirectory
            };
        }
    }

    /// <summary>
    /// Lazy-loads chapters as they're accessed
    /// </summary>
    public IAsyncEnumerable<Chapter> LoadChaptersLazyAsync(
        IEnumerable<string> chapterPaths,
        CancellationToken cancellationToken = default)
    {
        return LoadChaptersLazyInternalAsync(chapterPaths, cancellationToken);
    }

    private async IAsyncEnumerable<Chapter> LoadChaptersLazyInternalAsync(
        IEnumerable<string> chapterPaths,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int order = 0;
        foreach (var path in chapterPaths)
        {
            var content = await ReadEntryAsync(path, cancellationToken);

            // Extract title from content (simplified)
            var title = ExtractTitleFromHtml(content) ?? $"Chapter {order + 1}";

            yield return new Chapter(
                id: Path.GetFileNameWithoutExtension(path),
                title: title,
                content: content,
                order: order++,
                fileName: path
            );
        }
    }

    private string? ExtractTitleFromHtml(string html)
    {
        // Quick title extraction without full HTML parsing
        var titleStart = html.IndexOf("<title>", StringComparison.OrdinalIgnoreCase);
        if (titleStart == -1) return null;

        titleStart += 7;
        var titleEnd = html.IndexOf("</title>", titleStart, StringComparison.OrdinalIgnoreCase);
        if (titleEnd == -1) return null;

        return html[titleStart..titleEnd].Trim();
    }

    public void Dispose()
    {
        _zipFile?.Close();
        _memoryMappedFile?.Dispose();
        _cache.Clear();
    }
}

public sealed class EpubEntryInfo
{
    public required string Path { get; init; }
    public long CompressedSize { get; init; }
    public long UncompressedSize { get; init; }
    public DateTime LastModified { get; init; }
    public bool IsDirectory { get; init; }
}
```

#### Resource Caching Strategy

Create `Infrastructure/IO/EpubResourceCache.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Runtime.Caching;

namespace Alexandria.Infrastructure.IO;

/// <summary>
/// LRU cache for EPUB resources with size limits
/// </summary>
public sealed class EpubResourceCache
{
    private readonly MemoryCache _cache;
    private readonly long _maxSizeBytes;
    private long _currentSizeBytes;
    private readonly object _lock = new();

    public EpubResourceCache(long maxSizeBytes = 50 * 1024 * 1024) // 50MB default
    {
        _maxSizeBytes = maxSizeBytes;
        _cache = new MemoryCache("EpubResources");
    }

    public bool TryGet(string key, out byte[] data)
    {
        if (_cache.Get(key) is CachedResource resource)
        {
            data = resource.Data;
            resource.LastAccessed = DateTime.UtcNow;
            return true;
        }

        data = Array.Empty<byte>();
        return false;
    }

    public void Add(string key, byte[] data)
    {
        if (data.Length > _maxSizeBytes)
            return; // Don't cache items larger than max size

        lock (_lock)
        {
            // Evict items if necessary
            while (_currentSizeBytes + data.Length > _maxSizeBytes)
            {
                EvictOldest();
            }

            var resource = new CachedResource
            {
                Data = data,
                Size = data.Length,
                LastAccessed = DateTime.UtcNow
            };

            var policy = new CacheItemPolicy
            {
                SlidingExpiration = TimeSpan.FromMinutes(10),
                RemovedCallback = OnItemRemoved
            };

            _cache.Set(key, resource, policy);
            _currentSizeBytes += data.Length;
        }
    }

    private void EvictOldest()
    {
        // Find and remove least recently used item
        CachedResource? oldest = null;
        string? oldestKey = null;

        foreach (var item in _cache)
        {
            if (item.Value is CachedResource resource)
            {
                if (oldest == null || resource.LastAccessed < oldest.LastAccessed)
                {
                    oldest = resource;
                    oldestKey = item.Key;
                }
            }
        }

        if (oldestKey != null)
        {
            _cache.Remove(oldestKey);
        }
    }

    private void OnItemRemoved(CacheEntryRemovedArguments args)
    {
        if (args.RemovedReason != CacheEntryRemovedReason.Removed)
        {
            if (args.CacheItem.Value is CachedResource resource)
            {
                Interlocked.Add(ref _currentSizeBytes, -resource.Size);
            }
        }
    }

    private class CachedResource
    {
        public required byte[] Data { get; init; }
        public required long Size { get; init; }
        public DateTime LastAccessed { get; set; }
    }
}
```

#### Performance Requirements

- Stream ZIP entries without full extraction
- Use memory-mapped files for EPUBs > 10MB
- Cache frequently accessed resources (cover, CSS)
- Lazy-load chapters on demand
- Support partial content reading

#### Acceptance Criteria

- [ ] Opens 100MB EPUB in < 500ms
- [ ] Memory usage < 50MB for 100MB EPUB
- [ ] Supports concurrent entry reading
- [ ] Handles corrupted ZIP gracefully
- [ ] Thread-safe caching implementation
- [ ] Proper resource disposal

---

## Phase 2: Vertical Slice Architecture - Core Features 📚

### 2.1 LoadBook Feature with Performance Patterns

#### Implementation Requirements

Create `Features/LoadBook/LoadBookHandler.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OneOf;
using OneOf.Types;

namespace Alexandria.Application.Features.LoadBook;

public record LoadBookCommand(string FilePath) : IRequest<LoadBookResult>;

[GenerateOneOf]
public partial class LoadBookResult : OneOfBase<Book, LoadBookError> { }

public record LoadBookError(string Message, Exception? Exception = null);

public sealed class LoadBookHandler : IRequestHandler<LoadBookCommand, LoadBookResult>
{
    private readonly IBookCache _cache;
    private readonly IEpubParser _parser;
    private readonly IContentAnalyzer _contentAnalyzer;
    private readonly ILogger<LoadBookHandler> _logger;
    private readonly IProgress<LoadProgress>? _progress;

    public LoadBookHandler(
        IBookCache cache,
        IEpubParser parser,
        IContentAnalyzer contentAnalyzer,
        ILogger<LoadBookHandler> logger,
        IProgress<LoadProgress>? progress = null)
    {
        _cache = cache;
        _parser = parser;
        _contentAnalyzer = contentAnalyzer;
        _logger = logger;
        _progress = progress;
    }

    public async Task<LoadBookResult> Handle(
        LoadBookCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Report progress
            _progress?.Report(new LoadProgress(0, "Checking cache..."));

            // Check cache first (ValueTask for efficiency)
            var cached = await _cache.TryGetAsync(request.FilePath, cancellationToken);
            if (cached != null)
            {
                _logger.LogDebug("Book loaded from cache: {Path}", request.FilePath);
                return cached;
            }

            // Load book with streaming
            _progress?.Report(new LoadProgress(10, "Opening EPUB file..."));

            using var reader = new StreamingEpubReader(request.FilePath);

            // Parse metadata
            _progress?.Report(new LoadProgress(20, "Reading metadata..."));
            var metadata = await _parser.ParseMetadataAsync(reader, cancellationToken);

            // Load chapters lazily
            _progress?.Report(new LoadProgress(40, "Loading chapters..."));
            var chapters = new List<Chapter>();

            await foreach (var chapter in reader.LoadChaptersLazyAsync(
                metadata.SpineItems.Select(s => s.Href), cancellationToken))
            {
                chapters.Add(chapter);

                var progress = 40 + (chapters.Count * 40 / metadata.SpineItems.Count);
                _progress?.Report(new LoadProgress(
                    progress,
                    $"Loading chapter {chapters.Count}/{metadata.SpineItems.Count}..."));
            }

            // Analyze content in parallel
            _progress?.Report(new LoadProgress(80, "Analyzing content..."));

            var analysisTask = chapters
                .AsParallel()
                .WithCancellation(cancellationToken)
                .Select(async c =>
                {
                    var metrics = await _contentAnalyzer.AnalyzeContentAsync(
                        c.Content, cancellationToken);
                    c.SetMetrics(metrics);
                    return c;
                })
                .Select(t => t.Result);

            // Create book
            var book = new Book(
                title: metadata.Title,
                authors: metadata.Authors,
                chapters: chapters,
                metadata: metadata
            );

            // Cache the result
            _progress?.Report(new LoadProgress(95, "Caching..."));
            await _cache.SetAsync(request.FilePath, book, cancellationToken);

            _progress?.Report(new LoadProgress(100, "Complete"));

            return book;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Book loading cancelled: {Path}", request.FilePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load book: {Path}", request.FilePath);
            return new LoadBookError($"Failed to load book: {ex.Message}", ex);
        }
    }
}

public record LoadProgress(int Percentage, string Message);
```

#### Book Cache Implementation

Create `Infrastructure/Caching/BookCache.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace Alexandria.Infrastructure.Caching;

public interface IBookCache
{
    ValueTask<Book?> TryGetAsync(string path, CancellationToken cancellationToken = default);
    ValueTask SetAsync(string path, Book book, CancellationToken cancellationToken = default);
}

/// <summary>
/// High-performance book cache using ValueTask for sync path optimization
/// </summary>
public sealed class BookCache : IBookCache
{
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    public BookCache(IMemoryCache cache)
    {
        _cache = cache;
        _cacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(30),
            Size = 1, // Each book counts as 1 unit
            PostEvictionCallbacks =
            {
                new PostEvictionCallbackRegistration
                {
                    EvictionCallback = OnEviction,
                    State = this
                }
            }
        };
    }

    public ValueTask<Book?> TryGetAsync(string path, CancellationToken cancellationToken = default)
    {
        // Synchronous path - no allocation when cache hit
        if (_cache.TryGetValue<Book>(GetCacheKey(path), out var book))
        {
            return new ValueTask<Book?>(book);
        }

        // Async path only when cache miss
        return new ValueTask<Book?>((Book?)null);
    }

    public ValueTask SetAsync(string path, Book book, CancellationToken cancellationToken = default)
    {
        _cache.Set(GetCacheKey(path), book, _cacheOptions);
        return ValueTask.CompletedTask;
    }

    private static string GetCacheKey(string path) => $"book:{path}";

    private static void OnEviction(object key, object value, EvictionReason reason, object state)
    {
        if (value is Book book)
        {
            // Clean up resources if needed
            book.Dispose();
        }
    }
}
```

#### Performance Patterns Applied

- ✅ ValueTask for cache hits (no allocation)
- ✅ Lazy chapter loading with IAsyncEnumerable
- ✅ Parallel content analysis with PLINQ
- ✅ Progress reporting with IProgress<T>
- ✅ Proper cancellation token propagation
- ✅ OneOf pattern for error handling
- ✅ Memory cache with sliding expiration

#### Acceptance Criteria

- [ ] Loads 50MB EPUB in < 3 seconds
- [ ] Cache hit completes in < 1ms
- [ ] Memory usage scales linearly with book size
- [ ] Supports cancellation at any point
- [ ] Progress reporting is accurate
- [ ] Error messages are descriptive

---

### 2.3 SearchContent Feature with Lucene.NET

#### Implementation Requirements

Create `Features/SearchContent/SearchContentHandler.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using MediatR;

namespace Alexandria.Application.Features.SearchContent;

public record SearchContentQuery(
    string BookId,
    string SearchTerm,
    SearchOptions Options) : IRequest<SearchResults>;

public record SearchOptions(
    bool CaseSensitive = false,
    bool WholeWord = false,
    bool UseRegex = false,
    bool EnableFuzzy = false,
    float FuzzyThreshold = 0.8f,
    int MaxResults = 100);

public record SearchResults(
    IReadOnlyList<SearchResult> Results,
    int TotalHits,
    TimeSpan SearchTime,
    string? Suggestion = null);

public record SearchResult(
    string ChapterId,
    string ChapterTitle,
    float Score,
    string Highlight,
    int Position);

public sealed class SearchContentHandler : IRequestHandler<SearchContentQuery, SearchResults>
{
    private readonly ISearchIndexManager _indexManager;
    private readonly IFuzzySearchProvider _fuzzySearch;
    private readonly ILogger<SearchContentHandler> _logger;

    public SearchContentHandler(
        ISearchIndexManager indexManager,
        IFuzzySearchProvider fuzzySearch,
        ILogger<SearchContentHandler> logger)
    {
        _indexManager = indexManager;
        _fuzzySearch = fuzzySearch;
        _logger = logger;
    }

    public async Task<SearchResults> Handle(
        SearchContentQuery request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Get or build index
            var index = await _indexManager.GetOrBuildIndexAsync(
                request.BookId, cancellationToken);

            // Perform search
            var results = request.Options.EnableFuzzy
                ? await SearchWithFuzzyAsync(index, request, cancellationToken)
                : await SearchExactAsync(index, request, cancellationToken);

            stopwatch.Stop();

            return new SearchResults(
                results,
                results.Count,
                stopwatch.Elapsed,
                GenerateSuggestion(request.SearchTerm, results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for book {BookId}", request.BookId);
            throw;
        }
    }

    private async Task<IReadOnlyList<SearchResult>> SearchExactAsync(
        ISearchIndex index,
        SearchContentQuery request,
        CancellationToken cancellationToken)
    {
        using var searcher = index.GetSearcher();

        // Build query
        var query = BuildLuceneQuery(request);

        // Execute search
        var hits = searcher.Search(query, request.Options.MaxResults);

        // Process results
        var results = new List<SearchResult>();
        foreach (var hit in hits.ScoreDocs)
        {
            var doc = searcher.Doc(hit.Doc);
            var highlight = await GenerateHighlightAsync(
                doc, request.SearchTerm, cancellationToken);

            results.Add(new SearchResult(
                ChapterId: doc.Get("id"),
                ChapterTitle: doc.Get("title"),
                Score: hit.Score,
                Highlight: highlight,
                Position: int.Parse(doc.Get("position"))
            ));
        }

        return results;
    }

    private async Task<IReadOnlyList<SearchResult>> SearchWithFuzzyAsync(
        ISearchIndex index,
        SearchContentQuery request,
        CancellationToken cancellationToken)
    {
        // Use fuzzy search provider for typo-tolerant searching
        var fuzzyResults = await _fuzzySearch.SearchAsync(
            index,
            request.SearchTerm,
            request.Options.FuzzyThreshold,
            cancellationToken);

        return fuzzyResults;
    }

    private Query BuildLuceneQuery(SearchContentQuery request)
    {
        var analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
        var parser = new QueryParser(LuceneVersion.LUCENE_48, "content", analyzer);

        // Build query string
        var queryStr = request.SearchTerm;

        if (request.Options.WholeWord)
        {
            queryStr = $"\"{queryStr}\""; // Phrase query for whole word
        }

        if (request.Options.UseRegex)
        {
            // Convert to Lucene regex syntax
            queryStr = $"/{queryStr}/";
        }

        return parser.Parse(queryStr);
    }

    private async Task<string> GenerateHighlightAsync(
        Document doc,
        string searchTerm,
        CancellationToken cancellationToken)
    {
        // Use Lucene highlighter
        var highlighter = new Highlighter(
            new SimpleHTMLFormatter("<mark>", "</mark>"),
            new QueryScorer(BuildHighlightQuery(searchTerm)));

        var content = doc.Get("content");
        var tokenStream = new StandardAnalyzer(LuceneVersion.LUCENE_48)
            .GetTokenStream("content", new StringReader(content));

        var fragment = highlighter.GetBestFragment(tokenStream, content);
        return fragment ?? content.Substring(0, Math.Min(200, content.Length)) + "...";
    }

    private string? GenerateSuggestion(string searchTerm, IReadOnlyList<SearchResult> results)
    {
        if (results.Count > 0)
            return null;

        // Generate "Did you mean?" suggestion
        return _fuzzySearch.GetBestSuggestion(searchTerm);
    }
}
```

#### Search Index Manager

Create `Infrastructure/Search/SearchIndexManager.cs`:

```csharp
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace Alexandria.Infrastructure.Search;

public interface ISearchIndexManager
{
    Task<ISearchIndex> GetOrBuildIndexAsync(string bookId, CancellationToken cancellationToken);
    Task InvalidateIndexAsync(string bookId);
}

public sealed class SearchIndexManager : ISearchIndexManager, IDisposable
{
    private readonly ConcurrentDictionary<string, SearchIndex> _indexes = new();
    private readonly IBookRepository _bookRepository;
    private readonly IContentAnalyzer _contentAnalyzer;

    public SearchIndexManager(
        IBookRepository bookRepository,
        IContentAnalyzer contentAnalyzer)
    {
        _bookRepository = bookRepository;
        _contentAnalyzer = contentAnalyzer;
    }

    public async Task<ISearchIndex> GetOrBuildIndexAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        return await _indexes.GetOrAddAsync(bookId,
            async (key, ct) => await BuildIndexAsync(key, ct),
            cancellationToken);
    }

    private async Task<SearchIndex> BuildIndexAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        var book = await _bookRepository.GetByIdAsync(bookId, cancellationToken);
        if (book == null)
            throw new InvalidOperationException($"Book not found: {bookId}");

        // Create in-memory index
        var directory = new RAMDirectory();
        var analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
        var config = new IndexWriterConfig(LuceneVersion.LUCENE_48, analyzer)
        {
            OpenMode = OpenMode.CREATE
        };

        using (var writer = new IndexWriter(directory, config))
        {
            // Index each chapter
            foreach (var chapter in book.Chapters)
            {
                await IndexChapterAsync(writer, chapter, cancellationToken);
            }

            writer.Commit();
        }

        return new SearchIndex(directory, analyzer);
    }

    private async Task IndexChapterAsync(
        IndexWriter writer,
        Chapter chapter,
        CancellationToken cancellationToken)
    {
        // Extract plain text
        var plainText = _contentAnalyzer.ExtractPlainText(
            chapter.Content.AsSpan());

        // Create document
        var doc = new Document
        {
            new StringField("id", chapter.Id, Field.Store.YES),
            new StringField("title", chapter.Title, Field.Store.YES),
            new TextField("content", plainText, Field.Store.YES),
            new Int32Field("position", chapter.Order, Field.Store.YES),
            new Int32Field("wordCount",
                _contentAnalyzer.CountWords(plainText.AsSpan()),
                Field.Store.YES)
        };

        // Index sentences for better highlighting
        var sentences = _contentAnalyzer.ExtractSentences(
            plainText.AsSpan(), 100);

        for (int i = 0; i < sentences.Length; i++)
        {
            doc.Add(new TextField($"sentence_{i}", sentences[i], Field.Store.YES));
        }

        writer.AddDocument(doc);
    }

    public Task InvalidateIndexAsync(string bookId)
    {
        _indexes.TryRemove(bookId, out var index);
        index?.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var index in _indexes.Values)
        {
            index.Dispose();
        }
        _indexes.Clear();
    }
}

public interface ISearchIndex : IDisposable
{
    IndexSearcher GetSearcher();
}

public sealed class SearchIndex : ISearchIndex
{
    private readonly Directory _directory;
    private readonly Analyzer _analyzer;
    private readonly DirectoryReader _reader;
    private readonly IndexSearcher _searcher;

    public SearchIndex(Directory directory, Analyzer analyzer)
    {
        _directory = directory;
        _analyzer = analyzer;
        _reader = DirectoryReader.Open(_directory);
        _searcher = new IndexSearcher(_reader);
    }

    public IndexSearcher GetSearcher() => _searcher;

    public void Dispose()
    {
        _reader?.Dispose();
        _analyzer?.Dispose();
        _directory?.Dispose();
    }
}
```

#### Fuzzy Search Provider

Create `Infrastructure/Search/FuzzySearchProvider.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fastenshtein;

namespace Alexandria.Infrastructure.Search;

public interface IFuzzySearchProvider
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        ISearchIndex index,
        string searchTerm,
        float threshold,
        CancellationToken cancellationToken);

    string? GetBestSuggestion(string searchTerm);
}

public sealed class FuzzySearchProvider : IFuzzySearchProvider
{
    private readonly Dictionary<string, List<string>> _wordIndex = new();
    private readonly Levenshtein _levenshtein = new();

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        ISearchIndex index,
        string searchTerm,
        float threshold,
        CancellationToken cancellationToken)
    {
        var results = new List<SearchResult>();
        var searcher = index.GetSearcher();

        // Find similar terms
        var similarTerms = FindSimilarTerms(searchTerm, threshold);

        // Search for each similar term
        foreach (var term in similarTerms)
        {
            var query = new TermQuery(new Term("content", term));
            var hits = searcher.Search(query, 10);

            foreach (var hit in hits.ScoreDocs)
            {
                var doc = searcher.Doc(hit.Doc);

                // Adjust score based on similarity
                var similarity = CalculateSimilarity(searchTerm, term);
                var adjustedScore = hit.Score * similarity;

                results.Add(new SearchResult(
                    ChapterId: doc.Get("id"),
                    ChapterTitle: doc.Get("title"),
                    Score: adjustedScore,
                    Highlight: GenerateFuzzyHighlight(doc.Get("content"), term),
                    Position: int.Parse(doc.Get("position"))
                ));
            }
        }

        return results.OrderByDescending(r => r.Score)
                     .Take(100)
                     .ToList();
    }

    private List<string> FindSimilarTerms(string searchTerm, float threshold)
    {
        var similar = new List<string>();
        var maxDistance = (int)(searchTerm.Length * (1 - threshold));

        foreach (var word in _wordIndex.Keys)
        {
            var distance = _levenshtein.Distance(searchTerm.ToLower(), word.ToLower());
            if (distance <= maxDistance)
            {
                similar.Add(word);
            }
        }

        return similar;
    }

    private float CalculateSimilarity(string term1, string term2)
    {
        var distance = _levenshtein.Distance(term1.ToLower(), term2.ToLower());
        var maxLength = Math.Max(term1.Length, term2.Length);
        return 1f - ((float)distance / maxLength);
    }

    private string GenerateFuzzyHighlight(string content, string term)
    {
        var index = content.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (index == -1)
            return content.Substring(0, Math.Min(200, content.Length)) + "...";

        var start = Math.Max(0, index - 50);
        var end = Math.Min(content.Length, index + term.Length + 50);

        var highlight = content.Substring(start, end - start);
        return $"...{highlight}...";
    }

    public string? GetBestSuggestion(string searchTerm)
    {
        var candidates = FindSimilarTerms(searchTerm, 0.7f);

        if (candidates.Count == 0)
            return null;

        // Return most frequent similar term
        return candidates
            .OrderByDescending(c => _wordIndex[c].Count)
            .ThenBy(c => _levenshtein.Distance(searchTerm, c))
            .FirstOrDefault();
    }
}
```

#### Performance Patterns Applied

- ✅ Lucene.NET for inverted index
- ✅ In-memory index with RAMDirectory
- ✅ Fuzzy search with Levenshtein distance
- ✅ Concurrent index management
- ✅ Search result highlighting
- ✅ "Did you mean?" suggestions

#### Acceptance Criteria

- [ ] Searches 100MB book in < 100ms
- [ ] Fuzzy search finds words with 1-2 typos
- [ ] Highlights show search terms in context
- [ ] Index builds in < 5 seconds for 500 chapters
- [ ] Supports concurrent searches
- [ ] Memory usage < 100MB for index

---

## Performance Testing Framework

### BenchmarkDotNet Configuration

Create `Alexandria.Benchmarks/Config/BenchmarkConfig.cs`:

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace Alexandria.Benchmarks.Config;

[Config(typeof(AntiVirusFriendlyConfig))]
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public abstract class BenchmarkBase
{
    // Base configuration for all benchmarks
}

public class AntiVirusFriendlyConfig : ManualConfig
{
    public AntiVirusFriendlyConfig()
    {
        AddJob(Job.Default
            .WithToolchain(InProcessNoEmitToolchain.Instance));

        AddDiagnoser(MemoryDiagnoser.Default);
        AddDiagnoser(ThreadingDiagnoser.Default);

        WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }
}
```

### Performance Regression Tests

Create `Alexandria.Tests/Performance/PerformanceTests.cs`:

```csharp
[TestFixture]
public class PerformanceRegressionTests
{
    [Test]
    [Timeout(1000)] // 1 second timeout
    public async Task ContentAnalyzer_Should_Process_1MB_Under_100ms()
    {
        // Arrange
        var analyzer = new AngleSharpContentAnalyzer();
        var html = GenerateHtml(1024 * 1024); // 1MB

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = analyzer.ExtractPlainText(html.AsSpan());
        stopwatch.Stop();

        // Assert
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(100));
    }

    [Test]
    public async Task LoadBook_Should_Not_Allocate_On_CacheHit()
    {
        // Arrange
        var cache = new BookCache(new MemoryCache(new MemoryCacheOptions()));
        var book = CreateTestBook();
        await cache.SetAsync("test.epub", book);

        // Act & Assert
        var before = GC.GetTotalAllocatedBytes();
        var cached = await cache.TryGetAsync("test.epub");
        var after = GC.GetTotalAllocatedBytes();

        var allocated = after - before;
        Assert.That(allocated, Is.LessThan(1000)); // Less than 1KB allocated
    }
}
```

### Memory Profiling Requirements

```yaml
# .github/workflows/performance.yml
name: Performance Tests

on:
  pull_request:
    paths:
      - 'src/**'
      - 'tests/**'

jobs:
  benchmark:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Run Benchmarks
        run: |
          dotnet run -c Release --project Alexandria.Benchmarks -- \
            --filter "*" --memoryrandomization \
            --artifacts ./artifacts

      - name: Check Performance Regression
        run: |
          dotnet tool install -g dotnet-benchmark-compare
          benchmark-compare ./artifacts/previous ./artifacts/current \
            --threshold 5 --fail-on-regression
```

---

## Migration Checklist

### Phase 1: Regex to AngleSharp Migration

#### Pre-Migration Checklist
- [ ] Backup existing ContentProcessor implementation
- [ ] Document current regex patterns and their purposes
- [ ] Create comprehensive test suite for content extraction
- [ ] Benchmark current performance

#### Migration Steps

1. **Create Parallel Implementation**
   ```csharp
   public interface IContentProcessor
   {
       string ExtractPlainText(string html);
   }

   public class LegacyContentProcessor : IContentProcessor { }
   public class AngleSharpContentProcessor : IContentProcessor { }
   ```

2. **Add Feature Toggle**
   ```csharp
   public class ContentProcessorFactory
   {
       public IContentProcessor Create(bool useLegacy = false)
       {
           return useLegacy
               ? new LegacyContentProcessor()
               : new AngleSharpContentProcessor();
       }
   }
   ```

3. **Parallel Testing**
   ```csharp
   [Test]
   public void CompareProcessors()
   {
       var legacy = new LegacyContentProcessor();
       var modern = new AngleSharpContentProcessor();

       var result1 = legacy.ExtractPlainText(html);
       var result2 = modern.ExtractPlainText(html);

       Assert.That(result2, Is.EqualTo(result1).Within(5).Percent);
   }
   ```

4. **Gradual Rollout**
   - Week 1: 10% of requests use new processor
   - Week 2: 50% if metrics are good
   - Week 3: 100% with ability to rollback

#### Post-Migration Validation
- [ ] Performance metrics match or exceed baseline
- [ ] Memory usage reduced by at least 20%
- [ ] All unit tests pass
- [ ] No customer-reported issues

### Data Migration for Metadata

#### Migration Script
```csharp
public class MetadataMigration
{
    public async Task MigrateAsync()
    {
        // 1. Read old format
        var oldBooks = await ReadOldFormatAsync();

        // 2. Transform to new format
        var newBooks = oldBooks.Select(TransformBook);

        // 3. Validate
        foreach (var book in newBooks)
        {
            ValidateBook(book);
        }

        // 4. Write new format
        await WriteNewFormatAsync(newBooks);

        // 5. Verify
        await VerifyMigrationAsync();
    }
}
```

---

## Code Review Guidelines

### Performance Checklist for PRs

#### Required for All PRs
- [ ] No regex for simple text operations
- [ ] Spans used for string manipulation
- [ ] ArrayPool used for buffers > 4KB
- [ ] No unnecessary allocations in hot paths
- [ ] Cancellation tokens properly propagated
- [ ] ValueTask used for cache scenarios

#### Required for Feature PRs
- [ ] Benchmark results included
- [ ] Memory profiler output attached
- [ ] Performance tests added
- [ ] Load test results (if applicable)

#### Review Template
```markdown
## Performance Review

### Allocations
- Heap allocations: [X KB/operation]
- GC pressure: [Low/Medium/High]

### Benchmarks
- Baseline: [X ms]
- This PR: [Y ms]
- Change: [+/-Z%]

### Memory Profile
- Peak memory: [X MB]
- Steady state: [Y MB]

### Concerns
- [ ] None
- [ ] [Describe concerns]
```

---

## Success Metrics

### Performance Targets

| Operation | Target | Maximum |
|-----------|--------|---------|
| Load 10MB EPUB | < 2s | 3s |
| Search 100MB book | < 100ms | 200ms |
| Extract plain text (1MB HTML) | < 100ms | 150ms |
| Count words (100KB text) | < 10ms | 20ms |
| Cache hit | < 1ms | 5ms |
| Index build (100 chapters) | < 5s | 10s |

### Memory Targets

| Scenario | Target | Maximum |
|----------|--------|---------|
| Idle memory | < 50MB | 100MB |
| 10MB EPUB loaded | < 100MB | 150MB |
| 100MB EPUB loaded | < 200MB | 300MB |
| Search index (500 chapters) | < 100MB | 150MB |

### Quality Gates

- Unit test coverage > 80%
- Performance test coverage > 60%
- Zero critical security vulnerabilities
- No memory leaks detected
- Benchmark regression < 5%

---

## Conclusion

This enhanced development plan provides:

1. **Concrete Implementation Details** - Actual code templates developers can use
2. **Performance Patterns** - Specific techniques from the performance guide
3. **Acceptance Criteria** - Measurable targets for each component
4. **Testing Requirements** - Comprehensive test scenarios
5. **Migration Strategy** - Safe path from old to new implementation

Following this plan ensures consistent, high-performance implementation across the team while maintaining architectural principles.