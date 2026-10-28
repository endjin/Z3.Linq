#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:project ../solutions/Z3.Linq.Examples/Z3.Linq.Examples.csproj
#:package Spectre.Console@0.57.2

// N queens: place N queens on an N x N chessboard so that no two attack each other. Each row
// holds one queen, so the board is one integer symbol per row (the queen's column). Two queens
// may not share a column, and the distance between their columns may not equal the distance
// between their rows. NQueens.Create(ctx, n) holds the model. The demo solves 8 x 8 and 12 x 12,
// then shows that 2 x 2 and 3 x 3 have no placement. From "SAT/SMT by Example" (Dennis
// Yurichev), section 8.4, https://smt.st
//
//   dotnet run demos/n-queens.cs

using System.Diagnostics;
using System.Text;

using Spectre.Console;

using Z3.Linq;
using Z3.Linq.Examples.Puzzles;

AnsiConsole.Write(new FigletText("N Queens").Color(Color.HotPink).Centered());
AnsiConsole.Write(new Rule("[hotpink]no two queens on one row, column or diagonal[/]"));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    "One [hotpink]integer per row[/] holds the column of that row's queen,\n" +
    "so two queens can never share a row. For every pair of rows:\n" +
    "  - the two columns must differ, and\n" +
    "  - the column distance must not equal the row distance (a diagonal).")
    .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

using var ctx = new Z3Context();

var boards = new Grid().AddColumn().AddColumn();
boards.AddRow(Board(8), Board(12));
AnsiConsole.Write(boards);
AnsiConsole.WriteLine();

var impossible = new Table().Border(TableBorder.Rounded).BorderColor(Color.HotPink)
    .Title("[hotpink]Small boards[/]")
    .AddColumn("[white]Board[/]")
    .AddColumn("[white]Result[/]")
    .AddColumn(new TableColumn("[white]Time[/]").RightAligned());

foreach (int size in new[] { 2, 3, 4 })
{
    var (solved, ms) = Time(() => NQueens.Create(ctx, size).TrySolve(out _));
    impossible.AddRow(
        $"{size} x {size}",
        solved ? "[green]placement found[/]" : "[red]no placement - unsatisfiable[/]",
        $"{ms:F1} ms");
}

AnsiConsole.Write(impossible);

Panel Board(int size)
{
    QueensBoard? board = null;
    bool solved = false;
    double ms = 0;
    AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("hotpink"))
        .Start($"[hotpink]Solving {size} x {size}...[/]", _ => (solved, ms) = Time(() => NQueens.Create(ctx, size).TrySolve(out board)));

    var sb = new StringBuilder();
    if (!solved || board is null)
    {
        sb.Append("[red]No placement.[/]");
    }
    else
    {
        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++)
            {
                bool dark = (row + column) % 2 == 1;
                sb.Append(board.Column[row] == column
                    ? "[bold hotpink]Q[/] "
                    : dark ? "[grey]·[/] " : "[grey35]·[/] ");
            }

            sb.AppendLine();
        }

        sb.Append($"[grey]solved in {ms:F1} ms[/]");
    }

    return new Panel(sb.ToString())
        .Header($"[hotpink]{size} x {size}[/]").Border(BoxBorder.Rounded).BorderColor(Color.HotPink);
}

static (T Result, double Ms) Time<T>(Func<T> solve)
{
    var sw = Stopwatch.StartNew();
    T result = solve();
    sw.Stop();
    return (result, sw.Elapsed.TotalMilliseconds);
}
