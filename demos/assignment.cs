#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:package Spectre.Console@0.57.2

// The assignment problem, solved as a Z3 optimisation and rendered with Spectre.Console. Three
// workers each take a different one of three jobs, and each worker charges a different price for
// each job. Z3 picks the assignment with the lowest total cost: `Distinct` gives every worker a
// different job, and `orderby` minimises the bill. The cost table is from "SAT/SMT by Example"
// (Dennis Yurichev), section 14.5, https://smt.st.
//
//   dotnet run demos/assignment.cs

using System.Diagnostics;

using Spectre.Console;

using Z3.Linq;

// Rows are workers, columns are jobs.
int[,] costs =
{
    { 250, 400, 350 },
    { 400, 600, 350 },
    { 200, 400, 250 },
};

AnsiConsole.Write(new FigletText("Assignment").Color(Color.Salmon1).Centered());
AnsiConsole.Write(new Rule("[salmon1]one job per worker, at the least total cost[/]"));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    "Three workers, three jobs. Each worker quotes a price for each job (the table below). " +
    "Every worker must get exactly one job, and no two workers the same job.\n\n" +
    "[italic]Z3Methods.Distinct[/] keeps the jobs apart; [italic]orderby[/] minimises the total cost.")
    .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

using var ctx = new Z3Context();
var cheapest = from t in ctx.NewTheorem<(int w0, int w1, int w2)>()
               where t.w0 >= 0 && t.w0 <= 2 && t.w1 >= 0 && t.w1 <= 2 && t.w2 >= 0 && t.w2 <= 2
               where Z3Methods.Distinct(t.w0, t.w1, t.w2)
               orderby (t.w0 == 0 ? 250 : t.w0 == 1 ? 400 : 350)
                     + (t.w1 == 0 ? 400 : t.w1 == 1 ? 600 : 350)
                     + (t.w2 == 0 ? 200 : t.w2 == 1 ? 400 : 250)
               select t;

var (result, ms) = Time(() =>
{
    (int w0, int w1, int w2) assignment = default;
    AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("salmon1"))
        .Start("[salmon1]Optimising...[/]", _ => assignment = cheapest.Solve());
    return assignment;
});

int[] jobOf = [result.w0, result.w1, result.w2];

var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Salmon1)
    .Title("[salmon1]Cost of each worker on each job[/]")
    .AddColumn("[white]Worker[/]");
for (int job = 0; job < 3; job++)
{
    table.AddColumn(new TableColumn($"[white]Job {job}[/]").RightAligned());
}

int total = 0;
for (int worker = 0; worker < 3; worker++)
{
    var cells = new List<string> { $"Worker {worker}" };
    for (int job = 0; job < 3; job++)
    {
        bool chosen = jobOf[worker] == job;
        cells.Add(chosen ? $"[bold salmon1]{costs[worker, job]}[/]" : $"[grey]{costs[worker, job]}[/]");
    }

    total += costs[worker, jobOf[worker]];
    table.AddRow(cells.ToArray());
}

AnsiConsole.Write(table);
AnsiConsole.MarkupLine($"  Assignment: {string.Join(", ", jobOf.Select((job, worker) => $"worker {worker} -> job {job}"))}");
AnsiConsole.MarkupLine($"  Total cost: [bold salmon1]{total}[/] [grey](the cheapest of all 3! = 6 assignments)[/]");
AnsiConsole.MarkupLine($"[grey]  Optimised in {ms:F1} ms[/]");

static (T Result, double Ms) Time<T>(Func<T> solve)
{
    var sw = Stopwatch.StartNew();
    T result = solve();
    sw.Stop();
    return (result, sw.Elapsed.TotalMilliseconds);
}
