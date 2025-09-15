# EPUB Loading Architecture

## Overview

This document defines the complete architecture for loading EPUB files in Alexandria, including decision criteria, component responsibilities, and integration patterns.

## Loading Strategies

### 1. Eager Loading (Default)

**When to Use:**

- File size < 10MB
- Full book analysis required (search, indexing)
- Sufficient available memory (>2x file size)

**Components:**

- LoadBookHandler → EpubLoader → AdaptiveEpubParser → Epub2/3Parser

**Characteristics:**

- Entire book loaded into memory
- All chapters immediately available
- Higher memory usage
- Faster subsequent operations

### 2. Lazy Loading (Medium Files)

**When to Use:**

- File size 10MB - 50MB
- Normal reading use case
- Memory constraints present

**Components:**

- LoadBookHandler → EpubLoader → AdaptiveEpubParser (with lazy chapter loading)

**Characteristics:**

- Metadata loaded immediately
- Chapters loaded on first access
- Cached after loading
- Balanced memory usage

### 3. Streaming (Large Files)

**When to Use:**

- File size > 50MB
- Limited memory available
- Sequential reading pattern

**Components:**

- LoadBookHandler → EpubLoader → StreamingEpubReader (via adapter)

**Characteristics:**

- Memory-mapped file access
- Chapters streamed on demand
- Minimal memory footprint
- Slower random access

## Decision Flow

```
LoadBookCommand received
    ↓
[Check File Size]
    ├─ < 10MB → Eager Loading
    ├─ 10-50MB → Check Available Memory
    │   ├─ Memory > 2x file size → Eager Loading
    │   └─ Memory constrained → Lazy Loading
    └─ > 50MB → Streaming
        ├─ Memory available → Optional: Lazy Loading
        └─ Memory constrained → Required: Streaming
```

## Component Responsibilities

### Domain Layer

#### IEpubLoader

- Defines contract for loading EPUBs from various sources
- Returns OneOf<Book, ParsingError>
- Strategy-agnostic interface

#### IEpubParser

- Defines contract for parsing EPUB streams
- Returns complete or partial Book entities
- Supports cancellation

#### ILoadingStrategy (NEW)

```csharp
public interface ILoadingStrategy
{
    bool CanLoad(long fileSize, long availableMemory);
    Task<OneOf<Book, ParsingError>> LoadAsync(
        string filePath,
        CancellationToken cancellationToken);
}
```

### Application Layer

#### LoadBookHandler

**Responsibilities:**

- Orchestrates loading process
- Validates input
- Manages caching
- Reports progress
- Delegates to IEpubLoader

**NOT Responsible For:**

- Choosing loading strategy (that's Infrastructure's job)
- Parsing logic
- File I/O

### Infrastructure Layer

#### EpubLoader

**Responsibilities:**

- Implements IEpubLoader
- **Makes strategic decision** on which loading approach to use
- Coordinates between different parsers/readers
- Handles file access

**Decision Logic:**

```csharp
public async Task<OneOf<Book, ParsingError>> LoadFromFileAsync(
    string filePath,
    CancellationToken cancellationToken)
{
    var fileInfo = new FileInfo(filePath);
    var availableMemory = GetAvailableMemory();

    // Streaming for large files
    if (fileInfo.Length > StreamingThreshold ||
        availableMemory < fileInfo.Length * 2)
    {
        return await LoadViaStreaming(filePath, cancellationToken);
    }

    // Lazy loading for medium files
    if (fileInfo.Length > LazyLoadingThreshold)
    {
        return await LoadViaLazyLoading(filePath, cancellationToken);
    }

    // Eager loading for small files
    return await LoadViaEagerLoading(filePath, cancellationToken);
}
```

#### StreamingEpubReaderAdapter (NEW)

**Purpose:** Adapts StreamingEpubReader to work with our domain model

```csharp
public sealed class StreamingEpubReaderAdapter : IEpubParser
{
    public async Task<OneOf<Book, ParsingError>> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        // Note: StreamingEpubReader needs file path, not stream
        // This adapter handles that mismatch
    }
}
```

## Domain Model Adaptations

### Supporting Partial Books

**Option 1: Lazy Properties**

```csharp
public sealed class Book
{
    private readonly Func<Task<string>>? _contentLoader;

    public string Content => _content ??
        throw new InvalidOperationException("Content not loaded");

    public async Task<string> GetContentAsync()
    {
        _content ??= await _contentLoader();
        return _content;
    }
}
```

**Option 2: Explicit Loading States**

```csharp
public sealed class ChapterContent
{
    public bool IsLoaded { get; }
    public string? Content { get; }
    public Func<Task<string>>? Loader { get; }
}
```

## Caching Strategy

### Multi-Tier Caching

1. **L1: Chapter Cache** - Recently accessed chapters (Memory)
2. **L2: Book Metadata Cache** - Book info without content (Memory)
3. **L3: Persistent Cache** - Serialized books (LiteDB)

### Cache Keys

- Metadata: `book:{filePath}:meta`
- Chapter: `book:{filePath}:chapter:{index}`
- Full Book: `book:{filePath}:full`

## Integration Plan for StreamingEpubReader

### Phase 1: Create Adapter

1. Create StreamingEpubReaderAdapter implementing IEpubParser
2. Handle file path vs. stream mismatch
3. Map EpubMetadata to our Book domain model

### Phase 2: Integrate into EpubLoader

1. Add file size checking logic
2. Add memory availability checking
3. Route large files to StreamingEpubReaderAdapter

### Phase 3: Update Domain Model

1. Add support for lazy-loaded chapters
2. Update Chapter entity to support deferred content loading
3. Ensure immutability is preserved

### Phase 4: Update Caching

1. Implement per-chapter caching
2. Update cache keys structure
3. Add cache coordination logic

## Error Handling

### Streaming-Specific Errors

- Memory mapping failures → Fall back to regular loading
- Partial read failures → Retry with smaller chunks
- Chapter corruption → Skip chapter, log error

## Performance Targets

| File Size | Strategy | Load Time | Memory Usage |
|-----------|----------|-----------|--------------|
| < 10MB | Eager | < 500ms | ~2x file size |
| 10-50MB | Lazy | < 1s metadata | ~30% file size |
| > 50MB | Streaming | < 500ms metadata | < 10MB |

## Migration Path

1. **Immediate**: Document current state (this document)
2. **Next Sprint**: Implement ILoadingStrategy interface
3. **Following Sprint**: Integrate StreamingEpubReader via adapter
4. **Future**: Add user preferences for loading strategy

## Open Questions

1. Should users be able to override the automatic strategy selection?
2. How do we handle EPUBs with very large individual chapters?
3. Should we pre-fetch the next chapter when streaming?
4. How do we handle search across a streamed book?

## Decision Log

- **2024-01-15**: Decided to use file size + available memory for strategy selection
- **2024-01-15**: Chose to implement adapter pattern for StreamingEpubReader
- **2024-01-15**: Deferred lazy loading implementation to focus on streaming first
