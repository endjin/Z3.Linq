#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:package Spectre.Console@0.57.2

// Kantorovich's wood-workshop problem, solved as a Z3 integer optimisation and rendered with
// Spectre.Console. A 6x13 workpiece can be cut four ways, and each way gives a different number
// of type-A and type-B pieces. The workshop needs 800 A and 400 B; Z3 finds how many workpieces
// to cut each way so that as few workpieces as possible are used. From "SAT/SMT by Example"
// (Dennis Yurichev), section 3.3, https://smt.st.
//
//   dotnet run demos/wood-workshop.cs

using System.Diagnostics;

using Spectre.Console;

using Z3.Linq;

(string Name, int A, int B)[] cuts =
[
    ("Cut 1", 3, 1),
    ("Cut 2", 2, 6),
    ("Cut 3", 1, 9),
    ("Cut 4", 0, 13),
];

AnsiConsole.Write(new FigletText("Wood Workshop").Color(Color.SandyBrown).Centered());
AnsiConsole.Write(new Rule("[sandybrown]the fewest workpieces for the order[/]"));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    "A 6x13 workpiece can be cut four ways. Each way gives (A, B) pieces: " +
    "(3, 1), (2, 6), (1, 9) or (0, 13). The order is exactly [white]800[/] pieces of type A " +
    "and [white]400[/] of type B.\n\n" +
    "Z3 chooses a whole number of workpieces for each cut; [italic]orderby[/] minimises the total.")
    .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

using var ctx = new Z3Context();
var fewest = from t in ctx.NewTheorem<(int cutA, int cutB, int cutC, int cutD)>()
             where t.cutA >= 0 && t.cutB >= 0 && t.cutC >= 0 && t.cutD >= 0
             where (3 * t.cutA) + (2 * t.cutB) + t.cutC == 800
             where t.cutA + (6 * t.cutB) + (9 * t.cutC) + (13 * t.cutD) == 400
             orderby t.cutA + t.cutB + t.cutC + t.cutD
             select t;

var (result, ms) = Time(() =>
{
    (int cutA, int cutB, int cutC, int cutD) plan = default;
    AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("sandybrown"))
        .Start("[sandybrown]Optimising...[/]", _ => plan = fewest.Solve());
    return plan;
});

int[] counts = [result.cutA, result.cutB, result.cutC, result.cutD];

var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.SandyBrown)
    .Title("[sandybrown]Cutting plan[/]")
    .AddColumn("[white]Cut[/]")
    .AddColumn(new TableColumn("[white]A per piece[/]").RightAligned())
    .AddColumn(new TableColumn("[white]B per piece[/]").RightAligned())
    .AddColumn(new TableColumn("[white]Workpieces[/]").RightAligned())
    .AddColumn(new TableColumn("[white]A made[/]").RightAligned())
    .AddColumn(new TableColumn("[white]B made[/]").RightAligned());

for (int i = 0; i < cuts.Length; i++)
{
    string count = counts[i] == 0 ? "[grey]0[/]" : $"[bold sandybrown]{counts[i]}[/]";
    table.AddRow(cuts[i].Name, $"{cuts[i].A}", $"{cuts[i].B}", count, $"{counts[i] * cuts[i].A}", $"{counts[i] * cuts[i].B}");
}

table.AddEmptyRow();
table.AddRow(
    "[white]Total[/]",
    string.Empty,
    string.Empty,
    $"[bold sandybrown]{counts.Sum()}[/]",
    $"[white]{counts.Select((n, i) => n * cuts[i].A).Sum()}[/]",
    $"[white]{counts.Select((n, i) => n * cuts[i].B).Sum()}[/]");

AnsiConsole.Write(table);
AnsiConsole.MarkupLine($"  Fewest workpieces: [bold sandybrown]{counts.Sum()}[/]");
AnsiConsole.MarkupLine($"[grey]  Optimised in {ms:F1} ms[/]");

static (T Result, double Ms) Time<T>(Func<T> solve)
{
    var sw = Stopwatch.StartNew();
    T result = solve();
    sw.Stop();
    return (result, sw.Elapsed.TotalMilliseconds);
}
