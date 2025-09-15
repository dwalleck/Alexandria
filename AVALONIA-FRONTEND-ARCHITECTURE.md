# Alexandria EPUB Reader - AvaloniaUI Frontend Architecture

## Overview
This document outlines the complete frontend architecture for the Alexandria EPUB Reader using AvaloniaUI, ensuring a modern, responsive, and cross-platform reading experience.

## Why AvaloniaUI?

- **True Cross-Platform**: Runs on Windows, macOS, Linux, iOS, Android, and WebAssembly
- **XAML-Based**: Familiar to WPF developers, declarative UI
- **MVVM Support**: Built-in support for Model-View-ViewModel pattern
- **Performance**: Hardware-accelerated rendering
- **Theming**: Comprehensive styling and theming support
- **.NET Native**: Works seamlessly with our C# backend

## Architecture Overview

```
Alexandria.UI/
├── App.axaml                           # Application resources and styles
├── App.axaml.cs                        # Application startup
├── Program.cs                          # Entry point
├── ViewLocator.cs                      # View resolution
├── Views/                              # XAML Views
│   ├── MainWindow.axaml
│   ├── ReaderView.axaml
│   ├── LibraryView.axaml
│   ├── SettingsView.axaml
│   └── Controls/
│       ├── BookCard.axaml
│       ├── ChapterPanel.axaml
│       ├── NavigationPane.axaml
│       ├── SearchPanel.axaml
│       └── ReadingControls.axaml
├── ViewModels/                        # MVVM ViewModels
│   ├── MainWindowViewModel.cs
│   ├── ReaderViewModel.cs
│   ├── LibraryViewModel.cs
│   ├── SettingsViewModel.cs
│   └── ViewModelBase.cs
├── Models/                             # UI Models
│   ├── BookDisplayModel.cs
│   ├── ChapterDisplayModel.cs
│   └── SearchResultModel.cs
├── Services/                           # UI Services
│   ├── INavigationService.cs
│   ├── IThemeService.cs
│   ├── IDialogService.cs
│   └── IReadingSessionService.cs
├── Converters/                         # Value Converters
│   ├── ProgressToPercentageConverter.cs
│   └── HtmlToFlowDocumentConverter.cs
├── Controls/                           # Custom Controls
│   ├── HtmlViewer.cs
│   ├── TouchScrollViewer.cs
│   └── AnnotationLayer.cs
├── Themes/                             # Styling
│   ├── DefaultTheme.axaml
│   ├── DarkTheme.axaml
│   ├── SepiaTheme.axaml
│   └── HighContrastTheme.axaml
└── Assets/                             # Images, Fonts, Icons
    ├── Icons/
    ├── Fonts/
    └── Images/
```

## Core Components

### 1. Main Window Layout

```xml
<!-- MainWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Alexandria EPUB Reader"
        Width="1200" Height="800">

    <DockPanel>
        <!-- Top Menu Bar -->
        <Menu DockPanel.Dock="Top">
            <MenuItem Header="_File">
                <MenuItem Header="_Open Book" Command="{Binding OpenBookCommand}"/>
                <MenuItem Header="_Import Library" Command="{Binding ImportLibraryCommand}"/>
                <Separator/>
                <MenuItem Header="_Exit" Command="{Binding ExitCommand}"/>
            </MenuItem>
            <MenuItem Header="_View">
                <MenuItem Header="_Library" Command="{Binding ShowLibraryCommand}"/>
                <MenuItem Header="_Reader" Command="{Binding ShowReaderCommand}"/>
                <MenuItem Header="_Full Screen" Command="{Binding ToggleFullScreenCommand}"/>
            </MenuItem>
        </Menu>

        <!-- Main Content Area -->
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="250" MinWidth="200"/>
                <ColumnDefinition Width="5"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="300" MinWidth="0"/>
            </Grid.ColumnDefinitions>

            <!-- Navigation Pane (Collapsible) -->
            <Border Grid.Column="0" Classes="navigation-pane">
                <ContentControl Content="{Binding NavigationContent}"/>
            </Border>

            <!-- Splitter -->
            <GridSplitter Grid.Column="1"/>

            <!-- Main Reading Area -->
            <Border Grid.Column="2" Classes="reading-area">
                <ContentControl Content="{Binding MainContent}"/>
            </Border>

            <!-- Side Panel (Search/Notes/Bookmarks) -->
            <Border Grid.Column="3" Classes="side-panel"
                    IsVisible="{Binding IsSidePanelVisible}">
                <ContentControl Content="{Binding SidePanelContent}"/>
            </Border>
        </Grid>

        <!-- Status Bar -->
        <Border DockPanel.Dock="Bottom" Classes="status-bar">
            <StatusBar/>
        </Border>
    </DockPanel>
</Window>
```

### 2. Reader View Component

```xml
<!-- ReaderView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:Alexandria.UI.Controls">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Reading Toolbar -->
        <ToolBar Grid.Row="0">
            <Button Command="{Binding PreviousChapterCommand}">
                <PathIcon Data="{StaticResource PreviousIcon}"/>
            </Button>
            <Button Command="{Binding NextChapterCommand}">
                <PathIcon Data="{StaticResource NextIcon}"/>
            </Button>
            <Separator/>

            <!-- Font Controls -->
            <Button Command="{Binding DecreaseFontCommand}">A-</Button>
            <TextBlock Text="{Binding FontSize}" VerticalAlignment="Center"/>
            <Button Command="{Binding IncreaseFontCommand}">A+</Button>
            <Separator/>

            <!-- Theme Selector -->
            <ComboBox Items="{Binding Themes}"
                      SelectedItem="{Binding SelectedTheme}">
                <ComboBox.ItemTemplate>
                    <DataTemplate>
                        <StackPanel Orientation="Horizontal">
                            <Rectangle Width="20" Height="20"
                                      Fill="{Binding PreviewColor}"/>
                            <TextBlock Text="{Binding Name}" Margin="5,0"/>
                        </StackPanel>
                    </DataTemplate>
                </ComboBox.ItemTemplate>
            </ComboBox>

            <!-- Search -->
            <TextBox Watermark="Search..."
                     Text="{Binding SearchQuery}"
                     Width="200"/>
            <Button Command="{Binding SearchCommand}">
                <PathIcon Data="{StaticResource SearchIcon}"/>
            </Button>
        </ToolBar>

        <!-- Content Display Area -->
        <ScrollViewer Grid.Row="1"
                      Name="ContentScroller"
                      VerticalScrollBarVisibility="Auto">
            <controls:HtmlViewer x:Name="HtmlContent"
                                Html="{Binding ChapterHtml}"
                                Theme="{Binding CurrentTheme}"
                                FontSize="{Binding FontSize}"
                                FontFamily="{Binding FontFamily}"
                                LineHeight="{Binding LineHeight}"
                                HighlightedText="{Binding SearchHighlight}"/>
        </ScrollViewer>

        <!-- Reading Progress Bar -->
        <Grid Grid.Row="2" Classes="progress-bar">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <TextBlock Grid.Column="0"
                       Text="{Binding CurrentLocation}"
                       Margin="10,5"/>

            <ProgressBar Grid.Column="1"
                         Value="{Binding ReadingProgress}"
                         Maximum="100"/>

            <TextBlock Grid.Column="2"
                       Text="{Binding ProgressPercentage, StringFormat='{}{0:F0}%'}"
                       Margin="10,5"/>
        </Grid>
    </Grid>
</UserControl>
```

### 3. Library View Component

```xml
<!-- LibraryView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <DockPanel>
        <!-- Library Toolbar -->
        <ToolBar DockPanel.Dock="Top">
            <Button Command="{Binding AddBooksCommand}">
                <StackPanel Orientation="Horizontal">
                    <PathIcon Data="{StaticResource AddIcon}"/>
                    <TextBlock Text="Add Books" Margin="5,0"/>
                </StackPanel>
            </Button>

            <Separator/>

            <!-- View Mode Toggle -->
            <ToggleButton IsChecked="{Binding IsGridView}">
                <PathIcon Data="{StaticResource GridIcon}"/>
            </ToggleButton>
            <ToggleButton IsChecked="{Binding IsListView}">
                <PathIcon Data="{StaticResource ListIcon}"/>
            </ToggleButton>

            <Separator/>

            <!-- Sort Options -->
            <ComboBox Items="{Binding SortOptions}"
                      SelectedItem="{Binding SelectedSort}"/>

            <!-- Filter -->
            <TextBox Watermark="Filter books..."
                     Text="{Binding FilterText}"
                     Width="200"/>
        </ToolBar>

        <!-- Books Display -->
        <ScrollViewer>
            <ItemsControl Items="{Binding FilteredBooks}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <WrapPanel Orientation="Horizontal"/>
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>

                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <controls:BookCard
                            Title="{Binding Title}"
                            Author="{Binding Author}"
                            CoverImage="{Binding CoverImage}"
                            Progress="{Binding ReadingProgress}"
                            LastOpened="{Binding LastOpened}"
                            Command="{Binding $parent[ItemsControl].DataContext.OpenBookCommand}"
                            CommandParameter="{Binding}"/>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>
    </DockPanel>
</UserControl>
```

### 4. Custom HTML Viewer Control

```csharp
// Controls/HtmlViewer.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using System.Threading.Tasks;

namespace Alexandria.UI.Controls
{
    public class HtmlViewer : Control
    {
        public static readonly StyledProperty<string> HtmlProperty =
            AvaloniaProperty.Register<HtmlViewer, string>(nameof(Html));

        public static readonly StyledProperty<double> FontSizeProperty =
            AvaloniaProperty.Register<HtmlViewer, double>(nameof(FontSize), 16);

        public static readonly StyledProperty<string> ThemeProperty =
            AvaloniaProperty.Register<HtmlViewer, string>(nameof(Theme), "Default");

        public string Html
        {
            get => GetValue(HtmlProperty);
            set => SetValue(HtmlProperty, value);
        }

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public string Theme
        {
            get => GetValue(ThemeProperty);
            set => SetValue(ThemeProperty, value);
        }

        static HtmlViewer()
        {
            HtmlProperty.Changed.AddClassHandler<HtmlViewer>((x, e) => x.RenderHtml());
            FontSizeProperty.Changed.AddClassHandler<HtmlViewer>((x, e) => x.UpdateStyles());
            ThemeProperty.Changed.AddClassHandler<HtmlViewer>((x, e) => x.UpdateTheme());
        }

        private async void RenderHtml()
        {
            // Use AvaloniaUI's HTML rendering capabilities
            // Or integrate with a WebView-like component
            await RenderHtmlContent(Html);
        }

        private async Task RenderHtmlContent(string html)
        {
            // Implementation options:
            // 1. Use Avalonia.HtmlRenderer (pure .NET)
            // 2. Use platform-specific WebView
            // 3. Convert HTML to Avalonia controls

            // For pure Avalonia approach:
            var processedHtml = ProcessHtmlForDisplay(html);
            InvalidateVisual();
        }

        private string ProcessHtmlForDisplay(string html)
        {
            // Apply theme CSS
            var themeStyles = GetThemeStyles(Theme);

            // Wrap content with styled container
            return $@"
                <html>
                <head>
                    <style>
                        body {{
                            font-size: {FontSize}px;
                            {themeStyles}
                        }}
                    </style>
                </head>
                <body>
                    {html}
                </body>
                </html>";
        }

        private string GetThemeStyles(string theme)
        {
            return theme switch
            {
                "Dark" => "background: #1e1e1e; color: #e0e0e0;",
                "Sepia" => "background: #f4ecd8; color: #5c4033;",
                "HighContrast" => "background: #000; color: #fff;",
                _ => "background: #fff; color: #000;"
            };
        }

        public override void Render(DrawingContext context)
        {
            // Custom rendering logic if needed
            base.Render(context);
        }
    }
}
```

### 5. ViewModels

```csharp
// ViewModels/ReaderViewModel.cs
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Features.LoadBook;

namespace Alexandria.UI.ViewModels
{
    public class ReaderViewModel : ViewModelBase
    {
        private readonly ILoadBookHandler _loadBookHandler;
        private readonly IContentAnalyzer _contentAnalyzer;
        private readonly INavigationService _navigationService;
        private readonly IReadingSessionService _sessionService;

        private Book? _currentBook;
        private Chapter? _currentChapter;
        private string _chapterHtml = string.Empty;
        private double _fontSize = 16;
        private string _selectedTheme = "Default";
        private double _readingProgress;
        private string _searchQuery = string.Empty;

        public ReaderViewModel(
            ILoadBookHandler loadBookHandler,
            IContentAnalyzer contentAnalyzer,
            INavigationService navigationService,
            IReadingSessionService sessionService)
        {
            _loadBookHandler = loadBookHandler;
            _contentAnalyzer = contentAnalyzer;
            _navigationService = navigationService;
            _sessionService = sessionService;

            // Commands
            OpenBookCommand = ReactiveCommand.CreateFromTask(OpenBookAsync);
            NextChapterCommand = ReactiveCommand.Create(
                NavigateToNextChapter,
                this.WhenAnyValue(x => x.CurrentBook).Select(b => b != null));
            PreviousChapterCommand = ReactiveCommand.Create(
                NavigateToPreviousChapter,
                this.WhenAnyValue(x => x.CurrentBook).Select(b => b != null));
            IncreaseFontCommand = ReactiveCommand.Create(() => FontSize += 2);
            DecreaseFontCommand = ReactiveCommand.Create(() => FontSize -= 2);
            SearchCommand = ReactiveCommand.CreateFromTask(SearchInBookAsync);

            // Auto-save reading progress
            this.WhenAnyValue(x => x.CurrentChapter)
                .Where(c => c != null)
                .Throttle(TimeSpan.FromSeconds(5))
                .Subscribe(_ => SaveReadingProgress());
        }

        public Book? CurrentBook
        {
            get => _currentBook;
            private set => this.RaiseAndSetIfChanged(ref _currentBook, value);
        }

        public Chapter? CurrentChapter
        {
            get => _currentChapter;
            private set
            {
                this.RaiseAndSetIfChanged(ref _currentChapter, value);
                if (value != null)
                {
                    LoadChapterContent(value);
                    UpdateReadingProgress();
                }
            }
        }

        public string ChapterHtml
        {
            get => _chapterHtml;
            private set => this.RaiseAndSetIfChanged(ref _chapterHtml, value);
        }

        public double FontSize
        {
            get => _fontSize;
            set => this.RaiseAndSetIfChanged(ref _fontSize, value);
        }

        public string SelectedTheme
        {
            get => _selectedTheme;
            set => this.RaiseAndSetIfChanged(ref _selectedTheme, value);
        }

        public double ReadingProgress
        {
            get => _readingProgress;
            private set => this.RaiseAndSetIfChanged(ref _readingProgress, value);
        }

        public ReactiveCommand<Unit, Unit> OpenBookCommand { get; }
        public ReactiveCommand<Unit, Unit> NextChapterCommand { get; }
        public ReactiveCommand<Unit, Unit> PreviousChapterCommand { get; }
        public ReactiveCommand<Unit, Unit> IncreaseFontCommand { get; }
        public ReactiveCommand<Unit, Unit> DecreaseFontCommand { get; }
        public ReactiveCommand<Unit, Unit> SearchCommand { get; }

        private async Task OpenBookAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "EPUB Files", Extensions = { "epub" } },
                    new FileDialogFilter { Name = "All Files", Extensions = { "*" } }
                }
            };

            var result = await dialog.ShowAsync(_navigationService.MainWindow);
            if (result?.Length > 0)
            {
                var command = new LoadBookCommand(result[0]);
                var bookResult = await _loadBookHandler.HandleAsync(command);

                bookResult.Switch(
                    book =>
                    {
                        CurrentBook = book;
                        CurrentChapter = book.Chapters.FirstOrDefault();
                        _sessionService.StartSession(book);
                    },
                    error => ShowError($"Failed to load book: {error.Message}")
                );
            }
        }

        private void NavigateToNextChapter()
        {
            if (CurrentBook != null && CurrentChapter != null)
            {
                var nextChapter = CurrentBook.GetNextChapter(CurrentChapter);
                if (nextChapter != null)
                {
                    CurrentChapter = nextChapter;
                }
            }
        }

        private void NavigateToPreviousChapter()
        {
            if (CurrentBook != null && CurrentChapter != null)
            {
                var prevChapter = CurrentBook.GetPreviousChapter(CurrentChapter);
                if (prevChapter != null)
                {
                    CurrentChapter = prevChapter;
                }
            }
        }

        private async void LoadChapterContent(Chapter chapter)
        {
            // Process HTML for display
            var processedHtml = await _contentAnalyzer.ProcessForDisplayAsync(chapter.Content);
            ChapterHtml = processedHtml;
        }

        private void UpdateReadingProgress()
        {
            if (CurrentBook != null && CurrentChapter != null)
            {
                var currentIndex = CurrentBook.Chapters.IndexOf(CurrentChapter);
                ReadingProgress = (currentIndex + 1.0) / CurrentBook.Chapters.Count * 100;
            }
        }

        private async Task SearchInBookAsync()
        {
            if (string.IsNullOrWhiteSpace(_searchQuery) || CurrentBook == null)
                return;

            var results = await _searchService.SearchAsync(CurrentBook, _searchQuery);
            await _navigationService.ShowSearchResults(results);
        }

        private async void SaveReadingProgress()
        {
            if (CurrentBook != null && CurrentChapter != null)
            {
                await _sessionService.SaveProgressAsync(
                    CurrentBook.GetIsbn() ?? CurrentBook.Title.Value,
                    CurrentChapter.Id,
                    ReadingProgress);
            }
        }
    }
}
```

## Features & Components

### 1. Core Reading Features
- **HTML Rendering**: Custom HtmlViewer control or WebView integration
- **Pagination**: Support for both scrolling and paginated views
- **Text Selection**: Native text selection with context menu
- **Zoom Controls**: Font size adjustment with live reflow
- **Themes**: Light, Dark, Sepia, High Contrast
- **Reading Progress**: Visual progress bar and percentage

### 2. Navigation
- **Table of Contents**: Hierarchical chapter navigation
- **Bookmarks**: Save and navigate to bookmarks
- **History**: Back/forward navigation
- **Go to Page**: Direct navigation to specific locations

### 3. Search & Annotations
- **Full-Text Search**: Integration with Lucene.NET backend
- **Search Highlighting**: Visual highlighting of search results
- **Annotations**: Add notes and highlights
- **Export**: Export annotations as separate file

### 4. Library Management
- **Grid/List Views**: Toggle between display modes
- **Sorting**: By title, author, date added, last read
- **Filtering**: By author, tags, reading status
- **Collections**: Create custom book collections
- **Import**: Drag-and-drop or file dialog

### 5. Settings & Preferences
- **Reading Preferences**: Font, size, line spacing, margins
- **Theme Customization**: Create custom themes
- **Keyboard Shortcuts**: Customizable shortcuts
- **Sync Settings**: Cloud sync for preferences

## Platform-Specific Considerations

### Desktop (Windows/macOS/Linux)
- Full keyboard navigation support
- Context menus
- System tray integration
- File associations

### Mobile (iOS/Android)
- Touch gestures for navigation
- Responsive layout
- Platform-specific controls
- Reduced feature set for performance

### Web (WebAssembly)
- Progressive Web App support
- Local storage for offline reading
- Service worker for caching

## Styling & Themes

```xml
<!-- Themes/DarkTheme.axaml -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Color Palette -->
    <Color x:Key="BackgroundColor">#1e1e1e</Color>
    <Color x:Key="ForegroundColor">#e0e0e0</Color>
    <Color x:Key="AccentColor">#007ACC</Color>
    <Color x:Key="SecondaryColor">#2d2d30</Color>

    <!-- Brushes -->
    <SolidColorBrush x:Key="BackgroundBrush" Color="{StaticResource BackgroundColor}"/>
    <SolidColorBrush x:Key="ForegroundBrush" Color="{StaticResource ForegroundColor}"/>
    <SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}"/>

    <!-- Control Styles -->
    <Style Selector="Window">
        <Setter Property="Background" Value="{StaticResource BackgroundBrush}"/>
        <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}"/>
    </Style>

    <Style Selector="Button">
        <Setter Property="Background" Value="{StaticResource SecondaryColor}"/>
        <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
    </Style>

    <Style Selector="Button:pointerover">
        <Setter Property="Background" Value="{StaticResource AccentBrush}"/>
    </Style>
</ResourceDictionary>
```

## Required NuGet Packages

```xml
<!-- Alexandria.UI.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <!-- Avalonia Core -->
    <PackageReference Include="Avalonia" Version="11.0.*" />
    <PackageReference Include="Avalonia.Desktop" Version="11.0.*" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.0.*" />
    <PackageReference Include="Avalonia.Diagnostics" Version="11.0.*" Condition="'$(Configuration)' == 'Debug'" />

    <!-- MVVM Support -->
    <PackageReference Include="Avalonia.ReactiveUI" Version="11.0.*" />
    <PackageReference Include="ReactiveUI" Version="19.*" />

    <!-- HTML Rendering Options -->
    <PackageReference Include="Avalonia.HtmlRenderer" Version="11.0.*" />
    <!-- OR -->
    <PackageReference Include="CefNet.Avalonia" Version="*" /> <!-- For Chromium-based rendering -->

    <!-- Additional UI -->
    <PackageReference Include="Avalonia.Controls.DataGrid" Version="11.0.*" />
    <PackageReference Include="Material.Avalonia" Version="3.*" /> <!-- Material Design -->
    <PackageReference Include="Avalonia.Controls.TreeDataGrid" Version="11.0.*" />

    <!-- Icons -->
    <PackageReference Include="Material.Icons.Avalonia" Version="2.*" />

    <!-- Dependency Injection -->
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.*" />

    <!-- Project References -->
    <ProjectReference Include="..\Alexandria.Parser\Alexandria.Parser.csproj" />
  </ItemGroup>
</Project>
```

## Performance Optimizations

### 1. Virtualization
- Use `VirtualizingStackPanel` for large lists
- Implement viewport-based chapter loading
- Lazy load images and resources

### 2. Caching
- Cache rendered HTML
- Cache processed chapters
- Implement memory limits

### 3. Threading
- Background loading of chapters
- Async search operations
- UI thread protection

## Accessibility Features

- **Screen Reader Support**: Proper ARIA labels
- **Keyboard Navigation**: Full keyboard support
- **High Contrast Mode**: System integration
- **Font Scaling**: Respect system font size
- **Focus Indicators**: Clear focus states

## Testing Strategy

### 1. Unit Tests
- ViewModel logic
- Converters
- Services

### 2. UI Tests
- Avalonia.Headless for automated UI testing
- User interaction scenarios
- Navigation flows

### 3. Integration Tests
- Backend integration
- File operations
- Search functionality

## Development Workflow

1. **Design**: Create mockups in Figma/Sketch
2. **XAML**: Build views with sample data
3. **ViewModels**: Implement business logic
4. **Binding**: Connect views to ViewModels
5. **Styling**: Apply themes and polish
6. **Testing**: Unit and UI tests
7. **Optimization**: Performance profiling

## Library Management System

### Overview
The Library Management System is the central hub for organizing, cataloging, and accessing the user's EPUB collection. It provides persistent storage of book metadata, reading progress, and user collections.

### Architecture

```
Alexandria.UI.Library/
├── Database/
│   ├── LibraryDatabase.cs          # LiteDB database wrapper
│   ├── Models/
│   │   ├── LibraryBook.cs          # Local book record
│   │   ├── LibraryAuthor.cs        # Author with multiple books
│   │   ├── LibraryCollection.cs     # User-defined collections
│   │   └── ReadingStatistics.cs    # Per-book statistics
│   └── Repositories/
│       ├── BookRepository.cs       # Book CRUD operations
│       └── CollectionRepository.cs # Collection management
├── Services/
│   ├── ILibraryService.cs          # Core library operations
│   ├── LibraryService.cs           # Implementation
│   ├── IMetadataEnricher.cs        # Enhance book metadata
│   ├── ICoverExtractor.cs          # Extract/generate covers
│   └── IDuplicateDetector.cs       # Find duplicate books
└── Import/
    ├── BookImporter.cs              # Main import orchestrator
    ├── ImportQueue.cs               # Batch import management
    └── WatchFolderService.cs        # Auto-import from folders
```

### Database Schema

```csharp
// Local LiteDB database for library catalog
public class LibraryBook
{
    [BsonId]
    public Guid Id { get; set; }

    [BsonField("path")]
    public string FilePath { get; set; }

    public string Title { get; set; }
    public string? Subtitle { get; set; }

    [BsonRef("authors")]
    public List<LibraryAuthor> Authors { get; set; }

    public string? Isbn { get; set; }
    public string? Publisher { get; set; }
    public DateTime? PublicationDate { get; set; }
    public string? Language { get; set; }
    public string? Description { get; set; }

    // Cover stored separately in LiteDB FileStorage for efficiency
    [BsonIgnore]
    public byte[]? CoverImage { get; set; }

    public string? CoverImageId { get; set; }  // Reference to LiteDB FileStorage

    // Reading progress
    public double ProgressPercentage { get; set; }
    public string? LastReadPosition { get; set; }  // CFI or chapter+offset
    public DateTime? LastOpened { get; set; }
    public TimeSpan TotalReadingTime { get; set; }

    // Organization
    [BsonRef("collections")]
    public List<LibraryCollection> Collections { get; set; }

    public List<string> Tags { get; set; }  // Simple string tags for LiteDB
    public int? Rating { get; set; }
    public bool IsFavorite { get; set; }

    // Technical metadata
    public long FileSizeBytes { get; set; }

    [BsonField("hash")]
    [BsonIndex(unique: true)]
    public string FileHash { get; set; }  // For duplicate detection

    public DateTime DateAdded { get; set; }
    public DateTime DateModified { get; set; }
    public int WordCount { get; set; }
    public int ChapterCount { get; set; }
    public TimeSpan EstimatedReadingTime { get; set; }
}

public class LibraryCollection
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public CollectionType Type { get; set; }  // Manual, Smart
    public string? SmartCriteria { get; set; }  // JSON query for smart collections
    public List<LibraryBook> Books { get; set; }
    public DateTime Created { get; set; }
    public int SortOrder { get; set; }
}
```

### LiteDB Database Implementation

```csharp
public class LibraryDatabase : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<LibraryBook> _books;
    private readonly ILiteCollection<LibraryAuthor> _authors;
    private readonly ILiteCollection<LibraryCollection> _collections;
    private readonly ILiteStorage<string> _fileStorage;

    public LibraryDatabase(string connectionString)
    {
        _database = new LiteDatabase(connectionString);

        // Get collections
        _books = _database.GetCollection<LibraryBook>("books");
        _authors = _database.GetCollection<LibraryAuthor>("authors");
        _collections = _database.GetCollection<LibraryCollection>("collections");
        _fileStorage = _database.GetStorage<string>("covers");

        // Create indexes
        _books.EnsureIndex(x => x.Title);
        _books.EnsureIndex(x => x.FileHash, unique: true);
        _books.EnsureIndex(x => x.LastOpened);
        _books.EnsureIndex(x => x.DateAdded);
        _authors.EnsureIndex(x => x.Name);

        // Configure BsonMapper
        BsonMapper.Global.Entity<LibraryBook>()
            .DbRef(x => x.Authors, "authors")
            .DbRef(x => x.Collections, "collections");
    }

    public ILiteCollection<LibraryBook> Books => _books;
    public ILiteCollection<LibraryAuthor> Authors => _authors;
    public ILiteCollection<LibraryCollection> Collections => _collections;
    public ILiteStorage<string> CoverStorage => _fileStorage;

    public void Dispose() => _database?.Dispose();
}
```

### Library Service

```csharp
public class LibraryService : ILibraryService
{
    private readonly LibraryDatabase _db;
    private readonly ILogger<LibraryService> _logger;

    public LibraryService(LibraryDatabase database, ILogger<LibraryService> logger)
    {
        _db = database;
        _logger = logger;
    }

    public async Task<LibraryBook> AddBookAsync(string filePath, ImportOptions options)
    {
        return await Task.Run(() =>
        {
            using var trans = _db.Database.BeginTrans();
            try
            {
                var book = new LibraryBook { /* ... */ };

                // Store cover image in FileStorage
                if (book.CoverImage != null)
                {
                    var coverId = $"cover_{book.Id}";
                    _db.CoverStorage.Upload(coverId, $"{book.Title}.jpg",
                        new MemoryStream(book.CoverImage));
                    book.CoverImageId = coverId;
                    book.CoverImage = null; // Don't store in document
                }

                _db.Books.Insert(book);
                trans.Commit();
                return book;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        });
    }

    public async Task<IEnumerable<LibraryBook>> SearchBooksAsync(string query)
    {
        return await Task.Run(() =>
        {
            // LiteDB supports LINQ queries
            return _db.Books
                .Include(x => x.Authors)
                .Find(x => x.Title.Contains(query) ||
                          x.Authors.Any(a => a.Name.Contains(query)) ||
                          x.Description.Contains(query));
        });
    }

    public async Task<IEnumerable<LibraryBook>> GetRecentBooksAsync(int count = 10)
    {
        return await Task.Run(() =>
        {
            return _db.Books
                .Include(x => x.Authors)
                .Find(Query.All(Query.Descending))
                .Where(x => x.LastOpened != null)
                .OrderByDescending(x => x.LastOpened)
                .Take(count);
        });
    }

    public async Task UpdateReadingProgressAsync(Guid bookId, double percentage, string position)
    {
        await Task.Run(() =>
        {
            var book = _db.Books.FindById(bookId);
            if (book != null)
            {
                book.ProgressPercentage = percentage;
                book.LastReadPosition = position;
                book.LastOpened = DateTime.Now;
                _db.Books.Update(book);
            }
        });
    }
}
```

### Import Workflow

```csharp
public class BookImporter
{
    private readonly ILibraryService _libraryService;
    private readonly IBookLoader _bookLoader;
    private readonly IMetadataEnricher _metadataEnricher;
    private readonly ICoverExtractor _coverExtractor;
    private readonly IDuplicateDetector _duplicateDetector;

    public async Task<ImportResult> ImportBookAsync(string filePath, ImportOptions options)
    {
        // 1. Check for duplicates
        if (options.SkipDuplicates)
        {
            var isDuplicate = await _duplicateDetector.IsDuplicateAsync(filePath);
            if (isDuplicate)
                return ImportResult.Skipped("Duplicate book detected");
        }

        // 2. Load and parse EPUB
        var bookResult = await _bookLoader.LoadAsync(filePath);
        if (bookResult.IsT1) // Error
            return ImportResult.Failed(bookResult.AsT1.Message);

        var book = bookResult.AsT0;

        // 3. Extract cover image
        var cover = await _coverExtractor.ExtractCoverAsync(book);

        // 4. Enrich metadata (optional)
        if (options.EnrichMetadata)
        {
            var enriched = await _metadataEnricher.EnrichAsync(book.Metadata);
            book = book.WithMetadata(enriched);
        }

        // 5. Create library record
        var libraryBook = new LibraryBook
        {
            FilePath = filePath,
            Title = book.Title,
            Authors = book.Authors.Select(a => new LibraryAuthor { Name = a.Name }).ToList(),
            CoverImage = cover,
            FileHash = ComputeFileHash(filePath),
            DateAdded = DateTime.Now,
            WordCount = book.WordCount,
            ChapterCount = book.Chapters.Count,
            EstimatedReadingTime = book.EstimatedReadingTime
        };

        // 6. Save to database
        await _libraryService.AddBookAsync(libraryBook);

        return ImportResult.Success(libraryBook);
    }
}
```

### Duplicate Detection

```csharp
public class DuplicateDetector : IDuplicateDetector
{
    public async Task<bool> IsDuplicateAsync(string filePath)
    {
        // Level 1: File hash comparison
        var fileHash = await ComputeFileHashAsync(filePath);
        if (await ExistsByHashAsync(fileHash))
            return true;

        // Level 2: Metadata comparison
        var metadata = await ExtractMetadataAsync(filePath);
        if (await ExistsByMetadataAsync(metadata))
            return true;

        // Level 3: Fuzzy content comparison
        if (await HasSimilarContentAsync(filePath))
            return true;

        return false;
    }

    private async Task<bool> ExistsByMetadataAsync(BookMetadata metadata)
    {
        // Check ISBN first (most reliable)
        if (!string.IsNullOrEmpty(metadata.Isbn))
            return await _library.ExistsByIsbnAsync(metadata.Isbn);

        // Check title + author combination
        var similar = await _library.FindSimilarAsync(metadata.Title, metadata.Authors);
        return similar.Any(book =>
            LevenshteinDistance(book.Title, metadata.Title) < 3 &&
            book.Authors.Any(a => metadata.Authors.Contains(a)));
    }
}
```

## Book Import Architecture

### Import Pipeline

```
User Action → Import Queue → Validation → Parsing → Metadata Extraction →
Enrichment → Cover Processing → Duplicate Check → Database Save → Index Update
```

### Components

```csharp
public class ImportQueue
{
    private readonly Channel<ImportJob> _queue;
    private readonly ConcurrentDictionary<Guid, ImportProgress> _progress;

    public async Task<Guid> EnqueueAsync(ImportJob job)
    {
        var jobId = Guid.NewGuid();
        _progress[jobId] = new ImportProgress { Status = ImportStatus.Queued };
        await _queue.Writer.WriteAsync(job);
        return jobId;
    }

    public async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                UpdateProgress(job.Id, ImportStatus.Processing);
                await ProcessImportJobAsync(job);
                UpdateProgress(job.Id, ImportStatus.Completed);
            }
            catch (Exception ex)
            {
                UpdateProgress(job.Id, ImportStatus.Failed, ex.Message);
            }
        }
    }
}

public class WatchFolderService : IHostedService
{
    private readonly FileSystemWatcher _watcher;
    private readonly IImportQueue _importQueue;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _watcher.Created += OnNewFile;
        _watcher.EnableRaisingEvents = true;
        return Task.CompletedTask;
    }

    private async void OnNewFile(object sender, FileSystemEventArgs e)
    {
        if (Path.GetExtension(e.FullPath).Equals(".epub", StringComparison.OrdinalIgnoreCase))
        {
            // Wait for file to be fully written
            await WaitForFileReady(e.FullPath);
            await _importQueue.EnqueueAsync(new ImportJob { FilePath = e.FullPath });
        }
    }
}
```

### Metadata Enrichment

```csharp
public class MetadataEnricher : IMetadataEnricher
{
    private readonly IOpenLibraryClient _openLibrary;
    private readonly IGoogleBooksClient _googleBooks;

    public async Task<EnrichedMetadata> EnrichAsync(BookMetadata original)
    {
        var enriched = new EnrichedMetadata(original);

        // Try multiple sources
        if (!string.IsNullOrEmpty(original.Isbn))
        {
            // ISBN is most reliable for lookup
            var openLibData = await _openLibrary.GetByIsbnAsync(original.Isbn);
            var googleData = await _googleBooks.GetByIsbnAsync(original.Isbn);

            enriched.MergeFrom(openLibData, googleData);
        }
        else
        {
            // Fallback to title/author search
            var results = await SearchByTitleAuthorAsync(original.Title, original.Authors);
            enriched.MergeFrom(results);
        }

        return enriched;
    }
}
```

## State Management Architecture

### Application State Store

```csharp
// Using Reactive Extensions for state management
public class ApplicationStore
{
    private readonly BehaviorSubject<ApplicationState> _state;

    public ApplicationStore()
    {
        _state = new BehaviorSubject<ApplicationState>(ApplicationState.Initial);
    }

    public IObservable<ApplicationState> State => _state.AsObservable();
    public ApplicationState CurrentState => _state.Value;

    public void Dispatch(IAction action)
    {
        var newState = Reduce(CurrentState, action);
        _state.OnNext(newState);

        // Persist certain state changes
        if (action is IPersistableAction)
            PersistState(newState);
    }

    private ApplicationState Reduce(ApplicationState state, IAction action)
    {
        return action switch
        {
            OpenBookAction openBook => state with
            {
                CurrentBook = openBook.Book,
                ReadingSession = new ReadingSession(openBook.Book)
            },

            NavigateChapterAction nav => state with
            {
                ReadingSession = state.ReadingSession with
                {
                    CurrentChapter = nav.Chapter,
                    History = state.ReadingSession.History.Push(nav.Chapter)
                }
            },

            UpdateProgressAction progress => state with
            {
                ReadingSession = state.ReadingSession with
                {
                    Progress = progress.Percentage,
                    LastPosition = progress.Position
                }
            },

            _ => state
        };
    }
}
```

### Reading Session State

```csharp
public record ReadingSession
{
    public Book? CurrentBook { get; init; }
    public Chapter? CurrentChapter { get; init; }
    public double Progress { get; init; }
    public string LastPosition { get; init; }  // CFI or offset
    public ImmutableStack<NavigationPoint> History { get; init; }
    public ImmutableStack<NavigationPoint> ForwardHistory { get; init; }
    public ViewportState Viewport { get; init; }
    public PaginationState Pagination { get; init; }
    public SearchState? ActiveSearch { get; init; }
    public ImmutableList<Bookmark> SessionBookmarks { get; init; }
    public ImmutableList<Highlight> SessionHighlights { get; init; }

    public ReadingSession(Book book)
    {
        CurrentBook = book;
        CurrentChapter = book.Chapters.FirstOrDefault();
        History = ImmutableStack<NavigationPoint>.Empty;
        ForwardHistory = ImmutableStack<NavigationPoint>.Empty;
        SessionBookmarks = ImmutableList<Bookmark>.Empty;
        SessionHighlights = ImmutableList<Highlight>.Empty;
    }
}
```

### Cross-View Communication

```csharp
// Message bus for decoupled communication
public interface IMessageBus
{
    void Publish<TMessage>(TMessage message) where TMessage : IMessage;
    IObservable<TMessage> Listen<TMessage>() where TMessage : IMessage;
}

public class ReactiveMessageBus : IMessageBus
{
    private readonly Subject<IMessage> _messages = new();

    public void Publish<TMessage>(TMessage message) where TMessage : IMessage
    {
        _messages.OnNext(message);
    }

    public IObservable<TMessage> Listen<TMessage>() where TMessage : IMessage
    {
        return _messages.OfType<TMessage>();
    }
}

// Usage in ViewModels
public class ReaderViewModel
{
    public ReaderViewModel(IMessageBus messageBus)
    {
        // Listen for navigation from other views
        messageBus.Listen<NavigateToBookMessage>()
            .Subscribe(msg => OpenBook(msg.BookId));

        // Publish progress updates
        this.WhenAnyValue(x => x.Progress)
            .Throttle(TimeSpan.FromSeconds(1))
            .Subscribe(p => messageBus.Publish(new ProgressUpdatedMessage(p)));
    }
}
```

## Pagination Engine

### Overview
The Pagination Engine transforms continuous HTML content into discrete, viewport-sized pages. It handles dynamic text reflow, maintains reading position across font changes, and provides smooth navigation.

### Architecture

```csharp
public class PaginationEngine
{
    private readonly IHtmlMeasurementService _measurementService;
    private readonly IViewportCalculator _viewportCalculator;
    private readonly IPositionMapper _positionMapper;

    public async Task<PaginatedContent> PaginateAsync(
        string htmlContent,
        ViewportDimensions viewport,
        ReadingSettings settings)
    {
        // 1. Parse HTML into DOM
        var document = await ParseHtmlAsync(htmlContent);

        // 2. Apply reading settings (font, size, spacing)
        ApplySettings(document, settings);

        // 3. Measure content
        var measurements = await _measurementService.MeasureAsync(document, viewport);

        // 4. Calculate page breaks
        var pageBreaks = CalculatePageBreaks(measurements, viewport);

        // 5. Generate pages
        var pages = GeneratePages(document, pageBreaks);

        return new PaginatedContent
        {
            Pages = pages,
            TotalPages = pages.Count,
            CharacterMap = BuildCharacterMap(pages)
        };
    }
}
```

### Page Break Calculation

```csharp
public class PageBreakCalculator
{
    public List<PageBreak> CalculatePageBreaks(
        ContentMeasurements measurements,
        ViewportDimensions viewport)
    {
        var pageBreaks = new List<PageBreak>();
        var currentHeight = 0.0;
        var pageStartIndex = 0;

        foreach (var element in measurements.Elements)
        {
            // Check if element fits in current page
            if (currentHeight + element.Height > viewport.Height)
            {
                // Handle orphans and widows
                var adjustedBreak = AdjustForOrphansWidows(
                    pageStartIndex,
                    element.Index,
                    measurements);

                pageBreaks.Add(new PageBreak
                {
                    StartIndex = pageStartIndex,
                    EndIndex = adjustedBreak,
                    Height = currentHeight
                });

                pageStartIndex = adjustedBreak + 1;
                currentHeight = 0;
            }

            currentHeight += element.Height + element.MarginBottom;
        }

        return pageBreaks;
    }

    private int AdjustForOrphansWidows(
        int pageStart,
        int breakPoint,
        ContentMeasurements measurements)
    {
        // Avoid single lines at page top/bottom
        var paragraph = FindParagraphBoundaries(breakPoint, measurements);

        if (IsOrphan(breakPoint, paragraph))
            return paragraph.Start - 1;

        if (IsWidow(breakPoint, paragraph))
            return paragraph.End;

        return breakPoint;
    }
}
```

### CSS Column-Based Pagination

```csharp
public class CssColumnPaginator
{
    public string GeneratePaginatedHtml(string content, ViewportDimensions viewport)
    {
        return $@"
        <html>
        <head>
            <style>
                body {{
                    margin: 0;
                    padding: {viewport.Padding}px;
                    height: {viewport.Height - (viewport.Padding * 2)}px;
                    column-width: {viewport.Width - (viewport.Padding * 2)}px;
                    column-gap: 0;
                    column-fill: auto;
                    overflow: hidden;
                }}

                .page-content {{
                    break-inside: avoid;
                    position: relative;
                }}

                img {{
                    max-width: 100%;
                    height: auto;
                    break-inside: avoid;
                }}

                table {{
                    break-inside: avoid;
                }}

                h1, h2, h3, h4, h5, h6 {{
                    break-after: avoid;
                }}

                p {{
                    orphans: 2;
                    widows: 2;
                }}
            </style>
        </head>
        <body>
            <div class=""paginated-content"">
                {content}
            </div>
        </body>
        </html>";
    }
}
```

### Position Tracking

```csharp
public class PositionTracker
{
    private readonly Dictionary<int, PagePosition> _pageMap;
    private readonly Dictionary<string, int> _cfiToPage;

    public PagePosition GetCurrentPosition(int pageNumber)
    {
        return _pageMap[pageNumber];
    }

    public int GetPageFromCfi(string cfi)
    {
        return _cfiToPage.TryGetValue(cfi, out var page) ? page : 0;
    }

    public int GetPageFromCharacterOffset(int offset)
    {
        return _pageMap.Values
            .FirstOrDefault(p => p.StartOffset <= offset && p.EndOffset >= offset)
            ?.PageNumber ?? 0;
    }

    public PagePosition RestorePosition(ReadingPosition savedPosition)
    {
        // Try CFI first (most accurate)
        if (!string.IsNullOrEmpty(savedPosition.Cfi))
        {
            var page = GetPageFromCfi(savedPosition.Cfi);
            if (page > 0) return _pageMap[page];
        }

        // Fall back to character offset
        if (savedPosition.CharacterOffset > 0)
        {
            var page = GetPageFromCharacterOffset(savedPosition.CharacterOffset);
            if (page > 0) return _pageMap[page];
        }

        // Last resort: percentage
        var estimatedPage = (int)(savedPosition.Percentage * _pageMap.Count / 100);
        return _pageMap[Math.Min(estimatedPage, _pageMap.Count - 1)];
    }
}
```

### Dynamic Reflow

```csharp
public class ReflowManager
{
    private PaginatedContent? _cachedPagination;
    private ReadingSettings? _lastSettings;

    public async Task<PaginatedContent> ReflowAsync(
        string content,
        ViewportDimensions viewport,
        ReadingSettings newSettings,
        PagePosition? currentPosition)
    {
        // Check if reflow is needed
        if (!NeedsReflow(newSettings, viewport))
            return _cachedPagination!;

        // Save current reading position
        var savedPosition = currentPosition != null
            ? ConvertToAbsolutePosition(currentPosition)
            : null;

        // Repaginate with new settings
        var repaginated = await _paginationEngine.PaginateAsync(
            content,
            viewport,
            newSettings);

        // Restore reading position
        if (savedPosition != null)
        {
            var newPage = FindClosestPage(repaginated, savedPosition);
            repaginated.CurrentPage = newPage;
        }

        _cachedPagination = repaginated;
        _lastSettings = newSettings;

        return repaginated;
    }
}
```

## Visual Rendering Systems

### Highlight Rendering

```csharp
public class HighlightRenderer
{
    public async Task RenderHighlightsAsync(
        IHtmlDocument document,
        IEnumerable<Highlight> highlights)
    {
        foreach (var highlight in highlights.OrderBy(h => h.StartOffset))
        {
            await ApplyHighlightToRangeAsync(document, highlight);
        }
    }

    private async Task ApplyHighlightToRangeAsync(
        IHtmlDocument document,
        Highlight highlight)
    {
        // Find text nodes in range
        var textNodes = FindTextNodesInRange(
            document.Body,
            highlight.StartOffset,
            highlight.EndOffset);

        foreach (var node in textNodes)
        {
            // Wrap text in highlight span
            var highlightSpan = document.CreateElement("span");
            highlightSpan.SetAttribute("class", $"highlight highlight-{highlight.Color}");
            highlightSpan.SetAttribute("data-highlight-id", highlight.Id);
            highlightSpan.SetAttribute("style", GenerateHighlightStyle(highlight));

            // Handle partial node highlighting
            if (NeedsPartialHighlight(node, highlight))
            {
                SplitAndWrapNode(node, highlightSpan, highlight);
            }
            else
            {
                WrapEntireNode(node, highlightSpan);
            }
        }
    }

    private string GenerateHighlightStyle(Highlight highlight)
    {
        return highlight.Style switch
        {
            HighlightStyle.Background => $"background-color: {highlight.ColorHex}; opacity: {highlight.Opacity};",
            HighlightStyle.Underline => $"border-bottom: 2px solid {highlight.ColorHex};",
            HighlightStyle.Strikethrough => $"text-decoration: line-through; text-decoration-color: {highlight.ColorHex};",
            _ => $"background-color: {highlight.ColorHex};"
        };
    }
}
```

### Bookmark Visualization

```csharp
public class BookmarkRenderer
{
    public void RenderBookmarks(
        IHtmlDocument document,
        IEnumerable<Bookmark> bookmarks,
        int currentPage)
    {
        // Add bookmark indicators to page margin
        var marginContainer = document.GetElementById("bookmark-margin")
            ?? CreateMarginContainer(document);

        marginContainer.InnerHtml = "";

        foreach (var bookmark in bookmarks.Where(b => b.Page == currentPage))
        {
            var indicator = CreateBookmarkIndicator(document, bookmark);
            marginContainer.AppendChild(indicator);
        }
    }

    private IHtmlElement CreateBookmarkIndicator(
        IHtmlDocument document,
        Bookmark bookmark)
    {
        var indicator = document.CreateElement("div");
        indicator.SetAttribute("class", "bookmark-indicator");
        indicator.SetAttribute("style", $@"
            position: absolute;
            top: {CalculateVerticalPosition(bookmark)}px;
            right: -30px;
            width: 25px;
            height: 30px;
            background-image: url('data:image/svg+xml;base64,{GetBookmarkSvg()}');
            cursor: pointer;
        ");

        // Add tooltip
        indicator.SetAttribute("title", bookmark.Note ?? "Bookmark");

        // Add click handler via data attribute
        indicator.SetAttribute("data-bookmark-id", bookmark.Id);
        indicator.SetAttribute("onclick", "handleBookmarkClick(this)");

        return indicator;
    }
}
```

### Annotation Layer

```csharp
public class AnnotationLayer
{
    public void RenderAnnotations(
        IHtmlDocument document,
        IEnumerable<Annotation> annotations)
    {
        var annotationLayer = GetOrCreateAnnotationLayer(document);

        foreach (var annotation in annotations)
        {
            var bubble = CreateAnnotationBubble(document, annotation);
            PositionAnnotationBubble(bubble, annotation);
            annotationLayer.AppendChild(bubble);
        }
    }

    private IHtmlElement CreateAnnotationBubble(
        IHtmlDocument document,
        Annotation annotation)
    {
        var bubble = document.CreateElement("div");
        bubble.SetAttribute("class", "annotation-bubble");
        bubble.InnerHtml = $@"
            <div class=""annotation-header"">
                <span class=""annotation-author"">{annotation.Author}</span>
                <span class=""annotation-date"">{annotation.Created:g}</span>
            </div>
            <div class=""annotation-content"">
                {annotation.Text}
            </div>
            <div class=""annotation-actions"">
                <button onclick=""replyToAnnotation('{annotation.Id}')"">Reply</button>
                <button onclick=""editAnnotation('{annotation.Id}')"">Edit</button>
            </div>
        ";

        return bubble;
    }
}
```

### Search Result Highlighting

```csharp
public class SearchHighlighter
{
    private readonly List<TemporaryHighlight> _activeHighlights = new();

    public async Task HighlightSearchResultsAsync(
        IHtmlDocument document,
        IEnumerable<SearchMatch> matches,
        SearchMatch? currentMatch = null)
    {
        // Clear previous search highlights
        ClearSearchHighlights(document);

        foreach (var match in matches)
        {
            var isCurrentMatch = currentMatch?.Id == match.Id;
            var highlight = new TemporaryHighlight
            {
                Id = $"search-{match.Id}",
                StartOffset = match.StartOffset,
                EndOffset = match.EndOffset,
                ColorHex = isCurrentMatch ? "#FFD700" : "#FFFF00",  // Gold for current, yellow for others
                Opacity = isCurrentMatch ? 0.6f : 0.3f
            };

            _activeHighlights.Add(highlight);
            await ApplyTemporaryHighlight(document, highlight);
        }

        // Scroll to current match
        if (currentMatch != null)
        {
            ScrollToElement(document, $"search-{currentMatch.Id}");
        }
    }

    private void ScrollToElement(IHtmlDocument document, string elementId)
    {
        var element = document.GetElementById(elementId);
        if (element != null)
        {
            // Inject scroll script
            var script = document.CreateElement("script");
            script.InnerHtml = $@"
                document.getElementById('{elementId}').scrollIntoView({{
                    behavior: 'smooth',
                    block: 'center'
                }});
            ";
            document.Body.AppendChild(script);
        }
    }
}
```

## Frontend Caching Strategy

### Multi-Tier Cache Architecture

```csharp
public class FrontendCacheManager
{
    private readonly IMemoryCache _l1Cache;  // Hot data
    private readonly LiteDatabase _l2Cache;  // Warm data (LiteDB)
    private readonly IDiskCache _l3Cache;  // Cold data

    public async Task<T?> GetAsync<T>(string key, Func<Task<T>> factory)
    {
        // L1: Memory cache (fastest)
        if (_l1Cache.TryGetValue(key, out T cached))
            return cached;

        // L2: LiteDB cache
        var cacheCollection = _l2Cache.GetCollection<CacheEntry<T>>("cache");
        var l2Entry = cacheCollection.FindById(key);
        if (l2Entry != null && !l2Entry.IsExpired)
        {
            _l1Cache.Set(key, l2Entry.Value, TimeSpan.FromMinutes(5));
            return l2Entry.Value;
        }

        // L3: Disk cache
        var l3Data = await _l3Cache.GetAsync<T>(key);
        if (l3Data != null)
        {
            await PromoteToL2(key, l3Data);
            _l1Cache.Set(key, l3Data, TimeSpan.FromMinutes(5));
            return l3Data;
        }

        // Generate and cache
        var fresh = await factory();
        await CacheAtAllLevels(key, fresh);
        return fresh;
    }
}
```

### Page Render Cache

```csharp
public class PageRenderCache
{
    private readonly LruCache<PageCacheKey, RenderedPage> _cache;
    private readonly int _maxPages = 50;  // Keep 50 pages in memory

    public async Task<RenderedPage> GetOrRenderPageAsync(
        PageCacheKey key,
        Func<Task<RenderedPage>> renderFunc)
    {
        if (_cache.TryGet(key, out var cached))
        {
            // Move to front (LRU)
            return cached;
        }

        var rendered = await renderFunc();
        _cache.Add(key, rendered);

        // Pre-render adjacent pages
        _ = Task.Run(() => PreRenderAdjacentPages(key));

        return rendered;
    }

    private async Task PreRenderAdjacentPages(PageCacheKey currentKey)
    {
        // Pre-render next 2 and previous 1 page
        var tasks = new List<Task>();

        for (int i = -1; i <= 2; i++)
        {
            if (i == 0) continue;  // Skip current page

            var adjacentKey = currentKey with { PageNumber = currentKey.PageNumber + i };
            if (!_cache.Contains(adjacentKey))
            {
                tasks.Add(RenderPageAsync(adjacentKey));
            }
        }

        await Task.WhenAll(tasks);
    }
}

public record PageCacheKey(
    Guid BookId,
    int ChapterIndex,
    int PageNumber,
    ReadingSettings Settings,
    ViewportDimensions Viewport);
```

### Thumbnail Cache

```csharp
public class ThumbnailCache
{
    private readonly string _cacheDirectory;
    private readonly IImageProcessor _imageProcessor;

    public async Task<byte[]> GetOrGenerateThumbnailAsync(
        string imagePath,
        ThumbnailSize size)
    {
        var cacheKey = GenerateCacheKey(imagePath, size);
        var cachePath = Path.Combine(_cacheDirectory, cacheKey);

        if (File.Exists(cachePath))
            return await File.ReadAllBytesAsync(cachePath);

        // Generate thumbnail
        var thumbnail = await _imageProcessor.GenerateThumbnailAsync(
            imagePath,
            size.Width,
            size.Height);

        // Save to cache
        await File.WriteAllBytesAsync(cachePath, thumbnail);

        return thumbnail;
    }

    private string GenerateCacheKey(string imagePath, ThumbnailSize size)
    {
        var fileInfo = new FileInfo(imagePath);
        var hash = ComputeHash($"{imagePath}_{fileInfo.LastWriteTimeUtc}_{size}");
        return $"{hash}.jpg";
    }
}
```

## Backend Service Consumption

### Service Integration Layer

```csharp
public class BackendServiceAdapter
{
    private readonly IBookLoader _bookLoader;
    private readonly ISearchService _searchService;
    private readonly IBookmarkService _bookmarkService;
    private readonly IAnnotationService _annotationService;
    private readonly IReadingProgressService _progressService;
    private readonly IContentAnalyzer _contentAnalyzer;

    // Resilience policies
    private readonly IAsyncPolicy<OneOf<Book, ParsingError>> _loadBookPolicy;
    private readonly IAsyncPolicy<SearchResults> _searchPolicy;

    public BackendServiceAdapter(/* dependencies */)
    {
        // Configure Polly resilience policies
        _loadBookPolicy = Policy<OneOf<Book, ParsingError>>
            .HandleResult(r => r.IsT1)  // Retry on error
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retry, context) =>
                {
                    Log.Warning("Book load retry {Retry} after {Delay}s", retry, timespan.TotalSeconds);
                });

        _searchPolicy = Policy<SearchResults>
            .HandleResult(r => r == null || r.HasError)
            .CircuitBreakerAsync(
                3,
                TimeSpan.FromMinutes(1),
                onBreak: (result, duration) =>
                {
                    Log.Error("Search circuit breaker opened for {Duration}", duration);
                },
                onReset: () => Log.Information("Search circuit breaker reset"));
    }
}
```

### Book Loading Integration

```csharp
public class BookLoadingService
{
    private readonly IBookLoader _backendLoader;
    private readonly ILibraryService _libraryService;
    private readonly IProgress<LoadingProgress> _progressReporter;

    public async Task<BookLoadResult> LoadBookAsync(string filePath)
    {
        try
        {
            // Report progress
            _progressReporter.Report(new LoadingProgress
            {
                Stage = "Parsing EPUB",
                Percentage = 0
            });

            // Call backend service
            var result = await _backendLoader.LoadAsync(filePath);

            if (result.IsT1) // Error
            {
                return BookLoadResult.Failed(result.AsT1);
            }

            var book = result.AsT0;

            _progressReporter.Report(new LoadingProgress
            {
                Stage = "Processing content",
                Percentage = 50
            });

            // Convert to frontend model
            var displayBook = await ConvertToDisplayModel(book);

            // Update library
            await _libraryService.AddOrUpdateBookAsync(displayBook);

            _progressReporter.Report(new LoadingProgress
            {
                Stage = "Complete",
                Percentage = 100
            });

            return BookLoadResult.Success(displayBook);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load book from {Path}", filePath);
            return BookLoadResult.Failed(ex);
        }
    }

    private async Task<DisplayBook> ConvertToDisplayModel(Book domainBook)
    {
        return new DisplayBook
        {
            Id = domainBook.Id,
            Title = domainBook.Title,
            Authors = domainBook.Authors.Select(a => a.Name).ToList(),
            Chapters = domainBook.Chapters.Select(ConvertChapter).ToList(),
            Metadata = ConvertMetadata(domainBook.Metadata),
            // Frontend-specific properties
            CoverImagePath = await ExtractCoverImage(domainBook),
            LastOpened = DateTime.Now,
            IsLoaded = true
        };
    }
}
```

### Search Service Integration

```csharp
public class SearchServiceAdapter
{
    private readonly ISearchService _backendSearch;
    private readonly IHighlightRenderer _highlightRenderer;
    private readonly CancellationTokenSource _searchCancellation = new();

    public async Task<SearchViewModel> SearchAsync(
        Guid bookId,
        string query,
        SearchOptions options)
    {
        // Cancel previous search
        _searchCancellation.Cancel();
        var cts = new CancellationTokenSource();

        try
        {
            // Call backend search
            var results = await _backendSearch.SearchAsync(
                bookId,
                query,
                cts.Token);

            // Convert to display models
            var displayResults = results.Matches.Select(match => new SearchResultDisplay
            {
                Id = match.Id,
                ChapterTitle = match.ChapterTitle,
                Preview = GeneratePreview(match),
                Position = match.Position,
                Score = match.Score
            }).ToList();

            // Group by chapter
            var groupedResults = displayResults
                .GroupBy(r => r.ChapterTitle)
                .Select(g => new ChapterSearchResults
                {
                    ChapterTitle = g.Key,
                    Results = g.ToList(),
                    TotalCount = g.Count()
                })
                .ToList();

            return new SearchViewModel
            {
                Query = query,
                TotalResults = displayResults.Count,
                ChapterGroups = groupedResults,
                SearchTime = results.SearchDuration
            };
        }
        catch (OperationCanceledException)
        {
            return SearchViewModel.Cancelled();
        }
    }

    private string GeneratePreview(SearchMatch match)
    {
        const int contextLength = 50;
        var start = Math.Max(0, match.StartOffset - contextLength);
        var end = Math.Min(match.Content.Length, match.EndOffset + contextLength);

        var preview = match.Content.Substring(start, end - start);

        // Wrap match in emphasis
        var matchText = match.Content.Substring(match.StartOffset, match.EndOffset - match.StartOffset);
        preview = preview.Replace(matchText, $"<mark>{matchText}</mark>");

        return $"...{preview}...";
    }
}
```

### Bookmark Service Integration

```csharp
public class BookmarkServiceAdapter
{
    private readonly IBookmarkService _backendBookmarks;
    private readonly IPositionTracker _positionTracker;
    private readonly Subject<BookmarkChange> _bookmarkChanges = new();

    public IObservable<BookmarkChange> BookmarkChanges => _bookmarkChanges;

    public async Task<BookmarkDisplay> AddBookmarkAsync(
        Guid bookId,
        ViewportPosition viewportPosition,
        string? note = null)
    {
        // Convert viewport position to backend position
        var backendPosition = _positionTracker.ConvertToBackendPosition(viewportPosition);

        // Call backend service
        var bookmark = await _backendBookmarks.AddBookmarkAsync(
            bookId,
            backendPosition,
            note);

        // Convert to display model
        var display = new BookmarkDisplay
        {
            Id = bookmark.Id,
            PageNumber = _positionTracker.GetPageFromPosition(bookmark.Position),
            VerticalPosition = CalculateVerticalPosition(bookmark.Position),
            Note = bookmark.Note,
            Created = bookmark.Created,
            Color = GetBookmarkColor(bookmark.Type)
        };

        // Also persist to local LiteDB for offline access
        await _localDb.Bookmarks.InsertAsync(display);

        // Notify observers
        _bookmarkChanges.OnNext(new BookmarkChange
        {
            Type = ChangeType.Added,
            Bookmark = display
        });

        return display;
    }

    public async Task<IReadOnlyList<BookmarkDisplay>> GetBookmarksAsync(Guid bookId)
    {
        var bookmarks = await _backendBookmarks.GetBookmarksAsync(bookId);

        return bookmarks.Select(b => new BookmarkDisplay
        {
            Id = b.Id,
            PageNumber = _positionTracker.GetPageFromPosition(b.Position),
            VerticalPosition = CalculateVerticalPosition(b.Position),
            Note = b.Note,
            Created = b.Created,
            Color = GetBookmarkColor(b.Type)
        }).ToList();
    }
}
```

### Offline Mode Support

```csharp
public class OfflineModeManager
{
    private readonly IConnectivityService _connectivity;
    private readonly ILocalCache _localCache;
    private readonly Queue<PendingOperation> _pendingOperations = new();

    public async Task<T> ExecuteWithFallbackAsync<T>(
        Func<Task<T>> onlineOperation,
        Func<Task<T>> offlineOperation)
    {
        if (await _connectivity.IsOnlineAsync())
        {
            try
            {
                var result = await onlineOperation();
                await ProcessPendingOperations();
                return result;
            }
            catch (NetworkException)
            {
                // Fall back to offline
            }
        }

        return await offlineOperation();
    }

    public async Task QueueOperationAsync(PendingOperation operation)
    {
        _pendingOperations.Enqueue(operation);
        await _localCache.SavePendingOperationsAsync(_pendingOperations);

        if (await _connectivity.IsOnlineAsync())
        {
            await ProcessPendingOperations();
        }
    }

    private async Task ProcessPendingOperations()
    {
        while (_pendingOperations.Count > 0)
        {
            var operation = _pendingOperations.Peek();

            try
            {
                await operation.ExecuteAsync();
                _pendingOperations.Dequeue();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to process pending operation {Type}", operation.Type);
                break;  // Stop processing, will retry later
            }
        }

        await _localCache.SavePendingOperationsAsync(_pendingOperations);
    }
}
```

## Performance Optimizations

### Virtual Scrolling

```csharp
public class VirtualizingBookGrid : UserControl
{
    private readonly VirtualizingStackPanel _virtualPanel;
    private readonly int _itemsPerRow = 4;
    private readonly double _itemHeight = 250;

    public VirtualizingBookGrid()
    {
        _virtualPanel = new VirtualizingStackPanel
        {
            VirtualizationMode = VirtualizationMode.Recycling,
            Orientation = Orientation.Vertical
        };

        ScrollViewer.SetCanContentScroll(this, true);
        ScrollViewer.SetVirtualizationMode(this, VirtualizationMode.Recycling);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var visibleRange = CalculateVisibleRange(availableSize);
        RealizeItems(visibleRange);
        VirtualizeItems(visibleRange);

        return base.MeasureOverride(availableSize);
    }

    private void RealizeItems(Range visibleRange)
    {
        for (int i = visibleRange.Start.Value; i < visibleRange.End.Value; i++)
        {
            if (!_realizedItems.ContainsKey(i))
            {
                var item = CreateBookCard(_books[i]);
                _realizedItems[i] = item;
                _virtualPanel.Children.Add(item);
            }
        }
    }

    private void VirtualizeItems(Range visibleRange)
    {
        var toVirtualize = _realizedItems
            .Where(kvp => kvp.Key < visibleRange.Start.Value || kvp.Key >= visibleRange.End.Value)
            .ToList();

        foreach (var kvp in toVirtualize)
        {
            _virtualPanel.Children.Remove(kvp.Value);
            _itemPool.Return(kvp.Value);
            _realizedItems.Remove(kvp.Key);
        }
    }
}
```

### Lazy Loading

```csharp
public class LazyContentLoader
{
    private readonly Dictionary<string, Lazy<Task<string>>> _contentLoaders = new();

    public void RegisterChapter(Chapter chapter)
    {
        _contentLoaders[chapter.Id] = new Lazy<Task<string>>(
            () => LoadChapterContentAsync(chapter),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<string> GetChapterContentAsync(string chapterId)
    {
        if (_contentLoaders.TryGetValue(chapterId, out var lazy))
        {
            return await lazy.Value;
        }

        throw new KeyNotFoundException($"Chapter {chapterId} not registered");
    }

    private async Task<string> LoadChapterContentAsync(Chapter chapter)
    {
        // Simulate expensive content loading
        await Task.Delay(100);

        // Process HTML content
        var processed = await ProcessHtmlAsync(chapter.RawContent);

        // Cache processed content
        await CacheProcessedContentAsync(chapter.Id, processed);

        return processed;
    }
}
```

### Background Processing

```csharp
public class BackgroundTaskQueue
{
    private readonly Channel<BackgroundTask> _queue;
    private readonly IHostedService _processor;

    public BackgroundTaskQueue()
    {
        _queue = Channel.CreateUnbounded<BackgroundTask>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async ValueTask EnqueueAsync(BackgroundTask task)
    {
        await _queue.Writer.WriteAsync(task);
    }

    public async ValueTask<BackgroundTask> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}

public class BackgroundTaskProcessor : BackgroundService
{
    private readonly BackgroundTaskQueue _queue;
    private readonly IServiceProvider _services;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var task = await _queue.DequeueAsync(stoppingToken);

            try
            {
                using var scope = _services.CreateScope();
                await task.ExecuteAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Background task {TaskType} failed", task.GetType().Name);
            }
        }
    }
}
```

### Memory Management

```csharp
public class MemoryManager
{
    private readonly Timer _gcTimer;
    private readonly long _memoryThreshold = 500 * 1024 * 1024; // 500MB

    public MemoryManager()
    {
        _gcTimer = new Timer(CheckMemoryPressure, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    private void CheckMemoryPressure(object? state)
    {
        var memoryInfo = GC.GetTotalMemory(false);

        if (memoryInfo > _memoryThreshold)
        {
            Log.Information("Memory pressure detected: {Memory:N0} bytes", memoryInfo);

            // Clear caches
            ClearLowPriorityCaches();

            // Force garbage collection
            GC.Collect(2, GCCollectionMode.Optimized);
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var newMemory = GC.GetTotalMemory(false);
            Log.Information("Memory after cleanup: {Memory:N0} bytes", newMemory);
        }
    }

    private void ClearLowPriorityCaches()
    {
        // Clear render cache for pages not recently viewed
        _pageRenderCache.ClearOldEntries(TimeSpan.FromMinutes(10));

        // Clear thumbnail cache
        _thumbnailCache.Clear();

        // Reduce image cache
        _imageCache.Trim(50); // Keep only 50 most recent
    }
}
```

## Error Handling & Recovery

### Global Exception Handler

```csharp
public class GlobalExceptionHandler
{
    private readonly IDialogService _dialogService;
    private readonly ICrashReporter _crashReporter;
    private readonly IApplicationState _appState;

    public void Initialize()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        RxApp.DefaultExceptionHandler = Observer.Create<Exception>(OnReactiveException);
    }

    private async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Log.Fatal(exception, "Unhandled exception");

        try
        {
            // Save current state
            await _appState.SaveEmergencyBackupAsync();

            // Report crash
            await _crashReporter.ReportCrashAsync(exception);

            // Show error dialog
            await _dialogService.ShowFatalErrorAsync(
                "An unexpected error occurred",
                "The application needs to restart. Your reading progress has been saved.",
                exception);
        }
        finally
        {
            Environment.Exit(1);
        }
    }

    private void OnReactiveException(Exception exception)
    {
        Log.Error(exception, "Reactive exception");

        // Classify error severity
        var severity = ClassifyException(exception);

        switch (severity)
        {
            case ErrorSeverity.Minor:
                ShowToastNotification("A minor error occurred", exception.Message);
                break;

            case ErrorSeverity.Major:
                _ = ShowErrorDialogAsync("Error", exception.Message);
                break;

            case ErrorSeverity.Critical:
                _ = HandleCriticalErrorAsync(exception);
                break;
        }
    }
}
```

### Error Recovery

```csharp
public class ErrorRecoveryService
{
    private readonly IApplicationState _appState;
    private readonly ILibraryService _libraryService;

    public async Task<RecoveryResult> AttemptRecoveryAsync(Exception error)
    {
        Log.Information("Attempting recovery from {ErrorType}", error.GetType().Name);

        return error switch
        {
            CorruptedStateException => await RecoverFromCorruptedState(),
            DatabaseException => await RecoverDatabase(),
            FileNotFoundException => await RecoverMissingFile(error.Message),
            OutOfMemoryException => await RecoverFromMemoryPressure(),
            _ => RecoveryResult.Failed("Unable to recover from this error")
        };
    }

    private async Task<RecoveryResult> RecoverFromCorruptedState()
    {
        try
        {
            // Load last known good state
            var backup = await _appState.LoadLastBackupAsync();
            if (backup != null)
            {
                await _appState.RestoreFromBackupAsync(backup);
                return RecoveryResult.Success("State restored from backup");
            }

            // Reset to defaults
            await _appState.ResetToDefaultsAsync();
            return RecoveryResult.Partial("State reset to defaults");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to recover from corrupted state");
            return RecoveryResult.Failed("Recovery failed");
        }
    }

    private async Task<RecoveryResult> RecoverDatabase()
    {
        try
        {
            // Try to repair database
            await _libraryService.RepairDatabaseAsync();

            // Reindex if needed
            await _libraryService.RebuildIndexesAsync();

            return RecoveryResult.Success("Database recovered");
        }
        catch
        {
            // Create new database
            await _libraryService.RecreateDatabaseAsync();
            return RecoveryResult.Partial("Database recreated, some data may be lost");
        }
    }
}
```

### User-Friendly Error Messages

```csharp
public class ErrorMessageService
{
    private readonly Dictionary<Type, string> _errorMessages = new()
    {
        [typeof(FileNotFoundException)] = "The book file could not be found. It may have been moved or deleted.",
        [typeof(UnauthorizedAccessException)] = "Permission denied. Please check that you have access to this file.",
        [typeof(InvalidEpubException)] = "This file doesn't appear to be a valid EPUB. The file may be corrupted.",
        [typeof(OutOfMemoryException)] = "Not enough memory to load this book. Try closing other applications.",
        [typeof(NetworkException)] = "Network connection lost. Some features may be unavailable.",
        [typeof(DatabaseException)] = "There was a problem with your library database. It has been repaired."
    };

    public string GetUserFriendlyMessage(Exception exception)
    {
        // Check for specific exception type
        if (_errorMessages.TryGetValue(exception.GetType(), out var message))
            return message;

        // Check base types
        var baseType = exception.GetType().BaseType;
        while (baseType != null)
        {
            if (_errorMessages.TryGetValue(baseType, out message))
                return message;
            baseType = baseType.BaseType;
        }

        // Generic message
        return "An unexpected error occurred. Please try again.";
    }

    public ErrorAction GetSuggestedAction(Exception exception)
    {
        return exception switch
        {
            FileNotFoundException => ErrorAction.BrowseForFile,
            UnauthorizedAccessException => ErrorAction.RequestPermission,
            OutOfMemoryException => ErrorAction.ClearCache,
            NetworkException => ErrorAction.RetryOffline,
            DatabaseException => ErrorAction.RepairDatabase,
            _ => ErrorAction.Retry
        };
    }
}
```

## Summary

This comprehensive AvaloniaUI frontend architecture provides:

### Core Features
- ✅ True cross-platform support (Windows, macOS, Linux, mobile, web)
- ✅ Modern MVVM architecture with ReactiveUI
- ✅ Rich reading experience with themes and customization
- ✅ Efficient HTML rendering for EPUB content

### Library Management
- ✅ LiteDB NoSQL database for local book catalog
- ✅ Smart collections and tagging system
- ✅ Duplicate detection (file hash, metadata, fuzzy content)
- ✅ Watch folder for automatic imports
- ✅ Metadata enrichment from online sources
- ✅ FileStorage for efficient cover image handling

### Reading Experience
- ✅ CSS column-based pagination with dynamic reflow
- ✅ Position tracking with CFI, character offset, and percentage
- ✅ Visual bookmarks and annotations
- ✅ Search result highlighting with context
- ✅ Orphan/widow control for better typography

### State Management
- ✅ Reactive state management with RX.NET
- ✅ Message bus for cross-view communication
- ✅ Reading session persistence
- ✅ Undo/redo support
- ✅ Navigation history stack

### Performance
- ✅ Multi-tier caching (Memory → LiteDB → Disk)
- ✅ Virtual scrolling for large libraries
- ✅ Lazy loading with adjacent page pre-rendering
- ✅ Background task processing
- ✅ Memory pressure management

### Backend Integration
- ✅ Resilient service consumption with Polly
- ✅ Offline mode with operation queuing
- ✅ Progress reporting for long operations
- ✅ Adapter pattern for domain/UI separation

### Error Handling
- ✅ Global exception handling
- ✅ Automatic error recovery
- ✅ User-friendly error messages
- ✅ Crash reporting and state backup

The architecture is modular, testable, and integrates seamlessly with the Alexandria backend services. It provides a rich, performant reading experience while maintaining clean separation between presentation and business logic.
- ✅ Reactive state management with RX.NET
- ✅ Search and annotation features
- ✅ Accessibility and performance optimizations

The architecture is modular, testable, and integrates seamlessly with the backend parser architecture we've already designed.