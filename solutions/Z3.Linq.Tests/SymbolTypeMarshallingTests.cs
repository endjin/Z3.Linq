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
/// Every type listed as supported now works. Three did not when this file was written: short
/// (#63), float (#54) and DateTime (#56). All three failed in the marshalling layer, which no
/// example in the repository exercises, which is why they went unnoticed. DateTime is the one
/// worth remembering - it returned a value rather than throwing, so only a test asserting the
/// value could find it at all.
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
    /// A <c>short</c> symbol round-trips the value it was constrained to.
    /// </summary>
    /// <remarks>
    /// The case in #63, which had to be fixed twice over. C# widens <c>short</c> to <c>int</c>
    /// for the comparison, and the visitor read that <c>Convert</c> node as a real-to-int
    /// conversion and cast the <c>IntExpr</c> to <c>RealExpr</c>; behind that,
    /// <c>TypeCode.Int16</c> shared the <c>Int32</c> arm of the marshalling switch and handed
    /// reflection an <c>int</c>, which a <c>short</c> member rejects. Neither defect was
    /// reachable while the other stood.
    /// </remarks>
    [TestMethod]
    [DataRow((short)0, DisplayName = "Zero")]
    [DataRow((short)42, DisplayName = "Positive")]
    [DataRow((short)-42, DisplayName = "Negative")]
    [DataRow(short.MaxValue, DisplayName = "Int16.MaxValue")]
    [DataRow(short.MinValue, DisplayName = "Int16.MinValue")]
    public void Solve_ShortSymbol_RoundTripsTheValue(short value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<short, short>>()
            .Where(t => t.X1 == value)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(value);
    }

    /// <summary>
    /// A <c>short</c> symbol whose model value no <c>short</c> can hold fails loudly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The symbol is an unbounded <c>MkIntConst</c> - nothing tells Z3 the value has to fit in
    /// 16 bits - so a constraint written against the widened <c>int</c> can be satisfied by a
    /// number the member cannot hold. The read is a checked cast, which throws; an unchecked one
    /// would wrap 40000 to -25536 and hand back a wrong answer that looks like a right one.
    /// </para>
    /// <para>
    /// C# blocks the direct spelling of this - <c>t.X1 == 40000</c> against a <c>short</c> is
    /// <c>error CS0652</c> - so it takes an <c>int</c> variable to reach. Pinned because the
    /// choice between wrapping and throwing is the whole point of the arm, and a later
    /// simplification to a plain cast would pass every other test in this file. See #63, and
    /// #87 for bounding the symbol so Z3 cannot pick the value in the first place.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_ShortSymbolConstrainedOutsideShortRange_ThrowsOverflowException()
    {
        // Arrange
        using var context = new Z3Context();
        int beyondShortRange = 40000;
        var theorem = context.NewTheorem<Symbols<short, int>>()
            .Where(t => t.X1 == beyondShortRange);

        // Act & Assert
        Should.Throw<OverflowException>(() => theorem.Solve());
    }

    /// <summary>
    /// <c>short</c> symbols work in arithmetic, not only in a bare equality.
    /// </summary>
    /// <remarks>
    /// The fix guards a <c>Convert</c> node, and C# emits one wherever a <c>short</c> is used in
    /// arithmetic - so a fix that covered only the comparison form would leave this failing.
    /// </remarks>
    [TestMethod]
    public void Solve_ShortSymbolsInArithmetic_RoundTripTheValues()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<short, short>>()
            .Where(t => t.X1 + t.X2 == 10)
            .Where(t => t.X1 == 4)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe((short)4);
        result.X2.ShouldBe((short)6);
    }

    /// <summary>
    /// A <c>short</c> symbol can be compared against an <c>int</c> one.
    /// </summary>
    /// <remarks>
    /// Both sides widen to <c>int</c>, so this is the case where the guard has to leave one
    /// operand alone and still produce a well-sorted comparison.
    /// </remarks>
    [TestMethod]
    public void Solve_ShortSymbolComparedToAnIntSymbol_RoundTripsBoth()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<short, int>>()
            .Where(t => t.X1 == t.X2)
            .Where(t => t.X2 == 9)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe((short)9);
        result.X2.ShouldBe(9);
    }

    /// <summary>
    /// An <c>enum</c> symbol round-trips the member it was constrained to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An enum's <c>TypeCode</c> is that of its underlying type, so <see cref="DayOfWeek"/> is
    /// <c>Int32</c> and takes the same path a <c>short</c> does - which is why the same guard
    /// fixes both, as #63 records.
    /// </para>
    /// <para>
    /// Unlike <c>short</c>, an enum needs nothing on the marshalling side: the model value is an
    /// <c>int</c>, and reflection converts an <c>int</c> to an enum member on its own. #63
    /// predicted a second defect here and there is not one - measured, not assumed.
    /// </para>
    /// </remarks>
    [TestMethod]
    [DataRow(DayOfWeek.Sunday, DisplayName = "Underlying value zero")]
    [DataRow(DayOfWeek.Monday, DisplayName = "Positive")]
    [DataRow(DayOfWeek.Saturday, DisplayName = "Largest member")]
    public void Solve_EnumSymbol_RoundTripsTheValue(DayOfWeek value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<EnumEnvironment>()
            .Where(t => t.Day == value)
            .Where(t => t.Other == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Day.ShouldBe(value);
        result.Other.ShouldBe(1);
    }

    /// <summary>
    /// An explicit <c>(int)</c> cast of a real-sorted symbol still converts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other side of the guard #63 added. That guard makes the <c>Int32</c> arm of the
    /// conversion switch conditional on the operand's Z3 sort: an integer passes through, a real
    /// goes to <c>MkReal2Int</c>. Only the first half is exercised by the <c>short</c> and enum
    /// tests above, so without this one a guard widened to return the operand unconditionally
    /// would pass the whole suite and silently stop converting reals.
    /// </para>
    /// <para>
    /// Worth knowing that this path had no test before #63 either. It was reachable only from a
    /// widening the visitor misread, so every execution of it threw - it was covered without
    /// ever having worked.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_DoubleSymbolCastToInt_ConvertsRatherThanPassingThrough()
    {
        // Arrange
        using var context = new Z3Context();

        // Act: the second constraint is what makes the truncation observable. Z3.s real-to-int
        // is a floor, so together these ask for a real in (3.5, 4). Pass the operand through
        // unconverted and they read as X1 == 3 and X1 > 3.5, which has no solution at all - so
        // this fails by returning null rather than by returning a wrong number.
        var result = context.NewTheorem<Symbols<double, int>>()
            .Where(t => (int)t.X1 == 3)
            .Where(t => t.X1 > 3.5)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBeGreaterThan(3.5);
        result.X1.ShouldBeLessThan(4d);
        result.X2.ShouldBe(1);
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
    /// A <see cref="DateTime"/> symbol round-trips: what comes back equals what went in, as UTC.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A DateTime is carried through Z3 as a Windows file time - a position on the UTC timeline -
    /// so the kind cannot survive the trip and the read path has to choose one. It chooses UTC, to
    /// match the <c>ToFileTimeUtc</c> the write path already used. Before #56 it chose local time,
    /// and the same theorem answered differently on every machine that ran it.
    /// </para>
    /// <para>
    /// The kind assertion is the load-bearing one. Comparing ticks alone would have passed on UTC
    /// CI both before and after the fix, because the shift is the machine's UTC offset and that
    /// offset is zero there - which is how the defect survived a green build for so long. The kind
    /// was wrong in every zone, UTC included.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_DateTimeSymbol_RoundTripsTheValueAsUtc()
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
        result.X1.Kind.ShouldBe(DateTimeKind.Utc);
        result.X1.ShouldBe(utc);
    }

    /// <summary>
    /// A DateTime constant with no kind is read as UTC, not as local time.
    /// </summary>
    /// <remarks>
    /// <c>new DateTime(2026, 6, 1, 12, 0, 0)</c> - the ordinary way to write a date into a
    /// constraint - carries <see cref="DateTimeKind.Unspecified"/>, and the two file-time
    /// conversions disagree about what that means: <c>ToFileTime</c> reads it as local time,
    /// <c>ToFileTimeUtc</c> as UTC. The write path uses the second, which is why #56 was fixed on
    /// the read path: switching the write to <c>ToFileTime</c> instead would have made this shape
    /// agree while leaving an explicitly-UTC constant, the issue's own repro, still shifted.
    /// </remarks>
    [TestMethod]
    public void Solve_DateTimeSymbolWithNoKind_RoundTripsTheValueAsUtc()
    {
        // Arrange
        using var context = new Z3Context();
        var unspecified = new DateTime(2026, 6, 1, 12, 0, 0);

        // Act
        var result = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 == unspecified)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.Kind.ShouldBe(DateTimeKind.Utc);
        result.X1.Ticks.ShouldBe(unspecified.Ticks);
    }

    /// <summary>
    /// A local DateTime comes back as the same instant, expressed in UTC.
    /// </summary>
    /// <remarks>
    /// The one shape #56 changed rather than repaired. A local constant used to come back as local
    /// time with identical ticks - the single case the old read path got right - and now comes back
    /// as UTC, so a caller comparing the result against what it supplied gets false where it used
    /// to get true. The instant is the same either way and one <see cref="DateTime.ToLocalTime"/>
    /// recovers the old answer. The trade is deliberate: only one kind can be exact, and choosing
    /// UTC is what makes the result independent of the machine that computed it.
    /// </remarks>
    [TestMethod]
    public void Solve_DateTimeSymbolWithLocalKind_ReturnsTheSameInstantAsUtc()
    {
        // Arrange
        using var context = new Z3Context();
        var local = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Local);

        // Act
        var result = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 == local)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.Kind.ShouldBe(DateTimeKind.Utc);
        result.X1.ShouldBe(local.ToUniversalTime());
        result.X1.ToLocalTime().ShouldBe(local);
    }

    /// <summary>
    /// <see cref="DateTime.MaxValue"/> survives the round trip.
    /// </summary>
    /// <remarks>
    /// The top of the range, and a case the old read path got right only by luck: converting the
    /// maximum to local time overflows, and <c>ToLocalTime</c> clamps instead of throwing, so east
    /// of Greenwich the ticks happened to match. West of it they did not. Reading as UTC removes
    /// the conversion, so there is nothing left to clamp. The bottom of the range is #83.
    /// </remarks>
    [TestMethod]
    public void Solve_DateTimeSymbolAtMaxValue_RoundTripsTheValue()
    {
        // Arrange
        using var context = new Z3Context();
        DateTime max = DateTime.MaxValue;

        // Act
        var result = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 == max)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.Kind.ShouldBe(DateTimeKind.Utc);
        result.X1.ShouldBe(max);
    }

    /// <summary>
    /// KNOWN DEFECT (#83): a DateTime constant before 1601 fails during translation.
    /// </summary>
    /// <remarks>
    /// A Windows file time counts from 1601-01-01 UTC and cannot express anything earlier, so the
    /// constant throws out of <c>ToFileTimeUtc</c> before Z3 sees the theorem. Unchanged by #56 -
    /// the range belongs to the encoding, not to the conversion that inverts it. The null
    /// <c>ParamName</c> is what identifies this as the write path; the read path supplies one.
    /// </remarks>
    [TestMethod]
    public void Solve_DateTimeSymbolBeforeTheFileTimeEpoch_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var context = new Z3Context();
        DateTime min = DateTime.MinValue;
        var theorem = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 == min);

        // Act
        ArgumentOutOfRangeException exception =
            Should.Throw<ArgumentOutOfRangeException>(() => theorem.Solve());

        // Assert
        exception.ParamName.ShouldBeNull();
    }

    /// <summary>
    /// KNOWN DEFECT (#83): a satisfiable theorem whose only models lie before 1601 throws while
    /// its solution is being marshalled.
    /// </summary>
    /// <remarks>
    /// The other half of #83, and the worse one: nothing here is malformed. The constraints are
    /// well-formed, Z3 finds a model, and the read path then refuses to express it. Every model
    /// satisfying this theorem is a negative file time, so the throw does not depend on which one
    /// Z3 picks. <c>ParamName</c> distinguishes this from the write-side case above, so a fix to
    /// one half cannot pass as a fix to both.
    /// </remarks>
    [TestMethod]
    public void Solve_DateTimeSymbolConstrainedBeforeTheFileTimeEpoch_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var context = new Z3Context();
        var epoch = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var theorem = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 < epoch && t.X2 == 0);

        // Act
        ArgumentOutOfRangeException exception =
            Should.Throw<ArgumentOutOfRangeException>(() => theorem.Solve());

        // Assert
        exception.ParamName.ShouldBe("fileTime");
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
    /// The tests above pin what each type does with a value; these eight pin what each does with
    /// no value at all. Every type takes a different arm of the marshalling switch, so each can
    /// regress on its own, and all but <c>bool</c> threw before #51. The value a free symbol
    /// comes back with is supplied by model completion and is deliberately not asserted.
    /// </para>
    /// <para>
    /// Two of the eight could not be written when this family was: <c>float</c> and <c>short</c>
    /// both evaluated cleanly under completion and then failed at the reflection write, which
    /// was #54 and #63 rather than anything to do with completion. <c>DateTime</c> never threw
    /// here, but read back in local time until #56, so its case asserts the kind rather than
    /// only that a result appeared.
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

    [TestMethod]
    public void Solve_UnconstrainedDateTimeSymbol_ReturnsAResult()
    {
        // Arrange: DateTime shares the integer sort with long but has its own read-back through
        // the file-time encoding, which can only express 1601 onwards - so completion has to
        // supply a value that encoding can carry, not merely an integer.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X2 == 0)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe(0);

        // The instant is completion's to choose, but the kind is not: it is fixed by the read
        // path, so it can be asserted where the value cannot.
        result.X1.Kind.ShouldBe(DateTimeKind.Utc);
    }

    /// <summary>
    /// An unconstrained <c>short</c> symbol is populated rather than throwing.
    /// </summary>
    /// <remarks>
    /// <c>short</c> could not be in this family until #63: it evaluated cleanly under completion
    /// and then failed at the reflection write, so its absence here said nothing about
    /// completion. It takes the <c>Int16</c> arm of the marshalling switch, which no other test
    /// in this family reaches.
    /// </remarks>
    [TestMethod]
    public void Solve_UnconstrainedShortSymbol_ReturnsAResult()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<short, int>>()
            .Where(t => t.X2 == 0)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe(0);
    }

    private sealed class EnumEnvironment
    {
        public DayOfWeek Day { get; set; }

        public int Other { get; set; }
    }
}
