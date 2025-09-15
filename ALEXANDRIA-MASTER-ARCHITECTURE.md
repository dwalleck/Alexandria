# Alexandria Master Architecture

## Executive Summary

Alexandria is a .NET 9.0-based EPUB reader library implementing Domain-Driven Design (DDD) with a Vertical Slice Architecture. The project provides comprehensive EPUB parsing, content analysis, and reading session management capabilities with planned cross-platform UI support via AvaloniaUI.

## Project Status

- **Backend**: Core EPUB parsing and domain logic ✅ Implemented
- **Frontend**: AvaloniaUI application 🚧 Planned
- **Architecture Migration**: Moving from layered to vertical slice architecture 🔄 In Progress

## Directory Structure

```
Alexandria/
├── src/                                    # Source code
│   ├── Alexandria.Domain/                 # Domain layer (pure business logic)
│   │   ├── Common/                        # Shared domain abstractions
│   │   ├── Entities/                      # Domain entities (Book, Chapter)
│   │   ├── Enums/                         # Domain enumerations
│   │   ├── Errors/                        # Domain error types
│   │   ├── Exceptions/                    # Domain exceptions
│   │   ├── Repositories/                  # Repository interfaces
│   │   ├── Services/                      # Domain service interfaces
│   │   ├── Specifications/                # Specification pattern implementations
│   │   └── ValueObjects/                  # Value objects (Author, BookTitle, etc.)
│   │
│   ├── Alexandria.Application/            # Application layer (use cases)
│   │   ├── Common/                        # Shared application logic
│   │   ├── Features/                      # Vertical slices
│   │   │   └── LoadBook/                  # Book loading feature
│   │   │       ├── LoadBookCommand.cs     # Command/Request
│   │   │       ├── LoadBookHandler.cs     # Business logic
│   │   │       ├── LoadBookValidator.cs   # Input validation
│   │   │       └── LoadBookResult.cs      # Response
│   │   ├── Interfaces/                    # Application service interfaces
│   │   └── Services/                      # Application services
│   │
│   └── Alexandria.Infrastructure/         # Infrastructure layer (external concerns)
│       ├── Caching/                       # Memory caching implementations
│       ├── ContentProcessing/             # HTML/CSS processing
│       ├── FileSystem/                    # File system operations
│       │   ├── Models/                    # Infrastructure models
│       │   └── Parsers/                   # EPUB parser implementations
│       │       ├── Models/                 # Parser-specific models
│       │       │   ├── Epub2/              # EPUB 2 format models
│       │       │   └── Epub3/              # EPUB 3 format models
│       │       ├── AdaptiveEpubParser.cs  # Auto-detecting parser
│       │       ├── Epub2Parser.cs         # EPUB 2.0 parser
│       │       ├── Epub3Parser.cs         # EPUB 3.0 parser
│       │       ├── EpubParserFactory.cs   # Parser factory
│       │       └── EpubVersionDetector.cs # Version detection
│       ├── IO/                            # I/O operations
│       │   ├── EpubResourceExtractor.cs   # Resource extraction
│       │   └── StreamingEpubReader.cs     # Large file streaming
│       ├── Persistence/                   # Data persistence
│       │   └── EpubLoader.cs              # Main loading orchestrator
│       └── Services/                      # Infrastructure services
│           └── AngleSharpContentAnalyzer.cs # HTML content analysis
│
├── tests/                                  # Test projects
│   ├── Alexandria.Domain.Tests/           # Domain unit tests
│   ├── Alexandria.Application.Tests/      # Application layer tests
│   ├── Alexandria.Infrastructure.Tests/   # Infrastructure tests
│   │   └── sample-epubs/                  # Test EPUB files
│   ├── Alexandria.Benchmarks/             # Performance benchmarks
│   └── Alexandria.Repositories.Tests/     # Repository integration tests
│
├── Alexandria.sln                         # Solution file
├── ALEXANDRIA-SYSTEM-ARCHITECTURE.md      # Detailed system architecture
├── AVALONIA-FRONTEND-ARCHITECTURE.md      # Frontend architecture plans
└── CLAUDE.md                              # AI assistant instructions
```

## Technology Stack

### Core Framework

- **.NET 9.0** - Target framework (using preview version)
- **C# 13** - Programming language with nullable reference types enabled
- **ImplicitUsings** - Enabled for cleaner code

### Domain Layer

| Package | Version | Purpose |
|---------|---------|---------|
| OneOf | 3.0.271 | Discriminated unions for result types |

### Application Layer

| Package | Version | Purpose |
|---------|---------|---------|
| MediatR | 13.0.0 | CQRS pattern implementation |
| FluentValidation | 12.0.0 | Input validation |

### Infrastructure Layer

| Package | Version | Purpose |
|---------|---------|---------|
| AngleSharp | 1.3.0 | HTML/CSS parsing and DOM manipulation |
| LiteDB | 5.0.21 | NoSQL embedded database |
| SharpZipLib | 1.4.2 | ZIP archive handling for EPUB files |
| Microsoft.Extensions.Caching.Memory | 9.0.9 | In-memory caching |
| Microsoft.Extensions.DependencyInjection | 9.0.9 | Dependency injection |
| Microsoft.Extensions.Logging | 9.0.9 | Logging abstractions |
| Microsoft.Extensions.Configuration | 9.0.9 | Configuration management |
| Microsoft.Extensions.Options | 9.0.9 | Options pattern |

### Testing Stack

| Package | Version | Purpose |
|---------|---------|---------|
| TUnit | Latest | Unit testing framework |
| Moq | Latest | Mocking framework |
| BenchmarkDotNet | Latest | Performance benchmarking |

### Planned Frontend Stack (AvaloniaUI)

| Package | Purpose |
|---------|---------|
| Avalonia | Cross-platform UI framework |
| Avalonia.ReactiveUI | MVVM support |
| Avalonia.HtmlRenderer | HTML content rendering |
| Material.Avalonia | Material Design components |
| Lucene.NET | Full-text search (planned) |

## Architectural Patterns

### 1. Domain-Driven Design (DDD)

- **Rich Domain Models**: Entities contain business logic
- **Value Objects**: Immutable objects for concepts (Author, BookTitle)
- **Domain Services**: Cross-entity operations (IContentAnalyzer)
- **Repository Pattern**: Data access abstraction
- **Specification Pattern**: Reusable query logic

### 2. Vertical Slice Architecture

- **Feature-Based Organization**: Each feature is self-contained
- **CQRS Pattern**: Command/Query separation with MediatR
- **Request/Handler/Response**: Clear flow for each use case
- **Minimal Cross-Feature Dependencies**: Features are isolated

### 3. Clean Architecture Principles

- **Dependency Inversion**: Domain doesn't depend on infrastructure
- **Interface Segregation**: Small, focused interfaces
- **Single Responsibility**: Each class has one reason to change
- **Open/Closed**: Open for extension, closed for modification

## Key Components

### Domain Entities

```
Book (Aggregate Root)
├── Metadata (Value Object)
├── Chapters (Collection)
├── Resources (Images, CSS, Fonts)
└── Navigation (Table of Contents)

Chapter (Entity)
├── Title
├── Content (HTML)
├── Order
└── Navigation Points
```

### Core Services

#### Domain Services

- `IEpubLoader` - Main EPUB loading interface
- `IContentAnalyzer` - Content metrics and analysis
- `IBookRepository` - Book persistence abstraction

#### Infrastructure Services

- `AdaptiveEpubParser` - Auto-detects EPUB version
- `StreamingEpubReader` - Handles large EPUB files
- `AngleSharpContentAnalyzer` - HTML processing
- `BookCache` - In-memory caching with LRU eviction

### Loading Strategy

```
File Size → Strategy
< 10MB   → Eager Loading (full in-memory)
10-50MB  → Adaptive (based on available memory)
> 50MB   → Streaming (on-demand chapter loading)
```

## Current Implementation Status

### ✅ Implemented

- EPUB 2.0/3.0 parsing with adaptive detection
- Domain model with rich entities and value objects
- Vertical slice for LoadBook feature
- Content analysis with AngleSharp
- Basic caching infrastructure
- Streaming support for large files
- MediatR integration for CQRS

### 🔄 In Progress

- Moving from layered to vertical slice architecture
- Consolidating duplicate parsing implementations
- Implementing proper streaming adapter pattern

### 🚧 Planned

- Search functionality with Lucene.NET
- Bookmark and annotation persistence
- Reading progress tracking
- AvaloniaUI frontend application
- Library management with LiteDB
- Multi-tier caching strategy

## Build Configuration

### Solution Configuration

- **Debug/Release** configurations
- **Multiple platforms**: Any CPU, ARM, ARM64, x64, x86
- **9 projects** total (3 source, 6 test)

### Known Issues

- MediatR version mismatch warning (using 13.0.0 with extensions expecting 11.0.0)
- Using .NET 10 preview SDK (backwards compatible)

## Development Workflow

### Building

```bash
# Build entire solution
dotnet build

# Run tests
dotnet test

# Run benchmarks
dotnet run -c Release --project tests/Alexandria.Benchmarks
```

### Testing Strategy

- **Unit Tests**: Domain and application logic
- **Integration Tests**: Infrastructure components
- **Benchmark Tests**: Performance critical paths
- **Sample EPUBs**: Real-world test files in `tests/Alexandria.Infrastructure.Tests/sample-epubs/`

## Architecture Decision Records

### ADR-001: Vertical Slice Architecture

**Decision**: Migrate from traditional layered architecture to vertical slices
**Rationale**: Better feature isolation, easier to understand and modify individual features
**Status**: In progress

### ADR-002: Streaming for Large Files

**Decision**: Implement streaming reader for files >50MB
**Rationale**: Prevent memory exhaustion with large EPUB files
**Status**: Implemented

### ADR-003: AvaloniaUI for Frontend

**Decision**: Use AvaloniaUI for cross-platform UI
**Rationale**: True cross-platform support, XAML-based, familiar to WPF developers
**Status**: Planned

### ADR-004: OneOf for Result Types

**Decision**: Use OneOf library for discriminated unions
**Rationale**: Type-safe error handling without exceptions
**Status**: Implemented

## Backend Class Diagrams

### Domain Layer Architecture

```mermaid
classDiagram
    class Book {
        +Guid Id
        +BookTitle Title
        +List~Author~ Authors
        +BookMetadata Metadata
        +List~Chapter~ Chapters
        +List~Resource~ Resources
        +Navigation Navigation
        +GetChapter(int index) Chapter
        +GetNextChapter(Chapter current) Chapter
        +GetPreviousChapter(Chapter current) Chapter
        +GetResource(string id) Resource
        +GetWordCount() int
        +GetEstimatedReadingTime() TimeSpan
    }

    class Chapter {
        +string Id
        +string Title
        +ChapterContent Content
        +int Order
        +List~NavigationPoint~ NavigationPoints
        +GetContent() string
        +GetPlainText() string
        +GetMetrics() ContentMetrics
    }

    class BookMetadata {
        +BookIdentifier Identifier
        +string Publisher
        +DateTime PublicationDate
        +string Language
        +string Description
        +List~string~ Subjects
        +string Rights
        +Dictionary~string,string~ CustomMetadata
    }

    class Author {
        +string Name
        +string Role
        +string FileAs
        +ToString() string
    }

    class BookTitle {
        +string Value
        +string Subtitle
        +ToString() string
    }

    class BookIdentifier {
        +string Isbn
        +string Asin
        +string Doi
        +string Uuid
        +ToString() string
    }

    class Resource {
        +string Id
        +string Href
        +ResourceType Type
        +string MediaType
        +byte[] Content
        +bool IsImage()
        +bool IsStylesheet()
        +bool IsFont()
    }

    class ContentMetrics {
        +int WordCount
        +int CharacterCount
        +int ParagraphCount
        +TimeSpan EstimatedReadingTime
        +double ReadabilityScore
    }

    Book --> BookMetadata
    Book --> "*" Chapter
    Book --> "*" Author
    Book --> "*" Resource
    BookMetadata --> BookIdentifier
    BookMetadata --> BookTitle
    Chapter --> ChapterContent
    Chapter --> ContentMetrics
```

### Application Layer Architecture

```mermaid
classDiagram
    class LoadBookHandler {
        -IEpubLoader _loader
        -IBookCache _cache
        -IContentAnalyzer _analyzer
        +HandleAsync(LoadBookCommand) LoadBookResult
        -ValidateFile(string path) bool
        -CheckCache(string path) Book
        -AnalyzeContent(Book book) void
    }

    class SearchContentHandler {
        -ISearchIndex _index
        -IBookRepository _repository
        +HandleAsync(SearchContentQuery) SearchResults
        -BuildIndexIfNeeded(BookId) void
        -ExecuteSearch(string query) List~Match~
        -RankResults(List~Match~) List~SearchResult~
    }

    class BookmarkHandler {
        -IBookmarkRepository _repository
        -IPositionCalculator _calculator
        +HandleAsync(AddBookmarkCommand) BookmarkResult
        -CalculatePosition(ViewportInfo) Position
        -GeneratePreview(Position) string
        -ValidateBookmark(Bookmark) bool
    }

    class NavigateHandler {
        -INavigationService _navigation
        -IReadingSessionService _session
        +HandleAsync(NavigateCommand) NavigationResult
        -UpdateHistory(NavigationPoint) void
        -LoadChapter(ChapterIndex) Chapter
        -UpdateProgress(Position) void
    }

    class AnnotateHandler {
        -IAnnotationRepository _repository
        -ITextSelector _selector
        +HandleAsync(AddAnnotationCommand) AnnotationResult
        -ExtractSelection(Range) TextSelection
        -CreateHighlight(Selection) Highlight
        -SaveAnnotation(Annotation) void
    }

    LoadBookHandler --> IEpubLoader
    LoadBookHandler --> IBookCache
    LoadBookHandler --> IContentAnalyzer
    SearchContentHandler --> ISearchIndex
    SearchContentHandler --> IBookRepository
    BookmarkHandler --> IBookmarkRepository
    NavigateHandler --> INavigationService
    AnnotateHandler --> IAnnotationRepository
```

### Infrastructure Layer Architecture

```mermaid
classDiagram
    class AdaptiveEpubParser {
        -IEpubVersionDetector _detector
        -Dictionary~Version,IEpubParser~ _parsers
        +ParseAsync(Stream) Book
        -DetectVersion(Stream) EpubVersion
        -SelectParser(Version) IEpubParser
    }

    class StreamingEpubReader {
        -MemoryMappedFile _mmf
        -ZipArchive _archive
        +OpenAsync(string path) void
        +StreamChapterAsync(int index) Stream
        +GetMetadataAsync() BookMetadata
        -MapFile(string path) MemoryMappedFile
    }

    class LuceneSearchIndex {
        -IndexWriter _writer
        -IndexSearcher _searcher
        +BuildIndexAsync(Book) void
        +SearchAsync(string query) SearchResults
        -CreateDocument(Chapter) Document
        -ParseQuery(string) Query
    }

    class LiteDbBookRepository {
        -LiteDatabase _database
        -ILiteCollection~Book~ _books
        +AddAsync(Book) void
        +GetByIdAsync(Guid) Book
        +GetByIsbnAsync(string) Book
        +FindAsync(ISpecification) List~Book~
    }

    class AngleSharpContentAnalyzer {
        -IBrowsingContext _context
        +AnalyzeContentAsync(string html) ContentMetrics
        -ExtractText(IHtmlDocument) string
        -CountWords(string) int
        -CalculateReadability(string) double
    }

    AdaptiveEpubParser --> IEpubVersionDetector
    AdaptiveEpubParser --> IEpubParser
    StreamingEpubReader --> MemoryMappedFile
    LuceneSearchIndex --> IndexWriter
    LiteDbBookRepository --> LiteDatabase
    AngleSharpContentAnalyzer --> IBrowsingContext
```

## Frontend Class Diagrams

### ViewModel Architecture

```mermaid
classDiagram
    class ViewModelBase {
        +IObservable~T~ WhenAnyValue()
        +RaiseAndSetIfChanged()
        +RaisePropertyChanged()
    }

    class ReaderViewModel {
        -ILoadBookHandler _loadHandler
        -IContentAnalyzer _analyzer
        -IReadingSessionService _session
        +Book CurrentBook
        +Chapter CurrentChapter
        +double FontSize
        +string SelectedTheme
        +double ReadingProgress
        +ReactiveCommand OpenBookCommand
        +ReactiveCommand NextChapterCommand
        +ReactiveCommand SearchCommand
        -LoadChapterContent(Chapter)
        -UpdateReadingProgress()
        -SaveReadingProgress()
    }

    class LibraryViewModel {
        -ILibraryService _library
        -IBookImporter _importer
        +ObservableCollection~LibraryBook~ Books
        +string FilterText
        +bool IsGridView
        +ReactiveCommand AddBooksCommand
        +ReactiveCommand OpenBookCommand
        -LoadLibrary()
        -ImportBooks(string[])
        -FilterBooks(string)
    }

    class SearchViewModel {
        -ISearchService _search
        -INavigationService _navigation
        +string Query
        +ObservableCollection~SearchResult~ Results
        +bool IsSearching
        +ReactiveCommand SearchCommand
        +ReactiveCommand NavigateToResultCommand
        -ExecuteSearch()
        -NavigateToResult(SearchResult)
    }

    class SettingsViewModel {
        -IThemeService _theme
        -IPreferencesRepository _prefs
        +string SelectedTheme
        +double DefaultFontSize
        +double LineHeight
        +bool EnableAnimations
        +ReactiveCommand SaveCommand
        -LoadSettings()
        -SaveSettings()
    }

    ViewModelBase <|-- ReaderViewModel
    ViewModelBase <|-- LibraryViewModel
    ViewModelBase <|-- SearchViewModel
    ViewModelBase <|-- SettingsViewModel
```

### Service Layer Architecture

```mermaid
classDiagram
    class ReadingSessionService {
        -Book _currentBook
        -Chapter _currentChapter
        -ReadingPosition _position
        -Stack~NavigationPoint~ _history
        +StartSession(Book)
        +NavigateToChapter(int)
        +SaveProgress()
        +GetCurrentPosition()
        -UpdatePosition()
        -PrefetchAdjacentChapters()
    }

    class PaginationEngine {
        -IHtmlMeasurementService _measurement
        -IViewportCalculator _viewport
        +PaginateAsync(string html, ViewportDimensions)
        -CalculatePageBreaks(ContentMeasurements)
        -AdjustForOrphansWidows(PageBreak)
        -GeneratePages(List~PageBreak~)
    }

    class LibraryService {
        -LibraryDatabase _database
        -IMetadataEnricher _enricher
        +AddBookAsync(string path)
        +GetRecentBooksAsync(int)
        +SearchBooksAsync(string)
        +UpdateReadingProgressAsync(Guid, double)
        -EnrichMetadata(Book)
        -DetectDuplicates(Book)
    }

    class BackendServiceAdapter {
        -IBookLoader _loader
        -ISearchService _search
        -IBookmarkService _bookmarks
        +LoadBookAsync(string) BookLoadResult
        +SearchAsync(string) SearchResults
        +AddBookmarkAsync(Position) Bookmark
        -ConvertToDisplayModel(Book)
        -HandleOfflineMode()
    }

    class ThemeService {
        -Dictionary~string,ThemeDefinition~ _themes
        -ThemeDefinition _currentTheme
        +ApplyTheme(string name)
        +CreateCustomTheme(ThemeDefinition)
        +GetAvailableThemes()
        -LoadThemeResources()
    }

    ReadingSessionService --> Book
    ReadingSessionService --> Chapter
    PaginationEngine --> IHtmlMeasurementService
    LibraryService --> LibraryDatabase
    BackendServiceAdapter --> IBookLoader
    ThemeService --> ThemeDefinition
```

### Custom Controls Architecture

```mermaid
classDiagram
    class HtmlViewer {
        +string Html
        +double FontSize
        +string Theme
        +List~Highlight~ Highlights
        -RenderHtml()
        -ApplyTheme()
        -ProcessHtmlForDisplay()
        -InjectHighlights()
    }

    class AnnotationLayer {
        +List~Annotation~ Annotations
        +bool IsVisible
        -RenderAnnotations()
        -PositionBubbles()
        -HandleAnnotationClick()
        -CreateAnnotationBubble()
    }

    class VirtualizingBookGrid {
        -VirtualizingStackPanel _panel
        -Dictionary~int,BookCard~ _realizedItems
        -ObjectPool~BookCard~ _itemPool
        +ObservableCollection~Book~ Books
        -RealizeItems(Range)
        -VirtualizeItems(Range)
        -CalculateVisibleRange()
    }

    class HighlightRenderer {
        -List~Highlight~ _highlights
        +RenderHighlightsAsync(IHtmlDocument)
        -ApplyHighlightToRange(Highlight)
        -ResolveOverlaps(List~Highlight~)
        -GenerateHighlightStyle(Highlight)
    }

    class BookmarkRenderer {
        -List~Bookmark~ _bookmarks
        +RenderBookmarks(IHtmlDocument, int page)
        -CreateBookmarkIndicator(Bookmark)
        -CalculateVerticalPosition(Bookmark)
        -HandleBookmarkClick()
    }

    HtmlViewer --> HighlightRenderer
    HtmlViewer --> AnnotationLayer
    AnnotationLayer --> Annotation
    VirtualizingBookGrid --> BookCard
    HighlightRenderer --> Highlight
    BookmarkRenderer --> Bookmark
```

## LiteDB Storage Architecture

### Database Schema

```mermaid
classDiagram
    class LiteDatabase {
        +GetCollection~T~(string name)
        +GetStorage~T~(string name)
        +BeginTrans() LiteTransaction
        +Checkpoint() void
        +Rebuild() long
    }

    class LibraryBook {
        +Guid Id
        +string FilePath
        +string Title
        +List~LibraryAuthor~ Authors
        +string CoverImageId
        +double ProgressPercentage
        +DateTime LastOpened
        +string FileHash
        +List~string~ Tags
        +int Rating
    }

    class LibraryCollection {
        +Guid Id
        +string Name
        +CollectionType Type
        +string SmartCriteria
        +List~LibraryBook~ Books
        +DateTime Created
    }

    class BookmarkEntity {
        +Guid Id
        +Guid BookId
        +string CFI
        +int ChapterIndex
        +double Progress
        +string PreviewText
        +DateTime Created
    }

    class AnnotationEntity {
        +Guid Id
        +Guid BookId
        +string StartCFI
        +string EndCFI
        +string SelectedText
        +string Note
        +string Color
        +DateTime Created
    }

    class CacheEntry {
        +string Key
        +T Value
        +DateTime Expiry
        +int Priority
        +bool IsExpired()
    }

    LiteDatabase --> LibraryBook : Books Collection
    LiteDatabase --> LibraryCollection : Collections
    LiteDatabase --> BookmarkEntity : Bookmarks
    LiteDatabase --> AnnotationEntity : Annotations
    LiteDatabase --> CacheEntry : L2 Cache
    LibraryCollection --> LibraryBook : References
```

### Repository Implementation

```mermaid
classDiagram
    class LiteDbContext {
        -LiteDatabase _database
        -string _connectionString
        +Books ILiteCollection
        +Bookmarks ILiteCollection
        +Annotations ILiteCollection
        +FileStorage ILiteStorage
        +Initialize()
        +CreateIndexes()
    }

    class LiteDbBookRepository {
        -LiteDbContext _context
        +AddAsync(Book) Guid
        +GetByIdAsync(Guid) Book
        +GetByIsbnAsync(string) Book
        +FindAsync(Expression) List~Book~
        +UpdateAsync(Book) void
        +DeleteAsync(Guid) void
    }

    class LiteDbBookmarkRepository {
        -LiteDbContext _context
        +AddBookmarkAsync(Bookmark) Guid
        +GetBookmarksAsync(Guid bookId) List~Bookmark~
        +UpdateBookmarkAsync(Bookmark) void
        +DeleteBookmarkAsync(Guid) void
    }

    class LiteDbAnnotationRepository {
        -LiteDbContext _context
        +AddAnnotationAsync(Annotation) Guid
        +GetAnnotationsAsync(Guid bookId) List~Annotation~
        +UpdateAnnotationAsync(Annotation) void
        +DeleteAnnotationAsync(Guid) void
    }

    class LiteDbCache {
        -ILiteCollection~CacheEntry~ _cache
        +GetAsync~T~(string key) T
        +SetAsync~T~(string key, T value) void
        +RemoveAsync(string key) void
        +ClearExpiredAsync() void
    }

    LiteDbContext --> LiteDatabase
    LiteDbBookRepository --> LiteDbContext
    LiteDbBookmarkRepository --> LiteDbContext
    LiteDbAnnotationRepository --> LiteDbContext
    LiteDbCache --> LiteDbContext
```

## Complete System Integration

### Data Flow Architecture

```mermaid
sequenceDiagram
    participant User
    participant UI as AvaloniaUI
    participant VM as ViewModel
    participant Service as BackendService
    participant Domain as Domain Layer
    participant Infra as Infrastructure
    participant LiteDB as LiteDB

    User->>UI: Open EPUB
    UI->>VM: OpenBookCommand
    VM->>Service: LoadBookAsync(path)
    Service->>Domain: LoadBookCommand
    Domain->>Infra: ParseEpub()
    Infra->>LiteDB: CheckCache()

    alt Cached
        LiteDB-->>Infra: Return cached
    else Not Cached
        Infra->>Infra: Parse file
        Infra->>LiteDB: Store metadata
    end

    Infra-->>Domain: Book entity
    Domain-->>Service: LoadBookResult
    Service-->>VM: BookDisplayModel
    VM-->>UI: Update view
    UI-->>User: Display book
```

### Caching Strategy

```mermaid
graph TD
    A[Request] --> B{L1 Memory Cache}
    B -->|Hit| C[Return Data]
    B -->|Miss| D{L2 LiteDB Cache}
    D -->|Hit| E[Promote to L1]
    E --> C
    D -->|Miss| F{L3 Disk Cache}
    F -->|Hit| G[Promote to L2]
    G --> E
    F -->|Miss| H[Generate Fresh]
    H --> I[Store in L3]
    I --> G
```

## Implementation Roadmap

### Phase 1: Core Infrastructure ✅ (Completed)

- [x] Domain entities and value objects
- [x] Basic EPUB parsing (2.0/3.0)
- [x] LoadBook vertical slice
- [x] Content analysis with AngleSharp
- [x] Basic caching infrastructure

### Phase 2: Backend Completion 🔄 (In Progress)

- [ ] Fix architectural violations
- [ ] Implement StreamingEpubReaderAdapter
- [ ] Complete Search infrastructure with Lucene.NET
- [ ] Implement LiteDB repositories
- [ ] Add remaining vertical slices (Navigate, Bookmark, Annotate)

### Phase 3: Frontend Foundation 🚧 (Planned)

- [ ] Create Alexandria.UI project
- [ ] Implement base ViewModels
- [ ] Create main window and navigation
- [ ] Implement HtmlViewer control
- [ ] Basic reader view

### Phase 4: Library Management 🚧 (Planned)

- [ ] Library database with LiteDB
- [ ] Book import pipeline
- [ ] Metadata enrichment
- [ ] Duplicate detection
- [ ] Collections and tagging

### Phase 5: Reading Experience 🚧 (Planned)

- [ ] Pagination engine
- [ ] Reading session management
- [ ] Bookmarks and annotations
- [ ] Search and highlighting
- [ ] Progress tracking

### Phase 6: Polish & Optimization 🚧 (Planned)

- [ ] Multi-tier caching
- [ ] Virtual scrolling
- [ ] Background processing
- [ ] Memory management
- [ ] Performance optimization

### Phase 7: Advanced Features 🚧 (Future)

- [ ] Cloud sync
- [ ] Export functionality
- [ ] Plugin architecture
- [ ] Mobile support
- [ ] WebAssembly deployment

## Technology Summary

### Backend Stack

- **.NET 9.0** - Core framework
- **MediatR** - CQRS implementation
- **FluentValidation** - Input validation
- **AngleSharp** - HTML/CSS processing
- **SharpZipLib** - EPUB extraction
- **Lucene.NET** - Full-text search
- **LiteDB** - NoSQL database for all storage
- **OneOf** - Discriminated unions

### Frontend Stack

- **AvaloniaUI 11.0** - Cross-platform UI
- **ReactiveUI** - MVVM framework
- **Avalonia.HtmlRenderer** - HTML rendering
- **Material.Avalonia** - Material Design
- **LiteDB** - Local storage

### Testing Stack

- **TUnit** - Unit testing
- **Moq** - Mocking
- **BenchmarkDotNet** - Performance
- **Avalonia.Headless** - UI testing

## Next Steps

1. **Complete Backend Refactoring**
   - Fix StreamingEpubReader integration
   - Implement missing domain services
   - Complete LiteDB repositories

2. **Start Frontend Implementation**
   - Create Alexandria.UI project
   - Implement core ViewModels
   - Build reader interface

3. **Integrate Search System**
   - Implement Lucene.NET indexing
   - Build search UI
   - Add highlighting support

4. **Complete Storage Layer**
   - Implement all LiteDB repositories
   - Add caching layers
   - Build import pipeline

## Key Architectural Decisions

### Storage Strategy

**LiteDB for Everything**: We use LiteDB as our primary storage solution for:

- Library catalog and metadata
- Bookmarks and annotations
- Reading progress and statistics
- User preferences
- L2 cache layer
- Cover image storage (FileStorage)

This provides a consistent, embedded NoSQL database that works across all platforms without external dependencies.

### Loading Strategy

- **Small files (<10MB)**: Eager loading into memory
- **Medium files (10-50MB)**: Adaptive based on available memory
- **Large files (>50MB)**: Streaming with memory-mapped files

### Search Architecture

- **Lucene.NET** for full-text indexing
- **On-demand indexing** for large books
- **Real-time search** fallback when index unavailable
- **LiteDB** for storing search index metadata

### Frontend Architecture

- **AvaloniaUI** for true cross-platform support
- **MVVM** with ReactiveUI for clean separation
- **CSS columns** for pagination
- **Virtual scrolling** for large libraries

## References

- [ALEXANDRIA-SYSTEM-ARCHITECTURE.md](./ALEXANDRIA-SYSTEM-ARCHITECTURE.md) - Detailed system design
- [AVALONIA-FRONTEND-ARCHITECTURE.md](./AVALONIA-FRONTEND-ARCHITECTURE.md) - Frontend plans
- [CLAUDE.md](./CLAUDE.md) - Development guidelines

---

*This document serves as the complete architectural blueprint for the Alexandria EPUB Reader project, consolidating all design decisions, implementation details, and technical specifications.*
