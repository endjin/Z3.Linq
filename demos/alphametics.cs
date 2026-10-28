#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:project ../solutions/Z3.Linq.Examples/Z3.Linq.Examples.csproj
#:package Spectre.Console@0.57.2

// SEND + MORE = MONEY: replace each letter with a different digit so that the sum is correct.
// Each letter is an integer symbol from 0 to 9, Distinct makes the digits different, and the
// column arithmetic becomes one linear equation. Z3 finds the assignment; the demo then excludes
// it and asks again, to show that no other answer exists. From "SAT/SMT by Example" (Dennis
// Yurichev), section 8.10, https://smt.st
//
//   dotnet run demos/alphametics.cs

using System.Diagnostics;

using Spectre.Console;

using Z3.Linq;
using Z3.Linq.Examples.Puzzles;

AnsiConsole.Write(new FigletText("Alphametics").Color(Color.Gold1).Centered());
AnsiConsole.Write(new Rule("[gold1]SEND + MORE = MONEY[/]"));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    "    [white]S E N D[/]\n" +
    "  + [white]M O R E[/]\n" +
    "  ---------\n" +
    "  [white]M O N E Y[/]\n\n" +
    "Each letter is a different digit, and no number starts with [white]0[/].\n" +
    "[gold1]Distinct[/] keeps the digits different; the sum is one linear equation.")
    .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

using var ctx = new Z3Context();
var theorem = Alphametics.SendMoreMoney(ctx);

(int S, int E, int N, int D, int M, int O, int R, int Y) r = default;
double solveMs = 0;
AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("gold1"))
    .Start("[gold1]Solving...[/]", _ => (r, solveMs) = Time(() => theorem.Solve()));

var letters = new Table().Border(TableBorder.Rounded).BorderColor(Color.Gold1)
    .Title("[gold1]Digits[/]");
foreach (string letter in new[] { "S", "E", "N", "D", "M", "O", "R", "Y" })
{
    letters.AddColumn(new TableColumn($"[white]{letter}[/]").Centered());
}

letters.AddRow(new[] { r.S, r.E, r.N, r.D, r.M, r.O, r.R, r.Y }.Select(d => $"[gold1]{d}[/]").ToArray());
AnsiConsole.Write(letters);

int send = Alphametics.ToNumber(r.S, r.E, r.N, r.D);
int more = Alphametics.ToNumber(r.M, r.O, r.R, r.E);
int money = Alphametics.ToNumber(r.M, r.O, r.N, r.E, r.Y);

var sum = new Table().Border(TableBorder.Rounded).BorderColor(Color.Gold1)
    .Title("[gold1]The sum[/]")
    .AddColumn("[white]Word[/]")
    .AddColumn(new TableColumn("[white]Number[/]").RightAligned());
sum.AddRow("SEND", $"{send}");
sum.AddRow("MORE", $"{more}");
sum.AddEmptyRow();
sum.AddRow("[white]MONEY[/]", $"[bold gold1]{money}[/]");
AnsiConsole.Write(sum);
AnsiConsole.MarkupLine($"[grey]  Solved in {solveMs:F1} ms - {send} + {more} = {send + more}[/]");
AnsiConsole.WriteLine();

// Exclude the answer just found and ask again. "Unsatisfiable" proves the answer is the only one.
var found = r;
var another = theorem.Where(t => !(t.S == found.S && t.E == found.E && t.N == found.N && t.D == found.D
                                   && t.M == found.M && t.O == found.O && t.R == found.R && t.Y == found.Y));
var (hasAnother, uniqueMs) = Time(() => another.TrySolve(out _));

AnsiConsole.MarkupLine(hasAnother
    ? "[red]  Another assignment also works - the answer is not unique.[/]"
    : "[green]  Unique:[/] with that assignment excluded, Z3 reports the theorem unsatisfiable.");
AnsiConsole.MarkupLine($"[grey]  Checked in {uniqueMs:F1} ms[/]");

static (T Result, double Ms) Time<T>(Func<T> solve)
{
    var sw = Stopwatch.StartNew();
    T result = solve();
    sw.Stop();
    return (result, sw.Elapsed.TotalMilliseconds);
}
