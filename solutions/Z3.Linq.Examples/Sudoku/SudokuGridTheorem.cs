namespace Z3.Linq.Examples.Sudoku;

using System.Linq.Expressions;

/// <summary>
/// Sudoku rules over a <see cref="SudokuGrid"/>, plus the extra constraint groups that turn a
/// plain Sudoku into one of its variants.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Create"/> supplies the rules every variant shares: each cell holds 1-9, and every
/// row, column and 3x3 box is all-distinct. The <c>With...</c> methods each add one kind of
/// clue: givens (<see cref="WithClues"/>), inequalities between neighbouring cells
/// (<see cref="WithGreaterThan"/>, Greater-Than Sudoku) and summed cages
/// (<see cref="WithCages"/>, Killer Sudoku). <see cref="Excluding"/> rules out a grid already
/// found, which is how to check whether a puzzle has only one solution.
/// </para>
/// <para>
/// Every constraint is built as an expression tree over <c>t.Cells[i]</c>, so the puzzle data
/// is the single source of truth. Constants are <see cref="uint"/> to match the bit-vector
/// cells.
/// </para>
/// <para>
/// The variants are the ones described in "SAT/SMT by Example" by Dennis Yurichev, sections
/// 8.1.3 (Greater Than Sudoku) and 8.1.4 (Killer Sudoku), https://smt.st.
/// </para>
/// </remarks>
public static class SudokuGridTheorem
{
    public static Theorem<SudokuGrid> Create(Z3Context context)
    {
        var theorem = context.NewTheorem<SudokuGrid>();

        for (int row = 0; row < 9; row++)
        {
            for (int column = 0; column < 9; column++)
            {
                theorem = theorem.Where(Constraint(t => Expression.AndAlso(
                    Expression.GreaterThanOrEqual(Cell(t, row, column), Value(1)),
                    Expression.LessThanOrEqual(Cell(t, row, column), Value(9)))));
            }
        }

        for (int row = 0; row < 9; row++)
        {
            theorem = theorem.Where(Constraint(t =>
                Distinct(Enumerable.Range(0, 9).Select(column => Cell(t, row, column)))));
        }

        for (int column = 0; column < 9; column++)
        {
            theorem = theorem.Where(Constraint(t =>
                Distinct(Enumerable.Range(0, 9).Select(row => Cell(t, row, column)))));
        }

        for (int box = 0; box < 9; box++)
        {
            int rowOffset = (box / 3) * 3;
            int columnOffset = (box % 3) * 3;

            theorem = theorem.Where(Constraint(t =>
                Distinct(Enumerable.Range(0, 9).Select(offset => Cell(t, rowOffset + (offset / 3), columnOffset + (offset % 3))))));
        }

        return theorem;
    }

    /// <summary>
    /// Adds one <c>cell == value</c> constraint per given. 0 marks a blank cell.
    /// </summary>
    public static Theorem<SudokuGrid> WithClues(this Theorem<SudokuGrid> theorem, int[,] clues)
    {
        for (int row = 0; row < 9; row++)
        {
            for (int column = 0; column < 9; column++)
            {
                int value = clues[row, column];
                if (value == 0)
                {
                    continue;
                }

                theorem = theorem.Where(Constraint(t => Expression.Equal(Cell(t, row, column), Value(value))));
            }
        }

        return theorem;
    }

    /// <summary>
    /// Adds one inequality per entry: the cell at (<c>Row</c>, <c>Col</c>) must be greater than
    /// the cell at (<c>ThanRow</c>, <c>ThanCol</c>). All coordinates are 0-based.
    /// </summary>
    public static Theorem<SudokuGrid> WithGreaterThan(
        this Theorem<SudokuGrid> theorem,
        (int Row, int Col, int ThanRow, int ThanCol)[] greater)
    {
        foreach (var (row, col, thanRow, thanCol) in greater)
        {
            theorem = theorem.Where(Constraint(t => Expression.GreaterThan(Cell(t, row, col), Cell(t, thanRow, thanCol))));
        }

        return theorem;
    }

    /// <summary>
    /// Adds the Killer Sudoku cage rules: the cells of each cage are all-distinct and add up to
    /// the cage's <c>Sum</c>. Coordinates are 0-based.
    /// </summary>
    public static Theorem<SudokuGrid> WithCages(
        this Theorem<SudokuGrid> theorem,
        (int Sum, (int Row, int Col)[] Cells)[] cages)
    {
        foreach (var (sum, cells) in cages)
        {
            theorem = theorem.Where(Constraint(t =>
            {
                // Every cell is at most 9, so a cage sum cannot wrap a 32-bit bit-vector.
                Expression total = cells
                    .Select(cell => Cell(t, cell.Row, cell.Col))
                    .Aggregate(Expression.Add);

                return Expression.AndAlso(
                    Expression.Equal(total, Value(sum)),
                    Distinct(cells.Select(cell => Cell(t, cell.Row, cell.Col))));
            }));
        }

        return theorem;
    }

    /// <summary>
    /// Rules out one particular grid: at least one cell must differ from <paramref name="grid"/>.
    /// </summary>
    /// <remarks>
    /// Solving again after excluding a solution is how to ask whether a puzzle is unique: if the
    /// theorem becomes unsatisfiable, the excluded grid was the only one.
    /// </remarks>
    public static Theorem<SudokuGrid> Excluding(this Theorem<SudokuGrid> theorem, SudokuGrid grid)
    {
        return theorem.Where(Constraint(t => Enumerable.Range(0, 81)
            .Select(i => (Expression)Expression.NotEqual(Cell(t, i / 9, i % 9), Value((int)grid.Cells[i])))
            .Aggregate(Expression.OrElse)));
    }

    /// <summary>
    /// Wraps a constraint body, built over the grid parameter <c>t</c>, as a <c>Where</c> lambda.
    /// </summary>
    private static Expression<Func<SudokuGrid, bool>> Constraint(Func<ParameterExpression, Expression> body)
    {
        var t = Expression.Parameter(typeof(SudokuGrid), "t");
        return Expression.Lambda<Func<SudokuGrid, bool>>(body(t), t);
    }

    /// <summary>
    /// <c>t.Cells[(row * 9) + column]</c>, with 0-based row and column.
    /// </summary>
    private static Expression Cell(ParameterExpression t, int row, int column)
    {
        if (row is < 0 or > 8 || column is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(row), $"Cell ({row}, {column}) is outside the 9x9 grid.");
        }

        return Expression.ArrayIndex(
            Expression.Property(t, nameof(SudokuGrid.Cells)),
            Expression.Constant((row * 9) + column));
    }

    private static Expression Value(int value) => Expression.Constant((uint)value, typeof(uint));

    private static Expression Distinct(IEnumerable<Expression> cells) =>
        Expression.Call(
            typeof(Z3Methods),
            nameof(Z3Methods.Distinct),
            [typeof(uint)],
            Expression.NewArrayInit(typeof(uint), cells));
}
