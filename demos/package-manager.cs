#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:project ../solutions/Z3.Linq.Examples/Z3.Linq.Examples.csproj
#:package Spectre.Console@0.57.2

// Dependency resolution for a small package catalogue, solved as a Z3 optimisation and rendered
// with Spectre.Console. Two programs depend on versions of up to three libraries, and one library
// depends on another. PackageManager.Create(ctx) holds the catalogue; this demo asks for some
// program versions and lets Z3 pick the libraries: the fewest that work, at the newest versions
// that work together. When no set of versions meets the request, Z3 says so. From "SAT/SMT by
// Example" (Dennis Yurichev), section 22.5.1, https://smt.st.
//
//   dotnet run demos/package-manager.cs

using System.Diagnostics;

using Spectre.Console;

using Z3.Linq;
using Z3.Linq.Examples.Packaging;

AnsiConsole.Write(new FigletText("Package Manager").Color(Color.Turquoise2).Centered());
AnsiConsole.Write(new Rule("[turquoise2]dependency resolution as an optimisation[/]"));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    "[white]libA[/] v1-v5    [white]libB[/] v1, v4, v5    [white]libC[/] v10, v11, v14    [white]libD[/] v1-v10\n" +
    "[white]libC[/] v10 needs libA v1-v3; v11 needs libA v3+; v14 needs libA v5+\n" +
    "[white]programA[/] v1 needs libA v2, libB v4 or v5, libC v10\n" +
    "[white]programA[/] v2 needs libA v3, libB v5, libC v11\n" +
    "[white]programB[/] v7 needs libA v2+ and libC v10+; v8 needs libA v6+ and libC v11+\n" +
    "Nothing needs [white]libD[/].")
    .Header("[white]The catalogue[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    "Each installed library costs [white]100 minus its version[/]; a library left out costs " +
    "nothing. No version reaches 100, so [italic]orderby[/] first installs only the libraries " +
    "the programs need, then picks the newest versions of those.\n\n" +
    "A missing library is version 0, which fails every \"v2+\" rule. The book uses 999 " +
    "for \"not installed\" and maximises the sum of versions instead. That rewards a " +
    "library for being absent, and lets an absent library pass a \"v6+\" rule.")
    .Header("[white]What Z3 minimises[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

(string Request, int? ProgramA, int? ProgramB)[] requests =
[
    ("A v1 + B v7", 1, 7),
    ("A v2 + B v7", 2, 7),
    ("A v1 + B v8", 1, 8),
    ("any versions", null, null),
];

var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Turquoise2)
    .Title("[turquoise2]Best installation plan for each request[/]")
    .AddColumn("[white]Request[/]")
    .AddColumn(new TableColumn("[white]programA[/]").Centered())
    .AddColumn(new TableColumn("[white]programB[/]").Centered())
    .AddColumn(new TableColumn("[white]libA[/]").Centered())
    .AddColumn(new TableColumn("[white]libB[/]").Centered())
    .AddColumn(new TableColumn("[white]libC[/]").Centered())
    .AddColumn(new TableColumn("[white]libD[/]").Centered())
    .AddColumn(new TableColumn("[white]Time[/]").RightAligned());

AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("turquoise2"))
    .Start("[turquoise2]Resolving...[/]", _ =>
    {
        foreach (var (request, programA, programB) in requests)
        {
            using var ctx = new Z3Context();
            var theorem = PackageManager.Create(ctx);
            if (programA is int a && programB is int b)
            {
                theorem = PackageManager.WithPrograms(theorem, a, b);
            }

            var (plan, ms) = Time(() => PackageManager.Resolve(theorem).Solve());

            if (plan is null)
            {
                string none = "[red]none[/]";
                table.AddRow(request, none, none, none, none, none, none, $"[grey]{ms:F0} ms[/]");
                continue;
            }

            table.AddRow(
                request,
                $"v{plan.ProgramA}",
                $"v{plan.ProgramB}",
                Version(plan.LibA),
                Version(plan.LibB),
                Version(plan.LibC),
                Version(plan.LibD),
                $"[grey]{ms:F0} ms[/]");
        }
    });

AnsiConsole.Write(table);
AnsiConsole.MarkupLine("[grey]  \"-\" means not installed; \"none\" means no plan meets the request.[/]");
AnsiConsole.MarkupLine("[grey]  programB v8 needs libA v6 or later, and the newest libA is v5.[/]");

static string Version(int version) =>
    version == PackageManager.NotInstalled ? "[grey]-[/]" : $"[bold turquoise2]v{version}[/]";

static (T Result, double Ms) Time<T>(Func<T> solve)
{
    var sw = Stopwatch.StartNew();
    T result = solve();
    sw.Stop();
    return (result, sw.Elapsed.TotalMilliseconds);
}
