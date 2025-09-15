using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using Alexandria.Domain.Services;

namespace Alexandria.Benchmarks;

/// <summary>
/// Base benchmark class for testing IContentAnalyzer implementations against performance targets.
/// Performance Targets:
/// - Process 1MB HTML in under 100ms
/// - Zero heap allocations for text under 4KB
/// - Word counting accuracy within ±2% of Microsoft Word
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90, warmupCount: 3, iterationCount: 10)]
[Config(typeof(Config))]
public abstract class ContentAnalyzerBenchmarks
{
    protected IContentAnalyzer Analyzer { get; set; } = null!;

    // Test data of various sizes
    protected string SmallHtml { get; private set; } = null!;    // 1KB
    protected string MediumHtml { get; private set; } = null!;   // 100KB
    protected string LargeHtml { get; private set; } = null!;    // 1MB

    protected string SmallText { get; private set; } = null!;    // 1KB plain text
    protected string MediumText { get; private set; } = null!;   // 100KB plain text
    protected string LargeText { get; private set; } = null!;    // 1MB plain text

    private class Config : ManualConfig
    {
        public Config()
        {
            WithOptions(ConfigOptions.DisableOptimizationsValidator);
            AddColumn(new PerformanceTargetColumn());
            AddColumn(new PassFailColumn());
            WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(50));
        }
    }

    [GlobalSetup]
    public virtual void Setup()
    {
        // Generate test HTML content
        SmallHtml = GenerateHtmlContent(1024);           // 1KB
        MediumHtml = GenerateHtmlContent(100 * 1024);    // 100KB
        LargeHtml = GenerateHtmlContent(1024 * 1024);    // 1MB

        // Generate plain text content
        SmallText = GeneratePlainText(1024);             // 1KB
        MediumText = GeneratePlainText(100 * 1024);      // 100KB
        LargeText = GeneratePlainText(1024 * 1024);      // 1MB

        // Derived classes must set the Analyzer property
        InitializeAnalyzer();
    }

    /// <summary>
    /// Derived classes must implement this to set the Analyzer property.
    /// </summary>
    protected abstract void InitializeAnalyzer();

    #region ExtractPlainText Benchmarks

    [Benchmark]
    [BenchmarkCategory("ExtractPlainText", "Small")]
    [PerformanceTarget("Zero allocations for text <4KB", MaxBytesAllocated = 1024)]
    public string ExtractPlainText_Small()
    {
        return Analyzer.ExtractPlainText(SmallHtml.AsSpan());
    }

    [Benchmark]
    [BenchmarkCategory("ExtractPlainText", "Medium")]
    public string ExtractPlainText_Medium()
    {
        return Analyzer.ExtractPlainText(MediumHtml.AsSpan());
    }

    [Benchmark]
    [BenchmarkCategory("ExtractPlainText", "Large")]
    [PerformanceTarget("Process 1MB HTML in <100ms", MaxMilliseconds = 100)]
    public string ExtractPlainText_Large()
    {
        return Analyzer.ExtractPlainText(LargeHtml.AsSpan());
    }

    [Benchmark]
    [BenchmarkCategory("ExtractPlainText", "WithBuffer")]
    public string ExtractPlainText_WithBuffer()
    {
        var buffer = new char[8192]; // 8KB buffer
        return Analyzer.ExtractPlainText(MediumHtml.AsSpan(), buffer);
    }

    #endregion

    #region CountWords Benchmarks

    [Benchmark]
    [BenchmarkCategory("CountWords", "Small")]
    [PerformanceTarget("Zero allocations for small text", MaxBytesAllocated = 1024)]
    public int CountWords_Small()
    {
        return Analyzer.CountWords(SmallText.AsSpan());
    }

    [Benchmark]
    [BenchmarkCategory("CountWords", "Medium")]
    public int CountWords_Medium()
    {
        return Analyzer.CountWords(MediumText.AsSpan());
    }

    [Benchmark]
    [BenchmarkCategory("CountWords", "Large")]
    [PerformanceTarget("Count words in 1MB text in <50ms", MaxMilliseconds = 50)]
    public int CountWords_Large()
    {
        return Analyzer.CountWords(LargeText.AsSpan());
    }

    #endregion

    #region EstimateReadingTime Benchmarks

    [Benchmark]
    [BenchmarkCategory("ReadingTime")]
    public TimeSpan EstimateReadingTime_Medium()
    {
        return Analyzer.EstimateReadingTime(MediumText.AsSpan());
    }

    #endregion

    #region AnalyzeContent Benchmarks

    [Benchmark]
    [BenchmarkCategory("AnalyzeContent", "Small")]
    public async Task AnalyzeContent_Small()
    {
        await Analyzer.AnalyzeContentAsync(SmallHtml);
    }

    [Benchmark]
    [BenchmarkCategory("AnalyzeContent", "Large")]
    public async Task AnalyzeContent_Large()
    {
        await Analyzer.AnalyzeContentAsync(LargeHtml);
    }

    #endregion

    #region Helper Methods

    private static string GenerateHtmlContent(int targetSizeInBytes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head><title>Test Document</title></head>");
        sb.AppendLine("<body>");

        // Generate paragraphs with Lorem Ipsum-like content
        var paragraph = @"<p>Lorem ipsum dolor sit amet, consectetur adipiscing elit.
            Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.
            Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris
            nisi ut aliquip ex ea commodo consequat.</p>";

        var currentSize = sb.Length;
        while (currentSize < targetSizeInBytes)
        {
            sb.AppendLine(paragraph);
            sb.AppendLine($"<h2>Section {currentSize / 1000}</h2>");
            currentSize = sb.Length;
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        var result = sb.ToString();

        // Trim to exact size if needed
        if (result.Length > targetSizeInBytes)
        {
            result = result.Substring(0, targetSizeInBytes);
        }

        return result;
    }

    private static string GeneratePlainText(int targetSizeInBytes)
    {
        var sb = new StringBuilder();

        // Common English words for realistic word counting
        var words = new[]
        {
            "the", "be", "to", "of", "and", "a", "in", "that", "have", "I",
            "it", "for", "not", "on", "with", "he", "as", "you", "do", "at",
            "this", "but", "his", "by", "from", "they", "we", "say", "her", "she",
            "or", "an", "will", "my", "one", "all", "would", "there", "their",
            "what", "so", "up", "out", "if", "about", "who", "get", "which", "go"
        };

        var random = new Random(42); // Fixed seed for reproducible benchmarks
        var currentSize = 0;

        while (currentSize < targetSizeInBytes)
        {
            // Add a sentence
            var sentenceLength = random.Next(10, 20);
            for (int i = 0; i < sentenceLength; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(words[random.Next(words.Length)]);
            }
            sb.Append(". ");

            // Add paragraph break occasionally
            if (random.Next(10) == 0)
            {
                sb.AppendLine();
                sb.AppendLine();
            }

            currentSize = sb.Length;
        }

        var result = sb.ToString();

        // Trim to exact size if needed
        if (result.Length > targetSizeInBytes)
        {
            result = result.Substring(0, targetSizeInBytes);
        }

        return result;
    }

    #endregion
}