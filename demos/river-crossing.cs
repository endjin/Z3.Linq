#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:project ../solutions/Z3.Linq.Examples/Z3.Linq.Examples.csproj
#:package Spectre.Console@0.57.2

// The Missionaries & Cannibals river-crossing puzzle, solved as a Z3 theorem and rendered with
// Spectre.Console. Three missionaries and three cannibals must cross a river in a two-person boat,
// and the missionaries must never be outnumbered on either bank. Z3 produces the sequence of moves.
//
//   dotnet run demos/river-crossing.cs

using System.Diagnostics;

using Spectre.Console;

using Z3.Linq;
using Z3.Linq.Examples.RiverCrossing;

AnsiConsole.Write(new FigletText("River Crossing").Color(Color.DodgerBlue1).Centered());
AnsiConsole.Write(new Rule("[dodgerblue1]Missionaries & Cannibals[/]"));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    "[white]3[/] missionaries and [white]3[/] cannibals must cross a river in a boat that holds [white]2[/].\n" +
    "On neither bank may missionaries be [red]outnumbered[/] by cannibals, and the boat cannot cross empty.\n" +
    "Z3 is asked only for [italic]a[/] plan by [green]Solve[/], and for the [italic]shortest[/] plan by [green]Optimize[/] and [green]orderby[/].")
    .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

using var ctx = new Z3Context();
var theorem = from t in MissionariesAndCannibals.Create(ctx, 50)
              where t.MissionaryAndCannibalCount == 3
              where t.SizeBoat == 2
              select t;

// Solve(): any valid plan.
MissionariesAndCannibals? plan = null;
var solveTime = Time(() => plan = theorem.Solve());

if (plan is null)
{
    AnsiConsole.MarkupLine("[red]No solution found.[/]");
    return;
}

AnsiConsole.Write(BuildPlan(plan));
AnsiConsole.WriteLine();

// Optimize() and orderby: the shortest plan.
MissionariesAndCannibals? optimized = null;
var optimizeTime = Time(() => optimized = theorem.Optimize(Optimization.Minimize, t => t.Length));

MissionariesAndCannibals? ordered = null;
var orderByTime = Time(() => ordered = (from t in theorem orderby t.Length select t).Solve());

var compare = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
    .AddColumn("[white]Method[/]")
    .AddColumn(new TableColumn("[white]Steps[/]").Centered())
    .AddColumn(new TableColumn("[white]Time[/]").RightAligned());
compare.AddRow("[green]Solve[/] - any plan", plan.Length.ToString(), $"[grey]{solveTime:F0} ms[/]");
compare.AddRow("[green]Optimize[/] - minimise Length", (optimized?.Length)?.ToString() ?? "-", $"[grey]{optimizeTime:F0} ms[/]");
compare.AddRow("[green]orderby[/] - minimise Length", (ordered?.Length)?.ToString() ?? "-", $"[grey]{orderByTime:F0} ms[/]");
AnsiConsole.Write(new Panel(compare).Header("[white]Solve vs. optimise[/]").Border(BoxBorder.None));

// Renders the crossing plan as a table: bank populations at each step, and the move between them.
static Table BuildPlan(MissionariesAndCannibals plan)
{
    int n = plan.MissionaryAndCannibalCount;

    var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.DodgerBlue1)
        .Title("[dodgerblue1]The crossing[/]")
        .AddColumn(new TableColumn("[white]Step[/]").Centered())
        .AddColumn("[white]Start bank[/]")
        .AddColumn(new TableColumn("[white]Boat[/]").Centered())
        .AddColumn("[white]Far bank[/]");

    for (int i = 0; i < plan.Length; i++)
    {
        int m = plan.Missionaries[i];
        int c = plan.Cannibals[i];
        string start = Bank(m, c);
        string far = Bank(n - m, n - c);
        string boat = Move(plan, i);
        table.AddRow((i + 1).ToString(), start, boat, far);
    }

    return table;
}

static string Bank(int missionaries, int cannibals) =>
    $"[yellow]{new string('M', missionaries)}[/][red]{new string('C', cannibals)}[/]" +
    (missionaries + cannibals == 0 ? "[grey](empty)[/]" : $" [grey]({missionaries}M {cannibals}C)[/]");

static string Move(MissionariesAndCannibals plan, int i)
{
    if (i >= plan.Length - 1)
    {
        return "[green]done[/]";
    }

    // Even steps send the boat to the far bank; odd steps bring it back.
    int dm = Math.Abs(plan.Missionaries[i + 1] - plan.Missionaries[i]);
    int dc = Math.Abs(plan.Cannibals[i + 1] - plan.Cannibals[i]);
    string load = $"[yellow]{new string('M', dm)}[/][red]{new string('C', dc)}[/]";
    return i % 2 == 0 ? $"{load} [dodgerblue1]-->[/]" : $"[dodgerblue1]<--[/] {load}";
}

static double Time(Action action)
{
    var sw = Stopwatch.StartNew();
    AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("dodgerblue1"))
        .Start("[dodgerblue1]Solving...[/]", _ => action());
    sw.Stop();
    return sw.Elapsed.TotalMilliseconds;
}
