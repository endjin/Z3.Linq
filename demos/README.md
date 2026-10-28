# Z3.Linq demos

A set of standalone [Spectre.Console](https://spectreconsole.net/) demos for `Z3.Linq`, each a
single-file [.NET file-based app](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10#file-based-apps).
No project, no solution entry - just run the file:

```bash
dotnet run demos/sudoku.cs
```

The first run of a file restores its packages and builds it; later runs are fast. Each file
declares its own dependencies at the top (`#:package Spectre.Console`, `#:project ...` to the
`Z3.Linq` and `Z3.Linq.Examples` projects in [`../solutions`](../solutions)), so they build against
your local source.

## The demos

| File | Problem |
|------|---------|
| [`menu.cs`](menu.cs) | An interactive launcher - pick a demo from a Spectre menu and it runs the others. |
| [`river-crossing.cs`](river-crossing.cs) | Missionaries & Cannibals, showing `Solve` (any plan) against `Optimize`/`orderby` (the shortest plan). |
| [`sudoku.cs`](sudoku.cs) | Two Sudoku puzzles solved on a rendered 9x9 grid, clues driven through expression-tree constraints. |
| [`boolean-logic.cs`](boolean-logic.cs) | `x XOR y` over an anonymous type, a value tuple, and a record - the same theorem, three environment shapes. |
| [`linear-systems.cs`](linear-systems.cs) | Small integer constraint systems, including Bart De Smet's TechEd Europe 2012 example. |
| [`oil-purchase.cs`](oil-purchase.cs) | A least-cost linear program (`orderby` minimises the bill). |
| [`warehouse.cs`](warehouse.cs) | Least-cost shipping across two warehouses to four customers. |

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
```

Output is styled when run in a terminal and degrades to plain text when redirected (piped to a file
or another program), so the demos are safe to capture in scripts.
