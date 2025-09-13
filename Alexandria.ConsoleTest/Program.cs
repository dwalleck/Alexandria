using Alexandria.Parser;
using Alexandria.Parser.Application.UseCases.LoadBook;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Alexandria.ConsoleTest;

class Program
{
    static async Task Main(string[] args)
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddAlexandriaParser();

        var serviceProvider = services.BuildServiceProvider();

        // Example 1: Using the simplified API
        await UseSimplifiedApiExample();

        // Example 2: Using dependency injection
        await UseDependencyInjectionExample(serviceProvider);

        // Example 3: Process chapter content
        await ProcessChapterExample();
    }

    static async Task UseSimplifiedApiExample()
    {
        Console.WriteLine("=== Simplified API Example ===");

        // Get the first EPUB file from current directory or use a sample
        var epubFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.epub", SearchOption.AllDirectories);

        if (epubFiles.Length == 0)
        {
            Console.WriteLine("No EPUB files found in current directory.");
            Console.WriteLine("Please place an EPUB file in the directory and run again.");
            return;
        }

        var epubPath = epubFiles[0];
        Console.WriteLine($"Loading: {Path.GetFileName(epubPath)}");

        try
        {
            var reader = new EpubReader();
            var book = await reader.LoadBookAsync(epubPath);

            Console.WriteLine($"Title: {book.Title}");
            Console.WriteLine($"Authors: {string.Join(", ", book.Authors.Select(a => a.Name))}");
            Console.WriteLine($"Language: {book.Language}");
            Console.WriteLine($"Chapters: {book.Chapters.Count}");

            if (book.Metadata.Publisher != null)
                Console.WriteLine($"Publisher: {book.Metadata.Publisher}");

            if (book.Metadata.PublicationDate != null)
                Console.WriteLine($"Publication Date: {book.Metadata.PublicationDate:yyyy-MM-dd}");

            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading book: {ex.Message}");
        }
    }

    static async Task UseDependencyInjectionExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== Dependency Injection Example ===");

        var loadBookHandler = serviceProvider.GetRequiredService<ILoadBookHandler>();

        var epubFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.epub", SearchOption.AllDirectories);

        if (epubFiles.Length == 0)
        {
            Console.WriteLine("No EPUB files found.");
            return;
        }

        var epubPath = epubFiles[0];
        var command = new LoadBookCommand(epubPath);
        var result = await loadBookHandler.HandleAsync(command);

        if (result.IsSuccess && result.Book != null)
        {
            var book = result.Book;
            Console.WriteLine($"Successfully loaded: {book.Title}");
            Console.WriteLine($"Total chapters: {book.Chapters.Count}");

            // Show first 3 chapter titles
            foreach (var chapter in book.Chapters.Take(3))
            {
                Console.WriteLine($"  - Chapter {chapter.Order + 1}: {chapter.Title}");
            }
        }
        else
        {
            Console.WriteLine($"Failed to load book: {result.ErrorMessage}");
        }

        Console.WriteLine();
    }

    static async Task ProcessChapterExample()
    {
        Console.WriteLine("=== Chapter Processing Example ===");

        var epubFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.epub", SearchOption.AllDirectories);

        if (epubFiles.Length == 0)
        {
            Console.WriteLine("No EPUB files found.");
            return;
        }

        var reader = new EpubReader();
        var book = await reader.LoadBookAsync(epubFiles[0]);

        if (book.Chapters.Count > 0)
        {
            var firstChapter = book.Chapters[0];
            Console.WriteLine($"Processing chapter: {firstChapter.Title}");

            // Demonstrate pagination concept
            var pageSize = 3000;
            var content = firstChapter.GetContentMemory();
            var totalPages = (content.Length / pageSize) + 1;

            Console.WriteLine($"Chapter length: {content.Length} characters");
            Console.WriteLine($"Estimated pages (at {pageSize} chars/page): {totalPages}");
            Console.WriteLine($"Estimated reading time: {firstChapter.EstimateReadingTimeMinutes()} minutes");

            // Show first 500 characters of content (stripped of HTML for display)
            var preview = StripHtml(content.Span.Slice(0, Math.Min(500, content.Length)).ToString());
            Console.WriteLine($"\nPreview: {preview}...");
        }
    }

    static string StripHtml(string html)
    {
        // Very simple HTML stripping for demonstration
        var result = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ");
        return result.Trim();
    }
}
