# Story: [STORY_ID] - [TITLE]

## Status

- [ ] Not Started
- [ ] In Progress
- [ ] Code Complete
- [ ] PR Opened
- [ ] Merged

## Overview

[Brief description of what needs to be implemented, aligned with Alexandria's architecture]

## Architecture References

- **[ALEXANDRIA-MASTER-ARCHITECTURE.md](./ALEXANDRIA-MASTER-ARCHITECTURE.md)** - [Relevant section, e.g., "Backend Class Diagrams > Domain Layer"]
- **[ALEXANDRIA-SYSTEM-ARCHITECTURE.md](./ALEXANDRIA-SYSTEM-ARCHITECTURE.md)** - [Relevant component, e.g., "Component Registry > LoadBookHandler"]
- **[AVALONIA-FRONTEND-ARCHITECTURE.md](./AVALONIA-FRONTEND-ARCHITECTURE.md)** - [Relevant feature, e.g., "ReaderViewModel Implementation"]

## Acceptance Criteria

- [ ] [Specific measurable outcome 1]
- [ ] [Specific measurable outcome 2]
- [ ] All code builds successfully
- [ ] All tests pass (new and existing)
- [ ] Code follows Alexandria architecture patterns
- [ ] Pull request opened when complete

## Technical Context

### Architecture Requirements

- **Domain-Driven Design**: For domain layer stories
  - Rich domain models with business logic
  - Value objects for immutable concepts
  - No infrastructure dependencies
  - Repository interfaces only

- **Vertical Slice Architecture**: For application layer stories
  - MediatR command/query handlers
  - FluentValidation for input validation
  - OneOf<TSuccess, DomainError> pattern for results
  - Feature folders under `Features/`

- **Clean Architecture**: For all layers
  - Dependency direction: UI → Application → Domain
  - Infrastructure implements domain interfaces
  - No circular dependencies

- **Frontend MVVM**: For AvaloniaUI stories
  - ReactiveUI for data binding
  - ViewModels handle all logic
  - Views contain only XAML
  - Services injected via DI

### Implementation Location

- **Domain**: `src/Alexandria.Domain/`
  - Entities: `src/Alexandria.Domain/Entities/`
  - Value Objects: `src/Alexandria.Domain/ValueObjects/`
  - Services: `src/Alexandria.Domain/Services/`
  - Repositories: `src/Alexandria.Domain/Repositories/`

- **Application**: `src/Alexandria.Application/`
  - Features: `src/Alexandria.Application/Features/[FeatureName]/`
  - Services: `src/Alexandria.Application/Services/`

- **Infrastructure**: `src/Alexandria.Infrastructure/`
  - Parsers: `src/Alexandria.Infrastructure/FileSystem/Parsers/`
  - Persistence: `src/Alexandria.Infrastructure/Persistence/LiteDb/`
  - Search: `src/Alexandria.Infrastructure/Search/`
  - Caching: `src/Alexandria.Infrastructure/Caching/`

- **Frontend**: `src/Alexandria.UI/`
  - Views: `src/Alexandria.UI/Views/`
  - ViewModels: `src/Alexandria.UI/ViewModels/`
  - Controls: `src/Alexandria.UI/Controls/`
  - Services: `src/Alexandria.UI/Services/`

### Related Files

- [List specific files that need to be modified or referenced]
- [Include file paths from architecture documents]

### Component Reference

```csharp
// Paste relevant code examples from architecture documents
// For example, domain entity structure, handler pattern, etc.
```

## Implementation Steps

### 1. Create Feature Branch

```bash
git checkout -b story/[STORY_ID]-[brief-description]
# Example: git checkout -b story/DOM-P1-001-book-entity
```

### 2. Domain Layer (if applicable)

- [ ] Create/modify domain entities in `Entities/`
- [ ] Define value objects in `ValueObjects/`
- [ ] Add domain errors to `Errors/`
- [ ] Define repository interfaces in `Repositories/`
- [ ] Add domain services to `Services/`

### 3. Application Layer (if applicable)

- [ ] Create feature folder under `Features/`
- [ ] Create command/query record (e.g., `LoadBookCommand.cs`)
- [ ] Implement MediatR handler (e.g., `LoadBookHandler.cs`)
- [ ] Add FluentValidation validator (e.g., `LoadBookValidator.cs`)
- [ ] Define result type (e.g., `LoadBookResult.cs`)
- [ ] Use OneOf<TSuccess, DomainError> for return types

### 4. Infrastructure Layer (if applicable)

- [ ] Implement repository interfaces using LiteDB
- [ ] Add parser implementations for EPUB handling
- [ ] Configure caching with IMemoryCache/LiteDB
- [ ] Implement search indexing with Lucene.NET
- [ ] Add any external service integrations

### 5. Frontend Layer (if applicable)

- [ ] Create ViewModel inheriting from `ViewModelBase`
- [ ] Implement ReactiveUI commands and properties
- [ ] Create AXAML view file
- [ ] Add custom controls if needed
- [ ] Implement services (navigation, theme, etc.)
- [ ] Wire up data binding

### 6. Storage Layer (LiteDB)

- [ ] Define entity models for LiteDB collections
- [ ] Create/update repository implementations
- [ ] Add indexes for query optimization
- [ ] Implement FileStorage for binary data (images)
- [ ] Add database migrations if schema changes

### 7. Write Automated Tests

**Follow [Testing_Guidelines.md](./Testing_Guidelines.md) for test implementation patterns and best practices**

- [ ] Unit tests for domain logic (if applicable)
- [ ] Unit tests for application handlers
- [ ] Integration tests for infrastructure components
- [ ] UI tests using Avalonia.Headless (for frontend)
- [ ] Test both success and error scenarios
- [ ] Ensure all edge cases are covered
- [ ] Maintain or improve code coverage

Example test structure (using xUnit):

```csharp
// Domain unit test
[Fact]
public void Book_Should_CalculateCorrectWordCount()
{
    // Arrange
    var book = new Book(/* ... */);

    // Act
    var wordCount = book.GetWordCount();

    // Assert
    wordCount.Should().Be(expectedCount);
}

// Application handler test
[Fact]
public async Task LoadBookHandler_Should_ReturnBook_WhenFileExists()
{
    // Arrange
    var handler = new LoadBookHandler(/* mocked dependencies */);
    var command = new LoadBookCommand("path/to/book.epub");

    // Act
    var result = await handler.HandleAsync(command);

    // Assert
    result.IsT0.Should().BeTrue();
    result.AsT0.Should().NotBeNull();
}

// Infrastructure integration test
[Fact]
public async Task LiteDbBookRepository_Should_PersistAndRetrieveBook()
{
    // Use test database
    using var db = new LiteDatabase(":memory:");
    var repository = new LiteDbBookRepository(db);

    // Act & Assert
}

// Frontend UI test
[Fact]
public async Task ReaderViewModel_Should_LoadBook_WhenOpenCommandExecuted()
{
    // Using Avalonia.Headless for testing
}
```

### 8. Verify & Test

```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Alexandria.Domain.Tests

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run benchmarks (if applicable)
dotnet run -c Release --project tests/Alexandria.Benchmarks

# Manual testing for UI components
dotnet run --project src/Alexandria.UI
```

### 9. Create Pull Request

```bash
git add .
git commit -m "feat: [STORY_ID] - [Brief description of what was implemented]

- Implements [key feature/component]
- Adds [tests/validation]
- Follows [architecture pattern]

Closes #[issue_number]"

git push origin story/[STORY_ID]-[brief-description]

# Create PR via GitHub CLI
gh pr create \
  --title "[STORY_ID] - [Title]" \
  --body "## Description
  Implements [description]

  ## Changes
  - [List key changes]

  ## Testing
  - [How it was tested]

  Closes #[issue_number]" \
  --label "[appropriate labels]" \
  --milestone "[current phase]"
```

## Dependencies

- **Blocked By**: [List GitHub issue numbers that must be completed first, e.g., #101, #102]
- **Blocks**: [List GitHub issue numbers that depend on this one, e.g., #201, #202]

## Notes

[Any additional context, gotchas, or important information specific to Alexandria]

Common considerations:
- EPUB 2.0 vs 3.0 format differences
- Large file streaming for EPUBs > 50MB
- LiteDB FileStorage for cover images
- Memory management for caching
- Cross-platform UI considerations

## Definition of Done

- [ ] All acceptance criteria met
- [ ] Code follows Alexandria architecture patterns:
  - [ ] DDD principles for domain layer
  - [ ] Vertical slices for features
  - [ ] MVVM for frontend
  - [ ] Clean architecture boundaries maintained
- [ ] Storage implemented using LiteDB where applicable
- [ ] Appropriate caching strategy implemented
- [ ] Error handling with OneOf pattern (backend)
- [ ] **Unit tests written for domain logic**
- [ ] **Unit tests written for application handlers**
- [ ] **Integration tests for infrastructure components**
- [ ] **UI tests for frontend components (if applicable)**
- [ ] **All tests pass (new and existing)**
- [ ] **Code coverage maintained or improved**
- [ ] Solution builds without warnings
- [ ] Code follows C# coding standards
- [ ] XML documentation for public APIs
- [ ] Pull request opened with clear description
- [ ] Code review approved
- [ ] Merged to main branch
- [ ] GitHub issue closed