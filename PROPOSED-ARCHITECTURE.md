# Alexandria EPUB Parser - Proposed Architecture Redesign

## Executive Summary

This document proposes a comprehensive architecture redesign for the Alexandria EPUB Parser to address significant design inconsistencies, code duplication, and architectural divergence. The current codebase has evolved into multiple competing patterns that violate DDD principles and create maintenance overhead.

## Current Architecture Problems

### 1. Duplicate Entity Models
- **Legacy Model**: `Alexandria.Parser.Models.Book` (simple data container)
- **Domain Model**: `Alexandria.Parser.Domain.Entities.Book` (rich domain entity)
- **Issue**: Two competing book representations with different capabilities

### 2. Scattered Content Processing Logic
**Reading Time Calculations** (found in multiple places):
- `Chapter.EstimateReadingTimeMinutes()` - Simple word split approach
- `Chapter.GetEstimatedReadingTime()` - HTML-aware approach
- `ContentProcessor.EstimateReadingTime()` - Full HTML processing
- `Book.GetEstimatedReadingTime()` - Aggregates chapter times
- `ContentService.GetReadingStatistics()` - Uses ContentProcessor

**Word Count Calculations** (found in multiple places):
- `Chapter.GetWordCount()` - Basic regex HTML stripping
- `ContentProcessor.CountWords()` - Advanced HTML processing
- Used inconsistently across the codebase

### 3. Mixed Architectural Patterns
- Traditional layered architecture (Infrastructure → Domain → Application)
- Service-oriented approach (ContentService, SearchService)
- Direct entity behavior (methods on Book/Chapter entities)
- Command/Query separation (LoadBookHandler)

### 4. Violation of DDD Principles
- Anemic models alongside rich domain models
- Cross-cutting concerns mixed with domain logic
- Multiple representations of the same concepts
- Domain services doing application-level orchestration

## Proposed Architecture: Vertical Slice + DDD

### Core Principles

1. **Vertical Slice Architecture**: Organize code by feature/use case rather than technical layers
2. **Domain-Driven Design**: Clear domain boundaries with rich domain models
3. **Single Responsibility**: Each component has one reason to change
4. **Dependency Inversion**: Domain at center, dependencies point inward

### Architecture Overview

```
src/
├── Alexandria.Parser/
│   ├── Features/                          # Vertical slices
│   │   ├── LoadBook/
│   │   │   ├── LoadBookCommand.cs
│   │   │   ├── LoadBookHandler.cs
│   │   │   └── LoadBookValidator.cs
│   │   ├── AnalyzeContent/
│   │   │   ├── AnalyzeContentCommand.cs
│   │   │   ├── AnalyzeContentHandler.cs
│   │   │   └── ContentAnalysisResult.cs
│   │   ├── SearchContent/
│   │   │   ├── SearchContentQuery.cs
│   │   │   ├── SearchContentHandler.cs
│   │   │   └── SearchResult.cs
│   │   └── ExtractResources/
│   │       ├── ExtractResourcesCommand.cs
│   │       └── ExtractResourcesHandler.cs
│   ├── Domain/
│   │   ├── Book/                          # Book Aggregate
│   │   │   ├── Book.cs                    # Aggregate Root
│   │   │   ├── Chapter.cs                 # Entity
│   │   │   ├── BookMetadata.cs           # Value Object
│   │   │   ├── Author.cs                 # Value Object
│   │   │   └── ReadingStatistics.cs      # Value Object
│   │   ├── Content/                       # Content Processing Domain
│   │   │   ├── IContentAnalyzer.cs       # Domain Service Interface
│   │   │   ├── ContentMetrics.cs         # Value Object
│   │   │   └── TextSegment.cs            # Value Object
│   │   ├── Navigation/                    # Navigation Domain
│   │   │   ├── TableOfContents.cs        # Aggregate Root
│   │   │   └── NavigationItem.cs         # Entity
│   │   ├── Resources/                     # Resource Management Domain
│   │   │   ├── ResourceCollection.cs     # Aggregate Root
│   │   │   └── EpubResource.cs           # Entity
│   │   └── Shared/
│   │       ├── IRepository.cs
│   │       └── DomainEvents/
│   ├── Infrastructure/
│   │   ├── Persistence/
│   │   │   └── BookRepository.cs
│   │   ├── ContentProcessing/
│   │   │   └── HtmlContentAnalyzer.cs    # Implements IContentAnalyzer
│   │   └── EpubParsing/
│   │       ├── EpubParserFactory.cs
│   │       ├── Epub2Parser.cs
│   │       └── Epub3Parser.cs
│   └── Shared/
│       ├── MediatR registration
│       ├── DI Container setup
│       └── Cross-cutting concerns
```

### Key Design Decisions

#### 1. Vertical Slices as Primary Organization
Each feature is a self-contained slice with:
- **Command/Query**: Input contract
- **Handler**: Business logic orchestration
- **Validator**: Input validation
- **Result**: Output contract

**Benefits**:
- Features are independent and testable
- Easy to understand request-to-response flow
- Natural boundaries for team ownership
- Reduced coupling between features

#### 2. Domain-Centric Content Processing
Create a dedicated `Content` domain with:
- `IContentAnalyzer` interface (domain service)
- `ContentMetrics` value object (word count, reading time, etc.)
- `TextSegment` for content portions

**Eliminates Duplication**:
- Single source of truth for content analysis
- Consistent algorithms across all use cases
- Domain-appropriate abstractions

#### 3. Aggregate Redesign

**Book Aggregate** (simplified):
```csharp
public class Book : AggregateRoot
{
    private readonly List<Chapter> _chapters;

    public BookMetadata Metadata { get; }
    public IReadOnlyList<Chapter> Chapters => _chapters;

    // Domain behavior - no direct calculation
    public ContentMetrics AnalyzeContent(IContentAnalyzer analyzer)
    {
        return analyzer.AnalyzeBook(this);
    }

    // Pure domain logic only
    public Chapter? FindChapterByTitle(string title) { }
    public Chapter? GetNextChapter(Chapter current) { }
}
```

**Chapter Entity** (simplified):
```csharp
public class Chapter : Entity
{
    public string Title { get; }
    public string Content { get; }
    public int Order { get; }

    // No content processing logic here
    // Content analysis handled by domain service
}
```

#### 4. Feature Examples

**Analyze Content Feature**:
```csharp
public record AnalyzeContentCommand(string BookPath);

public class AnalyzeContentHandler : IRequestHandler<AnalyzeContentCommand, ContentAnalysisResult>
{
    public async Task<ContentAnalysisResult> Handle(AnalyzeContentCommand request)
    {
        // 1. Load book (via repository)
        var book = await _bookRepository.LoadAsync(request.BookPath);

        // 2. Analyze content (via domain service)
        var metrics = book.AnalyzeContent(_contentAnalyzer);

        // 3. Return application-specific result
        return new ContentAnalysisResult(
            TotalWords: metrics.TotalWords,
            EstimatedReadingTime: metrics.EstimatedReadingTime,
            ChapterStatistics: metrics.ChapterBreakdown
        );
    }
}
```

**Search Content Feature**:
```csharp
public record SearchContentQuery(string BookPath, string SearchTerm, SearchOptions Options);

public class SearchContentHandler : IRequestHandler<SearchContentQuery, SearchResults>
{
    public async Task<SearchResults> Handle(SearchContentQuery request)
    {
        var book = await _bookRepository.LoadAsync(request.BookPath);
        var results = book.Search(request.SearchTerm, request.Options, _contentAnalyzer);

        return new SearchResults(results);
    }
}
```

### Migration Strategy

#### Phase 1: Domain Cleanup
1. **Remove duplicate models**: Eliminate `Models.Book`, keep `Domain.Entities.Book`
2. **Consolidate content processing**: Create `IContentAnalyzer` domain service
3. **Remove scattered logic**: Move all content analysis to domain service
4. **Update tests**: Fix all broken references

#### Phase 2: Feature Extraction
1. **Extract LoadBook feature**: Move from UseCase to Feature slice
2. **Create AnalyzeContent feature**: Consolidate reading time/word count logic
3. **Create SearchContent feature**: Consolidate search functionality
4. **Create ExtractResources feature**: Handle resource extraction

#### Phase 3: Infrastructure Alignment
1. **Simplify repository**: Single `IBookRepository` with clear contract
2. **Content processor implementation**: `HtmlContentAnalyzer` implements `IContentAnalyzer`
3. **Remove redundant services**: Eliminate `ContentService`, `SearchService`

#### Phase 4: Testing & Validation
1. **Update all tests**: Align with new architecture
2. **Performance validation**: Ensure no regression
3. **API consistency**: Verify external contracts unchanged

### Benefits of New Architecture

#### 1. Eliminated Duplication
- **Single content analysis**: All word counting, reading time calculation in one place
- **Consistent behavior**: Same algorithms across all features
- **Easier maintenance**: Change algorithm once, effects everywhere

#### 2. Clear Boundaries
- **Domain logic**: Pure business rules in domain layer
- **Application logic**: Orchestration and coordination in features
- **Infrastructure concerns**: Technical details isolated

#### 3. Improved Testability
- **Feature isolation**: Test each use case independently
- **Domain purity**: Test business logic without infrastructure
- **Clear contracts**: Mock interfaces easily

#### 4. Better Scalability
- **Feature teams**: Different teams can own different features
- **Independent deployment**: Features can evolve separately
- **Reduced coupling**: Changes in one feature don't affect others

### Implementation Notes

1. **Preserve backward compatibility**: External APIs should remain unchanged during migration
2. **Incremental migration**: Implement new features in new style, migrate old features gradually
3. **Performance monitoring**: Ensure content analysis performance doesn't degrade
4. **Documentation updates**: Update all documentation to reflect new architecture

This architecture eliminates the current design inconsistencies while providing a clean, maintainable foundation for future development.