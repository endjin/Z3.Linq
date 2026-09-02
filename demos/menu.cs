#:package Spectre.Console@0.57.2

// An interactive launcher for the Z3.Linq demos. Presents a Spectre.Console menu and runs the
// chosen demo as its own file-based app (`dotnet run demos/<name>.cs`).
//
//   dotnet run demos/menu.cs

using System.Diagnostics;

using Spectre.Console;

var demos = new (string Name, string File, string Blurb)[]
{
    ("River crossing", "river-crossing.cs", "Missionaries & Cannibals - Solve, Optimize and orderby"),
    ("Sudoku", "sudoku.cs", "Two puzzles solved on a 9x9 grid"),
    ("Boolean logic", "boolean-logic.cs", "x XOR y over an anonymous type, a tuple and a record"),
    ("Linear systems", "linear-systems.cs", "Integer constraint systems, incl. Bart's TechEd example"),
    ("Oil purchase", "oil-purchase.cs", "A least-cost linear program"),
    ("Warehouse logistics", "warehouse.cs", "Least-cost shipping across two warehouses"),
};

// Run from the repo root (`dotnet run demos/menu.cs`) or from within demos/.
string dir = File.Exists("river-crossing.cs") ? "." : "demos";

AnsiConsole.Write(new FigletText("Z3.Linq").Color(Color.Aqua).Centered());
AnsiConsole.Write(new Rule("[aqua]demos[/]"));
AnsiConsole.WriteLine();

const string quit = "Quit";

while (true)
{
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Choose a [green]demo[/] to run:")
            .PageSize(10)
            .UseConverter(name => name == quit
                ? "[grey]Quit[/]"
                : $"{name}  [grey]- {demos.First(d => d.Name == name).Blurb}[/]")
            .AddChoices([.. demos.Select(d => d.Name), quit]));

    if (choice == quit)
    {
        break;
    }

    var demo = demos.First(d => d.Name == choice);

    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule($"[green]{Markup.Escape(demo.Name)}[/]"));

    var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
    start.ArgumentList.Add("run");
    start.ArgumentList.Add(Path.Combine(dir, demo.File));

    try
    {
        using var process = Process.Start(start);
        process!.WaitForExit();
    }
    catch (Exception ex)
    {
        AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths);
    }

    AnsiConsole.WriteLine();
    if (!AnsiConsole.Confirm("Run [green]another[/] demo?"))
    {
        break;
    }

    AnsiConsole.WriteLine();
}

AnsiConsole.MarkupLine("[grey]Bye![/]");
