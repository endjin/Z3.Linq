namespace Z3.Linq.Examples.Sudoku;

using System.Linq.Expressions;

/// <summary>
/// Factory for creating Sudoku theorems using the nested array SudokuGrid type.
/// Demonstrates int[][] support in Z3.Linq — cells accessed as grid.Cells[i][j].
/// </summary>
public static class SudokuGridTheorem
{
    /// <summary>
    /// Creates a Sudoku theorem with all standard constraints (range 1-9, distinct rows/columns/boxes).
    /// </summary>
    public static Theorem<SudokuGrid> Create(Z3Context context)
    {
        var theorem = context.NewTheorem<SudokuGrid>();

        // Range constraints: each cell must be between 1 and 9
        for (int i = 0; i < 9; i++)
        {
            for (int j = 0; j < 9; j++)
            {
                var (row, col) = (i, j);
                theorem = theorem.Where(g => g.Cells[row][col] >= 1 && g.Cells[row][col] <= 9);
            }
        }

        // Row distinctness: each row has 9 distinct values
        for (int i = 0; i < 9; i++)
        {
            var row = i;
            theorem = theorem.Where(g => Z3Methods.Distinct(
                g.Cells[row][0], g.Cells[row][1], g.Cells[row][2],
                g.Cells[row][3], g.Cells[row][4], g.Cells[row][5],
                g.Cells[row][6], g.Cells[row][7], g.Cells[row][8]));
        }

        // Column distinctness: each column has 9 distinct values
        for (int j = 0; j < 9; j++)
        {
            var col = j;
            theorem = theorem.Where(g => Z3Methods.Distinct(
                g.Cells[0][col], g.Cells[1][col], g.Cells[2][col],
                g.Cells[3][col], g.Cells[4][col], g.Cells[5][col],
                g.Cells[6][col], g.Cells[7][col], g.Cells[8][col]));
        }

        // Box distinctness: each 3x3 box has 9 distinct values
        for (int boxRow = 0; boxRow < 3; boxRow++)
        {
            for (int boxCol = 0; boxCol < 3; boxCol++)
            {
                var (br, bc) = (boxRow, boxCol);
                theorem = theorem.Where(g => Z3Methods.Distinct(
                    g.Cells[br * 3][bc * 3], g.Cells[br * 3][bc * 3 + 1], g.Cells[br * 3][bc * 3 + 2],
                    g.Cells[br * 3 + 1][bc * 3], g.Cells[br * 3 + 1][bc * 3 + 1], g.Cells[br * 3 + 1][bc * 3 + 2],
                    g.Cells[br * 3 + 2][bc * 3], g.Cells[br * 3 + 2][bc * 3 + 1], g.Cells[br * 3 + 2][bc * 3 + 2]));
            }
        }

        return theorem;
    }
}
