namespace Z3.Linq.Tests;

using Z3.Linq.Examples.Puzzles;

/// <summary>
/// Tests that solve the N queens puzzle from "SAT/SMT by Example" (section 8.4).
/// </summary>
/// <remarks>
/// <para>
/// Most board sizes have many solutions (92 for the 8 x 8 board), so the placement Z3 returns is
/// checked by the rules of the puzzle - one queen per row, no shared column, no shared diagonal -
/// and never compared with a fixed placement. Sizes 2 and 3 have no placement at all, so for them
/// <c>TrySolve</c> must return <see langword="false"/>.
/// </para>
/// <para>
/// The 12 x 12 board is the slowest case, at well under a second.
/// </para>
/// </remarks>
[TestClass]
public class NQueensTests
{
    [TestMethod]
    [DataRow(1, DisplayName = "1 x 1")]
    [DataRow(4, DisplayName = "4 x 4")]
    [DataRow(6, DisplayName = "6 x 6")]
    [DataRow(8, DisplayName = "8 x 8")]
    [DataRow(12, DisplayName = "12 x 12")]
    public void TrySolve_SolvableBoard_PlacesNonAttackingQueens(int size)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool solved = NQueens.Create(context, size).TrySolve(out var board);

        // Assert
        solved.ShouldBeTrue();
        board.ShouldNotBeNull();
        board.Column.Length.ShouldBe(size);

        for (int row = 0; row < size; row++)
        {
            board.Column[row].ShouldBeInRange(0, size - 1, $"row {row}");
        }

        for (int i = 0; i < size; i++)
        {
            for (int j = i + 1; j < size; j++)
            {
                board.Column[i].ShouldNotBe(board.Column[j], $"rows {i} and {j} share a column");
                Math.Abs(board.Column[i] - board.Column[j]).ShouldNotBe(j - i, $"rows {i} and {j} share a diagonal");
            }
        }
    }

    [TestMethod]
    [DataRow(2, DisplayName = "2 x 2")]
    [DataRow(3, DisplayName = "3 x 3")]
    public void TrySolve_BoardWithNoPlacement_ReturnsFalse(int size)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool solved = NQueens.Create(context, size).TrySolve(out _);

        // Assert
        solved.ShouldBeFalse();
    }

    [TestMethod]
    public void Create_SizeBelowOne_Throws()
    {
        // Arrange
        using var context = new Z3Context();

        // Act and assert
        Should.Throw<ArgumentOutOfRangeException>(() => NQueens.Create(context, 0));
    }
}
