#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:project ../solutions/Z3.Linq.Examples/Z3.Linq.Examples.csproj
#:package Spectre.Console@0.57.2

// Graph colouring, solved as a Z3 theorem and rendered with Spectre.Console. First the map of
// Europe: give each of 45 countries a colour so that no two neighbours share one, and let
// `orderby` find the fewest colours. Then two optimisations over the four-colour maps, and the
// same model reused to timetable committee meetings. From "SAT/SMT by Example" (Dennis
// Yurichev), sections 9.1, 9.1.1 and 9.2, https://smt.st
//
//   dotnet run demos/map-colouring.cs

using System.Diagnostics;

using Spectre.Console;

using Z3.Linq;
using Z3.Linq.Examples.GraphColouring;

string[] palette = ["red", "dodgerblue1", "yellow", "springgreen2", "orchid", "orange1"];

AnsiConsole.Write(new FigletText("Map Colouring").Color(Color.SpringGreen2).Centered());
AnsiConsole.Write(new Rule("[springgreen2]Europe in as few colours as possible[/]"));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    $"Give each of [white]{Europe.Countries.Length}[/] European countries a colour, so that no two countries\n" +
    $"that share one of the [white]{Europe.Edges.Length}[/] borders have the same colour. The number of\n" +
    "colours is a symbol too, so [italic]orderby[/] asks Z3 for the fewest. Then, with four\n" +
    "colours, how few and how many countries can one colour cover?")
    .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

using var ctx = new Z3Context();
var europe = Europe.Create(ctx);

// The fewest colours.
GraphColouring? map = null;
var minimumTime = Time(() => map = (from t in europe orderby t.Colours select t).Solve());

if (map is null)
{
    AnsiConsole.MarkupLine("[red]No colouring found.[/]");
    return;
}

AnsiConsole.Write(BuildMap(map, palette));
AnsiConsole.MarkupLine($"[grey]  Z3 needs [white]{map.Colours}[/] colours - solved in {minimumTime:F1} ms[/]");
AnsiConsole.WriteLine();

// Why not three: a proof by unsatisfiability.
bool threeColours = false;
var threeTime = Time(() => threeColours = europe.Where(t => t.Colours == 3).TrySolve(out _));
AnsiConsole.MarkupLine(threeColours
    ? "[red]Three colours were enough - the border data has changed.[/]"
    : $"[springgreen2]Three colours are impossible[/]: Z3 reports the theorem unsatisfiable [grey]({threeTime:F1} ms)[/].");
AnsiConsole.MarkupLine("[grey]  Belgium, France, Germany and Luxembourg all border one another.[/]");
AnsiConsole.WriteLine();

// The fewest and the most countries in one colour, with four colours.
var fourColours = europe.Where(t => t.Colours == 4);
var redCount = GraphColouring.CountOfColour(Europe.Countries.Length, 0);

GraphColouring? fewest = null;
var fewestTime = Time(() => fewest = fourColours.Optimize(Optimization.Minimize, redCount));
GraphColouring? most = null;
var mostTime = Time(() => most = fourColours.Optimize(Optimization.Maximize, redCount));

var extremes = new Table().Border(TableBorder.Rounded).BorderColor(Color.SpringGreen2)
    .Title("[springgreen2]Four colours: how much [red]red[/]?[/]")
    .AddColumn("[white]Objective[/]")
    .AddColumn(new TableColumn("[white]Red countries[/]").Centered())
    .AddColumn("[white]Which[/]")
    .AddColumn(new TableColumn("[white]Time[/]").RightAligned());
extremes.AddRow("[green]Optimize[/] - minimise red", Count(fewest), Reds(fewest), $"[grey]{fewestTime:F0} ms[/]");
extremes.AddRow("[green]Optimize[/] - maximise red", Count(most), Reds(most), $"[grey]{mostTime:F0} ms[/]");
AnsiConsole.Write(extremes);
AnsiConsole.WriteLine();

// The same model, reused: committee meetings, where a colour is an hour.
AnsiConsole.Write(new Rule("[springgreen2]Committee scheduling[/]").LeftJustified());
AnsiConsole.Write(new Panel(
    "Six committees each meet for an hour. Two committees with a member in common\n" +
    "cannot meet in the same hour. Each committee is a vertex, two committees that\n" +
    "share a member are joined by an edge, and each colour is an hour - so the\n" +
    "fewest colours give the shortest timetable.")
    .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

GraphColouring? timetable = null;
var timetableTime = Time(() => timetable = (from t in Committees.Create(ctx) orderby t.Colours select t).Solve());

if (timetable is null)
{
    AnsiConsole.MarkupLine("[red]No timetable found.[/]");
    return;
}

var hours = new Table().Border(TableBorder.Rounded).BorderColor(Color.SpringGreen2)
    .Title($"[springgreen2]{timetable.Colours} hours[/]")
    .AddColumn(new TableColumn("[white]Hour[/]").Centered())
    .AddColumn("[white]Committees (members)[/]");
for (int hour = 0; hour < timetable.Colours; hour++)
{
    var meeting = Enumerable.Range(0, Committees.Membership.Length)
        .Where(i => timetable.Colour[i] == hour)
        .Select(i => $"[{palette[hour]}]{Committees.Membership[i].Name}[/] [grey]{{{string.Join(",", Committees.Membership[i].Members)}}}[/]");
    hours.AddRow((hour + 1).ToString(), string.Join("   ", meeting));
}

AnsiConsole.Write(hours);
AnsiConsole.MarkupLine($"[grey]  Solved in {timetableTime:F1} ms. Committees in the same hour share no member.[/]");

// Lists the countries under the colour Z3 gave them, one column per colour.
static Table BuildMap(GraphColouring map, string[] palette)
{
    var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.SpringGreen2)
        .Title("[springgreen2]A colouring of Europe[/]");

    var columns = new List<string>[map.Colours];
    for (int colour = 0; colour < map.Colours; colour++)
    {
        columns[colour] = Enumerable.Range(0, Europe.Countries.Length)
            .Where(i => map.Colour[i] == colour)
            .Select(i => Europe.Countries[i])
            .ToList();
        table.AddColumn($"[{palette[colour]}]Colour {colour}[/] [grey]({columns[colour].Count})[/]");
    }

    int rows = columns.Max(column => column.Count);
    for (int row = 0; row < rows; row++)
    {
        table.AddRow(columns.Select(column => row < column.Count ? column[row] : string.Empty).ToArray());
    }

    return table;
}

static string Count(GraphColouring? map) =>
    map is null ? "-" : map.Colour.Count(colour => colour == 0).ToString();

static string Reds(GraphColouring? map)
{
    if (map is null)
    {
        return "[red]no solution[/]";
    }

    var reds = Enumerable.Range(0, Europe.Countries.Length).Where(i => map.Colour[i] == 0).Select(i => Europe.Countries[i]).ToList();
    return reds.Count <= 4
        ? $"[red]{string.Join(", ", reds)}[/]"
        : $"[red]{string.Join(", ", reds.Take(4))}[/] [grey]and {reds.Count - 4} more[/]";
}

static double Time(Action action)
{
    var sw = Stopwatch.StartNew();
    AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("springgreen2"))
        .Start("[springgreen2]Solving...[/]", _ => action());
    sw.Stop();
    return sw.Elapsed.TotalMilliseconds;
}
