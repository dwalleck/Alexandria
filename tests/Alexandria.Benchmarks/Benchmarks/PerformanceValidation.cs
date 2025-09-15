using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Alexandria.Benchmarks;

/// <summary>
/// Custom column that shows the performance target for each benchmark.
/// </summary>
public class PerformanceTargetColumn : IColumn
{
    public string Id => nameof(PerformanceTargetColumn);
    public string ColumnName => "Target";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 1;
    public bool IsNumeric => false;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Performance target for this benchmark";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var benchmarkName = benchmarkCase.Descriptor.WorkloadMethodDisplayInfo;

        // Define targets based on benchmark name
        return benchmarkName switch
        {
            var name when name.Contains("ExtractPlainText_Large") => "<100ms",
            var name when name.Contains("ExtractPlainText_Small") => "0 allocs",
            var name when name.Contains("CountWords_Small") => "0 allocs",
            var name when name.Contains("CountWords_Large") => "<50ms",
            var name when name.Contains("AnalyzeContent_Large") => "<100ms",
            _ => "-"
        };
    }

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        => GetValue(summary, benchmarkCase);

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    public bool IsAvailable(Summary summary) => true;
}

/// <summary>
/// Custom column that shows Pass/Fail based on performance targets.
/// </summary>
public class PassFailColumn : IColumn
{
    public string Id => nameof(PassFailColumn);
    public string ColumnName => "Pass/Fail";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 0;
    public bool IsNumeric => false;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Whether the benchmark meets its performance target";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var report = summary.Reports.FirstOrDefault(r => r.BenchmarkCase == benchmarkCase);
        if (report == null) return "?";

        var benchmarkName = benchmarkCase.Descriptor.WorkloadMethodDisplayInfo;
        var statistics = report.ResultStatistics;

        if (statistics == null) return "?";

        // Check performance against targets
        bool passed = benchmarkName switch
        {
            // 1MB HTML should process in <100ms
            var name when name.Contains("ExtractPlainText_Large") =>
                statistics.Mean < 100_000_000, // 100ms in nanoseconds

            // Small text should have minimal allocations
            var name when name.Contains("ExtractPlainText_Small") =>
                report.GcStats.GetBytesAllocatedPerOperation(benchmarkCase) < 1024,

            var name when name.Contains("CountWords_Small") =>
                report.GcStats.GetBytesAllocatedPerOperation(benchmarkCase) < 1024,

            // Large text word count should be fast
            var name when name.Contains("CountWords_Large") =>
                statistics.Mean < 50_000_000, // 50ms in nanoseconds

            // Analyze content for large HTML
            var name when name.Contains("AnalyzeContent_Large") =>
                statistics.Mean < 100_000_000, // 100ms in nanoseconds

            _ => true // No specific target
        };

        return passed ? "✓ PASS" : "✗ FAIL";
    }

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        => GetValue(summary, benchmarkCase);

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    public bool IsAvailable(Summary summary) => true;
}

/// <summary>
/// Attribute to mark a benchmark with its expected performance target.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class PerformanceTargetAttribute : Attribute
{
    public double MaxMilliseconds { get; set; }
    public long MaxBytesAllocated { get; set; }
    public string Description { get; set; }

    public PerformanceTargetAttribute(string description)
    {
        Description = description;
    }
}

/// <summary>
/// Static class containing all performance targets for validation.
/// </summary>
public static class ContentAnalyzerPerformanceTargets
{
    /// <summary>
    /// Maximum time to process 1MB of HTML content.
    /// </summary>
    public static readonly TimeSpan MaxTimeFor1MB = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Maximum heap allocations for text under 4KB (1KB allowance).
    /// </summary>
    public const long MaxAllocationsForSmallText = 1024;

    /// <summary>
    /// Maximum time for word counting on 1MB text.
    /// </summary>
    public static readonly TimeSpan MaxTimeForWordCount1MB = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Maximum acceptable deviation from Microsoft Word word count.
    /// </summary>
    public const double MaxWordCountDeviationPercent = 2.0;

    /// <summary>
    /// Maximum acceptable deviation from Medium.com reading time estimates.
    /// </summary>
    public const double MaxReadingTimeDeviationPercent = 10.0;
}