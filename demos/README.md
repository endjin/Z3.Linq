# Z3.Linq demos

A set of standalone [Spectre.Console](https://spectreconsole.net/) demos for `Z3.Linq`, each a
single-file [.NET file-based app](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10#file-based-apps).
No project, no solution entry - just run the file:

```bash
dotnet run demos/sudoku.cs
```

On a clean clone, populate the local `Microsoft.Z3` package feed first, from the repository root:

```bash
pwsh -File scripts/Install-Z3Package.ps1
```

The `Microsoft.Z3` version the library needs is not on nuget.org, so restore can only find it in
the repository's `_z3-feed` folder. The build script fills that folder automatically, but
`dotnet run` does not run the build script, so without this step the first run fails with
`NU1102: Unable to find package Microsoft.Z3`. The script does nothing if the package is already
there, so it is safe to run again.

The first run of a file restores its packages and builds it; later runs are fast. Each file
declares its own dependencies at the top (`#:package Spectre.Console`, `#:project ...` to the
`Z3.Linq` and `Z3.Linq.Examples` projects in [`../solutions`](../solutions)), so they build against
your local source.

## The demos

Eleven of the demos solve problems from Dennis Yurichev's free book
[SAT/SMT by Example](https://smt.st); each file names the section it comes from. The book is
published under [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/); only the problems'
data is reused, and the code and text are written for this repository.

| File | Problem |
|------|---------|
| [`menu.cs`](menu.cs) | An interactive launcher - pick a demo from a Spectre menu and it runs the others. |
| [`river-crossing.cs`](river-crossing.cs) | Missionaries & Cannibals, showing `Solve` (any plan) against `Optimize`/`orderby` (the shortest plan). |
| [`sudoku.cs`](sudoku.cs) | Two Sudoku puzzles solved on a rendered 9x9 grid, clues driven through expression-tree constraints. |
| [`boolean-logic.cs`](boolean-logic.cs) | `x XOR y` over an anonymous type, a value tuple, and a record - the same theorem, three environment shapes. |
| [`linear-systems.cs`](linear-systems.cs) | Small integer constraint systems, including Bart De Smet's TechEd Europe 2012 example. |
| [`oil-purchase.cs`](oil-purchase.cs) | A least-cost linear program (`orderby` minimises the bill). |
| [`warehouse.cs`](warehouse.cs) | Least-cost shipping across two warehouses to four customers. |
| [`bit-twiddling.cs`](bit-twiddling.cs) | Bit-twiddling proofs over `uint`/`ulong`: XOR swap, Dietz's overflow-free average, branchless abs/min/max, the has-a-zero-byte hack and a modular inverse, each proved by the absence of a counterexample. |
| [`alphametics.cs`](alphametics.cs) | SEND + MORE = MONEY: eight distinct digits found with `Z3Methods.Distinct`. |
| [`zebra.cs`](zebra.cs) | The Zebra (Einstein) puzzle over a nested environment type - who drinks water, who owns the zebra. |
| [`n-queens.cs`](n-queens.cs) | N non-attacking queens, one integer per row, on a template-sized array. |
| [`assignment.cs`](assignment.cs) | The assignment problem: three workers, three jobs, cost minimised through ternary expressions. |
| [`wood-workshop.cs`](wood-workshop.cs) | Kantorovich's 1939 cutting problem - the integer program that started linear programming. |
| [`knapsack.cs`](knapsack.cs) | Two small knapsacks: the most popsicles for $8, and every way to spend exactly $15.05 on XKCD 287's appetizers, found by blocking each answer in turn. |
| [`package-manager.cs`](package-manager.cs) | Dependency resolution: install only the libraries two programs need, at their newest compatible versions, and see a conflict reported as unsatisfiable. |
| [`map-colouring.cs`](map-colouring.cs) | Four-colouring the map of Europe (and proving three colours are not enough), then scheduling committees as graph colouring. |
| [`job-shop.cs`](job-shop.cs) | Job-shop scheduling: three jobs on three machines, `orderby` minimising the makespan, rendered as a Gantt chart. |
| [`sudoku-variants.cs`](sudoku-variants.cs) | Killer Sudoku (cage sums) and Greater-Than Sudoku on a bit-vector grid, which Z3 solves in about a second where integer cells took about a minute (Greater-Than) to several (Killer). |

## Running everything

```bash
# interactive launcher
dotnet run demos/menu.cs

# or one at a time
dotnet run demos/river-crossing.cs
dotnet run demos/sudoku.cs
dotnet run demos/boolean-logic.cs
dotnet run demos/linear-systems.cs
dotnet run demos/oil-purchase.cs
dotnet run demos/warehouse.cs
dotnet run demos/bit-twiddling.cs
dotnet run demos/alphametics.cs
dotnet run demos/zebra.cs
dotnet run demos/n-queens.cs
dotnet run demos/assignment.cs
dotnet run demos/wood-workshop.cs
dotnet run demos/knapsack.cs
dotnet run demos/package-manager.cs
dotnet run demos/map-colouring.cs
dotnet run demos/job-shop.cs
dotnet run demos/sudoku-variants.cs
```

Output is styled when run in a terminal and degrades to plain text when redirected (piped to a file
or another program), so the demos are safe to capture in scripts.
