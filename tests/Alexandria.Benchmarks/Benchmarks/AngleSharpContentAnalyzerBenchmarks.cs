using System.Text;
using Alexandria.Infrastructure.Services;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Alexandria.Benchmarks.Benchmarks;

/// <summary>
/// Performance benchmarks for AngleSharpContentAnalyzer following CSharp-Performance-Guide-EPUB-Parser.md guidelines.
/// Run with: dotnet run -c Release --project tests/Alexandria.Benchmarks -- --filter "*AngleSharpContentAnalyzerBenchmarks*"
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class AngleSharpContentAnalyzerBenchmarks
{
    private AngleSharpContentAnalyzer _analyzer = null!;
    private string _smallHtml = null!;
    private string _mediumHtml = null!;
    private string _largeHtml = null!;
    private string _plainText = null!;
    private string _complexText = null!;
    private char[] _reuseBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _analyzer = new AngleSharpContentAnalyzer();
        _reuseBuffer = new char[8192];

        // Small HTML (typical EPUB chapter paragraph)
        _smallHtml = @"<p>This is a typical paragraph in an EPUB chapter with <strong>some</strong>
                      <em>formatting</em> and &amp; entities. It contains about 20 words total.</p>";

        // Medium HTML (typical EPUB chapter)
        var sb = new StringBuilder();
        sb.Append("<html><body>");
        for (int i = 0; i < 100; i++)
        {
            sb.Append($"<p>Paragraph {i} contains some text content. ");
            sb.Append("It has <strong>bold</strong> and <em>italic</em> formatting.</p>");
        }
        sb.Append("</body></html>");
        _mediumHtml = sb.ToString();

        // Large HTML (1MB - performance target test)
        sb.Clear();
        sb.Append("<html><body>");
        for (int i = 0; i < 10000; i++)
        {
            sb.Append($"<p>This is paragraph {i} with sample content that needs processing. ");
            sb.Append("Contains <strong>bold</strong>, <em>italic</em> &amp; entities.</p>");
        }
        sb.Append("</body></html>");
        _largeHtml = sb.ToString();

        // Plain text for word counting
        var words = new string[10000];
        for (int i = 0; i < words.Length; i++)
        {
            words[i] = i % 3 == 0 ? "word" : (i % 3 == 1 ? "can't" : "state-of-the-art");
        }
        _plainText = string.Join(" ", words);

        // Complex text for readability scoring
        sb.Clear();
        for (int i = 0; i < 100; i++)
        {
            if (i % 3 == 0)
                sb.Append("The cat sat on the mat. ");
            else if (i % 3 == 1)
                sb.Append("The intelligent feline contemplated its existence thoroughly. ");
            else
                sb.Append("The extraordinarily perspicacious quadruped ruminated upon multifarious philosophical quandaries. ");
        }
        _complexText = sb.ToString();
    }

    [Benchmark(Baseline = true)]
    public string ExtractPlainText_Small()
    {
        return _analyzer.ExtractPlainText(_smallHtml.AsSpan());
    }

    [Benchmark]
    public string ExtractPlainText_Small_WithBuffer()
    {
        return _analyzer.ExtractPlainText(_smallHtml.AsSpan(), _reuseBuffer);
    }

    [Benchmark]
    public string ExtractPlainText_Medium()
    {
        return _analyzer.ExtractPlainText(_mediumHtml.AsSpan());
    }

    [Benchmark]
    public string ExtractPlainText_Large_1MB()
    {
        // Performance target: Process 1MB HTML in under 100ms
        return _analyzer.ExtractPlainText(_largeHtml.AsSpan());
    }

    [Benchmark]
    public int CountWords_Small()
    {
        return _analyzer.CountWords("The quick brown fox jumps over the lazy dog".AsSpan());
    }

    [Benchmark]
    public int CountWords_WithContractions()
    {
        return _analyzer.CountWords("I can't won't shouldn't wouldn't".AsSpan());
    }

    [Benchmark]
    public int CountWords_WithHyphens()
    {
        return _analyzer.CountWords("state-of-the-art well-known up-to-date".AsSpan());
    }

    [Benchmark]
    public int CountWords_Large()
    {
        return _analyzer.CountWords(_plainText.AsSpan());
    }

    [Benchmark]
    public TimeSpan EstimateReadingTime()
    {
        return _analyzer.EstimateReadingTime(_plainText.AsSpan());
    }

    [Benchmark]
    public string[] ExtractSentences_First10()
    {
        return _analyzer.ExtractSentences(_complexText.AsSpan(), 10);
    }

    [Benchmark]
    public string GeneratePreview_200Chars()
    {
        return _analyzer.GeneratePreview(_plainText.AsSpan(), 200);
    }

    [Benchmark]
    public string ExtractSnippet_WithSearch()
    {
        return _analyzer.ExtractSnippet(_plainText.AsSpan(), "word", 100);
    }

    [Benchmark]
    public string HighlightTerms_MultipleTerms()
    {
        var terms = new[] { "word", "can't", "state" };
        return _analyzer.HighlightTerms(_plainText.Substring(0, 500), terms);
    }

    [Benchmark]
    public double CalculateReadabilityScore()
    {
        return _analyzer.CalculateReadabilityScore(_complexText.AsSpan());
    }

    [Benchmark]
    public async Task<Domain.ValueObjects.ContentMetrics> AnalyzeContent_Small()
    {
        return await _analyzer.AnalyzeContentAsync(_smallHtml);
    }

    [Benchmark]
    public async Task<Domain.ValueObjects.ContentMetrics> AnalyzeContent_Medium()
    {
        return await _analyzer.AnalyzeContentAsync(_mediumHtml);
    }

    [Benchmark]
    public int CountSentences()
    {
        return _analyzer.CountSentences(_complexText.AsSpan());
    }

    [Benchmark]
    public int CountParagraphs()
    {
        return _analyzer.CountParagraphs(_mediumHtml.AsSpan());
    }
}