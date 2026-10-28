#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:project ../solutions/Z3.Linq.Examples/Z3.Linq.Examples.csproj
#:package Spectre.Console@0.57.2

// Job-shop scheduling, solved as a Z3 theorem and rendered with Spectre.Console. Three jobs each
// run a fixed sequence of tasks on three machines; a machine does one task at a time, and a task
// waits for the previous task of its job. `orderby` asks Z3 for the shortest schedule, and a
// second query proves that nothing shorter exists. From "SAT/SMT by Example" (Dennis Yurichev),
// section 22.8, https://smt.st
//
//   dotnet run demos/job-shop.cs

using System.Diagnostics;

using Spectre.Console;

using Z3.Linq;
using Z3.Linq.Examples.Scheduling;

string[] jobColours = ["orchid", "deepskyblue1", "gold1"];
var jobs = JobShop.ThreeJobs;
var tasks = JobShop.Tasks(jobs);
int machineCount = tasks.Max(task => task.Machine) + 1;

AnsiConsole.Write(new FigletText("Job Shop").Color(Color.Orchid).Centered());
AnsiConsole.Write(new Rule("[orchid]the shortest schedule[/]"));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    "Each job is a fixed sequence of tasks, and each task needs one machine for a\n" +
    "number of time units. A task cannot start until the previous task of its job\n" +
    "is done, and a machine runs one task at a time. The makespan - when the last\n" +
    "task ends - is a symbol, so [italic]orderby[/] asks Z3 for the shortest schedule.")
    .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

var input = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
    .Title("[white]The jobs[/]")
    .AddColumn("[white]Job[/]")
    .AddColumn("[white]Tasks, in order (machine: duration)[/]");
for (int job = 0; job < jobs.Length; job++)
{
    input.AddRow(
        $"[{jobColours[job]}]Job {job}[/]",
        string.Join("  [grey]->[/]  ", jobs[job].Select(task => $"M{task.Machine}: {task.Duration}")));
}

AnsiConsole.Write(input);
AnsiConsole.WriteLine();

using var ctx = new Z3Context();
var theorem = JobShop.Create(ctx, jobs);

JobShop? schedule = null;
var solveTime = Time(() => schedule = (from t in theorem orderby t.Makespan select t).Solve());

if (schedule is null)
{
    AnsiConsole.MarkupLine("[red]No schedule found.[/]");
    return;
}

AnsiConsole.Write(BuildGantt(schedule, tasks, machineCount, jobColours));
AnsiConsole.MarkupLine($"[grey]  Makespan [white]{schedule.Makespan}[/] - solved in {solveTime:F1} ms[/]");
AnsiConsole.WriteLine();

// Optimal, not just good: one unit less is unsatisfiable.
int shorter = schedule.Makespan - 1;
bool fitsShorter = false;
var proofTime = Time(() => fitsShorter = theorem.Where(t => t.Makespan <= shorter).TrySolve(out _));
AnsiConsole.MarkupLine(fitsShorter
    ? $"[red]A schedule of {shorter} units exists - the optimiser missed it.[/]"
    : $"[orchid]No schedule fits in {shorter} units[/]: Z3 reports the theorem unsatisfiable [grey]({proofTime:F1} ms)[/].");
AnsiConsole.MarkupLine("[grey]  Other schedules also take the same time; Z3 returns one of them.[/]");

// A Gantt chart: one row per machine, one column per time unit, each cell coloured by job.
static Table BuildGantt(JobShop schedule, (int Job, int Step, int Machine, int Duration)[] tasks, int machineCount, string[] jobColours)
{
    var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Orchid)
        .Title("[orchid]The schedule[/]")
        .AddColumn("[white]Machine[/]");
    for (int time = 0; time < schedule.Makespan; time++)
    {
        table.AddColumn(new TableColumn($"[white]{time}[/]").Centered());
    }

    for (int machine = 0; machine < machineCount; machine++)
    {
        var row = Enumerable.Repeat("[grey].[/]", schedule.Makespan).ToArray();
        for (int i = 0; i < tasks.Length; i++)
        {
            if (tasks[i].Machine != machine)
            {
                continue;
            }

            for (int time = schedule.Start[i]; time < schedule.Start[i] + tasks[i].Duration; time++)
            {
                row[time] = $"[black on {jobColours[tasks[i].Job]}] {tasks[i].Job} [/]";
            }
        }

        table.AddRow([$"M{machine}", .. row]);
    }

    return table;
}

static double Time(Action action)
{
    var sw = Stopwatch.StartNew();
    AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("orchid"))
        .Start("[orchid]Solving...[/]", _ => action());
    sw.Stop();
    return sw.Elapsed.TotalMilliseconds;
}
