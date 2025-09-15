# Alexandria Source Projects

This directory contains the core projects following Clean Architecture principles:

## Project Structure

```
src/
├── Alexandria.Domain/          # Core business logic (no dependencies)
├── Alexandria.Application/     # Use cases and application logic
└── Alexandria.Infrastructure/  # External concerns and implementations
```

## Dependency Flow

```
Alexandria.Infrastructure
    ↓ depends on
Alexandria.Application
    ↓ depends on
Alexandria.Domain (no dependencies)
```

## Getting Started

1. **Domain Layer** - Start here to understand the core business entities and rules
2. **Application Layer** - Review use cases and how the application orchestrates domain logic
3. **Infrastructure Layer** - See how external concerns are implemented

## Building

From the root directory:
```bash
dotnet build
```

## Testing

Each project should have corresponding test projects:
- Alexandria.Domain.Tests
- Alexandria.Application.Tests
- Alexandria.Infrastructure.Tests

## Migration Status

Code is being migrated from Alexandria.Parser to these new projects. During the transition:
- Alexandria.Parser references all three new projects
- New code should be added to the appropriate new project
- Existing code will be gradually moved