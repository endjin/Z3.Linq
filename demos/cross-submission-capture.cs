#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:package Spectre.Console@0.57.2
#:package Microsoft.CodeAnalysis.CSharp.Scripting@5.9.0

// A local declared in one scripting submission and used in a constraint in the next - what a
// notebook does when one cell declares a value and a later cell solves with it. The demo drives
// Roslyn scripting, the engine .NET Interactive runs each cell through, shows the member chain the
// later submission hands Z3.Linq, and solves with it.
//
//   dotnet run demos/cross-submission-capture.cs

using System.Diagnostics;
using System.Linq.Expressions;

using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

using Spectre.Console;

using Z3.Linq;

AnsiConsole.Write(new FigletText("Cross-Submission").Color(Color.Orange1).Centered());
AnsiConsole.Write(new Rule("[orange1]a local captured from an earlier submission[/]"));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    "Each notebook cell or REPL submission compiles to its own class,\n" +
    "[aqua]Submission#N[/], in its own dynamic assembly, and its top-level locals become\n" +
    "fields on that class. A later submission that uses one in a constraint\n" +
    "reaches Z3.Linq as a member chain: the later submission's instance as a\n" +
    "constant, a compiler-generated field to the earlier submission, then the\n" +
    "local.\n\n" +
    "Before [yellow]#118[/] the visitor read every link of such a chain from the first\n" +
    "constant, so it asked the later submission for the earlier one's local and\n" +
    "threw [red]ArgumentException[/]. Since [yellow]#118[/] each link is read from the value\n" +
    "of the one before it.")
    .Header("[white]The scenario[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

ScriptOptions options = ScriptOptions.Default
    .AddReferences(typeof(Z3Context).Assembly, typeof(Microsoft.Z3.Context).Assembly, typeof(Expression).Assembly)
    .AddImports("System", "System.Linq.Expressions", "Z3.Linq");

// Submission 0: the environment type and the locals, as the first cell of a notebook would declare them.
ScriptState state = await CSharpScript.RunAsync(
    """
    public class Triple { public int[] Values { get; set; } = new int[3]; }

    int b0 = 7, b1 = 8, b2 = 9;
    """,
    options);

// Submission 1: hand back a constraint that uses b0, to see the shape the visitor receives.
ScriptState<LambdaExpression> constraint = await state.ContinueWithAsync<LambdaExpression>(
    "(Expression<Func<Triple, bool>>)(t => t.Values[0] == b0)");

AnsiConsole.MarkupLine($"[grey]Submission 1 returned[/] [aqua]{Markup.Escape(constraint.ReturnValue.ToString())}[/]");
AnsiConsole.WriteLine();
AnsiConsole.Write(ChainTable((MemberExpression)((BinaryExpression)constraint.ReturnValue.Body).Right));
AnsiConsole.MarkupLine("[grey]  Link 1 starts at the constant: the submission that wrote the lambda.[/]");
AnsiConsole.WriteLine();

// Submission 2: solve with all three locals from submission 0.
try
{
    var sw = Stopwatch.StartNew();
    ScriptState<int[]> solved = await constraint.ContinueWithAsync<int[]>(
        """
        int[] Solve()
        {
            using var ctx = new Z3Context();
            return ctx.NewTheorem<Triple>()
                .Where(t => t.Values[0] == b0)
                .Where(t => t.Values[1] == b1)
                .Where(t => t.Values[2] == b2)
                .Solve()!
                .Values;
        }

        Solve()
        """);
    sw.Stop();

    AnsiConsole.MarkupLine(
        $"[green]Solution = {string.Join(", ", solved.ReturnValue)}[/] " +
        $"[grey]({sw.Elapsed.TotalMilliseconds:F1} ms, including compiling the submission)[/]");
}
catch (ArgumentException ex)
{
    AnsiConsole.Write(new Panel(
        $"[red]{Markup.Escape(ex.Message)}[/]\n\n" +
        "This build of Z3.Linq still reads every link of a captured member chain\n" +
        "from the first constant, so it asked the later submission for a field\n" +
        "that only the earlier one has.")
        .Header("[red]ArgumentException[/]").Border(BoxBorder.Rounded).BorderColor(Color.Red));
}

// One row per link, from the constant outwards.
static Table ChainTable(MemberExpression member)
{
    List<MemberExpression> links = [];
    for (Expression? e = member; e is MemberExpression m; e = m.Expression)
    {
        links.Insert(0, m);
    }

    var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
        .AddColumn(new TableColumn("[white]Link[/]").Centered())
        .AddColumn("[white]Member[/]")
        .AddColumn("[white]Declared on[/]")
        .AddColumn("[white]Read from[/]");

    for (int i = 0; i < links.Count; i++)
    {
        table.AddRow(
            $"{i + 1}",
            $"[aqua]{Markup.Escape(links[i].Member.Name)}[/]",
            Markup.Escape(links[i].Member.DeclaringType?.Name ?? "?"),
            Markup.Escape(links[i].Expression?.Type.Name ?? "(static)"));
    }

    return table;
}
