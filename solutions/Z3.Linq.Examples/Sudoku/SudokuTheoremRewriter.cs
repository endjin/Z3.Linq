namespace Z3.Linq.Examples.Sudoku;

using System.Collections.Generic;
using System.Linq.Expressions;

public class SudokuTheoremRewriter : ITheoremGlobalRewriter
{
    public IEnumerable<LambdaExpression> Rewrite(IEnumerable<LambdaExpression> constraints)
    {
        return constraints;
    }
}