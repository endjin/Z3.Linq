#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:project ../solutions/Z3.Linq.Examples/Z3.Linq.Examples.csproj
#:package Spectre.Console@0.57.2

// The same Boolean theorem - find x, y with x XOR y true - expressed three ways: an anonymous type,
// a value tuple, and a record. Shows that the environment type is just a shape Z3.Linq marshals to.
//
//   dotnet run demos/boolean-logic.cs

using System.Diagnostics;

using Spectre.Console;

using Z3.Linq;
using Z3.Linq.Examples;

AnsiConsole.Write(new FigletText("Boolean Logic").Color(Color.Aqua).Centered());
AnsiConsole.Write(new Rule("[aqua]x XOR y[/]"));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    "Find Boolean [aqua]x[/] and [aqua]y[/] such that [yellow]x ^ y[/] holds - the one theorem written\n" +
    "over three different environment types. Z3 solves each the same way.")
    .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
    .AddColumn("[white]Environment type[/]")
    .AddColumn(new TableColumn("[white]x[/]").Centered())
    .AddColumn(new TableColumn("[white]y[/]").Centered())
    .AddColumn(new TableColumn("[white]x ^ y[/]").Centered())
    .AddColumn(new TableColumn("[white]Time[/]").RightAligned());

using (var ctx = new Z3Context())
{
    var theorem = from t in ctx.NewTheorem(new { x = default(bool), y = default(bool) })
                  where t.x ^ t.y
                  select t;
    var (r, ms) = Time(() => theorem.Solve());
    table.AddRow("[grey]anonymous type[/]", Bool(r.x), Bool(r.y), Bool(r.x ^ r.y), Ms(ms));
}

using (var ctx = new Z3Context())
{
    var theorem = from t in ctx.NewTheorem<(bool x, bool y)>()
                  where t.x ^ t.y
                  select t;
    var (r, ms) = Time(() => theorem.Solve());
    table.AddRow("[grey]value tuple[/]", Bool(r.x), Bool(r.y), Bool(r.x ^ r.y), Ms(ms));
}

using (var ctx = new Z3Context())
{
    var theorem = from t in ctx.NewTheorem(new RecordTheorem<bool, bool>())
                  where t.X ^ t.Y
                  select t;
    var (r, ms) = Time(() => theorem.Solve());
    table.AddRow("[grey]record[/]", Bool(r.X), Bool(r.Y), Bool(r.X ^ r.Y), Ms(ms));
}

AnsiConsole.Write(table);
AnsiConsole.MarkupLine("[grey]  Each row is an independent solve; XOR has two solutions, so Z3 may pick either.[/]");

static string Bool(bool b) => b ? "[green]true[/]" : "[red]false[/]";

static string Ms(double ms) => $"[grey]{ms:F1} ms[/]";

static (T Result, double Ms) Time<T>(Func<T> solve)
{
    var sw = Stopwatch.StartNew();
    T result = solve();
    sw.Stop();
    return (result, sw.Elapsed.TotalMilliseconds);
}
