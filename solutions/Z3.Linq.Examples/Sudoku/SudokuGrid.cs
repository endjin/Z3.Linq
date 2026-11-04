namespace Z3.Linq.Examples.Sudoku;

using System.Text;

/// <summary>
/// Sudoku grid using int[][] for nested array support in Z3.Linq.
/// Cells[i][j] represents the value at row i, column j (0-indexed, values 1-9).
/// </summary>
public class SudokuGrid
{
    public int[][] Cells { get; set; } = new int[9][];

    public SudokuGrid()
    {
        for (int i = 0; i < 9; i++)
        {
            Cells[i] = new int[9];
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        var lineSep = new string('-', 25);

        sb.AppendLine(lineSep);

        for (int row = 0; row < 9; row++)
        {
            sb.Append("| ");
            for (int col = 0; col < 9; col++)
            {
                sb.Append(Cells[row][col]);
                if ((col + 1) % 3 == 0)
                {
                    sb.Append(" | ");
                }
                else
                {
                    sb.Append(' ');
                }
            }

            sb.AppendLine();

            if ((row + 1) % 3 == 0)
            {
                sb.AppendLine(lineSep);
            }
        }

        return sb.ToString();
    }
}
