#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:package Spectre.Console@0.57.2

// Small systems of linear integer constraints, solved as Z3 theorems and rendered with
// Spectre.Console. Includes Bart De Smet's example from TechEd Europe 2012, shown over both a
// Symbols<int,int,int> environment and an equivalent value tuple.
//
//   dotnet run demos/linear-systems.cs

using System.Diagnostics;

using Spectre.Console;

using Z3.Linq;

AnsiConsole.Write(new FigletText("Linear Systems").Color(Color.Yellow).Centered());
AnsiConsole.Write(new Rule("[yellow]integer constraint solving[/]"));
AnsiConsole.WriteLine();

// Bart's TechEd Europe 2012 example, over Symbols<int, int, int>.
using (var ctx = new Z3Context())
{
    var theorem = from t in ctx.NewTheorem<Symbols<int, int, int>>()
                  where t.X1 - t.X2 >= 1
                  where t.X1 - t.X2 <= 3
                  where t.X1 == (2 * t.X3) + t.X2
                  select t;

    var (r, ms) = Time(() => theorem.Solve());
    Section("Bart's example (TechEd Europe 2012)",
        ["x1 - x2 >= 1", "x1 - x2 <= 3", "x1 == 2*x3 + x2"],
        [("x1", r.X1), ("x2", r.X2), ("x3", r.X3)], ms);
}

// The same system over a value tuple.
using (var ctx = new Z3Context())
{
    var theorem = from t in ctx.NewTheorem<(int x, int y, int z)>()
                  where t.x - t.y >= 1
                  where t.x - t.y <= 3
                  where t.x == (2 * t.z) + t.y
                  select t;

    var (r, ms) = Time(() => theorem.Solve());
    Section("The same system over a value tuple",
        ["x - y >= 1", "x - y <= 3", "x == 2*z + y"],
        [("x", r.x), ("y", r.y), ("z", r.z)], ms);
}

// A Symbols<int, int> example with an inequality and a distinctness constraint.
using (var ctx = new Z3Context())
{
    var theorem = from t in ctx.NewTheorem<Symbols<int, int>>()
                  where t.X1 < t.X2 + 1
                  where t.X1 > 2
                  where t.X1 != t.X2
                  select t;

    var (r, ms) = Time(() => theorem.Solve());
    Section("A Symbols<int, int> example",
        ["x1 < x2 + 1", "x1 > 2", "x1 != x2"],
        [("x1", r.X1), ("x2", r.X2)], ms);
}

void Section(string title, string[] constraints, (string Name, int Value)[] solution, double ms)
{
    AnsiConsole.Write(new Rule($"[yellow]{title}[/]").LeftJustified());

    var constraintList = new Panel(string.Join("\n", constraints.Select(c => $"[white]{c}[/]")))
        .Header("[white]Constraints[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey);

    var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Yellow)
        .AddColumn(new TableColumn("[white]Symbol[/]").Centered())
        .AddColumn(new TableColumn("[white]Value[/]").Centered());
    foreach (var (name, value) in solution)
    {
        table.AddRow($"[yellow]{name}[/]", $"[white]{value}[/]");
    }

    var layout = new Grid().AddColumn().AddColumn();
    layout.AddRow(constraintList, new Panel(table).Header("[white]Solution[/]").Border(BoxBorder.None));
    AnsiConsole.Write(layout);
    AnsiConsole.MarkupLine($"[grey]  Solved in {ms:F1} ms[/]");
    AnsiConsole.WriteLine();
}

static (T Result, double Ms) Time<T>(Func<T> solve)
{
    var sw = Stopwatch.StartNew();
    T result = solve();
    sw.Stop();
    return (result, sw.Elapsed.TotalMilliseconds);
}
