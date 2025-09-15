# Alexandria.Application

## Overview
The Application layer contains the use cases and application logic for the Alexandria EPUB Parser. This layer orchestrates the flow of data between the Domain and Infrastructure layers, implementing the application's business rules using the CQRS pattern with MediatR.

## Structure

- **`/Features`** - Vertical slice features organized by use case
- **`/Common`** - Shared application logic, DTOs, and mapping profiles
- **`/Interfaces`** - Application service interfaces that Infrastructure will implement

## Key Principles

1. **Vertical Slice Architecture** - Each feature is self-contained with its own command/query, handler, validator, and result
2. **CQRS Pattern** - Separate commands (write) from queries (read)
3. **No Infrastructure Dependencies** - Only depends on Domain layer
4. **Use Case Driven** - Each feature represents a specific use case

## Features

Each feature follows this structure:
```
Features/
├── [FeatureName]/
│   ├── [Feature]Command.cs     # Input contract
│   ├── [Feature]Handler.cs     # Business logic orchestration
│   ├── [Feature]Validator.cs   # Input validation (FluentValidation)
│   └── [Feature]Result.cs      # Output contract (OneOf pattern)
```

### Planned Features

- `LoadBook` - Load and parse an EPUB file
- `AnalyzeContent` - Analyze chapter content for metrics
- `SearchContent` - Search within book content
- `ExtractResources` - Extract images, stylesheets, fonts
- `ExportBook` - Export to different formats
- `TrackReadingProgress` - Save and restore reading position
- `ManageBookmarks` - CRUD operations for bookmarks

## Usage

```csharp
// Example command usage with MediatR
public class LoadBookCommand : IRequest<OneOf<Book, ValidationError, FileNotFoundError>>
{
    public string FilePath { get; set; }
}

public class LoadBookHandler : IRequestHandler<LoadBookCommand, OneOf<Book, ValidationError, FileNotFoundError>>
{
    private readonly IBookRepository _repository;
    private readonly IContentAnalyzer _analyzer;

    public LoadBookHandler(IBookRepository repository, IContentAnalyzer analyzer)
    {
        _repository = repository;
        _analyzer = analyzer;
    }

    public async Task<OneOf<Book, ValidationError, FileNotFoundError>> Handle(
        LoadBookCommand request,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

## Dependencies

- Target Framework: .NET 9.0
- NuGet Packages:
  - MediatR - For CQRS implementation
  - FluentValidation - For request validation
  - AutoMapper - For object mapping
  - OneOf - For discriminated unions/result types

## Project References

- Alexandria.Domain - For domain entities and interfaces

## Migration Notes

Application logic is being migrated from various services and handlers in Alexandria.Parser. New features should be implemented as vertical slices in the Features folder.