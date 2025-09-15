# Alexandria.Domain

## Overview
The Domain layer contains the core business logic and entities of the Alexandria EPUB Parser. This layer has **NO external dependencies** and represents the heart of the application following Domain-Driven Design principles.

## Structure

- **`/Entities`** - Core domain entities (Book, Chapter, Author, etc.)
- **`/ValueObjects`** - Immutable value objects (ContentMetrics, BookMetadata, ISBN, etc.)
- **`/Services`** - Domain service interfaces (IContentAnalyzer, etc.)
- **`/Exceptions`** - Domain-specific exceptions

## Key Principles

1. **No External Dependencies** - This project must remain pure C# with no NuGet packages
2. **Rich Domain Models** - Entities contain business logic, not just data
3. **Immutable Value Objects** - Use value objects for concepts without identity
4. **Domain Services** - For operations that don't naturally fit in an entity

## Entities

- `Book` - Represents an EPUB book with chapters and metadata
- `Chapter` - Individual chapter within a book
- `Author` - Book author information
- `Publisher` - Publisher details
- `TableOfContents` - Book navigation structure

## Value Objects

- `ContentMetrics` - Word count, reading time, complexity metrics
- `BookMetadata` - Title, description, language, publication date
- `ISBN` - International Standard Book Number
- `ReadingProgress` - Current position and percentage complete

## Domain Services

- `IContentAnalyzer` - Interface for analyzing text content (word count, reading time, etc.)
- `IBookValidator` - Interface for validating EPUB structure and content

## Usage

Domain entities and services are used by the Application layer to implement use cases. The Infrastructure layer provides concrete implementations of domain service interfaces.

```csharp
// Example usage
var book = new Book(title: "Example Book", author: new Author("John Doe"));
book.AddChapter(new Chapter("Chapter 1", content));

// Domain services are injected via interfaces
IContentAnalyzer analyzer = // ... provided by Infrastructure
var metrics = analyzer.AnalyzeChapter(book.Chapters[0]);
```

## Dependencies

- Target Framework: .NET Standard 2.1 (for maximum compatibility)
- No external NuGet packages

## Migration Notes

This project is being populated by migrating code from `Alexandria.Parser/Domain/*`. During the migration period, both locations may contain domain code, but all new domain logic should be added here.