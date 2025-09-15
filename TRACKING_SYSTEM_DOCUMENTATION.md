# GitHub Issues Story Tracking System Documentation

## Overview

A dependency-aware, atomic story tracking system for managing the Alexandria EPUB Reader implementation using GitHub Issues. This system breaks down the architecture into small, self-contained units of work with clear dependencies, ensuring developers always know what to work on next.

## Core Philosophy

### 1. Atomic Stories

- Each story represents the smallest possible unit of shippable work
- Stories are self-contained with all context needed for implementation
- No story should take more than 1-2 days to complete
- Each story results in a pull request

### 2. Dependency-Driven Workflow

- Stories explicitly declare dependencies using GitHub's linking features
- GitHub Projects automatically track what can be worked on based on completed dependencies
- Prevents wasted work on blocked tasks
- Creates natural implementation order

### 3. Complete Context

- Each issue contains everything needed to implement it
- Links to architecture documents for reference
- Includes specific implementation requirements, code examples, test cases
- References specific files and line numbers from architecture

## System Components

### 1. GitHub Organization Structure

```
GitHub Repository
├── Issues/                     # All stories as GitHub Issues
│   ├── Labels/                # Categorization
│   │   ├── domain            # Domain layer stories
│   │   ├── application       # Application layer stories
│   │   ├── infrastructure   # Infrastructure layer stories
│   │   ├── frontend         # AvaloniaUI frontend stories
│   │   ├── vertical-slice   # Complete feature slices
│   │   ├── blocked          # Stories waiting on dependencies
│   │   ├── ready            # Stories ready to start
│   │   └── in-progress      # Active development
│   ├── Milestones/           # Implementation phases
│   │   ├── Phase 1: Core Infrastructure
│   │   ├── Phase 2: Backend Completion
│   │   ├── Phase 3: Frontend Foundation
│   │   ├── Phase 4: Library Management
│   │   ├── Phase 5: Reading Experience
│   │   ├── Phase 6: Polish & Optimization
│   │   └── Phase 7: Advanced Features
│   └── Projects/             # Kanban boards for tracking
│       └── Alexandria Implementation Board
├── .github/
│   └── ISSUE_TEMPLATE/
│       └── story.md          # Story template
└── Architecture Docs/
    ├── ALEXANDRIA-MASTER-ARCHITECTURE.md
    ├── ALEXANDRIA-SYSTEM-ARCHITECTURE.md
    └── AVALONIA-FRONTEND-ARCHITECTURE.md
```

### 2. Issue Structure

Each story issue follows this template:

```markdown
---
name: Story
about: Implementation story for Alexandria
title: '[STORY_ID] - [TITLE]'
labels: 'story', 'domain|application|infrastructure|frontend'
assignees: ''
---

## Overview
[Brief description aligned with architecture documents]

## Architecture References
- [ALEXANDRIA-MASTER-ARCHITECTURE.md](link) - Section X.Y
- [ALEXANDRIA-SYSTEM-ARCHITECTURE.md](link) - Component Z
- [AVALONIA-FRONTEND-ARCHITECTURE.md](link) - Feature A

## Acceptance Criteria
- [ ] [Specific measurable outcome 1]
- [ ] [Specific measurable outcome 2]
- [ ] All tests pass
- [ ] Code follows architecture patterns
- [ ] PR opened and reviewed

## Dependencies
**Blocked by:** #issue1, #issue2
**Blocks:** #issue3, #issue4

[Full implementation details...]
```

### 3. Story Identification Pattern

**Format**: `[CATEGORY]-[PHASE]-[NUMBER]`

Examples:

- `DOM-P1-001`: Domain layer, Phase 1, Story 001
- `APP-P2-015`: Application layer, Phase 2, Story 015
- `UI-P3-008`: Frontend UI, Phase 3, Story 008
- `VS-P2-003`: Vertical Slice, Phase 2, Story 003

### 4. GitHub Labels System

#### Layer Labels

- `domain` - Domain layer (entities, value objects)
- `application` - Application layer (handlers, services)
- `infrastructure` - Infrastructure (parsers, persistence)
- `frontend` - AvaloniaUI frontend
- `vertical-slice` - Complete feature implementation

#### Status Labels

- `ready` - No blockers, ready to start
- `blocked` - Waiting on dependencies
- `in-progress` - Active development
- `review` - In PR review
- `done` - Merged to main

#### Priority Labels

- `critical-path` - Must complete for next phase
- `high-priority` - Important but not blocking
- `nice-to-have` - Can be deferred

#### Technical Labels

- `epub-parsing` - EPUB format handling
- `litedb` - Database implementation
- `search` - Lucene.NET search
- `pagination` - Reading experience
- `testing` - Test implementation

### 5. Tracking Tools

#### A. GitHub CLI Commands

```bash
# Find next available story
gh issue list --label "ready" --label "-in-progress" --limit 1

# Check blocked stories
gh issue list --label "blocked"

# View stories for current phase
gh issue list --milestone "Phase 2: Backend Completion"

# Create new story
gh issue create --template story.md --title "[DOM-P2-001] - Create Book Entity"

# Link dependencies
gh issue edit 123 --add-label "blocked"
gh issue comment 123 --body "Blocked by #122"
```

#### B. GitHub Projects Board

Columns:

1. **Backlog** - All stories not yet started
2. **Ready** - Dependencies met, ready to start
3. **In Progress** - Active development
4. **In Review** - PR opened
5. **Done** - Merged to main

Automation:

- Issues automatically move based on labels
- PR linking moves to "In Review"
- PR merge moves to "Done"

#### C. Progress Dashboard

Using GitHub Insights and Projects:

- Burndown charts per milestone
- Velocity tracking
- Dependency visualization
- Blocked work identification

## Key Features

### 1. Dependency Management

GitHub's built-in features:

- Issue linking with `#123` references
- "Blocked by" and "Blocks" in issue body
- Dependency graph in Projects
- Automatic status updates when blockers resolve

### 2. Branch Strategy

Every story includes branch naming:

```bash
git checkout -b story/[STORY_ID]-[brief-description]
# Example: story/DOM-P1-001-create-book-entity
```

PR title format:

```
[STORY_ID] - Story Title
# Example: [DOM-P1-001] - Create Book Entity
```

### 3. Architecture Enforcement

Each story must reference:

- Relevant architecture document sections
- Implementation patterns to follow
- Specific component diagrams
- Layer boundaries to respect

Alexandria-specific patterns:

- DDD for domain layer
- Vertical Slices with MediatR
- AvaloniaUI MVVM for frontend
- LiteDB for all persistence

### 4. Progress Tracking

Using GitHub's native features:

- Issue state (Open/Closed)
- Milestone progress bars
- Project board metrics
- Label-based filtering

### 5. Self-Contained Context

Each issue contains:

- Links to architecture documents
- Implementation location (exact paths)
- Code examples from architecture
- Test requirements
- Definition of Done

## Implementation Guide

### Setting Up the System

1. **Create GitHub Labels**

```bash
# Layer labels
gh label create "domain" --description "Domain layer" --color "0E8A16"
gh label create "application" --description "Application layer" --color "1D76DB"
gh label create "infrastructure" --description "Infrastructure layer" --color "5319E7"
gh label create "frontend" --description "Frontend UI" --color "B60205"

# Status labels
gh label create "ready" --description "Ready to start" --color "0E8A16"
gh label create "blocked" --description "Blocked by dependencies" --color "D93F0B"
gh label create "in-progress" --description "Active development" --color "FBCA04"
```

2. **Create Milestones**

```bash
gh milestone create --title "Phase 1: Core Infrastructure" --description "Domain entities, basic parsing"
gh milestone create --title "Phase 2: Backend Completion" --description "All backend features"
gh milestone create --title "Phase 3: Frontend Foundation" --description "Basic UI implementation"
# ... etc
```

3. **Create Issue Template**

Save as `.github/ISSUE_TEMPLATE/story.md`

4. **Setup GitHub Project**

- Create new Project board
- Add automation rules
- Configure columns

### Creating a New Story

1. **Identify Atomic Unit**

Review architecture documents:

- Can it be implemented in 1-2 days?
- Does it align with a single component?
- Clear acceptance criteria?

2. **Determine Dependencies**

Check architecture diagrams:

- What components must exist first?
- What does this enable?

3. **Create Issue**

```bash
gh issue create \
  --title "[DOM-P1-001] - Create Book Entity" \
  --body "$(cat story-content.md)" \
  --label "domain,ready" \
  --milestone "Phase 1: Core Infrastructure"
```

4. **Link Dependencies**

```bash
# In issue body or comments
Blocked by: #100, #101
Blocks: #150, #151
```

### Working on a Story

1. **Find Next Story**

```bash
# Check what's ready in current phase
gh issue list --label "ready" --milestone "Phase 2: Backend Completion"

# Or check project board
gh project list
```

2. **Claim Story**

```bash
# Assign to yourself
gh issue edit 123 --add-assignee @me

# Add in-progress label
gh issue edit 123 --add-label "in-progress" --remove-label "ready"
```

3. **Create Branch**

```bash
# Follow naming convention
git checkout -b story/DOM-P1-001-book-entity
```

4. **Implement**

- Follow architecture documents
- Check implementation location
- Write required tests
- Ensure all builds

5. **Create PR**

```bash
# Commit with story reference
git commit -m "feat: [DOM-P1-001] Create Book entity with value objects

- Implements Book aggregate root
- Adds Author and BookTitle value objects
- Includes unit tests"

# Create PR linking to issue
gh pr create \
  --title "[DOM-P1-001] - Create Book Entity" \
  --body "Closes #123" \
  --base main
```

## Benefits

### For Individual Developers

- Clear next action via GitHub queries
- Complete context in issues
- Dependencies visible
- Architecture alignment ensured

### For Teams

- Parallel work on unblocked stories
- Clear communication via issue comments
- Consistent implementation via templates
- Easy onboarding with labeled issues

### For Project Management

- Real-time progress in GitHub Projects
- Milestone tracking
- Velocity metrics
- Risk identification via blocked labels

## Alexandria-Specific Guidelines

### Story Categories by Layer

#### Domain Stories

- Create entities (Book, Chapter, Navigation)
- Implement value objects (Author, BookTitle)
- Define repository interfaces
- Add domain services

#### Application Stories

- Create vertical slice features
- Implement MediatR handlers
- Add validators
- Define application services

#### Infrastructure Stories

- Implement EPUB parsers
- Create LiteDB repositories
- Add Lucene.NET search
- Implement caching

#### Frontend Stories

- Create ViewModels
- Implement controls
- Add services
- Build views

### Phase Alignment

Stories must align with roadmap phases:

1. **Phase 1**: Core domain and basic parsing
2. **Phase 2**: Complete backend features
3. **Phase 3**: Basic frontend
4. **Phase 4**: Library management
5. **Phase 5**: Reading experience
6. **Phase 6**: Performance optimization
7. **Phase 7**: Advanced features

### Testing Requirements

Each story must include:

- TUnit tests for domain logic
- Integration tests for infrastructure
- UI tests for frontend (Avalonia.Headless)
- All existing tests must pass

## Anti-Patterns to Avoid

1. **Stories Too Large**

- If touches multiple layers, split by layer
- If multiple features, create vertical slices

2. **Missing Architecture Links**

- Always reference specific sections
- Include component diagrams
- Note patterns to follow

3. **Incomplete Dependencies**

- Use GitHub's linking features
- Update when discovered
- Check architecture for implicit dependencies

4. **Skipping GitHub Features**

- Use labels consistently
- Link issues properly
- Update milestone assignment

## Maintenance

### Regular Updates

- Review blocked issues weekly
- Update milestones after phase completion
- Archive completed milestones
- Adjust priorities based on progress

### Issue Refinement

- Split issues that prove too large
- Add discovered dependencies
- Update architecture references
- Share learnings in comments

### System Evolution

- Add labels as patterns emerge
- Create new templates for common stories
- Update automation rules
- Document decisions in issues

## Conclusion

This GitHub Issues-based tracking system leverages GitHub's native features while maintaining the atomic story philosophy. By using issues, labels, milestones, and projects, we get powerful tracking and visualization while keeping all project management within the development platform.

The system ensures architectural alignment through mandatory references to Alexandria's architecture documents, maintains clear dependencies through issue linking, and provides excellent visibility through GitHub's built-in analytics.
