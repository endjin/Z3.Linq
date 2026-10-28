namespace Z3.Linq.Tests;

using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Shouldly;

using Z3.Linq;

/// <summary>
/// Covers <c>Z3Methods.Distinct</c> called with inline arguments, which the visitor reads
/// straight off the <c>NewArrayExpression</c> without going near the partial evaluator.
/// </summary>
/// <remarks>
/// This is the branch the Sudoku theorems take, and it is deliberately paired with
/// <see cref="DistinctSelectorTheoremTests"/>: the two branches are different code paths to
/// the same semantics, so holding them to the same expected results is what makes a change
/// to the partial-evaluation branch safe to trust.
/// </remarks>
[TestClass]
public class DistinctInlineArrayTheoremTests
{
    [TestMethod]
    public void Distinct_InlineDistinctSymbols_Solves()
    {
        // Act
        using var context = new Z3Context();
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where Z3Methods.Distinct(t.X1, t.X2)
                      where t.X1 == 3
                      where t.X2 > 3 && t.X2 < 5
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(3);
        result.X2.ShouldBe(4);
    }

    [TestMethod]
    public void Distinct_InlineSymbolsConstrainedEqual_IsUnsatisfiable()
    {
        // Act
        using var context = new Z3Context();
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where Z3Methods.Distinct(t.X1, t.X2)
                      where t.X1 == t.X2
                      select t).Solve();

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void Distinct_InlineAndSelectorForms_AgreeOnSatisfiability()
    {
        // Arrange: 'Distinct(X1*1, X1*2)' expressed both ways. The inline form goes through
        // the NewArrayExpression branch, the projected form through the partial-evaluation
        // branch. Pinning X1 to zero makes both terms zero, so both must be unsatisfiable.
        int[] multipliers = [1, 2];

        // Act
        using var inlineContext = new Z3Context();
        var inlineResult = (from t in inlineContext.NewTheorem<Symbols<int, int>>()
                            where Z3Methods.Distinct(t.X1 * 1, t.X1 * 2)
                            where t.X1 == 0
                            select t).Solve();

        using var selectorContext = new Z3Context();
        var selectorResult = (from t in selectorContext.NewTheorem<Symbols<int, int>>()
                              where Z3Methods.Distinct(multipliers.Select(m => t.X1 * m).ToArray())
                              where t.X1 == 0
                              select t).Solve();

        // Assert
        inlineResult.ShouldBeNull();
        selectorResult.ShouldBeNull();
    }

    [TestMethod]
    public void Distinct_InlineAndSelectorForms_AgreeOnSolution()
    {
        // Arrange: the same theorem written both ways must yield the same assignment.
        int[] multipliers = [1, 2];

        // Act
        using var inlineContext = new Z3Context();
        var inlineResult = (from t in inlineContext.NewTheorem<Symbols<int, int>>()
                            where Z3Methods.Distinct(t.X1 * 1, t.X1 * 2)
                            where t.X1 == 5
                            where t.X2 == 2
                            select t).Solve();

        using var selectorContext = new Z3Context();
        var selectorResult = (from t in selectorContext.NewTheorem<Symbols<int, int>>()
                              where Z3Methods.Distinct(multipliers.Select(m => t.X1 * m).ToArray())
                              where t.X1 == 5
                              where t.X2 == 2
                              select t).Solve();

        // Assert
        inlineResult.ShouldNotBeNull();
        selectorResult.ShouldNotBeNull();
        selectorResult.X1.ShouldBe(inlineResult.X1);
        selectorResult.X2.ShouldBe(inlineResult.X2);
    }
}
