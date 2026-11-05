namespace Z3.Linq.Examples.Sudoku;

using System.Text;

/// <summary>
/// A Sudoku grid held as one nested collection symbol: nine rows of nine cells.
/// </summary>
/// <remarks>
/// <para>
/// The same puzzle as <see cref="SudokuTable"/>, with the 81 <c>CellRC</c> properties replaced by
/// a single <c>int[][]</c>. Z3 sees one array from <c>Int</c> to an array from <c>Int</c> to
/// <c>Int</c>, and a constraint such as <c>g.Cells[0][3] == 2</c> is two selects over it. See #43.
/// </para>
/// <para>
/// The rows are initialised here because a collection symbol takes its length from an instance:
/// this initialiser, or a template passed to <c>NewTheorem</c>. See #53 and #78.
/// </para>
/// </remarks>
public class SudokuGrid
{
    /// <summary>
    /// Gets the cells, indexed from zero as <c>Cells[row][column]</c>.
    /// </summary>
    public int[][] Cells { get; private set; } =
        [new int[9], new int[9], new int[9], new int[9], new int[9], new int[9], new int[9], new int[9], new int[9]];

    /// <inheritdoc/>
    public override string ToString()
    {
        var lineSep = new string('-', 31);
        var blankSep = new string(' ', 8);

        var output = new StringBuilder();
        output.Append(lineSep);
        output.AppendLine();

        for (int row = 0; row < 9; row++)
        {
            output.Append("| ");
            for (int column = 0; column < 9; column++)
            {
                output.Append(this.Cells[row][column]);
                output.Append((column + 1) % 3 == 0 ? " | " : "  ");
            }

            output.AppendLine();
            if ((row + 1) % 3 == 0)
            {
                output.Append(lineSep);
            }
            else
            {
                output.Append("| ");
                for (int i = 0; i < 3; i++)
                {
                    output.Append(blankSep);
                    output.Append("| ");
                }
            }

            output.AppendLine();
        }

        return output.ToString();
    }
}
