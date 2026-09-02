namespace Z3.Linq.Benchmarks;

using BenchmarkDotNet.Running;

/// <summary>
/// Entry point for the Z3.Linq benchmark suite.
/// </summary>
/// <remarks>
/// Uses <see cref="BenchmarkSwitcher"/> rather than a single <c>BenchmarkRunner.Run</c> so the
/// suite is driven from the command line - <c>dotnet run -c Release -- --filter '*'</c> runs
/// everything, <c>--filter '*Solve*'</c> a subset, <c>--list flat</c> enumerates them. See the
/// project README for the optimise/re-measure loop.
/// </remarks>
public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
