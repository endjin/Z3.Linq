namespace Z3.Linq.Tests;

using Z3.Linq.Examples.Sudoku;

/// <summary>
/// Acceptance tests that solve Sudoku over the nested-array <see cref="SudokuGrid"/> environment.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart of <see cref="SudokuTheoremTests"/>: the same published puzzle and the same
/// expected solution, with the 81 scalar symbols of <see cref="SudokuTable"/> replaced by one
/// <c>int[][]</c> symbol. Each cell is a select of a select, so this is the realistic-size check
/// on nested collection symbols - 27 <c>Distinct</c> constraints over 81 nested selects, and a
/// read-back of nine rows. See #43.
/// </para>
/// <para>
/// The givens are built as expression trees by <see cref="SudokuGridTheorem.Given"/>, with the
/// indices and values as constants, so the tests depend on nothing captured from a closure.
/// </para>
/// </remarks>
[TestClass]
public class SudokuGridTheoremTests
{
    /// <summary>
    /// The puzzle from https://sandiway.arizona.edu/sudoku/examples.html, with 0 for a blank. It
    /// is the puzzle <see cref="SudokuTheoremTests"/> solves as puzzle B.
    /// </summary>
    private static readonly int[,] PublishedUniquePuzzle =
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

    [TestMethod]
    public void Solve_PublishedUniquePuzzle_ReturnsTheKnownSolution()
    {
        // Arrange: 36 givens and a single solution - so the whole grid is safe to assert.
        using var context = new Z3Context();

        // Act
        var result = Solve(context, PublishedUniquePuzzle);

        // Assert
        result.ShouldNotBeNull();

        int[][] expected =
        [
            [4, 3, 5, 2, 6, 9, 7, 8, 1],
            [6, 8, 2, 5, 7, 1, 4, 9, 3],
            [1, 9, 7, 8, 3, 4, 5, 6, 2],
            [8, 2, 6, 1, 9, 5, 3, 4, 7],
            [3, 7, 4, 6, 8, 2, 9, 1, 5],
            [9, 5, 1, 7, 4, 3, 6, 2, 8],
            [5, 1, 9, 3, 2, 6, 8, 7, 4],
            [2, 4, 8, 9, 5, 7, 1, 3, 6],
            [7, 6, 3, 4, 1, 8, 2, 5, 9],
        ];

        result.Cells.Length.ShouldBe(9);
        for (int row = 0; row < 9; row++)
        {
            result.Cells[row].ShouldBe(expected[row], $"row {row + 1}");
        }
    }

    [TestMethod]
    public void Solve_PublishedUniquePuzzle_ProducesAValidGrid()
    {
        // Arrange: the same solve checked structurally rather than against a fixed answer, and
        // with the givens checked to have survived.
        using var context = new Z3Context();

        // Act
        var result = Solve(context, PublishedUniquePuzzle);

        // Assert
        result.ShouldNotBeNull();

        for (int row = 0; row < 9; row++)
        {
            for (int column = 0; column < 9; column++)
            {
                if (PublishedUniquePuzzle[row, column] != 0)
                {
                    result.Cells[row][column].ShouldBe(
                        PublishedUniquePuzzle[row, column],
                        $"given R{row + 1}C{column + 1}");
                }
            }
        }

        AssertIsValidSudokuGrid(result.Cells);
    }

    [TestMethod]
    public void Solve_PuzzleWithContradictoryGivens_ReturnsNull()
    {
        // Arrange: two givens that collide in the same row.
        using var context = new Z3Context();

        // Act
        var result = SudokuGridTheorem.Create(context)
            .Where(SudokuGridTheorem.Given(0, 0, 5))
            .Where(SudokuGridTheorem.Given(0, 1, 5))
            .Solve();

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// Adds every non-zero cell of <paramref name="puzzle"/> as a given and solves.
    /// </summary>
    private static SudokuGrid? Solve(Z3Context context, int[,] puzzle)
    {
        var theorem = SudokuGridTheorem.Create(context);

        for (int row = 0; row < 9; row++)
        {
            for (int column = 0; column < 9; column++)
            {
                if (puzzle[row, column] != 0)
                {
                    theorem = theorem.Where(SudokuGridTheorem.Given(row, column, puzzle[row, column]));
                }
            }
        }

        return theorem.Solve();
    }

    /// <summary>
    /// Asserts the three Sudoku invariants: every row, column and 3x3 box holds 1-9 exactly once.
    /// </summary>
    private static void AssertIsValidSudokuGrid(int[][] cells)
    {
        int[] oneToNine = [1, 2, 3, 4, 5, 6, 7, 8, 9];

        for (int row = 0; row < 9; row++)
        {
            cells[row].OrderBy(value => value).ToArray().ShouldBe(oneToNine, $"row {row + 1}");
        }

        for (int column = 0; column < 9; column++)
        {
            Enumerable.Range(0, 9).Select(row => cells[row][column]).OrderBy(value => value).ToArray()
                .ShouldBe(oneToNine, $"column {column + 1}");
        }

        for (int box = 0; box < 9; box++)
        {
            int rowOffset = (box / 3) * 3;
            int columnOffset = (box % 3) * 3;

            Enumerable.Range(0, 9)
                .Select(offset => cells[rowOffset + (offset / 3)][columnOffset + (offset % 3)])
                .OrderBy(value => value).ToArray()
                .ShouldBe(oneToNine, $"box {box + 1}");
        }
    }
}
