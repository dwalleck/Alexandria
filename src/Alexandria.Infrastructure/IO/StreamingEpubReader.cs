using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alexandria.Domain.Entities;
using Alexandria.Domain.ValueObjects;
using ICSharpCode.SharpZipLib.Zip;

namespace Alexandria.Infrastructure.IO;

/// <summary>
/// High-performance streaming EPUB reader with minimal memory footprint.
/// </summary>
public sealed class StreamingEpubReader : IDisposable, IAsyncDisposable
{
    private const int DefaultBufferSize = 4096;
    private const int LargeFileThreshold = 50 * 1024 * 1024; // 50MB
    private const int MaxWorkingSetSize = 10 * 1024 * 1024; // 10MB
    
    private readonly string _filePath;
    private readonly ArrayPool<byte> _bytePool;
    private readonly ArrayPool<char> _charPool;
    private MemoryMappedFile? _memoryMappedFile;
    private MemoryMappedViewStream? _memoryMappedStream;
    private ZipInputStream? _zipStream;
    private bool _disposed;

    /// <summary>
    /// Gets whether the EPUB file is large enough to use memory mapping.
    /// </summary>
    public bool UsesMemoryMapping { get; private set; }

    /// <summary>
    /// Gets the size of the EPUB file in bytes.
    /// </summary>
    public long FileSize { get; private set; }

    public StreamingEpubReader(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _bytePool = ArrayPool<byte>.Shared;
        _charPool = ArrayPool<char>.Shared;
        
        if (!File.Exists(_filePath))
            throw new FileNotFoundException($"EPUB file not found: {_filePath}");
    }

    /// <summary>
    /// Opens the EPUB file for reading, using memory mapping for large files.
    /// </summary>
    public async Task<EpubMetadata> OpenAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(StreamingEpubReader));

        var fileInfo = new FileInfo(_filePath);
        FileSize = fileInfo.Length;
        UsesMemoryMapping = FileSize > LargeFileThreshold;

        if (UsesMemoryMapping)
        {
            await OpenWithMemoryMappingAsync(cancellationToken);
        }
        else
        {
            await OpenWithStreamAsync(cancellationToken);
        }

        // Read container.xml to find the OPF file
        var opfPath = await ReadContainerAsync(cancellationToken);
        
        // Read metadata from OPF
        return await ReadMetadataAsync(opfPath, cancellationToken);
    }

    /// <summary>
    /// Streams chapters lazily using IAsyncEnumerable for minimal memory usage.
    /// </summary>
    public async IAsyncEnumerable<Chapter> StreamChaptersAsync(
        string opfPath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(StreamingEpubReader));

        // Parse the OPF to get the spine
        var spine = await ReadSpineAsync(opfPath, cancellationToken);
        var manifest = await ReadManifestAsync(opfPath, cancellationToken);
        
        int chapterNumber = 1;
        
        foreach (var itemRef in spine)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            if (manifest.TryGetValue(itemRef, out var href))
            {
                var content = await ReadChapterContentAsync(href, cancellationToken);
                
                yield return new Chapter(
                    id: Guid.NewGuid().ToString(),
                    title: $"Chapter {chapterNumber}", // Can be extracted from nav.xhtml
                    content: content,
                    order: chapterNumber++,
                    href: href
                );
            }
        }
    }

    /// <summary>
    /// Reads a single chapter content using efficient streaming.
    /// </summary>
    public async Task<string> ReadChapterContentAsync(
        string href,
        CancellationToken cancellationToken = default)
    {
        // Recreate stream since ZipInputStream doesn't support seeking
        await ReopenZipStreamAsync(cancellationToken);
        
        ZipEntry? entry;
        while ((entry = _zipStream.GetNextEntry()) != null)
        {
            if (entry.Name.EndsWith(href, StringComparison.OrdinalIgnoreCase))
            {
                return await ReadEntryContentAsync(entry, cancellationToken);
            }
        }

        throw new FileNotFoundException($"Chapter not found: {href}");
    }

    /// <summary>
    /// Extracts resources on-demand with efficient buffering.
    /// </summary>
    public async Task<ReadOnlyMemory<byte>> ExtractResourceAsync(
        string resourcePath,
        CancellationToken cancellationToken = default)
    {
        // Recreate stream since ZipInputStream doesn't support seeking
        await ReopenZipStreamAsync(cancellationToken);
        
        ZipEntry? entry;
        while ((entry = _zipStream.GetNextEntry()) != null)
        {
            if (entry.Name.EndsWith(resourcePath, StringComparison.OrdinalIgnoreCase))
            {
                var buffer = new byte[entry.Size];
                var totalRead = 0;
                
                while (totalRead < buffer.Length)
                {
                    var read = await _zipStream.ReadAsync(
                        buffer.AsMemory(totalRead, buffer.Length - totalRead),
                        cancellationToken);
                    
                    if (read == 0)
                        break;
                    
                    totalRead += read;
                }
                
                return new ReadOnlyMemory<byte>(buffer, 0, totalRead);
            }
        }

        throw new FileNotFoundException($"Resource not found: {resourcePath}");
    }

    private async Task ReopenZipStreamAsync(CancellationToken cancellationToken)
    {
        // Dispose existing stream
        _zipStream?.Dispose();

        if (UsesMemoryMapping && _memoryMappedFile != null)
        {
            _memoryMappedStream?.Dispose();
            _memoryMappedStream = _memoryMappedFile.CreateViewStream(
                0, 0, MemoryMappedFileAccess.Read);
            _zipStream = new ZipInputStream(_memoryMappedStream);
        }
        else
        {
            // For non-memory-mapped files, create a new file stream each time
            // This avoids issues with seeking in already-compressed streams
            var newFileStream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: DefaultBufferSize,
                useAsync: true);
            _zipStream = new ZipInputStream(newFileStream);
        }

        await Task.CompletedTask;
    }

    private async Task OpenWithMemoryMappingAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // Create memory-mapped file directly from path
            // This avoids issues with FileStream ownership
            _memoryMappedFile = MemoryMappedFile.CreateFromFile(
                _filePath,
                FileMode.Open,
                mapName: null,
                capacity: 0,
                access: MemoryMappedFileAccess.Read);

            _memoryMappedStream = _memoryMappedFile.CreateViewStream(
                0, 0, MemoryMappedFileAccess.Read);

            _zipStream = new ZipInputStream(_memoryMappedStream);
        }, cancellationToken);
    }

    private async Task OpenWithStreamAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            var fileStream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: DefaultBufferSize,
                useAsync: true);

            _zipStream = new ZipInputStream(fileStream);
        }, cancellationToken);
    }

    private async Task<string> ReadContainerAsync(CancellationToken cancellationToken)
    {
        if (_zipStream == null)
            throw new InvalidOperationException("EPUB file is not open");

        ZipEntry? entry;
        while ((entry = _zipStream.GetNextEntry()) != null)
        {
            if (entry.Name.Equals("META-INF/container.xml", StringComparison.OrdinalIgnoreCase))
            {
                var content = await ReadEntryContentAsync(entry, cancellationToken);
                var doc = XDocument.Parse(content);
                
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                var rootfile = doc.Root?
                    .Element(ns + "rootfiles")?
                    .Element(ns + "rootfile")?
                    .Attribute("full-path")?.Value;
                
                return rootfile ?? throw new InvalidOperationException("OPF path not found in container.xml");
            }
        }

        throw new FileNotFoundException("container.xml not found in EPUB");
    }

    private async Task<EpubMetadata> ReadMetadataAsync(string opfPath, CancellationToken cancellationToken)
    {
        // Recreate stream since ZipInputStream doesn't support seeking
        await ReopenZipStreamAsync(cancellationToken);
        
        ZipEntry? entry;
        while ((entry = _zipStream.GetNextEntry()) != null)
        {
            if (entry.Name.EndsWith(opfPath, StringComparison.OrdinalIgnoreCase))
            {
                var content = await ReadEntryContentAsync(entry, cancellationToken);
                var doc = XDocument.Parse(content);
                
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                var metadata = doc.Root?.Element(ns + "metadata");
                
                if (metadata == null)
                    throw new InvalidOperationException("Metadata not found in OPF");

                // Extract metadata fields
                var dcNs = XNamespace.Get("http://purl.org/dc/elements/1.1/");
                
                return new EpubMetadata
                {
                    Title = metadata.Element(dcNs + "title")?.Value ?? "Unknown",
                    Creator = metadata.Element(dcNs + "creator")?.Value ?? "Unknown",
                    Language = metadata.Element(dcNs + "language")?.Value ?? "en",
                    Publisher = metadata.Element(dcNs + "publisher")?.Value,
                    Date = metadata.Element(dcNs + "date")?.Value,
                    Description = metadata.Element(dcNs + "description")?.Value,
                    Subject = metadata.Element(dcNs + "subject")?.Value,
                    Identifier = metadata.Element(dcNs + "identifier")?.Value
                };
            }
        }

        throw new FileNotFoundException($"OPF file not found: {opfPath}");
    }

    private async Task<List<string>> ReadSpineAsync(string opfPath, CancellationToken cancellationToken)
    {
        // Recreate stream since ZipInputStream doesn't support seeking
        await ReopenZipStreamAsync(cancellationToken);
        
        var spine = new List<string>();
        
        ZipEntry? entry;
        while ((entry = _zipStream.GetNextEntry()) != null)
        {
            if (entry.Name.EndsWith(opfPath, StringComparison.OrdinalIgnoreCase))
            {
                var content = await ReadEntryContentAsync(entry, cancellationToken);
                var doc = XDocument.Parse(content);
                
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                var spineElement = doc.Root?.Element(ns + "spine");
                
                if (spineElement != null)
                {
                    foreach (var itemRef in spineElement.Elements(ns + "itemref"))
                    {
                        var idref = itemRef.Attribute("idref")?.Value;
                        if (!string.IsNullOrEmpty(idref))
                            spine.Add(idref);
                    }
                }
                
                break;
            }
        }
        
        return spine;
    }

    private async Task<Dictionary<string, string>> ReadManifestAsync(
        string opfPath,
        CancellationToken cancellationToken)
    {
        // Recreate stream since ZipInputStream doesn't support seeking
        await ReopenZipStreamAsync(cancellationToken);
        
        var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        ZipEntry? entry;
        while ((entry = _zipStream.GetNextEntry()) != null)
        {
            if (entry.Name.EndsWith(opfPath, StringComparison.OrdinalIgnoreCase))
            {
                var content = await ReadEntryContentAsync(entry, cancellationToken);
                var doc = XDocument.Parse(content);
                
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                var manifestElement = doc.Root?.Element(ns + "manifest");
                
                if (manifestElement != null)
                {
                    foreach (var item in manifestElement.Elements(ns + "item"))
                    {
                        var id = item.Attribute("id")?.Value;
                        var href = item.Attribute("href")?.Value;
                        
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(href))
                            manifest[id] = href;
                    }
                }
                
                break;
            }
        }
        
        return manifest;
    }

    private async Task<string> ReadEntryContentAsync(ZipEntry entry, CancellationToken cancellationToken)
    {
        // For small entries, read directly into a rented buffer
        if (entry.Size < DefaultBufferSize * 2)
        {
            var buffer = _bytePool.Rent((int)entry.Size);
            try
            {
                int totalRead = 0;
                while (totalRead < entry.Size)
                {
                    var bytesRead = await _zipStream.ReadAsync(
                        buffer.AsMemory(totalRead, (int)(entry.Size - totalRead)),
                        cancellationToken);

                    if (bytesRead == 0)
                        break;

                    totalRead += bytesRead;
                }

                return Encoding.UTF8.GetString(buffer, 0, totalRead);
            }
            finally
            {
                _bytePool.Return(buffer, clearArray: true);
            }
        }

        // For larger entries, use streaming with StringBuilder
        var sb = new StringBuilder((int)Math.Min(entry.Size, int.MaxValue));
        var byteBuffer = _bytePool.Rent(DefaultBufferSize);
        var charBuffer = _charPool.Rent(DefaultBufferSize);

        try
        {
            var decoder = Encoding.UTF8.GetDecoder();
            int bytesRead;

            while ((bytesRead = await _zipStream.ReadAsync(
                byteBuffer.AsMemory(0, byteBuffer.Length),
                cancellationToken)) > 0)
            {
                int charsUsed = decoder.GetChars(byteBuffer, 0, bytesRead, charBuffer, 0);
                sb.Append(charBuffer, 0, charsUsed);
            }

            return sb.ToString();
        }
        finally
        {
            _bytePool.Return(byteBuffer, clearArray: true);
            _charPool.Return(charBuffer, clearArray: true);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _zipStream?.Dispose();
        _memoryMappedStream?.Dispose();
        _memoryMappedFile?.Dispose();
        // Note: _fileStream removed as we either use memory mapping or create streams on demand

        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        if (_zipStream != null)
            _zipStream.Dispose();

        if (_memoryMappedStream != null)
            await _memoryMappedStream.DisposeAsync();

        _memoryMappedFile?.Dispose();
        // Note: _fileStream removed as we either use memory mapping or create streams on demand

        _disposed = true;
    }
}

/// <summary>
/// EPUB metadata extracted from OPF file.
/// </summary>
public sealed class EpubMetadata
{
    public string Title { get; init; } = string.Empty;
    public string Creator { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public string? Publisher { get; init; }
    public string? Date { get; init; }
    public string? Description { get; init; }
    public string? Subject { get; init; }
    public string? Identifier { get; init; }
}