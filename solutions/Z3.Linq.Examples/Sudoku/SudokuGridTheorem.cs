namespace Z3.Linq.Examples.Sudoku;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

/// <summary>
/// Builds the Sudoku rules over a <see cref="SudokuGrid"/>: every cell between 1 and 9, and every
/// row, column and 3x3 box distinct.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart of <see cref="SudokuTheorem"/> for the nested-array environment. The rules
/// are built as expression trees rather than written as lambdas, so each cell reference is
/// <c>g.Cells[row][column]</c> with the indices as literal constants in the tree - nothing is
/// captured from a closure.
/// </para>
/// </remarks>
public static class SudokuGridTheorem
{
    /// <summary>
    /// Creates a theorem holding the Sudoku rules and no givens.
    /// </summary>
    /// <param name="context">The context the theorem is created in.</param>
    /// <returns>A theorem to add givens to with <c>Where</c> and <see cref="Given"/>.</returns>
    public static Theorem<SudokuGrid> Create(Z3Context context)
    {
        var theorem = context.NewTheorem<SudokuGrid>();

        for (int row = 0; row < 9; row++)
        {
            for (int column = 0; column < 9; column++)
            {
                theorem = theorem.Where(Between1And9(row, column));
            }
        }

        theorem = theorem.Where(AllDistinct(static (group, offset) => (group, offset)));
        theorem = theorem.Where(AllDistinct(static (group, offset) => (offset, group)));
        theorem = theorem.Where(AllDistinct(static (group, offset) =>
            (((group / 3) * 3) + (offset / 3), ((group % 3) * 3) + (offset % 3))));

        return theorem;
    }

    /// <summary>
    /// Builds the given <c>g =&gt; g.Cells[row][column] == value</c>, with the indices and the
    /// value as constants.
    /// </summary>
    /// <param name="row">The row, from zero.</param>
    /// <param name="column">The column, from zero.</param>
    /// <param name="value">The digit the cell holds.</param>
    /// <returns>A constraint to pass to <c>Where</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="row"/> or <paramref name="column"/> is outside 0 to 8.
    /// </exception>
    /// <remarks>
    /// The coordinates are checked here because Z3 would not catch them: the grid is a Z3 array
    /// indexed by any integer, so a given at row 9 would constrain an element the 9x9 grid never
    /// reads back, and the puzzle would solve with that given silently missing.
    /// </remarks>
    public static Expression<Func<SudokuGrid, bool>> Given(int row, int column, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(row, 8);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(column, 8);

        ParameterExpression grid = Expression.Parameter(typeof(SudokuGrid), "g");

        return Expression.Lambda<Func<SudokuGrid, bool>>(
            Expression.Equal(Cell(grid, row, column), Expression.Constant(value)),
            grid);
    }

    /// <summary>
    /// Builds <c>g.Cells[row][column]</c> with both indices as constants.
    /// </summary>
    private static Expression Cell(ParameterExpression grid, int row, int column)
        => Expression.ArrayIndex(
            Expression.ArrayIndex(Expression.Property(grid, nameof(SudokuGrid.Cells)), Expression.Constant(row)),
            Expression.Constant(column));

    /// <summary>
    /// Builds <c>g =&gt; g.Cells[row][column] &gt;= 1 &amp; g.Cells[row][column] &lt;= 9</c>.
    /// </summary>
    private static Expression<Func<SudokuGrid, bool>> Between1And9(int row, int column)
    {
        ParameterExpression grid = Expression.Parameter(typeof(SudokuGrid), "g");
        Expression cell = Cell(grid, row, column);

        return Expression.Lambda<Func<SudokuGrid, bool>>(
            Expression.And(
                Expression.GreaterThanOrEqual(cell, Expression.Constant(1)),
                Expression.LessThanOrEqual(cell, Expression.Constant(9))),
            grid);
    }

    /// <summary>
    /// Builds the conjunction of nine <c>Distinct</c> constraints, one per group of nine cells.
    /// </summary>
    /// <param name="cellOf">
    /// Maps a group (0-8) and an offset within it (0-8) to the cell's row and column, which is
    /// what makes the groups rows, columns or boxes.
    /// </param>
    private static Expression<Func<SudokuGrid, bool>> AllDistinct(Func<int, int, (int Row, int Column)> cellOf)
    {
        ParameterExpression grid = Expression.Parameter(typeof(SudokuGrid), "g");

        Expression? distincts = null;

        for (int group = 0; group < 9; group++)
        {
            var cells = new List<Expression>();
            for (int offset = 0; offset < 9; offset++)
            {
                (int row, int column) = cellOf(group, offset);
                cells.Add(Cell(grid, row, column));
            }

            MethodCallExpression distinct = Expression.Call(
                typeof(Z3Methods),
                nameof(Z3Methods.Distinct),
                [typeof(int)],
                Expression.NewArrayInit(typeof(int), cells));

            distincts = distincts is null ? distinct : Expression.And(distincts, distinct);
        }

        return Expression.Lambda<Func<SudokuGrid, bool>>(distincts!, grid);
    }
}
