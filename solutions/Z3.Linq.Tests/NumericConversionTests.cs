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

    /// <summary>
    /// A narrowing cast from <c>long</c> to <c>int</c> wraps, as an unchecked C# cast does.
    /// </summary>
    /// <remarks>
    /// Both types are integer-sorted in Z3, so a translation that compares sorts alone treats the
    /// cast as a no-op, and <c>(int)4294967295L</c> comes out as 4294967295 where C# gives -1. The
    /// expected value is computed with C# from the returned symbol.
    /// </remarks>
    [TestMethod]
    [DataRow(5L, DisplayName = "In range")]
    [DataRow(4294967295L, DisplayName = "UInt32.MaxValue")]
    [DataRow(2147483648L, DisplayName = "Int32.MaxValue + 1")]
    [DataRow(-2147483649L, DisplayName = "Int32.MinValue - 1")]
    [DataRow(long.MinValue, DisplayName = "Int64.MinValue")]
    public void Solve_LongSymbolCastToInt_WrapsAsCSharpDoes(long value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<long, int>>()
            .Where(t => t.X1 == value)
            .Where(t => t.X2 == (int)t.X1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe(unchecked((int)result.X1));
    }

    /// <summary>
    /// A narrowing cast to a signed or unsigned small integer wraps too.
    /// </summary>
    /// <remarks>
    /// <c>short</c> and <c>sbyte</c> wrap into a range centred on zero, <c>byte</c>,
    /// <c>ushort</c> and <c>char</c> into one starting at zero, so both shapes of the wrap are
    /// exercised, including negative operands into an unsigned type.
    /// </remarks>
    [TestMethod]
    [DataRow(40000, DisplayName = "40000")]
    [DataRow(-40000, DisplayName = "-40000")]
    [DataRow(300, DisplayName = "300")]
    [DataRow(-1, DisplayName = "-1")]
    [DataRow(int.MaxValue, DisplayName = "Int32.MaxValue")]
    public void Solve_IntSymbolCastToSmallIntegers_WrapsAsCSharpDoes(int value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, short, byte, sbyte, ushort>>()
            .Where(t => t.X1 == value)
            .Where(t => t.X2 == (short)t.X1)
            .Where(t => t.X3 == (byte)t.X1)
            .Where(t => t.X4 == (sbyte)t.X1)
            .Where(t => t.X5 == (ushort)t.X1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe(unchecked((short)result.X1));
        result.X3.ShouldBe(unchecked((byte)result.X1));
        result.X4.ShouldBe(unchecked((sbyte)result.X1));
        result.X5.ShouldBe(unchecked((ushort)result.X1));
    }

    /// <summary>
    /// A cast to <c>char</c> wraps into the <c>char</c> range.
    /// </summary>
    [TestMethod]
    [DataRow(65, DisplayName = "In range")]
    [DataRow(65601, DisplayName = "65536 + 65")]
    [DataRow(-1, DisplayName = "-1")]
    public void Solve_IntSymbolCastToChar_WrapsAsCSharpDoes(int value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == value)
            .Where(t => t.X2 == (char)t.X1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe((int)unchecked((char)result.X1));
    }

    /// <summary>
    /// A widening cast adds no wrap: the solver can still reach the whole range of the source.
    /// </summary>
    [TestMethod]
    public void Solve_IntSymbolWidenedToLong_KeepsTheValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, long>>()
            .Where(t => t.X1 == int.MinValue)
            .Where(t => t.X2 == (long)t.X1 - 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe((long)int.MinValue - 1);
    }

    /// <summary>
    /// A cast from <c>double</c> to <c>int</c> truncates toward zero, as C# does.
    /// </summary>
    /// <remarks>
    /// Z3's real-to-integer conversion is a floor, which agrees with C# only for a non-negative
    /// operand or a whole number: the floor of -2.5 is -3, but <c>(int)-2.5</c> is -2. The expected
    /// value is computed with C# from the returned symbol.
    /// </remarks>
    [TestMethod]
    [DataRow(2.5, DisplayName = "2.5")]
    [DataRow(-2.5, DisplayName = "-2.5")]
    [DataRow(-2.0, DisplayName = "-2.0")]
    [DataRow(-0.5, DisplayName = "-0.5")]
    [DataRow(0.0, DisplayName = "0.0")]
    public void Solve_DoubleSymbolCastToInt_TruncatesTowardZero(double value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<double, int>>()
            .Where(t => t.X1 == value)
            .Where(t => t.X2 == (int)t.X1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe((int)result.X1);
    }

    /// <summary>
    /// A cast from <c>decimal</c> to <c>long</c> truncates toward zero too.
    /// </summary>
    /// <remarks>
    /// Decimal's explicit conversion is an operator method on <c>decimal</c>, which takes the same
    /// real-to-integer path, so it needs the same treatment.
    /// </remarks>
    [TestMethod]
    [DataRow(7.75, DisplayName = "7.75")]
    [DataRow(-7.75, DisplayName = "-7.75")]
    public void Solve_DecimalSymbolCastToLong_TruncatesTowardZero(double value)
    {
        // Arrange: DataRow cannot carry a decimal, so the value travels as a double.
        using var context = new Z3Context();
        decimal amount = (decimal)value;

        // Act
        var result = context.NewTheorem<Symbols<decimal, long>>()
            .Where(t => t.X1 == amount)
            .Where(t => t.X2 == (long)t.X1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe((long)result.X1);
    }

    /// <summary>
    /// A user-defined conversion operator is refused rather than treated as a no-op.
    /// </summary>
    /// <remarks>
    /// <c>Doubled</c> is mapped to <c>float</c>, so its symbol is real-sorted, and its explicit
    /// conversion to <c>float</c> is real-sorted too. A translation that compares the sorts alone
    /// drops the operator, and the model for <c>(float)t.X1 == 6f</c> would hold 6 where the
    /// operator gives 12. The operator's body is C# the visitor cannot see into, so it says so.
    /// </remarks>
    [TestMethod]
    public void Solve_UserDefinedConversionOperator_IsRefused()
    {
        // Arrange
        using var context = new Z3Context();

        var theorem = context.NewTheorem<DoubledEnvironment>()
            .Where(t => (float)t.X1 == 6f);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => theorem.Solve()).Message.ShouldContain("op_Explicit");
    }

    [TheoremVariableTypeMapping(typeof(float))]
    private readonly struct Doubled
    {
        public Doubled(float value) => this.Value = value;

        public float Value { get; }

        public static explicit operator float(Doubled doubled) => doubled.Value * 2;
    }

    private sealed class DoubledEnvironment
    {
        public Doubled X1 { get; set; }
    }
}
