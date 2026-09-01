namespace Z3.Linq.Tests;

using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Shouldly;

using Z3.Linq;

/// <summary>
/// Covers <c>Z3Methods.Distinct(collection.Select(...).ToArray())</c>, which is the only
/// shape that reaches <c>PartialEvaluator.PartialEval</c> in <c>ExpressionVisitor.VisitCall</c>.
/// </summary>
/// <remarks>
/// The visitor takes one of two branches for a <c>Distinct</c> argument. An inline array
/// (a <c>NewArrayExpression</c>, which is what the Sudoku theorems build) is read directly;
/// a <c>Select(...).ToArray()</c> call is unrolled by interpreting the source collection,
/// substituting each item into the selector, and partially evaluating the result. Only the
/// second branch calls the partial evaluator, and these tests exist because
/// MiaPlaza.ExpressionUtils 1.3.x changed that API - it dropped the overload taking a bare
/// <see cref="System.Linq.Expressions.Expression"/> in favour of one constrained to
/// <see cref="System.Linq.Expressions.LambdaExpression"/>, so the call site now wraps the
/// substituted body in a lambda and unwraps the result.
/// </remarks>
[TestClass]
public class DistinctSelectorTheoremTests
{
    [TestMethod]
    public void Distinct_SelectorProducingDistinctTerms_Solves()
    {
        // Arrange: three multiples of X1 are pairwise distinct only when X1 is non-zero.
        int[] multipliers = [1, 2, 3];

        // Act
        using var context = new Z3Context();
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where Z3Methods.Distinct(multipliers.Select(m => t.X1 * m).ToArray())
                      where t.X1 > 10 && t.X1 < 14
                      where t.X2 == t.X1 + 1
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBeInRange(11, 13);
        result.X2.ShouldBe(result.X1 + 1);
    }

    [TestMethod]
    public void Distinct_SelectorProducingDuplicateTerms_IsUnsatisfiable()
    {
        // Arrange: the selector yields X1*5 twice, which can never be distinct from itself.
        int[] duplicates = [5, 5];

        // Act
        using var context = new Z3Context();
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where Z3Methods.Distinct(duplicates.Select(d => t.X1 * d).ToArray())
                      select t).Solve();

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void Distinct_SelectorWithFoldableArithmetic_Solves()
    {
        // Arrange: '(o * 2) + (3 - 3)' is closed once 'o' is substituted, so the partial
        // evaluator folds it to a constant; 't.X1' depends on the theorem parameter and
        // must survive untouched.
        int[] offsets = [0, 10, 20];

        // Act
        using var context = new Z3Context();
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where Z3Methods.Distinct(offsets.Select(o => t.X1 + (o * 2) + (3 - 3)).ToArray())
                      where t.X1 == 7
                      where t.X2 == 0
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(7);
        result.X2.ShouldBe(0);
    }

    [TestMethod]
    public void Distinct_SelectorReferencingMultipleSymbols_Solves()
    {
        // Arrange: the selector closes over the loop variable and two theorem symbols, so
        // the substituted body still has a free parameter when it is partially evaluated.
        int[] picks = [1, 2];

        // Act
        using var context = new Z3Context();
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where Z3Methods.Distinct(picks.Select(p => (t.X1 * p) + t.X2).ToArray())
                      where t.X1 == 4 && t.X2 == 9
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(4);
        result.X2.ShouldBe(9);
    }

    [TestMethod]
    [DataRow(1, 2, 3, DisplayName = "Distinct multipliers are satisfiable")]
    [DataRow(2, 4, 6, DisplayName = "Distinct even multipliers are satisfiable")]
    [DataRow(-1, 1, 2, DisplayName = "Negative and positive multipliers are satisfiable")]
    public void Distinct_SelectorOverDistinctMultipliers_Solves(int first, int second, int third)
    {
        // Arrange
        int[] multipliers = [first, second, third];

        // Act
        using var context = new Z3Context();
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where Z3Methods.Distinct(multipliers.Select(m => t.X1 * m).ToArray())
                      where t.X1 > 0
                      where t.X2 == 0
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBeGreaterThan(0);
    }

    [TestMethod]
    [DataRow(3, 3, DisplayName = "Repeated multiplier is unsatisfiable")]
    [DataRow(-2, -2, DisplayName = "Repeated negative multiplier is unsatisfiable")]
    public void Distinct_SelectorOverRepeatedMultipliers_IsUnsatisfiable(int first, int second)
    {
        // Arrange
        int[] multipliers = [first, second];

        // Act
        using var context = new Z3Context();
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where Z3Methods.Distinct(multipliers.Select(m => t.X1 * m).ToArray())
                      select t).Solve();

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void Distinct_SelectorCallingHostMethod_IsFoldedBeforeTranslation()
    {
        // Arrange: the selector calls an ordinary C# method on a captured object. Z3 has no
        // notion of it, and the visitor throws NotSupportedException on any method call it
        // does not recognise. The call is closed once the loop variable is substituted, so it
        // only reaches Z3 as a constant because the substituted body is partially evaluated
        // first. This is the assertion that actually pins the partial-evaluation step: the
        // arithmetic-only cases above still pass without it, because Z3 folds constant
        // arithmetic itself.
        int[] steps = [1, 2, 3];
        var offsets = new OffsetTable(100);

        // Act
        using var context = new Z3Context();
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where Z3Methods.Distinct(steps.Select(s => t.X1 + offsets.Get(s)).ToArray())
                      where t.X1 == 5
                      where t.X2 == 0
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(5);
        result.X2.ShouldBe(0);
    }

    [TestMethod]
    public void Distinct_SelectorCallingHostMethodProducingDuplicates_IsUnsatisfiable()
    {
        // Arrange: as above, but the host method maps every index to the same offset, so the
        // folded terms collide and the theorem cannot be satisfied. This guards against a
        // partial evaluator that silently produced the wrong constant rather than throwing.
        int[] steps = [1, 2];
        var offsets = new OffsetTable(0);

        // Act
        using var context = new Z3Context();
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where Z3Methods.Distinct(steps.Select(s => t.X1 + offsets.Get(s)).ToArray())
                      select t).Solve();

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void Distinct_SelectorOverSingleItem_Solves()
    {
        // Arrange: a one-element source still exercises substitution and partial evaluation,
        // and a single term is trivially distinct.
        int[] single = [7];

        // Act
        using var context = new Z3Context();
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where Z3Methods.Distinct(single.Select(s => t.X1 * s).ToArray())
                      where t.X1 == 6
                      where t.X2 == 1
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(6);
        result.X2.ShouldBe(1);
    }

    /// <summary>
    /// An ordinary object with no Z3 representation, used to prove that a closed call in the
    /// selector is folded to a constant before the visitor sees it.
    /// </summary>
    private sealed class OffsetTable(int scale)
    {
        public int Get(int index) => index * scale;
    }
}
