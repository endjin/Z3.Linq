namespace Z3.Linq.Tests;

/// <summary>
/// Numeric conversions inside a constraint: the ones the C# compiler inserts when two operands
/// have different types, and the ones a caller writes as a cast.
/// </summary>
/// <remarks>
/// <para>
/// Every mixed-type comparison or arithmetic expression carries a <c>Convert</c> node the caller
/// never wrote - <c>t.X1 == f</c> against a <c>double</c> symbol and a <c>float</c> variable is
/// <c>t.X1 == (double)f</c> to the expression tree. In Z3 such a conversion is one of exactly
/// three things: a no-op when both types map to the same sort, integer-to-real, or
/// real-to-integer. Which one depends on the sorts, and the visitor asks the same mapping the
/// symbols are declared with.
/// </para>
/// <para>
/// Until #76 the visitor chose from the target type alone - int-to-real for every conversion to
/// <c>double</c>, real-to-int for every conversion to <c>int</c> - and had no case at all for
/// <c>long</c>, <c>float</c> or <c>decimal</c>. So a <c>float</c> variable could not be compared
/// against a <c>double</c> symbol, two symbols of different real types could not be related, and
/// a <c>long</c> symbol could not be compared to an <c>int</c> one. #63 fixed the <c>int</c>
/// arm the same way earlier in this stack; this finishes the job across the switch.
/// </para>
/// <para>
/// The real-to-integer direction is covered by
/// <c>SymbolTypeMarshallingTests.Solve_DoubleSymbolCastToInt_ConvertsRatherThanPassingThrough</c>,
/// which needs a constraint the truncation can be observed through and lives beside the
/// marshalling tests it shares that trick with.
/// </para>
/// </remarks>
[TestClass]
public class NumericConversionTests
{
    /// <summary>
    /// The case in #76: a <c>double</c> symbol compared against a <c>float</c> variable.
    /// </summary>
    /// <remarks>
    /// A <c>float</c> literal in the same position is folded to a <c>double</c> constant before
    /// the visitor sees it, which is why this needs a variable to reproduce - and why it went
    /// unnoticed. The operand translates to a real, and the old <c>double</c> arm cast it to
    /// <c>IntExpr</c> on the assumption that anything converted to a double must have been an
    /// integer.
    /// </remarks>
    [TestMethod]
    public void Solve_DoubleSymbolComparedToAFloatVariable_RoundTripsTheValue()
    {
        // Arrange
        using var context = new Z3Context();
        float value = 1.5f;

        // Act
        var result = context.NewTheorem<Symbols<double, int>>()
            .Where(t => t.X1 == value)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(1.5);
    }

    /// <summary>
    /// Two symbols of different real types can be related to each other.
    /// </summary>
    /// <remarks>
    /// Stronger than the issue's repro, and the one that matters in practice: nothing about
    /// <c>Symbols&lt;double, float&gt;</c> suggests its two members cannot appear in the same
    /// constraint, yet every comparison between them widened the <c>float</c> and failed.
    /// </remarks>
    [TestMethod]
    public void Solve_DoubleSymbolComparedToAFloatSymbol_RelatesTheTwo()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<double, float>>()
            .Where(t => t.X1 == t.X2)
            .Where(t => t.X2 == 1.5f)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(1.5);
        result.X2.ShouldBe(1.5f);
    }

    [TestMethod]
    public void Solve_DoubleSymbolInArithmeticWithAFloatSymbol_RelatesTheTwo()
    {
        // Arrange: the conversion sits under a multiplication rather than directly under the
        // comparison, so the visitor meets it with an arithmetic parent.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<double, float>>()
            .Where(t => t.X1 == t.X2 * 2)
            .Where(t => t.X2 == 1.5f)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(3.0);
    }

    /// <summary>
    /// The widening the old <c>double</c> arm was written for still works.
    /// </summary>
    /// <remarks>
    /// An <c>int</c> operand converted to <c>double</c> is the one integer-to-real case the
    /// visitor always handled. It is here so the fix cannot be a regression in disguise: a
    /// version that returned every operand unchanged would pass the float tests above and fail
    /// this one with a sort mismatch.
    /// </remarks>
    [TestMethod]
    public void Solve_DoubleSymbolComparedToAnIntSymbol_RelatesTheTwo()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<double, int>>()
            .Where(t => t.X1 == t.X2)
            .Where(t => t.X2 == 7)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(7.0);
        result.X2.ShouldBe(7);
    }

    /// <summary>
    /// A <c>decimal</c> symbol can be compared to an <c>int</c> one.
    /// </summary>
    /// <remarks>
    /// There was no arm for a conversion to <c>decimal</c> at all, so this fell through to the
    /// catch-all as <see cref="NotImplementedException"/>. It is the same integer-to-real
    /// conversion as the <c>double</c> case, because <c>decimal</c> and <c>double</c> map to the
    /// same sort - which is exactly what choosing by sort rather than by type buys.
    /// </remarks>
    [TestMethod]
    public void Solve_DecimalSymbolComparedToAnIntSymbol_RelatesTheTwo()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<decimal, int>>()
            .Where(t => t.X1 == t.X2)
            .Where(t => t.X2 == 7)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(7m);
    }

    [TestMethod]
    public void Solve_FloatSymbolComparedToAnIntSymbol_RelatesTheTwo()
    {
        // Arrange: the third real type, and the third that had no arm.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<float, int>>()
            .Where(t => t.X1 == t.X2)
            .Where(t => t.X2 == 3)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(3f);
    }

    /// <summary>
    /// A <c>long</c> symbol can be compared to an <c>int</c> one.
    /// </summary>
    /// <remarks>
    /// Pinned in <c>UnsupportedExpressionTests</c> as unsupported until #76, with the note that
    /// widening an <c>int</c> to <c>long</c> is unremarkable C#. Both types map to the integer
    /// sort, so the conversion is a no-op in Z3 and the operand passes through unchanged.
    /// </remarks>
    [TestMethod]
    public void Solve_LongSymbolComparedToAnIntSymbol_RelatesTheTwo()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<long, int>>()
            .Where(t => t.X1 == t.X2)
            .Where(t => t.X2 == 5)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(5L);
    }

    [TestMethod]
    public void Solve_DoubleSymbolComparedToAWidenedDecimalVariable_RoundTripsTheValue()
    {
        // Arrange: an explicit cast the caller wrote, between two real types. The operand is
        // already real-sorted, so nothing is converted.
        using var context = new Z3Context();
        decimal value = 1.25m;

        // Act
        var result = context.NewTheorem<Symbols<double, int>>()
            .Where(t => t.X1 == (double)value)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(1.25);
    }

    /// <summary>
    /// A narrowing cast between real types is a no-op too.
    /// </summary>
    /// <remarks>
    /// <c>(float)</c> of a <c>double</c> narrows in C# and does nothing in Z3, where both are
    /// the same unbounded real. Worth pinning because a reader might expect a rounding step;
    /// there is none, and the value the constraint names is what comes back.
    /// </remarks>
    [TestMethod]
    public void Solve_FloatSymbolComparedToANarrowedDoubleVariable_RoundTripsTheValue()
    {
        // Arrange
        using var context = new Z3Context();
        double value = 2.5;

        // Act
        var result = context.NewTheorem<Symbols<float, int>>()
            .Where(t => t.X1 == (float)value)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(2.5f);
    }
}
