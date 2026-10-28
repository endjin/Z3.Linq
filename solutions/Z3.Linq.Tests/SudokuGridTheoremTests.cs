namespace Z3.Linq.Tests;

using Z3.Linq.Examples.Sudoku;

/// <summary>
/// Acceptance tests for the bit-vector Sudoku grid and the Killer and Greater-Than variants from
/// "SAT/SMT by Example" (sections 8.1.4 and 8.1.3).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SudokuGrid"/> holds its 81 cells as <see cref="uint"/>, which Z3.Linq maps to
/// 32-bit bit-vectors. The same Killer puzzle over the 81 <see cref="int"/> cells of
/// <see cref="SudokuTable"/> took about 230 s, and the Greater-Than puzzle about 56 s. On this
/// grid, measured one test at a time with Microsoft.Z3 5.3.2, Killer solves in about 0.75 s,
/// Greater-Than in about 1.1 s, and the Killer uniqueness check in about 1.6 s; the clue-only
/// puzzles take about 0.1 s. That gap is why these variants have their own environment.
/// </para>
/// <para>
/// The Killer puzzle is pinned cell by cell because it is unique, and one test proves that:
/// excluding the solution makes the theorem unsatisfiable. The Greater-Than signs in the book
/// do not determine a single grid - its printed answer is one of several, and the grid Z3 finds
/// can differ from it in many cells - so that puzzle is asserted by invariant only: every inequality
/// holds and the grid is well formed. Pinning the grid Z3 happens to find would pin this
/// version of Z3, not the puzzle.
/// </para>
/// <para>
/// The arizona.edu puzzle that <see cref="SudokuTheoremTests"/> pins on the integer grid is
/// solved here too, through <see cref="SudokuGridTheorem.WithClues"/>, so both encodings are
/// held to the same answer.
/// </para>
/// </remarks>
[TestClass]
public class SudokuGridTheoremTests
{
    private static readonly int[,] KillerSolution =
    {
        { 5, 3, 4, 7, 1, 2, 8, 9, 6 },
        { 8, 2, 1, 4, 6, 9, 7, 5, 3 },
        { 9, 6, 7, 8, 3, 5, 4, 2, 1 },
        { 2, 4, 6, 1, 9, 7, 3, 8, 5 },
        { 7, 1, 9, 3, 5, 8, 6, 4, 2 },
        { 3, 8, 5, 6, 2, 4, 9, 1, 7 },
        { 4, 7, 2, 5, 8, 3, 1, 6, 9 },
        { 6, 5, 8, 9, 7, 1, 2, 3, 4 },
        { 1, 9, 3, 2, 4, 6, 5, 7, 8 },
    };

    private static readonly int[,] PuzzleB =
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

    private static readonly int[,] PuzzleBSolution =
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

    [TestMethod]
    public void Solve_KillerSudoku_ReturnsTheKnownSolution()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = SudokuGridTheorem.Create(context).WithCages(KillerSudokuPuzzles.SuperTough);

        // Act
        var result = theorem.Solve();

        // Assert
        result.ShouldNotBeNull();
        AssertGridIs(result, KillerSolution);
    }

    [TestMethod]
    public void Solve_KillerSudoku_SatisfiesEveryCageAndTheSudokuRules()
    {
        // Arrange: the same solve checked structurally, so the cage rules are held to account
        // even if the expected grid above ever needs revisiting.
        using var context = new Z3Context();
        var theorem = SudokuGridTheorem.Create(context).WithCages(KillerSudokuPuzzles.SuperTough);

        // Act
        var result = theorem.Solve();

        // Assert
        result.ShouldNotBeNull();
        AssertIsValidSudokuGrid(result);

        foreach (var (sum, cells) in KillerSudokuPuzzles.SuperTough)
        {
            var values = cells.Select(cell => result.Cell(cell.Row, cell.Col)).ToArray();
            string cage = string.Join(" ", cells.Select(cell => $"R{cell.Row + 1}C{cell.Col + 1}"));

            values.Sum(value => (int)value).ShouldBe(sum, $"sum of cage {cage}");
            values.Distinct().Count().ShouldBe(values.Length, $"digits of cage {cage}");
        }
    }

    [TestMethod]
    public void Solve_KillerSudokuExcludingItsSolution_ReturnsNull()
    {
        // Arrange: rule out the known answer. If Z3 then finds nothing, the answer is the only
        // one, which is what licenses pinning it cell by cell above.
        using var context = new Z3Context();
        var theorem = SudokuGridTheorem.Create(context)
            .WithCages(KillerSudokuPuzzles.SuperTough)
            .Excluding(ToSudokuGrid(KillerSolution));

        // Act
        var result = theorem.Solve();

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void Solve_GreaterThanSudoku_SatisfiesEveryInequalityAndTheSudokuRules()
    {
        // Arrange: the book's clue list admits several grids, so assert what is true of all of
        // them rather than whichever one Z3 returns.
        using var context = new Z3Context();
        var theorem = SudokuGridTheorem.Create(context).WithGreaterThan(GreaterThanSudokuPuzzles.BookPuzzle);

        // Act
        var result = theorem.Solve();

        // Assert
        result.ShouldNotBeNull();
        AssertIsValidSudokuGrid(result);

        foreach (var (row, col, thanRow, thanCol) in GreaterThanSudokuPuzzles.BookPuzzle)
        {
            result.Cell(row, col).ShouldBeGreaterThan(
                result.Cell(thanRow, thanCol),
                $"R{row + 1}C{col + 1} > R{thanRow + 1}C{thanCol + 1}");
        }
    }

    [TestMethod]
    public void Solve_GreaterThanSudokuWithTheBookSolutionAsClues_IsSatisfiable()
    {
        // Arrange: a data check. The book's printed answer must satisfy the transcribed
        // inequalities; if it did not, a sign would have been copied the wrong way round.
        using var context = new Z3Context();
        var theorem = SudokuGridTheorem.Create(context)
            .WithGreaterThan(GreaterThanSudokuPuzzles.BookPuzzle)
            .WithClues(GreaterThanSudokuPuzzles.BookSolution);

        // Act
        var result = theorem.Solve();

        // Assert
        result.ShouldNotBeNull();
        AssertGridIs(result, GreaterThanSudokuPuzzles.BookSolution);
    }

    [TestMethod]
    public void GreaterThanBookPuzzle_Data_HasFortyEightDistinctSigns()
    {
        // A data check that the solve tests cannot make: a dropped sign would leave every grid
        // test green. The book's script has 49 sign lines, one of them repeated.
        var signs = GreaterThanSudokuPuzzles.BookPuzzle;

        signs.Length.ShouldBe(48);
        signs.Distinct().Count().ShouldBe(48);
    }

    [TestMethod]
    public void KillerSuperTough_Data_HasTwentyFiveCagesCoveringSeventySevenCells()
    {
        // A data check: 25 cages over 77 distinct cells, summing to 384. The book leaves out a
        // 26th cage of four cells summing to 21, which brings the grid's total to 405.
        var cages = KillerSudokuPuzzles.SuperTough;
        var cells = cages.SelectMany(cage => cage.Cells).ToArray();

        cages.Length.ShouldBe(25);
        cells.Length.ShouldBe(77);
        cells.Distinct().Count().ShouldBe(77);
        cages.Sum(cage => cage.Sum).ShouldBe(384);
    }

    [TestMethod]
    public void Solve_GreaterThanSudoku_HasASolutionOtherThanTheBookAnswer()
    {
        // Arrange: the reason the puzzle is not pinned. The book's printed answer satisfies
        // every inequality (checked above), so any other valid grid proves the clues admit more
        // than one solution.
        using var context = new Z3Context();
        var theorem = SudokuGridTheorem.Create(context).WithGreaterThan(GreaterThanSudokuPuzzles.BookPuzzle);
        var bookSolution = ToSudokuGrid(GreaterThanSudokuPuzzles.BookSolution);

        // Act: Microsoft.Z3 5.3.2 returns a different grid straight away. Should a later version
        // happen to return the book's answer first, rule it out and ask again - that second
        // solve is slower (about 3 s), so it only runs when needed.
        var result = theorem.Solve();
        if (result is not null && SameGrid(result, bookSolution))
        {
            result = theorem.Excluding(bookSolution).Solve();
        }

        // Assert
        result.ShouldNotBeNull();
        AssertIsValidSudokuGrid(result);
        SameGrid(result, bookSolution).ShouldBeFalse();

        foreach (var (row, col, thanRow, thanCol) in GreaterThanSudokuPuzzles.BookPuzzle)
        {
            result.Cell(row, col).ShouldBeGreaterThan(result.Cell(thanRow, thanCol));
        }
    }

    [TestMethod]
    public void Solve_PublishedUniquePuzzleOnTheBitVectorGrid_ReturnsTheKnownSolution()
    {
        // Arrange: puzzle B from SudokuTheoremTests, given as clues on the uint grid.
        using var context = new Z3Context();
        var theorem = SudokuGridTheorem.Create(context).WithClues(PuzzleB);

        // Act
        var result = theorem.Solve();

        // Assert
        result.ShouldNotBeNull();
        AssertGridIs(result, PuzzleBSolution);
    }

    [TestMethod]
    public void Solve_PuzzleWithContradictoryClues_ReturnsNull()
    {
        // Arrange: two 5s in the same row.
        var clues = new int[9, 9];
        clues[0, 0] = 5;
        clues[0, 1] = 5;

        using var context = new Z3Context();
        var theorem = SudokuGridTheorem.Create(context).WithClues(clues);

        // Act
        var result = theorem.Solve();

        // Assert
        result.ShouldBeNull();
    }

    private static bool SameGrid(SudokuGrid left, SudokuGrid right) => left.Cells.SequenceEqual(right.Cells);

    private static SudokuGrid ToSudokuGrid(int[,] values)
    {
        var grid = new SudokuGrid();

        for (int row = 0; row < 9; row++)
        {
            for (int column = 0; column < 9; column++)
            {
                grid.Cells[(row * 9) + column] = (uint)values[row, column];
            }
        }

        return grid;
    }

    private static void AssertGridIs(SudokuGrid grid, int[,] expected)
    {
        for (int row = 0; row < 9; row++)
        {
            for (int column = 0; column < 9; column++)
            {
                ((int)grid.Cell(row, column)).ShouldBe(expected[row, column], $"cell R{row + 1}C{column + 1}");
            }
        }
    }

    /// <summary>
    /// Asserts the three Sudoku invariants: every row, column and 3x3 box holds 1-9 exactly once.
    /// </summary>
    private static void AssertIsValidSudokuGrid(SudokuGrid grid)
    {
        uint[] oneToNine = [1, 2, 3, 4, 5, 6, 7, 8, 9];

        for (int row = 0; row < 9; row++)
        {
            Enumerable.Range(0, 9).Select(column => grid.Cell(row, column)).OrderBy(value => value).ToArray()
                .ShouldBe(oneToNine, $"row {row + 1}");
        }

        for (int column = 0; column < 9; column++)
        {
            Enumerable.Range(0, 9).Select(row => grid.Cell(row, column)).OrderBy(value => value).ToArray()
                .ShouldBe(oneToNine, $"column {column + 1}");
        }

        for (int box = 0; box < 9; box++)
        {
            int rowOffset = (box / 3) * 3;
            int columnOffset = (box % 3) * 3;

            Enumerable.Range(0, 9)
                .Select(offset => grid.Cell(rowOffset + (offset / 3), columnOffset + (offset % 3)))
                .OrderBy(value => value).ToArray()
                .ShouldBe(oneToNine, $"box {box + 1}");
        }
    }
}
