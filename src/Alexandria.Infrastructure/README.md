# Alexandria.Infrastructure

## Overview
The Infrastructure layer contains all external concerns and implementations for the Alexandria EPUB Parser. This includes file I/O, EPUB parsing, HTML processing, persistence, caching, and all third-party integrations.

## Structure

- **`/Persistence`** - Database access, repositories, and data persistence
- **`/FileSystem`** - File I/O operations, ZIP handling, and EPUB extraction
- **`/ContentProcessing`** - HTML/XHTML parsing, text analysis implementations
- **`/Caching`** - Memory caching and performance optimization

## Key Principles

1. **Dependency Inversion** - Implements interfaces defined in Domain and Application layers
2. **External Concerns** - All third-party libraries and external systems are accessed here
3. **No Business Logic** - Only technical implementation details
4. **Testable** - All implementations should be testable through their interfaces

## Key Implementations

### FileSystem
- `StreamingEpubReader` - Efficient EPUB file reading with streaming support
- `EpubResourceExtractor` - Extract images, fonts, and other resources
- `ZipArchiveHandler` - Handle ZIP operations using SharpZipLib

### ContentProcessing
- `AngleSharpContentAnalyzer` - Implements IContentAnalyzer using AngleSharp for HTML parsing
- `HtmlSanitizer` - Clean and sanitize untrusted HTML content
- `CssProcessor` - Parse and process CSS stylesheets using ExCSS

### Persistence
- `LiteDbBookRepository` - Store book metadata and reading progress
- `BookmarkRepository` - Manage bookmarks and annotations
- `ReadingSessionRepository` - Track reading statistics

### Caching
- `MemoryCacheService` - In-memory caching using Microsoft.Extensions.Caching.Memory
- `ContentCache` - Cache parsed chapter content
- `MetadataCache` - Cache book metadata

## Usage

```csharp
// Infrastructure services are registered in DI container
services.AddScoped<IContentAnalyzer, AngleSharpContentAnalyzer>();
services.AddScoped<IBookRepository, LiteDbBookRepository>();
services.AddSingleton<IMemoryCache, MemoryCache>();

// Concrete implementations
public class AngleSharpContentAnalyzer : IContentAnalyzer
{
    public async Task<ContentMetrics> AnalyzeChapter(Chapter chapter)
    {
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(chapter.Content));

        var text = document.Body.TextContent;
        var wordCount = CountWords(text);
        var readingTime = CalculateReadingTime(wordCount);

        return new ContentMetrics(wordCount, readingTime);
    }
}
```

## Dependencies

- Target Framework: .NET 9.0
- NuGet Packages:
  - **AngleSharp** - HTML/XHTML parsing
  - **SharpZipLib** - ZIP archive handling
  - **LiteDB** - Embedded NoSQL database
  - **Microsoft.Extensions.Caching.Memory** - Memory caching
  - **ExCSS** - CSS parsing
  - **HtmlSanitizer** - HTML sanitization
  - **System.IO.Pipelines** - High-performance I/O
  - **Microsoft.Extensions.Logging** - Logging abstractions

## Project References

- Alexandria.Domain - For domain entities and service interfaces
- Alexandria.Application - For application interfaces

## Performance Considerations

- Use `Span<T>` and `Memory<T>` for efficient text processing
- Implement streaming for large EPUB files
- Use `ArrayPool<T>` for temporary buffers
- Cache frequently accessed content
- See [C# Performance Guide](../../CSharp-Performance-Guide-EPUB-Parser.md) for detailed patterns

## Migration Notes

Infrastructure code is being migrated from Alexandria.Parser, including:
- File I/O operations from Parser.cs
- XML parsing logic
- ZIP handling code
- Any external library usage

All new infrastructure code should be added to this project, organized by concern.