#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:project ../solutions/Z3.Linq.Examples/Z3.Linq.Examples.csproj
#:package Spectre.Console@0.57.2

// Two Sudoku variants with no givens at all, solved as Z3 theorems and rendered with
// Spectre.Console. In Killer Sudoku the grid is split into cages whose digits must differ and
// add up to the cage's sum. In Greater-Than Sudoku a sign between neighbouring cells says which
// one is bigger. SudokuGridTheorem supplies the ordinary rules over a grid of 81 uint cells -
// bit-vectors to Z3, which is what makes these solve in about a second rather than minutes -
// and WithCages / WithGreaterThan add each variant's clues.
//
// Both puzzles come from "SAT/SMT by Example" (Dennis Yurichev), sections 8.1.4 and 8.1.3,
// https://smt.st
//
//   dotnet run demos/sudoku-variants.cs

using System.Diagnostics;
using System.Text;

using Spectre.Console;

using Z3.Linq;
using Z3.Linq.Examples.Sudoku;

AnsiConsole.Write(new FigletText("Sudoku Variants").Color(Color.Khaki1).Centered());
AnsiConsole.Write(new Rule("[khaki1]Killer and Greater-Than Sudoku on a bit-vector grid[/]"));
AnsiConsole.WriteLine();

Killer();
AnsiConsole.WriteLine();
GreaterThan();

void Killer()
{
    AnsiConsole.Write(new Rule("[khaki1]Killer Sudoku (section 8.1.4)[/]").LeftJustified());
    AnsiConsole.Write(new Panel(
        "No givens. Each lettered [khaki1]cage[/] holds different digits that add up to its sum, " +
        "and every row, column and box still holds 1-9 once. The book uses 25 cages; the original " +
        "puzzle's 26th cage (the four [grey].[/] cells) is not needed for a unique answer.")
        .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));

    var cages = KillerSudokuPuzzles.SuperTough;
    var cageOf = new int[9, 9];
    for (int r = 0; r < 9; r++)
    {
        for (int c = 0; c < 9; c++)
        {
            cageOf[r, c] = -1;
        }
    }

    for (int i = 0; i < cages.Length; i++)
    {
        foreach (var (row, col) in cages[i].Cells)
        {
            cageOf[row, col] = i;
        }
    }

    using var ctx = new Z3Context();
    var theorem = SudokuGridTheorem.Create(ctx).WithCages(cages);

    var (solution, ms) = Solve(() => theorem.Solve());
    if (solution is null)
    {
        AnsiConsole.MarkupLine("[red]No solution - the cages are contradictory.[/]");
        return;
    }

    string Letter(int r, int c) => cageOf[r, c] < 0
        ? "[grey].[/]"
        : $"[{CageColour(cageOf[r, c])}]{(char)('A' + cageOf[r, c])}[/]";

    var puzzle = new Panel(Grid((r, c) => Letter(r, c)))
        .Header("[grey]Cages[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey);
    var solved = new Panel(Grid((r, c) => cageOf[r, c] < 0
            ? $"[grey]{solution.Cell(r, c)}[/]"
            : $"[{CageColour(cageOf[r, c])}]{solution.Cell(r, c)}[/]"))
        .Header("[khaki1]Solution[/]").Border(BoxBorder.Rounded).BorderColor(Color.Khaki1);

    var layout = new Grid().AddColumn().AddColumn();
    layout.AddRow(puzzle, solved);
    AnsiConsole.Write(layout);

    // Each cage's digits as Z3 placed them, adding up to the cage's sum.
    var sums = cages.Select((cage, i) => new Markup(
        $"[{CageColour(i)}]{(char)('A' + i)}[/] [white]{cage.Sum,2}[/] = " +
        string.Join("+", cage.Cells.Select(cell => solution.Cell(cell.Row, cell.Col)))));
    AnsiConsole.Write(new Panel(new Columns(sums))
        .Header("[khaki1]Cage sums[/]").Border(BoxBorder.Rounded).BorderColor(Color.Khaki1));
    AnsiConsole.MarkupLine($"[grey]  Solved in {ms:F1} ms[/]");

    // Ask again with this grid ruled out: unsatisfiable means it was the only solution.
    var (other, uniqueMs) = Solve(() => theorem.Excluding(solution).Solve());
    AnsiConsole.MarkupLine(other is null
        ? $"[grey]  Unique: with this grid excluded Z3 finds no other ({uniqueMs:F1} ms)[/]"
        : $"[yellow]  Not unique: Z3 found another grid ({uniqueMs:F1} ms)[/]");
}

void GreaterThan()
{
    AnsiConsole.Write(new Rule("[khaki1]Greater-Than Sudoku (section 8.1.3)[/]").LeftJustified());
    AnsiConsole.Write(new Panel(
        "No givens. Inside each box, a sign between two neighbours says which is bigger: " +
        "[khaki1]>[/] or [khaki1]<[/] across a row, [khaki1]v[/] (upper is bigger) or [khaki1]^[/] (lower is bigger) " +
        $"down a column. The book's source lists {GreaterThanSudokuPuzzles.BookPuzzle.Length} distinct signs.")
        .Header("[white]The problem[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));

    var greater = GreaterThanSudokuPuzzles.BookPuzzle;
    var book = new SudokuGrid();
    for (int i = 0; i < 81; i++)
    {
        book.Cells[i] = (uint)GreaterThanSudokuPuzzles.BookSolution[i / 9, i % 9];
    }

    using var ctx = new Z3Context();
    var theorem = SudokuGridTheorem.Create(ctx).WithGreaterThan(greater);

    var (solution, ms) = Solve(() => theorem.Solve());
    if (solution is null)
    {
        AnsiConsole.MarkupLine("[red]No solution - the signs are contradictory.[/]");
        return;
    }

    AnsiConsole.MarkupLine($"[grey]  Solved in {ms:F1} ms[/]");

    // If Z3 happens to return the book's grid, ask for a different one so the comparison
    // below still shows that the signs allow more than one answer.
    if (solution.Cells.SequenceEqual(book.Cells))
    {
        var (other, otherMs) = Solve(() => theorem.Excluding(book).Solve());
        AnsiConsole.MarkupLine($"[grey]  Z3 returned the book's grid; asked for another in {otherMs:F1} ms[/]");
        solution = other ?? solution;
    }

    int differences = Enumerable.Range(0, 81).Count(i => solution.Cells[i] != book.Cells[i]);

    var found = new Panel(SignGrid(greater, (r, c) => solution.Cell(r, c) == book.Cell(r, c)
            ? $"[khaki1]{solution.Cell(r, c)}[/]"
            : $"[orange1 bold]{solution.Cell(r, c)}[/]"))
        .Header("[khaki1]Z3's solution[/]").Border(BoxBorder.Rounded).BorderColor(Color.Khaki1);
    var printed = new Panel(SignGrid(greater, (r, c) => solution.Cell(r, c) == book.Cell(r, c)
            ? $"[white]{book.Cell(r, c)}[/]"
            : $"[orange1 bold]{book.Cell(r, c)}[/]"))
        .Header("[white]The book's answer[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey);

    var layout = new Grid().AddColumn().AddColumn();
    layout.AddRow(found, printed);
    AnsiConsole.Write(layout);

    AnsiConsole.Write(new Panel(
        $"The two grids differ in the [orange1]{differences}[/] highlighted cells, yet both obey every Sudoku " +
        $"rule and all {greater.Length} signs, so both are correct answers to the puzzle as transcribed. " +
        "The book's source adds one sign twice, which suggests a sign of the original puzzle was lost; " +
        "without it the signs no longer pin down a single grid.")
        .Header("[white]More than one solution[/]").Border(BoxBorder.Rounded).BorderColor(Color.Orange1));
}

static (T Result, double Ms) Solve<T>(Func<T> solve)
{
    var sw = Stopwatch.StartNew();
    T result = default!;
    AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("khaki1"))
        .Start("[khaki1]Solving...[/]", _ => result = solve());
    sw.Stop();
    return (result, sw.Elapsed.TotalMilliseconds);
}

static string CageColour(int cage) =>
    (cage % 4) switch
    {
        0 => "khaki1",
        1 => "aqua",
        2 => "orchid",
        _ => "springgreen2",
    };

// A 9x9 markup grid with box separators. `glyph` returns the markup for a 0-based cell.
static string Grid(Func<int, int, string> glyph)
{
    const string top = "┌───────┬───────┬───────┐";
    const string mid = "├───────┼───────┼───────┤";
    const string bot = "└───────┴───────┴───────┘";

    var sb = new StringBuilder();
    sb.AppendLine(top);
    for (int r = 0; r < 9; r++)
    {
        sb.Append('│');
        for (int c = 0; c < 9; c++)
        {
            sb.Append(' ').Append(glyph(r, c));
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

// A 9x9 grid with the Greater-Than signs drawn between neighbouring cells: > or < between
// columns, v (upper is bigger) or ^ (lower is bigger) between rows.
static string SignGrid((int Row, int Col, int ThanRow, int ThanCol)[] greater, Func<int, int, string> glyph)
{
    const string top = "┌───────┬───────┬───────┐";
    const string mid = "├───────┼───────┼───────┤";
    const string bot = "└───────┴───────┴───────┘";

    var signs = new HashSet<(int, int, int, int)>(greater.Select(g => (g.Row, g.Col, g.ThanRow, g.ThanCol)));

    string Across(int r, int c) =>
        signs.Contains((r, c, r, c + 1)) ? "[khaki1]>[/]"
        : signs.Contains((r, c + 1, r, c)) ? "[khaki1]<[/]"
        : " ";

    string Down(int r, int c) =>
        signs.Contains((r, c, r + 1, c)) ? "[khaki1]v[/]"
        : signs.Contains((r + 1, c, r, c)) ? "[khaki1]^[/]"
        : " ";

    var sb = new StringBuilder();
    sb.AppendLine(top);
    for (int r = 0; r < 9; r++)
    {
        // Each box row is " d s d s d ": the sign between two digits sits in the gap.
        sb.Append("│ ");
        for (int c = 0; c < 9; c++)
        {
            sb.Append(glyph(r, c));
            sb.Append(c % 3 == 2 ? (c == 8 ? " │" : " │ ") : Across(r, c));
        }

        sb.AppendLine();
        if (r % 3 != 2)
        {
            sb.Append("│ ");
            for (int c = 0; c < 9; c++)
            {
                sb.Append(Down(r, c));
                sb.Append(c % 3 == 2 ? (c == 8 ? " │" : " │ ") : " ");
            }

            sb.AppendLine();
        }
        else if (r != 8)
        {
            sb.AppendLine(mid);
        }
    }

    sb.Append(bot);
    return sb.ToString();
}
