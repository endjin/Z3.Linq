#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:package Spectre.Console@0.57.2

// Two knapsack-style problems, solved with Z3 and rendered with Spectre.Console. The popsicle
// problem asks for the most popsicles that $8 buys, so `orderby ... descending` maximises the
// count. XKCD 287 ("NP-Complete") asks for an order that costs exactly $15.05; there is no
// objective, so the demo finds every order by blocking each one Z3 returns and solving again.
// From "SAT/SMT by Example" (Dennis Yurichev), sections 10.1 and 3.2, https://smt.st.
//
//   dotnet run demos/knapsack.cs

using System.Diagnostics;

using Spectre.Console;

using Z3.Linq;

AnsiConsole.Write(new FigletText("Knapsack").Color(Color.Lime).Centered());
AnsiConsole.Write(new Rule("[lime]fill the budget[/]"));
AnsiConsole.WriteLine();

Popsicles();
AnsiConsole.WriteLine();
Xkcd287();

static void Popsicles()
{
    AnsiConsole.Write(new Rule("[lime]Popsicles[/]").LeftJustified());
    AnsiConsole.Write(new Panel(
        "Single popsicles cost [green]$1[/], a 3-pack [green]$2[/] and a 5-pack [green]$3[/]. " +
        "What is the most popsicles that [white]$8[/] buys?\n\n" +
        "[italic]orderby ... descending[/] maximises the count.")
        .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));

    using var ctx = new Z3Context();
    var most = from t in ctx.NewTheorem<(int singles, int threes, int fives)>()
               where t.singles >= 0 && t.threes >= 0 && t.fives >= 0
               where t.singles + (2 * t.threes) + (3 * t.fives) == 8
               orderby t.singles + (3 * t.threes) + (5 * t.fives) descending
               select t;

    var (result, ms) = Time(() =>
    {
        (int singles, int threes, int fives) basket = default;
        AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("lime"))
            .Start("[lime]Optimising...[/]", _ => basket = most.Solve());
        return basket;
    });

    var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Lime)
        .Title("[lime]Best basket[/]")
        .AddColumn("[white]Item[/]")
        .AddColumn(new TableColumn("[white]Price[/]").RightAligned())
        .AddColumn(new TableColumn("[white]Bought[/]").RightAligned())
        .AddColumn(new TableColumn("[white]Popsicles[/]").RightAligned());
    table.AddRow("Single", "$1", Count(result.singles), $"{result.singles}");
    table.AddRow("3-pack", "$2", Count(result.threes), $"{3 * result.threes}");
    table.AddRow("5-pack", "$3", Count(result.fives), $"{5 * result.fives}");
    table.AddEmptyRow();
    table.AddRow(
        "[white]Total[/]",
        $"[green]${result.singles + (2 * result.threes) + (3 * result.fives)}[/]",
        string.Empty,
        $"[bold lime]{result.singles + (3 * result.threes) + (5 * result.fives)}[/]");

    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine($"[grey]  Optimised in {ms:F1} ms[/]");
}

static void Xkcd287()
{
    AnsiConsole.Write(new Rule("[lime]XKCD 287: NP-Complete[/]").LeftJustified());
    AnsiConsole.Write(new Panel(
        "The waiter wants appetisers that cost exactly [white]$15.05[/]: " +
        "mixed fruit $2.15, french fries $2.75, side salad $3.35, hot wings $3.55, " +
        "mozzarella sticks $4.20, sampler plate $5.80.\n\n" +
        "There is nothing to optimise, so each order Z3 finds is blocked with a new " +
        "[italic]where[/], and the theorem is solved again until no order is left.")
        .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));

    (string Name, int Cents)[] menu =
    [
        ("Mixed fruit", 215),
        ("French fries", 275),
        ("Side salad", 335),
        ("Hot wings", 355),
        ("Mozzarella sticks", 420),
        ("Sampler plate", 580),
    ];

    using var ctx = new Z3Context();
    var orders = from t in ctx.NewTheorem<(int fruit, int fries, int salad, int wings, int sticks, int plate)>()
                 where t.fruit >= 0 && t.fries >= 0 && t.salad >= 0 && t.wings >= 0 && t.sticks >= 0 && t.plate >= 0
                 where (215 * t.fruit) + (275 * t.fries) + (335 * t.salad) + (355 * t.wings) + (420 * t.sticks) + (580 * t.plate) == 1505
                 select t;

    var (found, ms) = Time(() =>
    {
        var all = new List<(int fruit, int fries, int salad, int wings, int sticks, int plate)>();
        AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("lime"))
            .Start("[lime]Searching...[/]", _ =>
            {
                while (orders.TrySolve(out var order))
                {
                    all.Add(order);

                    // A fresh local per order: the lambda is translated when the next solve runs.
                    var seen = order;
                    orders = orders.Where(t => !(t.fruit == seen.fruit && t.fries == seen.fries && t.salad == seen.salad
                                              && t.wings == seen.wings && t.sticks == seen.sticks && t.plate == seen.plate));
                }
            });
        return all;
    });

    var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Lime)
        .Title("[lime]Every order that costs $15.05[/]")
        .AddColumn("[white]Order[/]")
        .AddColumn("[white]Items[/]")
        .AddColumn(new TableColumn("[white]Total[/]").RightAligned());

    for (int i = 0; i < found.Count; i++)
    {
        var o = found[i];
        int[] quantities = [o.fruit, o.fries, o.salad, o.wings, o.sticks, o.plate];
        string items = string.Join(", ", quantities
            .Select((quantity, item) => (quantity, item))
            .Where(x => x.quantity > 0)
            .Select(x => $"{x.quantity} x {menu[x.item].Name}"));
        int cents = quantities.Select((quantity, item) => quantity * menu[item].Cents).Sum();
        table.AddRow($"{i + 1}", items, $"[green]${cents / 100}.{cents % 100:D2}[/]");
    }

    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine($"  [bold lime]{found.Count}[/] orders, and no others - the last solve was unsatisfiable.");
    AnsiConsole.MarkupLine($"[grey]  All found in {ms:F1} ms[/]");
}

static string Count(int n) => n == 0 ? "[grey]0[/]" : $"[bold lime]{n}[/]";

static (T Result, double Ms) Time<T>(Func<T> solve)
{
    var sw = Stopwatch.StartNew();
    T result = solve();
    sw.Stop();
    return (result, sw.Elapsed.TotalMilliseconds);
}
