# Z3.Linq.Benchmarks

A [BenchmarkDotNet](https://benchmarkdotnet.org/) suite for finding performance and memory hot paths
in Z3.Linq, and for confirming an optimisation actually helped.

## What it measures

The benchmarks solve theorems that Z3 decides in microseconds, so the time and - more usefully - the
`Allocated` column are dominated by the **library's** per-solve work, not the solver: building the
environment by reflection, translating the constraint expression trees, and marshalling the model
back onto the CLR members. Nothing is cached across solves today, so re-solving the same theorem
re-does all of that every time - which is exactly what these numbers can be used to change.

`SolveBenchmarks` covers the shapes that stress different parts of that path:

| Benchmark | Exercises |
|---|---|
| `ScalarSymbols` (baseline) | a flat `Symbols<int,int>` |
| `ValueTuple` | the value-tuple environment path |
| `NestedObject` | recursive marshalling of a nested object |
| `Collection` | the array element loop |
| `WideSymbols` | more members to reflect and translate (`Symbols<int,int,int,int,int>`) |
| `Optimize` | the optimiser's own solve path |

## Running it

BenchmarkDotNet refuses to run outside Release.

```bash
cd solutions/Z3.Linq.Benchmarks

# everything
dotnet run -c Release -- --filter '*'

# one shape
dotnet run -c Release -- --filter '*NestedObject*'

# list what's available
dotnet run -c Release -- --list flat

# a fast sanity run (one shot each, NOT accurate - for wiring only)
dotnet run -c Release -- --filter '*' --job dry
```

Results print to the console and are written under `BenchmarkDotNet.Artifacts/results/` as GitHub
markdown; add `--exporters json` for machine-readable output.

## The optimisation loop

1. Run the suite and read the `Allocated` and `Mean` columns - the shape that allocates most, or the
   one that scales worst as members/constraints grow, is where to look.
2. Find the allocation in the library (reflection over the environment type, the `ArrayList` and
   `ToArray` in the marshalling loop, boxing of value-type members, the per-solve attribute lookups)
   and change it.
3. Re-run the same filter and compare. Keep the `[Benchmark(Baseline = true)]` scalar case as the
   reference; the `Ratio` column shows movement relative to it.

For regression tracking across commits, export JSON and diff with the
[`bdna`](https://github.com/NewdayTechnology/benchmarkdotnet.analyser) tool, as the endjin
BenchmarkDotNet guidelines describe:

```bash
dotnet run -c Release -- --exporters json
dotnet bdna aggregate --new ./BenchmarkDotNet.Artifacts/results --aggregates ./aggregates --output ./aggregates
dotnet bdna analyse --aggregates ./aggregates --tolerance 10
```

## Extending it

Add a `[Benchmark]` method for any shape or path you want to track - build the theorem once in
`[GlobalSetup]` and measure only the `Solve`/`Optimize`, so the query construction is not counted.
Return the result (do not discard it) so the JIT cannot eliminate the call. To measure how cost
scales, add a `[Params]`-driven count (of constraints or symbols) rather than a fixed theorem.
