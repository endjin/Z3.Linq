#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:package Spectre.Console@0.57.2

// The classic oil-purchase linear program, solved as a Z3 theorem and rendered with
// Spectre.Console. Buy crude from Saudi Arabia and Venezuela to meet minimum yields of three
// products at the least cost. `orderby` asks Z3 to minimise total spend.
//
//   dotnet run demos/oil-purchase.cs

using System.Diagnostics;
using System.Globalization;

using Spectre.Console;

using Z3.Linq;

var money = CultureInfo.CreateSpecificCulture("en-US");

AnsiConsole.Write(new FigletText("Oil Purchase").Color(Color.Orange1).Centered());
AnsiConsole.Write(new Rule("[orange1]a least-cost linear program[/]"));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    "Each barrel of [orange1]Saudi[/] crude yields 0.3 gasoline / 0.4 jet fuel / 0.2 lubricant;\n" +
    "each [orange1]Venezuelan[/] barrel yields 0.4 / 0.2 / 0.3. Demand is at least [white]1900[/] / [white]1500[/] / [white]500[/].\n" +
    "Saudi is [green]$20[/]/barrel, Venezuela [green]$15[/] - [italic]orderby[/] minimises the total bill.")
    .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

using var ctx = new Z3Context();
var solveable = from t in ctx.NewTheorem<(double vz, double sa)>()
                where 0.3 * t.sa + 0.4 * t.vz >= 1900
                where 0.4 * t.sa + 0.2 * t.vz >= 1500
                where 0.2 * t.sa + 0.3 * t.vz >= 500
                where 0 <= t.sa && t.sa <= 9000
                where 0 <= t.vz && t.vz <= 6000
                orderby (20.0 * t.sa) + (15.0 * t.vz)
                select t;

(double vz, double sa) result = default;
var sw = Stopwatch.StartNew();
AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("orange1"))
    .Start("[orange1]Optimising...[/]", _ => result = solveable.Solve());
sw.Stop();

double saCost = result.sa * 20;
double vzCost = result.vz * 15;

var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Orange1)
    .Title("[orange1]Cheapest purchase[/]")
    .AddColumn("[white]Source[/]")
    .AddColumn(new TableColumn("[white]Barrels[/]").RightAligned())
    .AddColumn(new TableColumn("[white]Cost[/]").RightAligned());
table.AddRow("Saudi Arabia", $"{result.sa:N0}", $"[green]{saCost.ToString("C", money)}[/]");
table.AddRow("Venezuela", $"{result.vz:N0}", $"[green]{vzCost.ToString("C", money)}[/]");
table.AddEmptyRow();
table.AddRow("[white]Total[/]", string.Empty, $"[bold green]{(saCost + vzCost).ToString("C", money)}[/]");

AnsiConsole.Write(table);
AnsiConsole.MarkupLine($"[grey]  Optimised in {sw.Elapsed.TotalMilliseconds:F1} ms[/]");
