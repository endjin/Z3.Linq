namespace Z3.Linq.Tests;

/// <summary>
/// Round-trips a value of each scalar symbol type through <see cref="Theorem{T}.Solve"/>:
/// C# constant -> Z3 term -> model -> CLR property.
/// </summary>
/// <remarks>
/// <para>
/// Every test here pins its symbols to exactly one value, so the assertions hold for any model
/// Z3 returns. That matters more in this file than elsewhere: the point is what came back, not
/// which solution was chosen.
/// </para>
/// <para>
/// Two of the types listed as supported do not work, and are pinned as characterisation tests
/// rather than skipped - short (#63) and DateTime (#56). Both fail in the marshalling layer,
/// which no example in the repository exercises, which is why they went unnoticed. float was a
/// third until #54 was fixed.
/// </para>
/// </remarks>
[TestClass]
public class SymbolTypeMarshallingTests
{
    [TestMethod]
    [DataRow(0, DisplayName = "Zero")]
    [DataRow(42, DisplayName = "Positive")]
    [DataRow(-42, DisplayName = "Negative")]
    [DataRow(int.MaxValue, DisplayName = "Int32.MaxValue")]
    [DataRow(int.MinValue, DisplayName = "Int32.MinValue")]
    public void Solve_IntSymbol_RoundTripsTheValue(int value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == value)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(value);
    }

    [TestMethod]
    [DataRow(0L, DisplayName = "Zero")]
    [DataRow(9_000_000_000L, DisplayName = "Beyond Int32 range")]
    [DataRow(-9_000_000_000L, DisplayName = "Negative beyond Int32 range")]
    [DataRow(long.MaxValue, DisplayName = "Int64.MaxValue")]
    public void Solve_LongSymbol_RoundTripsTheValue(long value)
    {
        // Arrange: the values beyond Int32 range are the point - they would survive a
        // round-trip that silently narrowed to int only by accident.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<long, int>>()
            .Where(t => t.X1 == value)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(value);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "True")]
    [DataRow(false, DisplayName = "False")]
    public void Solve_BoolSymbol_RoundTripsTheValue(bool value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<bool, int>>()
            .Where(t => t.X1 == value)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(value);
    }

    [TestMethod]
    [DataRow(1.5, DisplayName = "Exactly representable")]
    [DataRow(0.0, DisplayName = "Zero")]
    [DataRow(-2.25, DisplayName = "Negative")]
    [DataRow(1234.5678, DisplayName = "Several decimal places")]
    public void Solve_DoubleSymbol_RoundTripsTheValue(double value)
    {
        // Arrange: doubles are carried as Z3 reals and read back through ToDecimalString(64),
        // so these are values that survive that text round-trip exactly.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<double, int>>()
            .Where(t => t.X1 == value)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(value);
    }

    [TestMethod]
    public void Solve_DecimalSymbol_RoundTripsTheValue()
    {
        // Arrange: decimal goes through ToDecimalString(128) and is parsed with
        // InvariantCulture. DataRow cannot carry a decimal constant, hence the single case.
        using var context = new Z3Context();
        const decimal Value = 2.25m;

        // Act
        var result = context.NewTheorem<Symbols<decimal, int>>()
            .Where(t => t.X1 == Value)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(Value);
    }

    [TestMethod]
    [DataRow("abc", DisplayName = "Simple string")]
    [DataRow("", DisplayName = "Empty string")]
    [DataRow("with space", DisplayName = "String containing a space")]
    public void Solve_StringSymbol_RoundTripsTheValue(string value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<string, int>>()
            .Where(t => t.X1 == value)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(value);
    }

    [TestMethod]
    public void Solve_DoubleSymbolWithInequality_SatisfiesTheConstraint()
    {
        // Arrange: pinning by equality is the easy case for a real-valued symbol. This checks an
        // inequality, where Z3 picks the value and marshalling still has to survive whatever it
        // chose - which need not be a tidy decimal.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<double, int>>()
            .Where(t => t.X1 > 10.5)
            .Where(t => t.X1 < 11.0)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBeGreaterThan(10.5);
        result.X1.ShouldBeLessThan(11.0);
    }

    /// <summary>
    /// KNOWN DEFECT (#63): no theorem over a short symbol can be solved.
    /// </summary>
    /// <remarks>
    /// A short symbol is created with MkIntConst, but C# widens short to int for the comparison
    /// and ExpressionVisitor.VisitUnary reads that Convert node as a real-to-int conversion,
    /// casting the IntExpr to RealExpr (ExpressionVisitor.cs:132). A second defect waits behind
    /// it: TypeCode.Int16 marshals to an int, which a short property rejects.
    /// This test pins current behaviour and must be updated when the defect is fixed.
    /// </remarks>
    [TestMethod]
    public void Solve_ShortSymbol_ThrowsInvalidCastException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<short, short>>()
            .Where(t => t.X1 == 7);

        // Act & Assert
        Should.Throw<InvalidCastException>(() => theorem.Solve());
    }

    /// <summary>
    /// A <c>float</c> symbol round-trips the value it was constrained to.
    /// </summary>
    /// <remarks>
    /// The case in #54. <c>TypeCode.Single</c> parsed the model value with <c>double.Parse</c>,
    /// so a perfectly good answer was boxed as a <c>double</c> and reflection then refused to
    /// write it to a <c>float</c> member. Nothing about the solve was wrong - translation, the
    /// solver and the model were all fine, and only the last step lost the result.
    /// </remarks>
    [TestMethod]
    [DataRow(1.5f, DisplayName = "Exactly representable")]
    [DataRow(0f, DisplayName = "Zero")]
    [DataRow(-2.25f, DisplayName = "Negative")]
    [DataRow(0.1f, DisplayName = "Not exactly representable in binary")]
    [DataRow(1.2345678f, DisplayName = "Eight significant digits")]
    [DataRow(float.MaxValue, DisplayName = "Single.MaxValue")]
    public void Solve_FloatSymbol_RoundTripsTheValue(float value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<float, int>>()
            .Where(t => t.X1 == value)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(value);
    }

    /// <summary>
    /// A <c>float</c> and a <c>double</c> in one environment each come back as their own type.
    /// </summary>
    /// <remarks>
    /// The two share the real sort and sit on adjacent arms of the marshalling switch, differing
    /// only in how many decimal places they ask the model for. A fix that made <c>float</c> work
    /// by treating it as a <c>double</c> would pass every test above and fail here.
    /// </remarks>
    [TestMethod]
    public void Solve_FloatAndDoubleSymbolsTogether_MarshalEachToItsOwnType()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<float, double>>()
            .Where(t => t.X1 == 1.5f)
            .Where(t => t.X2 == 2.5)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(1.5f);
        result.X2.ShouldBe(2.5);
    }

    /// <summary>
    /// KNOWN DEFECT (#6): a value whose decimal expansion does not terminate cannot be read back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>RatNum.ToDecimalString</c> marks a truncated expansion by appending <c>?</c>, and
    /// neither the <c>Single</c> nor the <c>Double</c> arm strips it before parsing - only the
    /// <c>Decimal</c> arm does. A third solves perfectly well and then fails on the way out with
    /// <c>The input string '0.33333333333333333333333333333333?' was not in a correct format</c>.
    /// </para>
    /// <para>
    /// This is not #54 and was not introduced by fixing it: the <c>double</c> case below fails
    /// identically, at 64 decimal places rather than 32. Both are here so the defect is recorded
    /// as shared rather than looking like a float problem.
    /// These tests pin current behaviour and must be updated when the defect is fixed.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_FloatSymbolWithANonTerminatingValue_ThrowsFormatException()
    {
        // Arrange: a third has no finite decimal expansion, so the model value comes back
        // truncated and flagged.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<float, int>>()
            .Where(t => t.X1 * 3 == 1);

        // Act & Assert
        Should.Throw<FormatException>(() => theorem.Solve());
    }

    /// <summary>
    /// KNOWN DEFECT (#6). The <c>double</c> form, which behaves identically.
    /// See <see cref="Solve_FloatSymbolWithANonTerminatingValue_ThrowsFormatException"/>.
    /// </summary>
    [TestMethod]
    public void Solve_DoubleSymbolWithANonTerminatingValue_ThrowsFormatException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<double, int>>()
            .Where(t => t.X1 * 3 == 1);

        // Act & Assert
        Should.Throw<FormatException>(() => theorem.Solve());
    }

    /// <summary>
    /// KNOWN DEFECT (#6): the smallest positive <c>float</c> cannot be read back either, because
    /// 32 decimal places cannot express it at all.
    /// </summary>
    /// <remarks>
    /// The <c>Single</c> arm asks the model for 32 decimal places, which reads as a nod to
    /// float's 32 bits but is a count of decimal digits after the point. <c>Single.Epsilon</c> is
    /// about 1.4e-45, so every one of those 32 places is a zero and the value is reported as
    /// <c>0.00000000000000000000000000000000?</c> - truncated to nothing. The <c>Double</c> arm
    /// has the same shape at 64 places against a range reaching 5e-324, so this is a property of
    /// the pair rather than of float.
    /// This test pins current behaviour and must be updated when the defect is fixed.
    /// </remarks>
    [TestMethod]
    public void Solve_FloatSymbolAtItsSmallestPositiveValue_ThrowsFormatException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<float, int>>()
            .Where(t => t.X1 == float.Epsilon);

        // Act & Assert
        Should.Throw<FormatException>(() => theorem.Solve());
    }

    /// <summary>
    /// KNOWN DEFECT (#56): a DateTime does not compare equal to the value put in.
    /// </summary>
    /// <remarks>
    /// The instant itself survives - the value is written with ToFileTimeUtc and read with
    /// DateTime.FromFileTime, which is its correct inverse for the instant - but the result
    /// comes back as <see cref="DateTimeKind.Local"/>. Since <see cref="DateTime"/> equality
    /// compares ticks and ignores Kind, a caller comparing the result against the UTC value it
    /// supplied gets false anywhere with a non-zero UTC offset. Asserted in a form that holds in
    /// any time zone, including UTC where the offset is zero and the shift vanishes.
    /// </remarks>
    [TestMethod]
    public void Solve_DateTimeSymbol_ReturnsTheSameInstantAsLocalTime()
    {
        // Arrange
        using var context = new Z3Context();
        var utc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 == utc)
            .Solve();

        // Assert
        result.ShouldNotBeNull();

        // The instant is preserved...
        result.X1.Kind.ShouldBe(DateTimeKind.Local);
        result.X1.ToUniversalTime().ShouldBe(utc);

        // ...but the value is shifted by the local UTC offset, so a direct comparison against
        // what went in only succeeds where that offset happens to be zero.
        TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(utc);
        result.X1.Ticks.ShouldBe(utc.Ticks + offset.Ticks);
    }

    [TestMethod]
    public void Solve_MixedTypeSymbols_MarshalsEveryPropertyIndependently()
    {
        // Arrange: the two properties take different marshalling branches, so this catches a
        // change that fixed one type by breaking the dispatch for another.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, string>>()
            .Where(t => t.X1 == 5)
            .Where(t => t.X2 == "five")
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(5);
        result.X2.ShouldBe("five");
    }

    /// <summary>
    /// An unconstrained <c>long</c> symbol is populated rather than throwing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tests above pin what each type does with a value; these six pin what each does with
    /// no value at all. Every type takes a different arm of the marshalling switch, so each can
    /// regress on its own, and all but <c>bool</c> threw before #51. The value a free symbol
    /// comes back with is supplied by model completion and is deliberately not asserted.
    /// </para>
    /// <para>
    /// <c>short</c> is absent on purpose: an unconstrained one evaluates cleanly and then fails
    /// at the reflection write, which is #63 rather than anything to do with completion. Its pin
    /// above is unchanged. <c>float</c> failed the same way until #54 was fixed, and now has a
    /// case here like every other working type.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_UnconstrainedLongSymbol_ReturnsAResult()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<long, int>>()
            .Where(t => t.X2 == 0)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe(0);
    }

    [TestMethod]
    public void Solve_UnconstrainedDoubleSymbol_ReturnsAResult()
    {
        // Arrange: the real-sorted counterpart. An uninterpreted real came back as a RealExpr
        // rather than a RatNum, so this arm failed on a different cast to the integer ones.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<double, int>>()
            .Where(t => t.X2 == 0)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe(0);
    }

    [TestMethod]
    public void Solve_UnconstrainedFloatSymbol_ReturnsAResult()
    {
        // Arrange: the other real-sorted arm, which needs both #51 and #54 to get here - model
        // completion to produce a numeral at all, and a float.Parse to write it anywhere.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<float, int>>()
            .Where(t => t.X2 == 0)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe(0);
    }

    [TestMethod]
    public void Solve_UnconstrainedDecimalSymbol_ReturnsAResult()
    {
        // Arrange: decimal shares the real sort with double but has its own read-back, with
        // trailing-'?' trimming and a decimal.Parse, so it is worth its own case.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<decimal, int>>()
            .Where(t => t.X2 == 0)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe(0);
    }

    /// <summary>
    /// An unconstrained <c>string</c> symbol is populated rather than throwing.
    /// </summary>
    /// <remarks>
    /// The one member of this group that did not fail with an <see cref="InvalidCastException"/>.
    /// Strings are read with <c>Expr.String</c> rather than a cast, so an uninterpreted symbol
    /// produced <c>Z3Exception: expression is not a string literal</c> instead. Same cause, and
    /// fixed by the same change.
    /// </remarks>
    [TestMethod]
    public void Solve_UnconstrainedStringSymbol_ReturnsAResult()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<string, int>>()
            .Where(t => t.X2 == 0)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldNotBeNull();
        result.X2.ShouldBe(0);
    }

    /// <summary>
    /// An unconstrained <c>bool</c> symbol is populated - as it always was.
    /// </summary>
    /// <remarks>
    /// The quiet half of #51, and the reason it went unnoticed for so long. Booleans are read
    /// with <c>Expr.IsTrue</c>, which is simply <c>false</c> for any term that is not literally
    /// true - including an uninterpreted symbol. So a free bool never threw; it silently
    /// returned <c>false</c> whether or not the model said anything about it. This test passes
    /// on both sides of the fix and is here to hold that branch still, not to prove the fix.
    /// </remarks>
    [TestMethod]
    public void Solve_UnconstrainedBoolSymbol_ReturnsAResult()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<bool, int>>()
            .Where(t => t.X2 == 0)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe(0);
    }
}
