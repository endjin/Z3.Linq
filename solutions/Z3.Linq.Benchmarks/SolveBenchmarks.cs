namespace Z3.Linq.Benchmarks;

using BenchmarkDotNet.Attributes;

using Z3.Linq;

/// <summary>
/// End-to-end <c>Solve</c>/<c>Optimize</c> across the environment shapes the library supports,
/// with <see cref="MemoryDiagnoserAttribute"/> so the allocation of each shape is visible.
/// </summary>
/// <remarks>
/// <para>
/// Each benchmark solves a theorem that Z3 decides in microseconds, so the time and - more usefully
/// - the allocation are dominated by the library's own per-solve work rather than by the solver:
/// building the environment by reflection, translating the constraint expression trees, and
/// marshalling the model back onto the CLR members. That is exactly the work an optimisation would
/// target, and the <c>Allocated</c> column is where a hot path shows up.
/// </para>
/// <para>
/// The theorem for each shape is built once in <see cref="Setup"/>; the benchmark measures only the
/// solve, which rebuilds the environment and translation every time (nothing is cached across
/// solves today - the point these numbers can be used to change). The shapes are chosen to exercise
/// different parts of that path: a flat symbol set, a value tuple, a nested object (recursive
/// marshalling), a collection (the element loop), a wider symbol set (more members to reflect and
/// translate), and an optimisation (the optimiser's own solve path).
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class SolveBenchmarks
{
    private Z3Context context = null!;
    private Theorem<Symbols<int, int>> scalar = null!;
    private Theorem<(int A, int B)> tuple = null!;
    private Theorem<NestedEnvironment> nested = null!;
    private Theorem<CollectionEnvironment> collection = null!;
    private Theorem<Symbols<int, int, int, int, int>> wide = null!;
    private Theorem<Symbols<int, int>> optimizeSource = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.context = new Z3Context();

        this.scalar = this.context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == 42)
            .Where(t => t.X2 == t.X1 + 1);

        this.tuple = this.context.NewTheorem<(int A, int B)>()
            .Where(t => t.A == 3)
            .Where(t => t.B == t.A * 2);

        this.nested = this.context.NewTheorem<NestedEnvironment>()
            .Where(t => t.Inner.A == 4)
            .Where(t => t.Inner.B == t.Top * 2)
            .Where(t => t.Top == 5);

        this.collection = this.context.NewTheorem<CollectionEnvironment>()
            .Where(t => t.Values[0] == 10)
            .Where(t => t.Values[1] == 20)
            .Where(t => t.Values[2] == 30)
            .Where(t => t.Length == 3);

        this.wide = this.context.NewTheorem<Symbols<int, int, int, int, int>>()
            .Where(t => t.X1 == 1)
            .Where(t => t.X2 == 2)
            .Where(t => t.X3 == 3)
            .Where(t => t.X4 == 4)
            .Where(t => t.X5 == 5);

        this.optimizeSource = this.context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 >= 0)
            .Where(t => t.X1 <= 100)
            .Where(t => t.X2 == 1);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.context.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Symbols<int, int>? ScalarSymbols()
    {
        return this.scalar.Solve();
    }

    [Benchmark]
    public (int A, int B) ValueTuple()
    {
        return this.tuple.Solve();
    }

    // Returns object? (not the private environment type) so the public benchmark method does not
    // expose a nested type; the reference-type result is not boxed, so the allocation figures stay
    // honest.
    [Benchmark]
    public object? NestedObject()
    {
        return this.nested.Solve();
    }

    [Benchmark]
    public object? Collection()
    {
        return this.collection.Solve();
    }

    [Benchmark]
    public Symbols<int, int, int, int, int>? WideSymbols()
    {
        return this.wide.Solve();
    }

    [Benchmark]
    public Symbols<int, int>? Optimize()
    {
        return this.optimizeSource.Optimize(Optimization.Maximize, t => t.X1);
    }

    private sealed class NestedEnvironment
    {
        public InnerEnvironment Inner { get; set; } = new();

        public int Top { get; set; }
    }

    private sealed class InnerEnvironment
    {
        public int A { get; set; }

        public int B { get; set; }
    }

    private sealed class CollectionEnvironment
    {
        public int[] Values { get; set; } = new int[3];

        public int Length { get; set; }
    }
}
