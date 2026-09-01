namespace Z3.Linq.Tests;

using System.Reflection;

using Z3.Linq.Examples.Sudoku;

/// <summary>
/// Acceptance tests that solve the published Sudoku sample theorems end to end.
/// </summary>
/// <remarks>
/// <para>
/// These are the only tests that exercise the library at realistic problem size: 81 symbols, 27
/// <c>Distinct</c> constraints and a global rewriter, all in one theorem. Everything else in the
/// suite works with a handful of symbols, so this is where a translation regression that only
/// shows up at scale would surface.
/// </para>
/// <para>
/// Puzzle B is a published, uniquely-solvable puzzle, so its grid is asserted cell by cell.
/// Puzzle A has only 21 givens and is almost certainly not unique, so it is asserted by
/// invariant: the givens survive, and every row, column and box is a permutation of 1-9. That
/// distinction is the determinism rule of this suite applied to its largest case - a fixed grid
/// for a puzzle with several solutions would pin whichever one this version of Z3 happened to
/// find.
/// </para>
/// </remarks>
[TestClass]
public class SudokuTheoremTests
{
    [TestMethod]
    public void Solve_PublishedUniquePuzzle_ReturnsTheKnownSolution()
    {
        // Arrange: the puzzle from https://sandiway.arizona.edu/sudoku/examples.html, which has
        // 36 givens and a single solution - so the whole grid is safe to assert.
        using var context = new Z3Context();

        // Act
        var result = SolvePuzzleB(context);

        // Assert
        result.ShouldNotBeNull();
        int[,] grid = ToGrid(result);

        int[,] expected =
        {
            { 4, 3, 5, 2, 6, 9, 7, 8, 1 },
            { 6, 8, 2, 5, 7, 1, 4, 9, 3 },
            { 1, 9, 7, 8, 3, 4, 5, 6, 2 },
            { 8, 2, 6, 1, 9, 5, 3, 4, 7 },
            { 3, 7, 4, 6, 8, 2, 9, 1, 5 },
            { 9, 5, 1, 7, 4, 3, 6, 2, 8 },
            { 5, 1, 9, 3, 2, 6, 8, 7, 4 },
            { 2, 4, 8, 9, 5, 7, 1, 3, 6 },
            { 7, 6, 3, 4, 1, 8, 2, 5, 9 },
        };

        for (int row = 0; row < 9; row++)
        {
            for (int column = 0; column < 9; column++)
            {
                grid[row, column].ShouldBe(
                    expected[row, column],
                    $"cell R{row + 1}C{column + 1}");
            }
        }
    }

    [TestMethod]
    public void Solve_PublishedUniquePuzzle_ProducesAValidGrid()
    {
        // Arrange: the same solve checked structurally rather than against a fixed answer. If
        // the expected grid above ever needs revisiting, this still holds the line on what a
        // Sudoku solution has to be.
        using var context = new Z3Context();

        // Act
        var result = SolvePuzzleB(context);

        // Assert
        result.ShouldNotBeNull();
        AssertIsValidSudokuGrid(ToGrid(result));
    }

    [TestMethod]
    public void Solve_PuzzleWithTwentyOneGivens_ProducesAValidGridPreservingTheGivens()
    {
        // Arrange: the demo's first puzzle. Too few givens to assume a unique solution, so this
        // asserts the two things true of every solution - the givens are untouched, and the grid
        // is well formed.
        using var context = new Z3Context();

        // Act
        var result = (from t in SudokuTheorem.Create(context)
                      where t.Cell13 == 2 && t.Cell16 == 1 && t.Cell18 == 6
                      where t.Cell23 == 7 && t.Cell26 == 4
                      where t.Cell31 == 5 && t.Cell37 == 9
                      where t.Cell42 == 1 && t.Cell44 == 3
                      where t.Cell51 == 8 && t.Cell55 == 5 && t.Cell59 == 4
                      where t.Cell66 == 6 && t.Cell68 == 2
                      where t.Cell73 == 6 && t.Cell79 == 7
                      where t.Cell84 == 8 && t.Cell87 == 3
                      where t.Cell92 == 4 && t.Cell94 == 9 && t.Cell97 == 2
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();

        // The givens must survive the solve.
        result.Cell13.ShouldBe(2);
        result.Cell16.ShouldBe(1);
        result.Cell18.ShouldBe(6);
        result.Cell31.ShouldBe(5);
        result.Cell37.ShouldBe(9);
        result.Cell55.ShouldBe(5);
        result.Cell92.ShouldBe(4);
        result.Cell97.ShouldBe(2);

        AssertIsValidSudokuGrid(ToGrid(result));
    }

    [TestMethod]
    public void Solve_PuzzleWithContradictoryGivens_ReturnsNull()
    {
        // Arrange: two givens that collide in the same row. The theorem is well formed, so this
        // checks that an 81-symbol problem reports unsatisfiable rather than failing some other
        // way at that size.
        using var context = new Z3Context();

        // Act
        var result = (from t in SudokuTheorem.Create(context)
                      where t.Cell11 == 5
                      where t.Cell12 == 5
                      select t).Solve();

        // Assert
        result.ShouldBeNull();
    }

    // A puzzle with no givens at all was tried here and deliberately dropped. It solved
    // correctly but took 25 seconds - fifty times the next slowest test in the suite - because
    // an unconstrained grid gives Z3 nothing to propagate from. It proved only that the
    // constraints are satisfiable on their own, which the two puzzles above already establish
    // while also checking an answer. Tagging it as slow rather than removing it would have left
    // a test that never actually runs.

    private static SudokuTable? SolvePuzzleB(Z3Context context)
        => (from t in SudokuTheorem.Create(context)
            where t.Cell14 == 2 && t.Cell15 == 6 && t.Cell17 == 7 && t.Cell19 == 1
            where t.Cell21 == 6 && t.Cell22 == 8 && t.Cell25 == 7 && t.Cell28 == 9
            where t.Cell31 == 1 && t.Cell32 == 9 && t.Cell36 == 4 && t.Cell37 == 5
            where t.Cell41 == 8 && t.Cell42 == 2 && t.Cell44 == 1 && t.Cell48 == 4
            where t.Cell53 == 4 && t.Cell54 == 6 && t.Cell56 == 2 && t.Cell57 == 9
            where t.Cell62 == 5 && t.Cell66 == 3 && t.Cell68 == 2 && t.Cell69 == 8
            where t.Cell73 == 9 && t.Cell74 == 3 && t.Cell78 == 7 && t.Cell79 == 4
            where t.Cell82 == 4 && t.Cell85 == 5 && t.Cell88 == 3 && t.Cell89 == 6
            where t.Cell91 == 7 && t.Cell93 == 3 && t.Cell95 == 1 && t.Cell96 == 8
            select t).Solve();

    /// <summary>
    /// Reads the 81 <c>CellRC</c> properties into a grid indexed from zero.
    /// </summary>
    private static int[,] ToGrid(SudokuTable table)
    {
        var grid = new int[9, 9];

        for (int row = 1; row <= 9; row++)
        {
            for (int column = 1; column <= 9; column++)
            {
                PropertyInfo cell = typeof(SudokuTable).GetProperty($"Cell{row}{column}")
                    ?? throw new InvalidOperationException($"SudokuTable has no Cell{row}{column}.");

                grid[row - 1, column - 1] = (int)cell.GetValue(table)!;
            }
        }

        return grid;
    }

    /// <summary>
    /// Asserts the three Sudoku invariants: every row, column and 3x3 box holds 1-9 exactly once.
    /// </summary>
    private static void AssertIsValidSudokuGrid(int[,] grid)
    {
        int[] oneToNine = [1, 2, 3, 4, 5, 6, 7, 8, 9];

        for (int row = 0; row < 9; row++)
        {
            Enumerable.Range(0, 9).Select(column => grid[row, column]).OrderBy(value => value).ToArray()
                .ShouldBe(oneToNine, $"row {row + 1}");
        }

        for (int column = 0; column < 9; column++)
        {
            Enumerable.Range(0, 9).Select(row => grid[row, column]).OrderBy(value => value).ToArray()
                .ShouldBe(oneToNine, $"column {column + 1}");
        }

        for (int box = 0; box < 9; box++)
        {
            int rowOffset = (box / 3) * 3;
            int columnOffset = (box % 3) * 3;

            Enumerable.Range(0, 9)
                .Select(offset => grid[rowOffset + (offset / 3), columnOffset + (offset % 3)])
                .OrderBy(value => value).ToArray()
                .ShouldBe(oneToNine, $"box {box + 1}");
        }
    }
}
