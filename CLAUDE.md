# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## ⚠️ CRITICAL: Before Making ANY Code Changes

**MANDATORY**: When working with external packages or encountering compilation errors:

1. **ALWAYS use context7 MCP** for NuGet package documentation
2. **NEVER guess** at API signatures or method names
3. **IMMEDIATELY check** context7 when you see "method not found" or "cannot convert type" errors

Example workflow:
```
Compilation error → Is it package-related? → Use context7 MCP
Need to use a NuGet package? → Check context7 FIRST
Unsure about API syntax? → Use context7 for current docs
```

## Project Overview

Alexandria is a .NET-based EPUB parser library written in C#. The solution consists of two projects:
- **Alexandria.Parser**: Core library for parsing EPUB files (targets .NET Standard 2.1)
- **Alexandria.ConsoleTest**: Console application for testing the parser (targets .NET 5.0)

## Build and Development Commands

```bash
# Build the entire solution
dotnet build

# Build specific project
dotnet build Alexandria.Parser/Alexandria.Parser.csproj
dotnet build Alexandria.ConsoleTest/Alexandria.ConsoleTest.csproj

# Run the console test application
dotnet run --project Alexandria.ConsoleTest/Alexandria.ConsoleTest.csproj

# Run tests (IMPORTANT: Never use --no-build)
dotnet test

# Run a single test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Format code
dotnet format

# Clean build artifacts
dotnet clean

# Restore dependencies
dotnet restore
```

## Architecture

### Target Architecture (Vertical Slice + DDD)

**We are migrating to a Vertical Slice Architecture with Domain-Driven Design**. New features should follow this pattern:

#### Feature Organization (Vertical Slices)
```
Features/
├── [FeatureName]/
│   ├── [Feature]Command.cs     # Input contract
│   ├── [Feature]Handler.cs     # Business logic orchestration
│   ├── [Feature]Validator.cs   # Input validation
│   └── [Feature]Result.cs      # Output contract
```

#### Domain Layer Rules
- **Rich Domain Models**: Entities contain business logic, not just data
- **Domain Services**: For cross-entity operations (e.g., `IContentAnalyzer`)
- **Value Objects**: Immutable objects representing concepts (e.g., `BookMetadata`, `ContentMetrics`)
- **No Infrastructure Dependencies**: Domain layer should be pure business logic

#### Content Processing
**ALL content analysis (word count, reading time, HTML processing) must go through the `IContentAnalyzer` domain service**. Do not add these calculations to entities directly.

Example:
```csharp
// WRONG - Don't add content processing to entities
public class Chapter
{
    public int GetWordCount() { /* DON'T DO THIS */ }
}

// RIGHT - Use domain service
public interface IContentAnalyzer
{
    ContentMetrics AnalyzeChapter(Chapter chapter);
}
```

### Current Architecture (Being Phased Out)

### EPUB Parsing Flow
1. **Entry Point**: `Parser.OpenBookAsync()` accepts a file path to an EPUB file
2. **Container Processing**: Reads `META-INF/container.xml` to locate the content.opf file
3. **Package Processing**: Deserializes content.opf to extract metadata, manifest, and spine
4. **Content Extraction**: Reads individual chapter files from the manifest and returns a `Book` object

### Legacy Components (To Be Refactored)
- **Parser**: Static class containing the main parsing logic using ZipArchive for EPUB extraction
- **Models/Book**: Simple data structure (DEPRECATED - use Domain.Entities.Book)
- **Models/Container**: XML-serializable model for container.xml
- **Models/Content/Package**: XML-serializable model for content.opf

### XML Deserialization Strategy
The codebase uses `System.Xml.Serialization` with strongly-typed models decorated with XML attributes to parse EPUB metadata files. Each EPUB structure component has a corresponding C# model class.

## Important Context

- EPUB files are ZIP archives containing XML metadata and HTML/XHTML content
- The parser currently loads all chapter content into memory as strings
- The ConsoleTest project contains experimental code for splitting chapters into pages based on character count
- File paths in the console test are currently hardcoded to Windows-specific paths

## Development Workflow

### Git Workflow
- **Never commit directly to main branch**
- Create feature branches for all changes
- Use conventional commit messages (fix:, feat:, test:, docs:, refactor:)
- Ensure all tests pass before marking tasks complete

### Task Management
- Break complex tasks into smaller, trackable items
- Mark tasks as completed only when fully done
- If blocked or unable to complete, keep task as in_progress and document blockers

### Code Quality
- Follow existing code patterns and conventions
- Maintain strong typing and clear interfaces
- Write modular, testable code
- Keep files focused and under 500 lines when possible

### Testing
- Write tests before implementing features when applicable
- Never run `dotnet test` with `--no-build` flag
- Ensure comprehensive test coverage for critical paths
- Fix failing tests immediately

### When Adding New Projects
- Always add new projects to the solution file using `dotnet sln add`
- Maintain consistent project structure and naming conventions

## Architectural Guidelines

### When Adding New Features
1. **Create a new feature slice** in the `Features/` folder
2. **Use MediatR pattern**: Command/Query → Handler → Result
3. **Keep domain logic pure**: No infrastructure dependencies in domain layer
4. **Use domain services** for cross-cutting concerns (e.g., content analysis)
5. **Avoid duplication**: Check if functionality already exists before creating new implementations

### Code Organization Principles
- **Vertical Slices over Layers**: Organize by feature, not by technical concern
- **Domain at the Center**: Dependencies point inward toward the domain
- **Single Source of Truth**: One implementation for each business capability
- **Explicit over Implicit**: Clear contracts and boundaries between components

### Migration Notes
- **Legacy code exists**: Some older patterns are being phased out
- **Incremental migration**: New features use new architecture, old features migrate gradually
- **Preserve compatibility**: External APIs remain stable during migration
- **Use Domain.Entities.Book**: The `Models.Book` class is deprecated

### Anti-patterns to Avoid
- ❌ Adding content processing logic directly to entities
- ❌ Creating duplicate implementations of the same functionality
- ❌ Mixing infrastructure concerns with domain logic
- ❌ Using the legacy `Models.Book` class
- ❌ Scattering business logic across multiple service classes