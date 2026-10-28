#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:project ../solutions/Z3.Linq.Examples/Z3.Linq.Examples.csproj
#:package Spectre.Console@0.57.2

// The Zebra puzzle (also called Einstein's puzzle). Five houses stand in a row; each has a
// different colour, owner nationality, drink, cigarette brand and pet. Fourteen clues link them.
// Who drinks water, and who owns the zebra? Every attribute is an integer symbol that holds its
// house number, so each clue is a short equation. ZebraPuzzle.Create(ctx) holds the model. From
// "SAT/SMT by Example" (Dennis Yurichev), section 8.2.1, https://smt.st
//
//   dotnet run demos/zebra.cs

using System.Diagnostics;
using System.Text.RegularExpressions;

using Spectre.Console;

using Z3.Linq;
using Z3.Linq.Examples.Puzzles;

AnsiConsole.Write(new FigletText("Zebra Puzzle").Color(Color.DeepSkyBlue1).Centered());
AnsiConsole.Write(new Rule("[deepskyblue1]who drinks water, and who owns the zebra?[/]"));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    " 1. The Englishman lives in the red house.\n" +
    " 2. The Spaniard owns the dog.\n" +
    " 3. Coffee is drunk in the green house.\n" +
    " 4. The Ukrainian drinks tea.\n" +
    " 5. The green house is immediately to the right of the ivory house.\n" +
    " 6. The Old Gold smoker owns snails.\n" +
    " 7. Kools are smoked in the yellow house.\n" +
    " 8. Milk is drunk in the middle house.\n" +
    " 9. The Norwegian lives in the first house.\n" +
    "10. The Chesterfield smoker lives next to the man with the fox.\n" +
    "11. Kools are smoked in the house next to the house with the horse.\n" +
    "12. The Lucky Strike smoker drinks orange juice.\n" +
    "13. The Japanese smokes Parliaments.\n" +
    "14. The Norwegian lives next to the blue house.\n\n" +
    "Each attribute is a [deepskyblue1]house number[/] from 1 to 5,\n" +
    "so [italic]next to[/] means the two numbers differ by one.")
    .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

using var ctx = new Z3Context();
var theorem = ZebraPuzzle.Create(ctx);

ZebraPuzzle? solution = null;
double ms = 0;
AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("deepskyblue1"))
    .Start("[deepskyblue1]Solving...[/]", _ => (solution, ms) = Time(() => theorem.Solve()));

if (solution is null)
{
    AnsiConsole.MarkupLine("[red]No solution - the clues are contradictory.[/]");
    return;
}

var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.DeepSkyBlue1)
    .Title("[deepskyblue1]The five houses[/]")
    .AddColumn(new TableColumn("[white]House[/]").Centered())
    .AddColumn("[white]Colour[/]")
    .AddColumn("[white]Nationality[/]")
    .AddColumn("[white]Drink[/]")
    .AddColumn("[white]Smoke[/]")
    .AddColumn("[white]Pet[/]");

for (int house = 1; house <= ZebraPuzzle.HouseCount; house++)
{
    string drink = Words(ZebraPuzzle.AttributeInHouse(solution.Drink, house));
    string pet = Words(ZebraPuzzle.AttributeInHouse(solution.Pet, house));

    table.AddRow(
        $"{house}",
        Words(ZebraPuzzle.AttributeInHouse(solution.Colour, house)),
        Words(ZebraPuzzle.AttributeInHouse(solution.Nation, house)),
        house == solution.Drink.Water ? $"[bold deepskyblue1]{drink}[/]" : drink,
        Words(ZebraPuzzle.AttributeInHouse(solution.Smoke, house)),
        house == solution.Pet.Zebra ? $"[bold deepskyblue1]{pet}[/]" : pet);
}

AnsiConsole.Write(table);

string waterDrinker = ZebraPuzzle.AttributeInHouse(solution.Nation, solution.Drink.Water);
string zebraOwner = ZebraPuzzle.AttributeInHouse(solution.Nation, solution.Pet.Zebra);
AnsiConsole.MarkupLine($"  The [bold deepskyblue1]{waterDrinker}[/] drinks water (house {solution.Drink.Water}).");
AnsiConsole.MarkupLine($"  The [bold deepskyblue1]{zebraOwner}[/] owns the zebra (house {solution.Pet.Zebra}).");
AnsiConsole.MarkupLine($"[grey]  Solved in {ms:F1} ms[/]");

// "OrangeJuice" -> "Orange Juice".
static string Words(string name) => Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");

static (T Result, double Ms) Time<T>(Func<T> solve)
{
    var sw = Stopwatch.StartNew();
    T result = solve();
    sw.Stop();
    return (result, sw.Elapsed.TotalMilliseconds);
}
