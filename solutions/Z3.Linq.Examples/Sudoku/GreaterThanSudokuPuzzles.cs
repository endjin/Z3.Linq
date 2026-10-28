namespace Z3.Linq.Examples.Sudoku;

/// <summary>
/// Greater-Than Sudoku puzzle data for <see cref="SudokuGridTheorem.WithGreaterThan"/>.
/// </summary>
/// <remarks>
/// <para>
/// A Greater-Than Sudoku has no givens. Instead, a "greater than" sign sits between some pairs of
/// neighbouring cells inside each 3x3 box. The usual row, column and box rules still apply.
/// </para>
/// <para>
/// <see cref="BookPuzzle"/> holds the inequalities used in "SAT/SMT by Example" by Dennis
/// Yurichev, section 8.1.3, https://smt.st, which takes the puzzle from killersudokuonline.com.
/// The book's source adds 49 inequalities, but one repeats another, so there are 48 distinct
/// signs. They do not determine a single grid: Z3 finds valid grids that differ from the answer
/// printed in the book. Presumably a sign of the original puzzle was lost on the way into the
/// book's source - perhaps where the repeated line now stands. <see cref="BookSolution"/> is
/// the printed answer. It satisfies all 48 inequalities, which shows the data was transcribed
/// correctly, but it is only one of several solutions.
/// </para>
/// </remarks>
public static class GreaterThanSudokuPuzzles
{
    /// <summary>
    /// The book's 48 distinct inequalities. Each entry means cell (Row, Col) is greater than cell
    /// (ThanRow, ThanCol), with 0-based coordinates.
    /// </summary>
    public static (int Row, int Col, int ThanRow, int ThanCol)[] BookPuzzle { get; } =
    [
        (0, 0, 0, 1), (1, 0, 1, 1), (2, 1, 2, 0), (0, 2, 0, 1), (1, 2, 0, 2),
        (0, 4, 1, 4), (1, 3, 2, 3), (1, 4, 2, 4), (1, 5, 2, 5),
        (0, 6, 0, 7), (0, 8, 0, 7), (1, 6, 0, 6), (1, 8, 1, 7), (1, 6, 2, 6), (1, 7, 2, 7), (1, 8, 2, 8),
        (4, 0, 3, 0), (5, 0, 4, 0), (4, 2, 4, 1), (4, 1, 5, 1), (5, 2, 4, 2),
        (3, 4, 4, 4), (3, 5, 3, 4), (4, 4, 4, 3), (5, 3, 4, 3),
        (3, 6, 3, 7), (3, 7, 3, 8), (3, 6, 4, 6), (4, 7, 4, 6), (4, 8, 4, 7), (5, 8, 5, 7),
        (6, 0, 6, 1), (6, 2, 6, 1), (6, 1, 7, 1), (7, 1, 7, 0), (7, 0, 8, 0), (7, 2, 8, 2),
        (6, 3, 6, 4), (6, 4, 6, 5), (7, 3, 7, 4), (7, 5, 7, 4), (8, 3, 8, 4), (8, 5, 8, 4), (7, 4, 8, 4),
        (6, 7, 6, 8), (6, 7, 7, 7), (7, 7, 8, 7), (8, 7, 8, 8),
    ];

    /// <summary>
    /// The answer printed in the book: one of the grids that satisfy <see cref="BookPuzzle"/>.
    /// </summary>
    public static int[,] BookSolution { get; } = new int[,]
    {
        { 7, 3, 4, 6, 9, 2, 5, 1, 8 },
        { 2, 1, 5, 8, 3, 7, 9, 4, 6 },
        { 6, 8, 9, 5, 1, 4, 7, 2, 3 },
        { 1, 7, 3, 2, 8, 9, 6, 5, 4 },
        { 5, 4, 6, 1, 7, 3, 2, 8, 9 },
        { 9, 2, 8, 4, 5, 6, 1, 3, 7 },
        { 8, 6, 7, 3, 2, 1, 4, 9, 5 },
        { 4, 5, 2, 9, 6, 8, 3, 7, 1 },
        { 3, 9, 1, 7, 4, 5, 8, 6, 2 },
    };
}
