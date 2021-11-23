namespace Z3.Linq.Examples.Sudoku;
 
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

public static class SudokuTheorem
{
    public static Theorem<SudokuTable> Create(Z3Context context)
    {
        var sudokuTheorem = context.NewTheorem<SudokuTable>();

        var cells = typeof(SudokuTable).GetProperties();

        foreach (var cell in cells)
        {
            sudokuTheorem = sudokuTheorem.Where(Between1And9(cell));
        }

        sudokuTheorem = sudokuTheorem.Where(DistinctRows(cells));
        sudokuTheorem = sudokuTheorem.Where(DistinctColumns(cells));

        sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(t.Cell11, t.Cell12, t.Cell13, t.Cell21, t.Cell22, t.Cell23, t.Cell31, t.Cell32, t.Cell33));
        sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(t.Cell14, t.Cell15, t.Cell16, t.Cell24, t.Cell25, t.Cell26, t.Cell34, t.Cell35, t.Cell36));
        sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(t.Cell17, t.Cell18, t.Cell19, t.Cell27, t.Cell28, t.Cell29, t.Cell37, t.Cell38, t.Cell39));
        sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(t.Cell41, t.Cell42, t.Cell43, t.Cell51, t.Cell52, t.Cell53, t.Cell61, t.Cell62, t.Cell63));
        sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(t.Cell44, t.Cell45, t.Cell46, t.Cell54, t.Cell55, t.Cell56, t.Cell64, t.Cell65, t.Cell66));
        sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(t.Cell47, t.Cell48, t.Cell49, t.Cell57, t.Cell58, t.Cell59, t.Cell67, t.Cell68, t.Cell69));
        sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(t.Cell71, t.Cell72, t.Cell73, t.Cell81, t.Cell82, t.Cell83, t.Cell91, t.Cell92, t.Cell93));
        sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(t.Cell74, t.Cell75, t.Cell76, t.Cell84, t.Cell85, t.Cell86, t.Cell94, t.Cell95, t.Cell96));
        sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(t.Cell77, t.Cell78, t.Cell79, t.Cell87, t.Cell88, t.Cell89, t.Cell97, t.Cell98, t.Cell99));

        return sudokuTheorem;
    }

    private static Expression<System.Func<SudokuTable, bool>> Between1And9(PropertyInfo cellProperty)
    {
        ParameterExpression tParam = Expression.Parameter(typeof(SudokuTable), "t");
        MemberExpression cell = Expression.Property(tParam, cellProperty);

        ConstantExpression one = Expression.Constant(1, typeof(int));
        ConstantExpression nine = Expression.Constant(9, typeof(int));

        BinaryExpression cellGreaterThanOrEqual1 = Expression.GreaterThanOrEqual(cell, one);
        BinaryExpression cellLessThanOrEqual9 = Expression.LessThanOrEqual(cell, nine);

        var expr = Expression.Lambda<System.Func<SudokuTable, bool>>(
            Expression.And(cellGreaterThanOrEqual1, cellLessThanOrEqual9),
            new[] { tParam });

        return expr;
    }

    private static Expression<System.Func<SudokuTable, bool>> Distinct(PropertyInfo[] cells, string cellPattern)
    {
        ParameterExpression tParam = Expression.Parameter(typeof(SudokuTable), "t");

        Expression? distincts = null;

        for (int distinctIndex = 1; distinctIndex <= 9; distinctIndex++)
        {
            var cellsInDistinct = new List<MemberExpression>();
            for (int otherIndex = 1; otherIndex <= 9; otherIndex++)
            {
                var cellName = string.Format(cellPattern, distinctIndex, otherIndex);

                MemberExpression cell = Expression.Property(tParam, cells.Single(_ => _.Name == cellName));
                cellsInDistinct.Add(cell);
            }

            NewArrayExpression distinctArray = Expression.NewArrayInit(typeof(int), cellsInDistinct);
            MethodCallExpression distinct = Expression.Call(typeof(Z3Methods), "Distinct", new[] { typeof(int) }, distinctArray);

            if (distincts == null)
            {
                distincts = distinct;
            }
            else
            {
                distincts = Expression.And(distincts, distinct);
            }
        }

        var expr = Expression.Lambda<System.Func<SudokuTable, bool>>(
            distincts!,
            new[] { tParam });

        return expr;
    }

    private static Expression<System.Func<SudokuTable, bool>> DistinctColumns(PropertyInfo[] cells)
    {
        return Distinct(cells, "Cell{1}{0}");
    }

    private static Expression<System.Func<SudokuTable, bool>> DistinctRows(PropertyInfo[] cells)
    {
        return Distinct(cells, "Cell{0}{1}");
    }
}