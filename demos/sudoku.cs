#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:project ../solutions/Z3.Linq.Examples/Z3.Linq.Examples.csproj
#:package Spectre.Console@0.57.2

// Sudoku solved as a Z3 theorem, rendered with Spectre.Console.
//
// SudokuTheorem.Create(ctx) supplies the rules (row/column/box all-distinct, 1..9); this demo
// adds the clues of a specific puzzle and asks Z3 for the unique completion. The clues drive both
// the constraints and the printed grid, so there is one source of truth for each puzzle.
//
//   dotnet run demos/sudoku.cs

using System.Diagnostics;
using System.Linq.Expressions;

using Spectre.Console;

using Z3.Linq;
using Z3.Linq.Examples.Sudoku;

AnsiConsole.Write(new FigletText("Sudoku").Color(Color.Green).Centered());
AnsiConsole.Write(new Rule("[green]solved as a Z3 theorem[/]"));
AnsiConsole.WriteLine();

// 0 = blank. Two well-known puzzles.
int[,] easy =
{
    { 0, 0, 2, 0, 0, 1, 0, 6, 0 },
    { 0, 0, 7, 0, 0, 4, 0, 0, 0 },
    { 5, 0, 0, 0, 0, 0, 9, 0, 0 },
    { 0, 1, 0, 3, 0, 0, 0, 0, 0 },
    { 8, 0, 0, 0, 5, 0, 0, 0, 4 },
    { 0, 0, 0, 0, 0, 6, 0, 2, 0 },
    { 0, 0, 6, 0, 0, 0, 0, 0, 7 },
    { 0, 0, 0, 8, 0, 0, 3, 0, 0 },
    { 0, 4, 0, 9, 0, 0, 2, 0, 0 },
};

int[,] hard =
{
    { 0, 0, 0, 2, 6, 0, 7, 0, 1 },
    { 6, 8, 0, 0, 7, 0, 0, 9, 0 },
    { 1, 9, 0, 0, 0, 4, 5, 0, 0 },
    { 8, 2, 0, 1, 0, 0, 0, 4, 0 },
    { 0, 0, 4, 6, 0, 2, 9, 0, 0 },
    { 0, 5, 0, 0, 0, 3, 0, 2, 8 },
    { 0, 0, 9, 3, 0, 0, 0, 7, 4 },
    { 0, 4, 0, 0, 5, 0, 0, 3, 6 },
    { 7, 0, 3, 0, 1, 8, 0, 0, 0 },
};

Solve("Easy", easy);
AnsiConsole.WriteLine();
Solve("Hard (arizona.edu)", hard);

void Solve(string name, int[,] clues)
{
    AnsiConsole.Write(new Rule($"[green]{name}[/]").LeftJustified());

    using var ctx = new Z3Context();
    var theorem = WithClues(SudokuTheorem.Create(ctx), clues);

    SudokuTable? solution = null;
    var sw = Stopwatch.StartNew();
    AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("green"))
        .Start("[green]Solving...[/]", _ => solution = theorem.Solve());
    sw.Stop();

    if (solution is null)
    {
        AnsiConsole.MarkupLine("[red]No solution - the clues are contradictory.[/]");
        return;
    }

    var puzzle = new Panel(Grid((r, c) => clues[r, c], (r, c) => clues[r, c] != 0))
        .Header("[grey]Puzzle[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey);

    var solved = new Panel(Grid((r, c) => Cell(solution, r, c), (r, c) => clues[r, c] != 0))
        .Header("[green]Solution[/]").Border(BoxBorder.Rounded).BorderColor(Color.Green);

    var grid = new Grid().AddColumn().AddColumn();
    grid.AddRow(puzzle, solved);
    AnsiConsole.Write(grid);
    AnsiConsole.MarkupLine($"[grey]  Clues in [white]white[/], values Z3 derived in [green]green[/] - solved in {sw.Elapsed.TotalMilliseconds:F0} ms[/]");
}

// Adds one `t.Cell{r}{c} == v` constraint per clue, built as an expression tree so the puzzle
// array is the single source of truth.
static Theorem<SudokuTable> WithClues(Theorem<SudokuTable> theorem, int[,] clues)
{
    var t = Expression.Parameter(typeof(SudokuTable), "t");

    for (int row = 1; row <= 9; row++)
    {
        for (int col = 1; col <= 9; col++)
        {
            int value = clues[row - 1, col - 1];
            if (value == 0)
            {
                continue;
            }

            var body = Expression.Equal(
                Expression.Property(t, $"Cell{row}{col}"),
                Expression.Constant(value));

            theorem = theorem.Where(Expression.Lambda<Func<SudokuTable, bool>>(body, t));
        }
    }

    return theorem;
}

static int Cell(SudokuTable table, int row, int col) =>
    (int)typeof(SudokuTable).GetProperty($"Cell{row + 1}{col + 1}")!.GetValue(table)!;

// A 9x9 markup grid with heavy 3x3 box separators. `cell`/`isClue` are 0-based.
static string Grid(Func<int, int, int> cell, Func<int, int, bool> isClue)
{
    const string top = "┌───────┬───────┬───────┐";
    const string mid = "├───────┼───────┼───────┤";
    const string bot = "└───────┴───────┴───────┘";

    var sb = new System.Text.StringBuilder();
    sb.AppendLine(top);
    for (int r = 0; r < 9; r++)
    {
        sb.Append('│');
        for (int c = 0; c < 9; c++)
        {
            int v = cell(r, c);
            string glyph = v == 0
                ? "[grey].[/]"
                : isClue(r, c) ? $"[white bold]{v}[/]" : $"[green]{v}[/]";
            sb.Append(' ').Append(glyph);
            if (c % 3 == 2)
            {
                sb.Append(" │");
            }
        }

        sb.AppendLine();
        if (r is 2 or 5)
        {
            sb.AppendLine(mid);
        }
    }

    sb.Append(bot);
    return sb.ToString();
}
