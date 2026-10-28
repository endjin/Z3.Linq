namespace Z3.Linq.Examples.Puzzles;

/// <summary>
/// Places N queens on an N x N chessboard so that no two queens attack each other.
/// </summary>
/// <remarks>
/// <para>
/// Ported from "SAT/SMT by Example" by Dennis Yurichev, section 8.4, https://smt.st.
/// </para>
/// <para>
/// Each row holds exactly one queen, so the board is one integer per row: the column of that
/// row's queen. Two queens attack each other when they share a column, or when the distance
/// between their columns equals the distance between their rows (a diagonal).
/// </para>
/// <para>
/// A placement exists for every N except 2 and 3. Most sizes have many placements (92 for N = 8),
/// so the solution Z3 returns is one valid placement, not a fixed answer.
/// </para>
/// </remarks>
public static class NQueens
{
    /// <summary>
    /// Builds the theorem for an N x N board.
    /// </summary>
    /// <param name="context">The Z3 context that owns the theorem.</param>
    /// <param name="size">The number of rows, columns and queens.</param>
    /// <returns>A theorem whose solutions are the non-attacking placements.</returns>
    public static Theorem<QueensBoard> Create(Z3Context context, int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        var theorem = context.NewTheorem(new QueensBoard { Column = new int[size] });

        for (int i = 0; i < size; i++)
        {
            // Copy the loop variable: the lambda is translated when the theorem is solved.
            int row = i;
            theorem = theorem.Where(t => t.Column[row] >= 0 && t.Column[row] < size);
        }

        for (int i = 0; i < size; i++)
        {
            for (int j = i + 1; j < size; j++)
            {
                int upper = i;
                int lower = j;
                int rowDistance = j - i;

                theorem = theorem.Where(t =>
                    t.Column[upper] != t.Column[lower]
                    && t.Column[upper] - t.Column[lower] != rowDistance
                    && t.Column[lower] - t.Column[upper] != rowDistance);
            }
        }

        return theorem;
    }
}

/// <summary>
/// A chessboard with one queen per row.
/// </summary>
public class QueensBoard
{
    /// <summary>
    /// Gets or sets the column (0-based) of the queen in each row. Its length is the board size.
    /// </summary>
    public int[] Column { get; set; } = [];
}
