#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:package Spectre.Console@0.57.2

// A warehouse-logistics linear program, solved as a Z3 theorem and rendered with Spectre.Console.
// Two warehouses ship a product to four customers within stock limits and exact orders; `orderby`
// minimises the total shipping cost.
//
//   dotnet run demos/warehouse.cs

using System.Diagnostics;
using System.Globalization;

using Spectre.Console;

using Z3.Linq;

var money = CultureInfo.CreateSpecificCulture("en-US");

AnsiConsole.Write(new FigletText("Warehouse").Color(Color.MediumPurple).Centered());
AnsiConsole.Write(new Rule("[mediumpurple]least-cost shipping[/]"));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    "Warehouse 1 holds up to [white]60,000[/] units, Warehouse 2 up to [white]80,000[/]. Four customers order\n" +
    "[white]35,000[/] / [white]22,000[/] / [white]18,000[/] / [white]30,000[/] exactly. Per-unit shipping cost varies by lane -\n" +
    "[italic]orderby[/] finds the assignment with the lowest total cost.")
    .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

using var ctx = new Z3Context();
var theorem =
    from t in ctx.NewTheorem<(double w1c1, double w1c2, double w1c3, double w1c4, double w2c1, double w2c2, double w2c3, double w2c4)>()
    where t.w1c1 + t.w1c2 + t.w1c3 + t.w1c4 <= 60_000 // Warehouse 1 availability
    where t.w2c1 + t.w2c2 + t.w2c3 + t.w2c4 <= 80_000 // Warehouse 2 availability
    where t.w1c1 + t.w2c1 == 35_000 && t.w1c1 >= 0 && t.w2c1 >= 0 // Customer 1 order
    where t.w1c2 + t.w2c2 == 22_000 && t.w1c2 >= 0 && t.w2c2 >= 0 // Customer 2 order
    where t.w1c3 + t.w2c3 == 18_000 && t.w1c3 >= 0 && t.w2c3 >= 0 // Customer 3 order
    where t.w1c4 + t.w2c4 == 30_000 && t.w1c4 >= 0 && t.w2c4 >= 0 // Customer 4 order
    orderby (1.00 * t.w1c1) + (3.00 * t.w1c2) + (0.50 * t.w1c3) + (4.00 * t.w1c4) +
            (2.50 * t.w2c1) + (5.00 * t.w2c2) + (1.50 * t.w2c3) + (2.50 * t.w2c4) // Total shipping cost
    select t;

(double w1c1, double w1c2, double w1c3, double w1c4, double w2c1, double w2c2, double w2c3, double w2c4) r = default;
var sw = Stopwatch.StartNew();
AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("mediumpurple"))
    .Start("[mediumpurple]Optimising...[/]", _ => r = theorem.Solve());
sw.Stop();

double total = (1.00 * r.w1c1) + (3.00 * r.w1c2) + (0.50 * r.w1c3) + (4.00 * r.w1c4) +
               (2.50 * r.w2c1) + (5.00 * r.w2c2) + (1.50 * r.w2c3) + (2.50 * r.w2c4);

var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.MediumPurple)
    .Title("[mediumpurple]Units shipped[/]")
    .AddColumn("[white]From \\ To[/]")
    .AddColumn(new TableColumn("[white]Customer 1[/]").RightAligned())
    .AddColumn(new TableColumn("[white]Customer 2[/]").RightAligned())
    .AddColumn(new TableColumn("[white]Customer 3[/]").RightAligned())
    .AddColumn(new TableColumn("[white]Customer 4[/]").RightAligned());
table.AddRow("[white]Warehouse 1[/]", Units(r.w1c1), Units(r.w1c2), Units(r.w1c3), Units(r.w1c4));
table.AddRow("[white]Warehouse 2[/]", Units(r.w2c1), Units(r.w2c2), Units(r.w2c3), Units(r.w2c4));

AnsiConsole.Write(table);
AnsiConsole.MarkupLine($"[white]  Total shipping cost:[/] [bold green]{total.ToString("C", money)}[/]");
AnsiConsole.MarkupLine($"[grey]  Optimised in {sw.Elapsed.TotalMilliseconds:F1} ms[/]");

static string Units(double value) => value <= 0 ? "[grey]-[/]" : $"[mediumpurple]{value:N0}[/]";
