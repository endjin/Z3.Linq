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
/// Three of the types listed as supported do not work, and are pinned as characterisation tests
/// rather than skipped - short (#63), float (#54) and DateTime (#56). All three fail in the
/// marshalling layer, which no example in the repository exercises, which is why they went
/// unnoticed.
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
    /// KNOWN DEFECT (#54): a float symbol solves but cannot be marshalled back.
    /// </summary>
    /// <remarks>
    /// TypeCode.Single parses the model value into a double (Theorem.cs:527) and then writes it
    /// to a float property, which reflection rejects. Note this fails later than short does -
    /// translation and solving both succeed, so only the result is lost.
    /// This test pins current behaviour and must be updated when the defect is fixed.
    /// </remarks>
    [TestMethod]
    public void Solve_FloatSymbol_ThrowsArgumentException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<float, int>>()
            .Where(t => t.X1 == 1.5f);

        // Act & Assert
        Should.Throw<ArgumentException>(() => theorem.Solve());
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
    /// The tests above pin what each type does with a value; these five pin what each does with
    /// no value at all. Every type takes a different arm of the marshalling switch, so each can
    /// regress on its own, and all but <c>bool</c> threw before #51. The value a free symbol
    /// comes back with is supplied by model completion and is deliberately not asserted.
    /// </para>
    /// <para>
    /// <c>short</c> and <c>float</c> are absent on purpose: an unconstrained one now evaluates
    /// cleanly and then fails at the reflection write, which is #63 and #54 respectively rather
    /// than anything to do with completion. Their pins above are unchanged.
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
