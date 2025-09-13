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

### EPUB Parsing Flow
1. **Entry Point**: `Parser.OpenBookAsync()` accepts a file path to an EPUB file
2. **Container Processing**: Reads `META-INF/container.xml` to locate the content.opf file
3. **Package Processing**: Deserializes content.opf to extract metadata, manifest, and spine
4. **Content Extraction**: Reads individual chapter files from the manifest and returns a `Book` object

### Key Components
- **Parser**: Static class containing the main parsing logic using ZipArchive for EPUB extraction
- **Models/Book**: Simple data structure containing titles, authors, and chapters as string lists
- **Models/Container**: XML-serializable model for container.xml
- **Models/Content/Package**: XML-serializable model for content.opf including Metadata, ManifestItem, SpineItemRef, and GuideReference

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