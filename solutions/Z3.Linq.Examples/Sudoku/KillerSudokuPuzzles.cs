namespace Z3.Linq.Examples.Sudoku;

/// <summary>
/// Killer Sudoku puzzle data for <see cref="SudokuGridTheorem.WithCages"/>.
/// </summary>
/// <remarks>
/// <para>
/// A Killer Sudoku has no givens. Instead the grid is divided into cages: the digits in a
/// cage are all different and add up to the number printed on it. The usual row, column and box
/// rules still apply.
/// </para>
/// <para>
/// <see cref="SuperTough"/> is the puzzle used in "SAT/SMT by Example" by Dennis Yurichev,
/// section 8.1.4, https://smt.st, which takes it from a krazydad.com "Super-Tough" Killer
/// Sudoku. The original puzzle has one more cage, cells (2,0), (2,1), (3,0) and (3,1) summing to
/// 21, which the book leaves out. The 25 cages here, covering 77 of the 81 cells, already have a
/// single solution, so the omission does not matter.
/// </para>
/// </remarks>
public static class KillerSudokuPuzzles
{
    /// <summary>
    /// The 25 cages of the book's puzzle, as (sum, cells) with 0-based (row, column) cells.
    /// </summary>
    public static (int Sum, (int Row, int Col)[] Cells)[] SuperTough { get; } =
    [
        (13, [(0, 0), (1, 0)]),
        (5, [(0, 1), (1, 1)]),
        (14, [(0, 2), (0, 3), (0, 4), (0, 5)]),
        (15, [(0, 6), (1, 6)]),
        (34, [(0, 7), (0, 8), (1, 7), (1, 8), (2, 7), (2, 8), (3, 7)]),
        (16, [(1, 2), (2, 2), (2, 3)]),
        (10, [(1, 3), (1, 4)]),
        (17, [(1, 5), (2, 4), (2, 5)]),
        (28, [(2, 6), (3, 5), (3, 6), (4, 5), (4, 6)]),
        (7, [(3, 2), (3, 3)]),
        (16, [(3, 4), (4, 4), (5, 4)]),
        (11, [(3, 8), (4, 7), (4, 8)]),
        (11, [(4, 0), (4, 1), (5, 0)]),
        (25, [(4, 2), (4, 3), (5, 2), (5, 3), (6, 2)]),
        (40, [(5, 1), (6, 0), (6, 1), (7, 0), (7, 1), (8, 0), (8, 1)]),
        (13, [(5, 5), (5, 6)]),
        (7, [(5, 7), (6, 7)]),
        (16, [(5, 8), (6, 8)]),
        (22, [(6, 3), (6, 4), (7, 3)]),
        (6, [(6, 5), (6, 6), (7, 6)]),
        (11, [(7, 2), (8, 2)]),
        (8, [(7, 4), (7, 5)]),
        (10, [(7, 7), (8, 7)]),
        (12, [(7, 8), (8, 8)]),
        (17, [(8, 3), (8, 4), (8, 5), (8, 6)]),
    ];
}
