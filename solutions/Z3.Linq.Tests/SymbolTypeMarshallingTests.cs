namespace Z3.Linq.Tests;

using System.Globalization;
using System.Threading;

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
    /// <summary>
    /// How long to wait for a solve running on a dedicated thread. Generous by design - it
    /// exists to turn a hang into a failure, not to police how long a solve should take.
    /// </summary>
    private static readonly TimeSpan SolveTimeout = TimeSpan.FromSeconds(30);

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
            .Where(t => t.X2 == 0)
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
            .Where(t => t.X2 == 0)
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
            .Where(t => t.X2 == 0)
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
            .Where(t => t.X2 == 0)
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
            .Where(t => t.X2 == 0)
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
            .Where(t => t.X2 == 0)
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
            .Where(t => t.X2 == 0)
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
    /// casting the IntExpr to RealExpr (ExpressionVisitor.cs:131). A second defect waits behind
    /// it: TypeCode.Int16 marshals to an int, which a short property rejects.
    /// This test pins current behaviour and must be updated when the defect is fixed.
    /// </remarks>
    [TestMethod]
    public void Solve_ShortSymbol_ThrowsInvalidCastException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<short, short>>()
            .Where(t => t.X1 == 7)
            .Where(t => t.X2 == 1);

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
            .Where(t => t.X1 == 1.5f)
            .Where(t => t.X2 == 0);

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
            .Where(t => t.X2 == 0)
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
    /// KNOWN DEFECT (#52): a real-valued constant is written to Z3 using the current culture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ExpressionVisitor.cs:304 calls MkReal(val.ToString()) with no format provider, so under a
    /// comma-decimal culture the literal 1.5 is handed to Z3 as "1,5" and its parser rejects it.
    /// Every other numeric conversion in the visitor passes InvariantCulture, and so does every
    /// read back on the marshalling side, which is what makes this one look like an oversight
    /// rather than a decision.
    /// </para>
    /// <para>
    /// The work runs on a dedicated thread rather than by setting the culture on the test's own
    /// thread. CurrentCulture is per-thread, these tests run in parallel at method level, and
    /// the runner hands out pooled threads - so mutating it here could leak into whatever runs
    /// next on the same thread. A thread created for the purpose cannot leak anywhere.
    /// </para>
    /// <para>This test pins current behaviour and must be updated when the defect is fixed.</para>
    /// </remarks>
    [TestMethod]
    public void Solve_DoubleSymbolUnderCommaDecimalCulture_ThrowsZ3ParserError()
    {
        // Arrange
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            try
            {
                using var context = new Z3Context();
                _ = context.NewTheorem<Symbols<double, int>>()
                    .Where(t => t.X1 == 1.5)
                    .Where(t => t.X2 == 0)
                    .Solve();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        // Act
        thread.Start();

        // Bounded, so that a solver that never returns fails this test rather than hanging the
        // whole run with no indication of which test stopped. The work itself takes single-digit
        // milliseconds, so the limit is enormous slack rather than a tuned value.
        thread.Join(SolveTimeout).ShouldBeTrue("the solve on the de-DE thread did not finish");

        // Assert
        captured.ShouldBeOfType<Microsoft.Z3.Z3Exception>();
    }

    [TestMethod]
    public void Solve_DoubleSymbolUnderInvariantCulture_RoundTripsTheValue()
    {
        // Arrange: the counterpart to the pin above, on the same dedicated-thread mechanism, so
        // that the two differ only by culture. This is what the defect above should look like
        // once it is fixed.
        double? value = null;

        var thread = new Thread(() =>
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            using var context = new Z3Context();
            var result = context.NewTheorem<Symbols<double, int>>()
                .Where(t => t.X1 == 1.5)
                .Where(t => t.X2 == 0)
                .Solve();

            value = result?.X1;
        });

        // Act
        thread.Start();
        thread.Join(SolveTimeout).ShouldBeTrue("the solve on the invariant-culture thread did not finish");

        // Assert
        value.ShouldBe(1.5);
    }
}
