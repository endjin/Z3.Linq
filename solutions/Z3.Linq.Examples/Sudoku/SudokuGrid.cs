namespace Z3.Linq.Examples.Sudoku;

using System.Text;

/// <summary>
/// A 9x9 Sudoku grid held as 81 unsigned cells in row-major order.
/// </summary>
/// <remarks>
/// <para>
/// This is the fast sibling of <see cref="SudokuTable"/>. Z3.Linq maps <see cref="uint"/> to a
/// 32-bit bit-vector rather than an unbounded integer, and on bit-vectors Z3 bit-blasts the
/// puzzle into SAT, which suits Sudoku far better than integer arithmetic. Variants that add
/// many arithmetic side constraints show the difference most: Killer Sudoku takes about a
/// second here, against several minutes over 81 <see cref="int"/> cells.
/// </para>
/// <para>
/// Cell (row, column), both 0-based, is <c>Cells[(row * 9) + column]</c>.
/// </para>
/// </remarks>
public class SudokuGrid
{
    public uint[] Cells { get; set; } = new uint[81];

    /// <summary>
    /// Gets the value of a cell, with 0-based row and column.
    /// </summary>
    /// <remarks>
    /// A method rather than an indexer: Z3.Linq treats every public property of an environment
    /// as a symbol and writes the solved value back through its setter, and an indexer is a
    /// property with no setter.
    /// </remarks>
    public uint Cell(int row, int column) => Cells[(row * 9) + column];

    public override string ToString()
    {
        var lineSep = new string('-', 25);

        var output = new StringBuilder();
        output.AppendLine(lineSep);

        for (int row = 0; row < 9; row++)
        {
            output.Append("| ");
            for (int column = 0; column < 9; column++)
            {
                output.Append(Cell(row, column));
                output.Append(column % 3 == 2 ? " | " : " ");
            }

            output.AppendLine();
            if (row % 3 == 2)
            {
                output.AppendLine(lineSep);
            }
        }

        return output.ToString();
    }
}
