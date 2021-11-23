namespace Z3.Linq.Examples.Sudoku;

using System.Text;

[TheoremGlobalRewriter(typeof(SudokuTheoremRewriter))]
public class SudokuTable
{
    public int Cell11 { get; private set; }

    public int Cell12 { get; private set; }

    public int Cell13 { get; private set; }

    public int Cell14 { get; private set; }

    public int Cell15 { get; private set; }

    public int Cell16 { get; private set; }

    public int Cell17 { get; private set; }

    public int Cell18 { get; private set; }

    public int Cell19 { get; private set; }

    public int Cell21 { get; private set; }

    public int Cell22 { get; private set; }

    public int Cell23 { get; private set; }

    public int Cell24 { get; private set; }

    public int Cell25 { get; private set; }

    public int Cell26 { get; private set; }

    public int Cell27 { get; private set; }

    public int Cell28 { get; private set; }

    public int Cell29 { get; private set; }

    public int Cell31 { get; private set; }

    public int Cell32 { get; private set; }

    public int Cell33 { get; private set; }

    public int Cell34 { get; private set; }

    public int Cell35 { get; private set; }

    public int Cell36 { get; private set; }

    public int Cell37 { get; private set; }

    public int Cell38 { get; private set; }

    public int Cell39 { get; private set; }

    public int Cell41 { get; private set; }

    public int Cell42 { get; private set; }

    public int Cell43 { get; private set; }

    public int Cell44 { get; private set; }

    public int Cell45 { get; private set; }

    public int Cell46 { get; private set; }

    public int Cell47 { get; private set; }

    public int Cell48 { get; private set; }

    public int Cell49 { get; private set; }

    public int Cell51 { get; private set; }

    public int Cell52 { get; private set; }

    public int Cell53 { get; private set; }

    public int Cell54 { get; private set; }

    public int Cell55 { get; private set; }

    public int Cell56 { get; private set; }

    public int Cell57 { get; private set; }

    public int Cell58 { get; private set; }

    public int Cell59 { get; private set; }

    public int Cell61 { get; private set; }

    public int Cell62 { get; private set; }

    public int Cell63 { get; private set; }

    public int Cell64 { get; private set; }

    public int Cell65 { get; private set; }

    public int Cell66 { get; private set; }

    public int Cell67 { get; private set; }

    public int Cell68 { get; private set; }

    public int Cell69 { get; private set; }

    public int Cell71 { get; private set; }

    public int Cell72 { get; private set; }

    public int Cell73 { get; private set; }

    public int Cell74 { get; private set; }

    public int Cell75 { get; private set; }

    public int Cell76 { get; private set; }

    public int Cell77 { get; private set; }

    public int Cell78 { get; private set; }

    public int Cell79 { get; private set; }

    public int Cell81 { get; private set; }

    public int Cell82 { get; private set; }

    public int Cell83 { get; private set; }

    public int Cell84 { get; private set; }

    public int Cell85 { get; private set; }

    public int Cell86 { get; private set; }

    public int Cell87 { get; private set; }

    public int Cell88 { get; private set; }

    public int Cell89 { get; private set; }

    public int Cell91 { get; private set; }

    public int Cell92 { get; private set; }

    public int Cell93 { get; private set; }

    public int Cell94 { get; private set; }

    public int Cell95 { get; private set; }

    public int Cell96 { get; private set; }

    public int Cell97 { get; private set; }

    public int Cell98 { get; private set; }

    public int Cell99 { get; private set; }

    public override string ToString()
    {
        var lineSep = new string('-', 31);
        var blankSep = new string(' ', 8);

        var cells = GetType().GetProperties();

        var output = new StringBuilder();
        output.Append(lineSep);
        output.AppendLine();

        for (int row = 1; row <= 9; row++)
        {
            output.Append("| ");
            for (int column = 1; column <= 9; column++)
            {
                var cellName = string.Format("Cell{0}{1}", row, column);
                var cellProp = cells.Single(_ => _.Name == cellName);

                var value = cellProp.GetValue(this, null);

                output.Append(value);
                if (column % 3 == 0)
                {
                    output.Append(" | ");
                }
                else
                {
                    output.Append("  ");
                }
            }

            output.AppendLine();
            if (row % 3 == 0)
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