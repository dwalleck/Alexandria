using BenchmarkDotNet.Running;

namespace Alexandria.Benchmarks;

/// <summary>
/// Entry point for running benchmarks.
/// Run with: dotnet run -c Release --project tests/Alexandria.Benchmarks -- --filter "*ContentAnalyzer*"
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        // Run benchmarks based on command line arguments
        // Default to running ContentAnalyzer benchmarks if no args provided
        if (args.Length == 0)
        {
            args = new[] { "--filter", "*ContentAnalyzer*", "--job", "Short" };
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}