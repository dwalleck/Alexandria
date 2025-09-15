using Alexandria.Infrastructure.Services;

namespace Alexandria.Benchmarks;

/// <summary>
/// Concrete benchmark implementation for AngleSharpContentAnalyzer.
/// Tests the actual implementation against performance targets.
/// </summary>
public class AngleSharpContentAnalyzerBenchmarks : ContentAnalyzerBenchmarks
{
    /// <summary>
    /// Initialize the AngleSharpContentAnalyzer for benchmarking.
    /// </summary>
    protected override void InitializeAnalyzer()
    {
        Analyzer = new AngleSharpContentAnalyzer();
    }
}