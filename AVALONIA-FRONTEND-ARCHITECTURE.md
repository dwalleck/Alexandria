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

## Summary

This AvaloniaUI frontend architecture provides:
- ✅ True cross-platform support (Windows, macOS, Linux, mobile, web)
- ✅ Modern MVVM architecture with ReactiveUI
- ✅ Rich reading experience with themes and customization
- ✅ Efficient HTML rendering for EPUB content
- ✅ Comprehensive library management
- ✅ Search and annotation features
- ✅ Accessibility and performance optimizations

The architecture is modular, testable, and integrates seamlessly with the backend parser architecture we've already designed.