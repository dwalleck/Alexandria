# Alexandria EPUB Parser - Development Plan

## Overview

This document outlines the comprehensive development plan for migrating Alexandria EPUB Parser to a clean Vertical Slice Architecture with Domain-Driven Design principles, including the replacement of custom HTML parsing with AngleSharp.

## Development Phases

### Phase 0: Project Structure & Clean Architecture Setup 📁

**Goal**: Establish proper project structure following Clean Architecture principles

#### 0.1 Create Solution Structure

- [ ] Create `src/` directory structure
- [ ] Create new projects in `src/`:
  - [ ] `Alexandria.Domain` - Core domain entities, value objects, interfaces
    - Target: .NET Standard 2.1 (for maximum compatibility)
    - No external dependencies
  - [ ] `Alexandria.Application` - Use cases, feature handlers, DTOs
    - Target: .NET 8.0
    - Dependencies: Domain project, MediatR, FluentValidation
  - [ ] `Alexandria.Infrastructure` - External concerns (file I/O, parsing, persistence)
    - Target: .NET 8.0
    - Dependencies: Domain, Application projects
    - External packages: AngleSharp, SharpZipLib, LiteDB, etc.
- [ ] Update solution file to include new projects
- [ ] Add project references:
  - Application → Domain
  - Infrastructure → Domain, Application
  - Alexandria.Parser → All three (temporarily, for migration)

#### 0.2 Initial Project Configuration

- [ ] Configure each project with appropriate folder structure:
  - Domain: `/Entities`, `/ValueObjects`, `/Services`, `/Exceptions`
  - Application: `/Features`, `/Common`, `/Interfaces`
  - Infrastructure: `/Persistence`, `/FileSystem`, `/ContentProcessing`, `/Caching`
- [ ] Add shared NuGet packages:
  - [ ] Domain: No external packages (pure C#)
  - [ ] Application: MediatR, FluentValidation, AutoMapper
  - [ ] Infrastructure: AngleSharp, SharpZipLib, LiteDB, Microsoft.Extensions.Caching.Memory
- [ ] Set up proper namespaces following project names
- [ ] Create README.md in each project explaining its purpose

#### 0.3 Migrate Existing Code

- [ ] Move domain entities from `Alexandria.Parser/Domain/Entities` to `Alexandria.Domain/Entities`
- [ ] Move domain services interfaces to `Alexandria.Domain/Services`
- [ ] Move value objects to `Alexandria.Domain/ValueObjects`
- [ ] Move application logic (handlers, services) to `Alexandria.Application/Features`
- [ ] Move infrastructure code (file I/O, parsing) to `Alexandria.Infrastructure`
- [ ] Update all namespace references
- [ ] Ensure Alexandria.Parser still compiles (using new project references)

#### 0.4 Establish Dependency Rules

- [ ] Domain project has NO external dependencies
- [ ] Application project only depends on Domain
- [ ] Infrastructure depends on Domain and Application
- [ ] Create architecture tests to enforce these rules
- [ ] Document dependency flow in solution README

#### 0.5 Migrate Domain Layer Code

- [ ] Move entities from `Alexandria.Parser/Domain/Entities/` to `Alexandria.Domain/Entities/`
  - [ ] Book.cs
  - [ ] Chapter.cs
- [ ] Move value objects from `Alexandria.Parser/Domain/ValueObjects/` to `Alexandria.Domain/ValueObjects/`
  - [ ] Author.cs
  - [ ] BookIdentifier.cs
  - [ ] Bookmark.cs
  - [ ] BookMetadata.cs
  - [ ] BookTitle.cs
  - [ ] EpubResource.cs
  - [ ] ImageResource.cs
  - [ ] Language.cs
  - [ ] NavigationItem.cs
  - [ ] NavigationStructure.cs
  - [ ] ReadingProgress.cs
  - [ ] ReadingStatistics.cs
  - [ ] ResourceCollection.cs
- [ ] Move domain interfaces to `Alexandria.Domain/Services/`
  - [ ] IBookRepository.cs (from Domain/Interfaces)
  - [ ] IEpubParser.cs (from Domain/Interfaces)
  - [ ] IContentService.cs (from Domain/Services)
- [ ] Move domain exceptions to `Alexandria.Domain/Exceptions/`
  - [ ] EpubParsingException.cs
  - [ ] InvalidEpubStructureException.cs
- [ ] Move domain enums to `Alexandria.Domain/Enums/`
  - [ ] EpubVersion.cs
- [ ] Move domain errors to `Alexandria.Domain/Errors/`
  - [ ] ParsingErrors.cs
- [ ] Update namespaces in all moved files
- [ ] Fix any circular dependencies

#### 0.6 Migrate Application Layer Code

- [ ] Move use cases from `Alexandria.Parser/Application/UseCases/` to `Alexandria.Application/Features/`
  - [ ] LoadBook/LoadBookCommand.cs
  - [ ] LoadBook/LoadBookHandler.cs
- [ ] Move domain services that contain business logic to `Alexandria.Application/Services/`
  - [ ] BookmarkService.cs (from Domain/Services)
  - [ ] ContentProcessor.cs (from Domain/Services)
  - [ ] ContentService.cs (from Domain/Services)
  - [ ] SearchService.cs (from Domain/Services)
- [ ] Update namespaces in all moved files
- [ ] Add MediatR handlers for existing commands

#### 0.7 Migrate Infrastructure Layer Code

- [ ] Move parsers from `Alexandria.Parser/Infrastructure/Parsers/` to `Alexandria.Infrastructure/FileSystem/`
  - [ ] AdaptiveEpubParser.cs
  - [ ] Epub2Parser.cs
  - [ ] Epub3Parser.cs
  - [ ] EpubParserFactory.cs
  - [ ] EpubVersionDetector.cs
  - [ ] Models/Epub2/ContainerXml.cs
  - [ ] Models/Epub2/PackageXml.cs
  - [ ] Models/Epub3/ContainerXml.cs
  - [ ] Models/Epub3/PackageXml.cs
- [ ] Move repositories to `Alexandria.Infrastructure/Persistence/`
  - [ ] BookRepository.cs
- [ ] Move XML models from `Alexandria.Parser/Models/` to `Alexandria.Infrastructure/FileSystem/Models/`
  - [ ] Container.cs
  - [ ] Rootfile.cs
  - [ ] Content/Package.cs
  - [ ] Content/Metadata.cs
  - [ ] Content/ManifestItem.cs
  - [ ] Content/SpineItemRef.cs
  - [ ] Content/MetaItem.cs
  - [ ] Content/GuideReference.cs
  - [ ] MediaType.cs
- [ ] Move main EPUB reading logic to `Alexandria.Infrastructure/FileSystem/`
  - [ ] EpubReader.cs (main entry point)
- [ ] Move DI configuration to `Alexandria.Infrastructure/`
  - [ ] ServiceCollectionExtensions.cs
- [ ] Update namespaces in all moved files

#### 0.8 Create Test Projects Structure ✅

- [x] Create tests directory
- [x] Create Alexandria.Domain.Tests (TUnit, .NET 9.0)
- [x] Create Alexandria.Application.Tests (TUnit, .NET 9.0)
- [x] Create Alexandria.Infrastructure.Tests (TUnit, .NET 9.0)
- [x] Add test projects to solution
- [x] Configure test project references

#### 0.9 Migrate Test Code

- [ ] Identify tests in Alexandria.Parser.Tests that belong to Domain layer
  - [ ] Entity tests (Book, Chapter)
  - [ ] Value object tests
  - [ ] Domain service tests
- [ ] Move domain tests to Alexandria.Domain.Tests
- [ ] Identify tests that belong to Application layer
  - [ ] Use case tests
  - [ ] Application service tests
- [ ] Move application tests to Alexandria.Application.Tests
- [ ] Identify tests that belong to Infrastructure layer
  - [ ] Parser tests (Epub2Parser, Epub3Parser, etc.)
  - [ ] Repository tests
  - [ ] File I/O tests
- [ ] Move infrastructure tests to Alexandria.Infrastructure.Tests
- [ ] Update test namespaces
- [ ] Update test project references
- [ ] Ensure all migrated tests compile

#### 0.10 Clean Up and Verify Migration

- [ ] Remove empty directories from Alexandria.Parser
- [ ] Update Alexandria.Parser to use types from new projects
- [ ] Fix any broken references in test projects
- [ ] Run all tests and fix failures
- [ ] Remove obsolete Models/Book.cs from Alexandria.Parser
- [ ] Document any breaking changes
- [ ] Consider removing Alexandria.Parser project once migration is complete

### Phase 1: Foundation & Core Domain Cleanup 🏗️

**Goal**: Establish clean domain boundaries and eliminate duplication

#### 1.1 Domain Service Interfaces

- [ ] Create `Domain/Services/IContentAnalyzer.cs` interface
  - Text extraction methods with `ReadOnlySpan<char>` signatures
  - Word counting using span-based processing
  - Reading time estimation with configurable WPM
  - Sentence extraction for preview generation
  - Preview generation maintaining word boundaries
  - Async content analysis returning comprehensive metrics
  - **Performance Target**: Process 1MB HTML in <100ms
  - **Memory Target**: Zero heap allocations for text <4KB
- [ ] Create `Domain/ValueObjects/ContentMetrics.cs`
  - Word count, character counts (with/without spaces)
  - Sentence and paragraph counts
  - Estimated reading time
  - Average words per sentence
  - Readability score (Flesch Reading Ease)
  - Word frequency analysis
  - Reading difficulty calculation

#### 1.2 Core Package Integration

- [ ] Add essential NuGet packages to Alexandria.Parser
  - [ ] AngleSharp - HTML/XHTML parsing
  - [ ] SharpZipLib - Efficient ZIP/EPUB handling
  - [ ] LiteDB - Embedded database for metadata/progress
  - [ ] Microsoft.Extensions.Caching.Memory - Runtime caching
  - [ ] ExCSS - CSS parsing for stylesheets
  - [ ] HtmlSanitizer - Security for untrusted EPUBs
- [ ] Create `Infrastructure/ContentProcessing/AngleSharpContentAnalyzer.cs`
  - Implement IContentAnalyzer using AngleSharp HTML parser
  - Use `SearchValues<char>` for efficient character matching
  - Implement `ArrayPool<char>` for buffers >4KB
  - Stack allocate small buffers with `stackalloc`
  - Configure AngleSharp for streaming mode
  - Handle malformed HTML with error recovery
  - Implement caching for parsed documents
- [ ] Create comprehensive tests for AngleSharpContentAnalyzer
  - Unit tests with 100% coverage
  - Performance benchmarks using BenchmarkDotNet
  - Memory allocation tests
  - Malformed HTML handling tests

#### 1.3 Remove Duplicate Models

- [ ] Mark `Models/Book.cs` as obsolete
- [ ] Update all references to use `Domain/Entities/Book.cs`
- [ ] Remove legacy Models namespace entirely
- [ ] Update tests to use domain entities

#### 1.4 Efficient EPUB File Handling

- [ ] Replace System.IO.Compression with SharpZipLib
- [ ] Implement `Infrastructure/IO/StreamingEpubReader.cs`
  - [ ] Stream-based ZIP entry reading with `ZipInputStream`
  - [ ] Lazy loading using `IAsyncEnumerable<Chapter>`
  - [ ] Memory-mapped file support via `MemoryMappedFile` for >50MB EPUBs
  - [ ] Implement `ReadOnlySequence<byte>` for efficient buffering
  - [ ] Use `PipeReader` for streaming large content
  - [ ] **Performance Target**: Open 100MB EPUB in <500ms
  - [ ] **Memory Target**: Max 10MB working set for any EPUB size
- [ ] Create `Infrastructure/IO/EpubResourceExtractor.cs`
  - [ ] Extract resources on-demand using spans
  - [ ] Support partial extraction with byte ranges
  - [ ] Implement LRU cache with configurable size
  - [ ] Use `ValueTask` for cache hits

#### 1.5 Consolidate Content Processing

- [ ] Remove `Chapter.GetWordCount()` method
- [ ] Remove `Chapter.EstimateReadingTimeMinutes()` method
- [ ] Remove `Chapter.GetEstimatedReadingTime()` method
- [ ] Update `Book.GetTotalWordCount()` to use IContentAnalyzer
- [ ] Update `Book.GetEstimatedReadingTime()` to use IContentAnalyzer
- [ ] Refactor ContentProcessor to use AngleSharp internally
- [ ] Update ContentService to use IContentAnalyzer

### Phase 2: Vertical Slice Architecture - Core Features 📚

**Goal**: Implement core features using vertical slice pattern

#### 2.1 LoadBook Feature

- [ ] Create `Features/LoadBook/` folder structure
- [ ] Implement `LoadBookCommand.cs` with caching key generation
- [ ] Implement `LoadBookHandler.cs` using MediatR
  - [ ] Use `ValueTask<LoadBookResult>` for cache hits
  - [ ] Implement two-tier caching (Memory + LiteDB)
  - [ ] Use `IAsyncDisposable` for stream cleanup
  - [ ] Implement progress reporting via `IProgress<int>`
  - [ ] **Performance Target**: Cache hit <10ms, miss <2s for 50MB
- [ ] Implement `LoadBookValidator.cs` using FluentValidation
  - [ ] Validate file exists and is readable
  - [ ] Check EPUB signature (PK magic bytes)
  - [ ] Validate file size limits
- [ ] Create `LoadBookResult.cs` with OneOf pattern
  - [ ] Success: Book entity with metadata
  - [ ] NotFound: File doesn't exist
  - [ ] InvalidFormat: Not a valid EPUB
  - [ ] TooLarge: Exceeds size limit
- [ ] Migrate existing LoadBookHandler logic
- [ ] Add comprehensive tests
  - [ ] Unit tests for handler logic
  - [ ] Integration tests with real EPUBs
  - [ ] Performance tests for various sizes
  - [ ] Cache behavior tests

#### 2.2 AnalyzeContent Feature

- [ ] Create `Features/AnalyzeContent/` folder structure
- [ ] Implement `AnalyzeContentCommand.cs`
- [ ] Implement `AnalyzeContentHandler.cs`
  - Use IContentAnalyzer for all metrics
  - Return comprehensive ContentAnalysisResult
- [ ] Implement `ContentAnalysisResult.cs`
- [ ] Add performance benchmarks
- [ ] Add comprehensive tests

#### 2.3 SearchContent Feature

- [ ] Create `Features/SearchContent/` folder structure
- [ ] Implement `SearchContentQuery.cs`
  - [ ] Search terms with operators (AND, OR, NOT)
  - [ ] Options: case sensitivity, whole word, regex
  - [ ] Pagination support
- [ ] Implement `SearchContentHandler.cs`
  - [ ] Integrate Lucene.NET for indexed search
  - [ ] Fall back to span-based search for non-indexed
  - [ ] Use `SearchValues<char>` for fast character matching
  - [ ] Implement parallel search with `Parallel.ForEachAsync`
  - [ ] **Performance Target**: <100ms for 100MB indexed content
- [ ] Implement `SearchResult.cs` and `SearchOptions.cs`
  - [ ] Chapter reference with position
  - [ ] Snippet with highlighted terms
  - [ ] Relevance score
  - [ ] Total match count
- [ ] Create `Infrastructure/Search/LuceneSearchIndex.cs`
  - [ ] Build inverted index per book
  - [ ] Support incremental updates
  - [ ] Implement BM25 scoring
  - [ ] Add phrase and proximity queries
- [ ] Migrate SearchService logic into handler
- [ ] Add comprehensive tests
  - [ ] Search accuracy tests
  - [ ] Performance benchmarks
  - [ ] Index consistency tests

#### 2.4 ExtractResources Feature

- [ ] Create `Features/ExtractResources/` folder structure
- [ ] Implement `ExtractResourcesCommand.cs`
- [ ] Implement `ExtractResourcesHandler.cs`
- [ ] Implement `ExtractResourcesResult.cs`
- [ ] Handle images, stylesheets, fonts
- [ ] Add comprehensive tests

### Phase 3: Navigation & Table of Contents 🗺️

**Goal**: Implement robust navigation handling

#### 3.1 ParseTableOfContents Feature

- [ ] Create `Features/ParseTableOfContents/` folder structure
- [ ] Implement `ParseTableOfContentsCommand.cs`
- [ ] Implement `ParseTableOfContentsHandler.cs`
- [ ] Support EPUB2 NCX navigation
- [ ] Support EPUB3 Navigation Document
- [ ] Create NavigationStructure value objects
- [ ] Add comprehensive tests

#### 3.2 Navigation Domain

- [ ] Create `Domain/Navigation/TableOfContents.cs` aggregate
- [ ] Create `Domain/Navigation/NavigationItem.cs` entity
- [ ] Implement hierarchical navigation structure
- [ ] Support fragment identifiers (#)
- [ ] Add navigation search capabilities

### Phase 4: Search & Indexing Infrastructure 🔍

**Goal**: Implement advanced search capabilities with proper indexing

#### 4.1 Lucene.NET Integration

- [ ] Add Lucene.NET packages
  - [ ] Lucene.Net (v4.8.0-beta00016)
  - [ ] Lucene.Net.Analysis.Common
  - [ ] Lucene.Net.Highlighter
  - [ ] Lucene.Net.Memory (for in-memory indexes)
- [ ] Create `Infrastructure/Search/LuceneSearchEngine.cs`
  - [ ] Use `RAMDirectory` for books <10MB
  - [ ] Use `MMapDirectory` for larger books
  - [ ] Implement custom `Analyzer` for EPUB content
  - [ ] Build inverted index with positions and offsets
  - [ ] Support phrase and proximity searches
  - [ ] Implement search result highlighting with `FastVectorHighlighter`
  - [ ] Add fuzzy matching with edit distance
  - [ ] **Performance Target**: Index 50MB in <30s
  - [ ] **Query Target**: <50ms for complex queries
- [ ] Create `Features/BuildSearchIndex/` feature
  - [ ] BuildSearchIndexCommand.cs with progress reporting
  - [ ] BuildSearchIndexHandler.cs with parallel indexing
  - [ ] Support incremental indexing for updates
  - [ ] Implement index optimization and compaction

#### 4.2 Fuzzy Search Implementation

- [ ] Add Fastenshtein package for fuzzy matching
- [ ] Create `Infrastructure/Search/FuzzySearchProvider.cs`
- [ ] Implement "Did you mean?" functionality
- [ ] Support typo-tolerant searching
- [ ] Add similarity threshold configuration

#### 4.3 Natural Language Search

- [ ] Add Microsoft.Recognizers.Text package
- [ ] Create `Features/NaturalLanguageSearch/` feature
  - [ ] Parse natural language queries
  - [ ] Extract dates, numbers, entities
  - [ ] Convert to search parameters

### Phase 5: Advanced Content Features 📚

**Goal**: Add advanced content processing capabilities

#### 5.1 ExtractMetadata Feature

- [ ] Create `Features/ExtractMetadata/` folder structure
- [ ] Implement comprehensive metadata extraction
- [ ] Support Dublin Core metadata
- [ ] Support custom metadata namespaces
- [ ] Handle series information
- [ ] Extract cover images

#### 4.2 GeneratePreview Feature

- [ ] Create `Features/GeneratePreview/` folder structure
- [ ] Implement smart preview generation
- [ ] Support configurable preview length
- [ ] Maintain paragraph boundaries
- [ ] Include key metadata in preview

#### 4.3 ExportContent Feature

- [ ] Create `Features/ExportContent/` folder structure
- [ ] Implement `ExportToMarkdownCommand.cs`
- [ ] Implement `ExportToPlainTextCommand.cs`
- [ ] Implement `ExportToJsonCommand.cs`
- [ ] Support batch export operations

### Phase 6: Persistence & State Management 💾

**Goal**: Implement proper data persistence with LiteDB

#### 6.1 LiteDB Repository Implementation

- [ ] Create `Infrastructure/Persistence/LiteDbBookRepository.cs`
  - [ ] Store book metadata
  - [ ] Track reading progress
  - [ ] Manage bookmarks
  - [ ] Cache search indexes
- [ ] Create domain models for persistence
  - [ ] BookMetadataDocument
  - [ ] ReadingProgressDocument
  - [ ] BookmarkDocument
- [ ] Implement repository pattern interfaces

#### 6.2 Caching Strategy

- [ ] Implement `Infrastructure/Caching/ChapterCache.cs`
  - [ ] Use IMemoryCache for runtime caching
  - [ ] Implement sliding expiration
  - [ ] Add cache size limits
  - [ ] Support cache invalidation
- [ ] Create `Infrastructure/Caching/SearchIndexCache.cs`
  - [ ] Cache Lucene indexes in memory
  - [ ] Implement lazy loading
  - [ ] Support partial index updates

### Phase 7: Reading Progress & Bookmarks 📖

**Goal**: Implement reading progress tracking using persistence layer

#### 7.1 TrackReadingProgress Feature

- [ ] Create `Features/TrackReadingProgress/` folder structure
- [ ] Implement progress tracking commands
- [ ] Support position within chapters
- [ ] Calculate percentage complete accurately
- [ ] Track reading time statistics

#### 5.2 ManageBookmarks Feature

- [ ] Create `Features/ManageBookmarks/` folder structure
- [ ] Implement bookmark CRUD operations
- [ ] Support bookmark categories/tags
- [ ] Implement bookmark search
- [ ] Support bookmark export/import

### Phase 6: Infrastructure Improvements 🔧

**Goal**: Improve infrastructure and cross-cutting concerns

#### 6.1 Caching Layer

- [ ] Implement memory caching for parsed books
- [ ] Cache content analysis results
- [ ] Implement cache invalidation strategies
- [ ] Add cache statistics and monitoring

#### 6.2 Performance Optimizations

- [ ] Implement lazy loading for chapter content
- [ ] Add streaming support for large EPUBs
- [ ] Implement parallel processing where applicable
- [ ] Add performance benchmarks

#### 6.3 Error Handling

- [ ] Implement comprehensive error types
- [ ] Add error recovery strategies
- [ ] Implement detailed error logging
- [ ] Create error documentation

### Phase 7: Testing & Quality Assurance ✅

**Goal**: Ensure comprehensive test coverage

#### 7.1 Unit Tests

- [ ] Achieve 80%+ code coverage
- [ ] Test all domain logic
- [ ] Test all feature handlers
- [ ] Test all value objects

#### 7.2 Integration Tests

- [ ] Test EPUB parsing end-to-end
- [ ] Test with various EPUB versions
- [ ] Test with malformed EPUBs
- [ ] Test performance with large files

#### 7.3 Benchmarks

- [ ] Create performance benchmarks
- [ ] Compare with old implementation
- [ ] Document performance characteristics
- [ ] Identify optimization opportunities

### Phase 8: AvaloniaUI Frontend Implementation 🖥️

**Goal**: Build cross-platform UI with AvaloniaUI

#### 8.1 Project Setup

- [ ] Create Alexandria.UI project with AvaloniaUI
- [ ] Add required NuGet packages
  - [ ] Avalonia and Avalonia.Desktop
  - [ ] Avalonia.ReactiveUI for MVVM
  - [ ] Avalonia.HtmlRenderer or CefNet.Avalonia
  - [ ] Material.Avalonia for Material Design
- [ ] Configure application resources and themes
- [ ] Set up dependency injection container

#### 8.2 Core Views Implementation

- [ ] Create MainWindow with navigation structure
- [ ] Implement ReaderView with HTML display
  - [ ] Custom HtmlViewer control
  - [ ] Reading toolbar (font, theme, search)
  - [ ] Progress indicator
- [ ] Implement LibraryView with book grid/list
  - [ ] BookCard custom control
  - [ ] Sorting and filtering
  - [ ] Import functionality
- [ ] Create SettingsView for preferences
- [ ] Implement NavigationPane for TOC

#### 8.3 ViewModels & Data Binding

- [ ] Create ReaderViewModel with reading logic
  - [ ] Chapter navigation
  - [ ] Search integration
  - [ ] Progress tracking
- [ ] Create LibraryViewModel with book management
- [ ] Implement NavigationViewModel for TOC
- [ ] Set up ReactiveUI bindings
- [ ] Implement INotifyPropertyChanged patterns

#### 8.4 Custom Controls

- [ ] Implement HtmlViewer control for EPUB content
  - [ ] Theme support
  - [ ] Text selection
  - [ ] Search highlighting
- [ ] Create TouchScrollViewer for mobile
- [ ] Build AnnotationLayer for highlights
- [ ] Implement BookCard for library display

#### 8.5 Themes & Styling

- [ ] Create Default (Light) theme
- [ ] Create Dark theme
- [ ] Create Sepia reading theme
- [ ] Create High Contrast accessibility theme
- [ ] Implement theme switching logic
- [ ] Add Material Design icons

#### 8.6 Platform-Specific Features

- [ ] Desktop: Keyboard shortcuts, context menus
- [ ] Mobile: Touch gestures, responsive layout
- [ ] Web: PWA support, service workers
- [ ] File associations for .epub files
- [ ] System tray integration (desktop)

### Phase 9: Documentation & Migration 📝

**Goal**: Document the new architecture and migration path

#### 9.1 API Documentation

- [ ] Document all public APIs
- [ ] Create usage examples
- [ ] Document migration guide
- [ ] Create architecture decision records (ADRs)

#### 9.2 Migration Tools

- [ ] Create migration scripts
- [ ] Document breaking changes
- [ ] Provide compatibility layer (if needed)
- [ ] Create upgrade guide

## Implementation Guidelines

### Priorities

1. **High Priority**:
   - Phase 1 (Foundation)
   - Phase 2.1 (LoadBook)
   - Phase 8.1-8.2 (Basic UI with ReaderView)
2. **Medium Priority**:
   - Phases 2.2-2.4 (Core Features)
   - Phase 3 (Navigation)
   - Phase 8.3-8.4 (Complete UI implementation)
3. **Low Priority**:
   - Phases 4-7 (Advanced Features)
   - Phase 8.5-8.6 (UI Polish and platform-specific features)

### Development Principles

- **Incremental Migration**: Keep old code working while building new
- **Test-First**: Write tests before implementation where possible
- **Performance Monitoring**: Benchmark before and after changes
- **Documentation**: Update docs as features are completed

### Success Metrics

- [ ] All tests passing
- [ ] No performance regression (< 5% slower)
- [ ] Zero duplicate code for content processing
- [ ] Clean separation of concerns
- [ ] All features follow vertical slice pattern

## Current Status

### Completed ✅

- [x] Architecture design documented
- [x] Development plan created

### In Progress 🚧

- [ ] Phase 1.2: Core Package Integration

### Blocked 🚫

- None currently

## Notes & Decisions

### Technology Choices

#### Backend

- **HTML Parsing**: AngleSharp (chosen over HtmlAgilityPack)
- **ZIP Handling**: SharpZipLib (stream-based, better than System.IO.Compression)
- **Search Engine**: Lucene.NET (advanced search with indexing)
- **Fuzzy Search**: Fastenshtein (fast Levenshtein distance)
- **Database**: LiteDB (embedded NoSQL for metadata/progress)
- **Caching**: Microsoft.Extensions.Caching.Memory
- **CSS Parsing**: ExCSS (for EPUB stylesheets)
- **Security**: HtmlSanitizer (for untrusted EPUBs)
- **Command/Query**: MediatR pattern
- **Validation**: FluentValidation
- **Result Types**: OneOf pattern

#### Frontend

- **UI Framework**: AvaloniaUI 11.0 (cross-platform XAML)
- **MVVM Framework**: ReactiveUI (reactive programming)
- **HTML Rendering**: Avalonia.HtmlRenderer or CefNet.Avalonia
- **Design System**: Material.Avalonia (Material Design)
- **Icons**: Material.Icons.Avalonia
- **DI Container**: Microsoft.Extensions.DependencyInjection

### Open Questions

- [x] Should we support streaming for very large EPUBs? **Yes - using SharpZipLib + memory-mapped files**
- [ ] Do we need a plugin architecture for custom extractors?
- [x] Should we implement a caching strategy from the start? **Yes - using IMemoryCache + LiteDB**
- [ ] Should we support cross-book searching in a library?
- [ ] Do we need real-time collaborative annotations?

## Contributing

To add new tasks or features to this plan:

1. Identify the appropriate phase
2. Add checkbox items with clear descriptions
3. Update priority if needed
4. Add any dependencies or blockers

## Performance Considerations

For detailed performance optimization patterns and best practices, refer to the [C# Performance Guide for EPUB Parser](./CSharp-Performance-Guide-EPUB-Parser.md). This guide covers:

- Memory-efficient string and text processing using Span<T>
- Optimal file I/O patterns for ZIP archive processing
- XML/HTML parsing optimizations
- Collection and LINQ performance improvements
- Async/await best practices

All implementations should follow these guidelines to ensure the parser remains performant even with large EPUB files.

Key principles to follow during implementation:

- Prefer Span<T> operations over string manipulation
- Use ArrayPool<T> for temporary buffers
- Implement streaming for large content processing
- Cache strategically using appropriate collection types

## Performance Testing Framework

### Benchmark Setup

#### Required Packages
- [ ] BenchmarkDotNet (v0.13.12)
- [ ] NBomber (for load testing)
- [ ] dotMemory Unit (for memory profiling)

#### Benchmark Categories

##### 1. Parsing Benchmarks
```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class EpubParsingBenchmarks
{
    // Benchmark EPUB loading for various sizes
    [Params(1, 10, 50, 100)] // MB
    public int FileSize { get; set; }

    [Benchmark]
    public async Task<Book> ParseEpub() { /* ... */ }
}
```

##### 2. Content Processing Benchmarks
- HTML to plain text extraction
- Word counting performance
- Search performance (indexed vs non-indexed)
- Memory allocation tracking

##### 3. I/O Benchmarks
- Stream vs memory-mapped file reading
- ZIP extraction performance
- Resource caching effectiveness

### Performance Targets

| Operation | Size | Target | Max Memory |
|-----------|------|--------|------------|
| Parse EPUB | 1MB | <100ms | 5MB |
| Parse EPUB | 50MB | <2s | 50MB |
| Parse EPUB | 100MB | <5s | 100MB |
| Word Count | 1MB text | <50ms | 1MB |
| Search (indexed) | 100MB | <100ms | 10MB |
| Search (non-indexed) | 10MB | <500ms | 5MB |
| Extract Plain Text | 100KB HTML | <10ms | 500KB |

### Continuous Performance Testing

- [ ] Set up benchmark CI pipeline
- [ ] Track performance regression with BenchmarkDotNet.Artifacts
- [ ] Create performance dashboard
- [ ] Alert on >10% regression

## Migration Checklist

### Phase 1: Regex to AngleSharp Migration

#### Step 1: Identify All Regex Usage
- [ ] Search for `Regex` class usage
- [ ] Document patterns being matched
- [ ] Categorize by complexity

#### Step 2: Replace Simple Patterns
- [ ] Replace `HtmlTagRegex` with AngleSharp parsing
- [ ] Replace `WhitespaceRegex` with span operations
- [ ] Replace `ParagraphRegex` with DOM queries

#### Step 3: Performance Validation
- [ ] Benchmark before/after each replacement
- [ ] Ensure no functionality regression
- [ ] Document performance improvements

### Phase 2: String to Span Migration

#### Identify String Operations
- [ ] `string.IndexOf` → `span.IndexOf`
- [ ] `string.Substring` → `span.Slice`
- [ ] `string.Split` → `SpanSplitEnumerator`
- [ ] `string.Concat` → `StringBuilder` or `ArrayPool`

#### Memory Optimization
- [ ] Replace `List<string>` with `List<ReadOnlyMemory<char>>` where possible
- [ ] Use `ArrayPool<char>` for temporary buffers
- [ ] Implement `IMemoryOwner<char>` for managed buffers

### Phase 3: Synchronous to Async Migration

#### I/O Operations
- [ ] File.ReadAllText → File.ReadAllTextAsync
- [ ] Stream.Read → Stream.ReadAsync
- [ ] ZipArchive operations → Async alternatives

#### CPU-Bound Operations
- [ ] Identify operations >50ms
- [ ] Wrap in Task.Run for UI responsiveness
- [ ] Consider parallel processing with PLINQ

## Code Review Guidelines

### Performance Checklist

When reviewing code, ensure:

#### Memory Efficiency
- [ ] No unnecessary allocations in hot paths
- [ ] Proper use of `ArrayPool<T>` for large buffers
- [ ] `Span<T>` used for string processing
- [ ] No boxing of value types

#### Async Patterns
- [ ] `ConfigureAwait(false)` in library code
- [ ] `ValueTask` for hot paths with caching
- [ ] Proper cancellation token propagation
- [ ] No `async void` except event handlers

#### Resource Management
- [ ] All `IDisposable` in using statements
- [ ] Streams properly disposed
- [ ] No resource leaks in error paths
- [ ] Proper cleanup in finally blocks

#### Algorithm Efficiency
- [ ] O(n) or better for common operations
- [ ] Appropriate data structures chosen
- [ ] Caching used where beneficial
- [ ] No unnecessary iterations

## Summary

This development plan integrates:

1. **Vertical Slice Architecture** - Features organized by use case, not technical layers
2. **Domain-Driven Design** - Rich domain models with clear boundaries
3. **Lifecycle-Aware Optimizations** - Packages chosen based on EPUB processing phases
4. **Performance Focus** - Streaming, caching, indexing for scalability
5. **Advanced Search** - Lucene.NET indexing with fuzzy and natural language support
6. **Proper Persistence** - LiteDB for metadata and progress tracking
7. **Cross-Platform UI** - AvaloniaUI for Windows, macOS, Linux, mobile, and web

The plan addresses all identified issues:

- ✅ Eliminates duplicate HTML processing
- ✅ Consolidates content analysis
- ✅ Provides efficient EPUB handling
- ✅ Adds advanced search capabilities
- ✅ Implements proper state management
- ✅ Follows clean architecture principles
- ✅ Delivers native cross-platform experience

## Change Log

- **2024-01-13**: Initial plan created combining architecture redesign and HTML parsing migration
- **2024-01-13**: Added lifecycle-aware improvements from text_manipulation.md analysis
- **2024-01-13**: Integrated search indexing (Lucene.NET) and persistence (LiteDB)
- **2024-01-13**: Added streaming ZIP handling with SharpZipLib
- **2024-01-13**: Added comprehensive AvaloniaUI frontend architecture (Phase 8)
- **2025-01-14**: Added Performance Considerations section with reference to performance guide
- **2025-01-14**: Added Phase 0 for project restructuring into Clean Architecture with separate Domain, Application, and Infrastructure projects
