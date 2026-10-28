namespace Z3.Linq.Tests;

using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Threading;

/// <summary>
/// Pins that translating a C# constant into a Z3 term does not depend on the ambient culture.
/// </summary>
/// <remarks>
/// <para>
/// Z3's parser accepts only <c>.</c> as a decimal separator and only ASCII <c>-</c> as a sign,
/// so a number handed to it has to be rendered invariantly. Until #52 was fixed,
/// <c>ExpressionVisitor</c> rendered real constants with the current culture. Measured over the
/// 606 specific cultures installed on the development machine, 301 of them produced a literal
/// Z3 rejected with <c>Z3Exception: parser error</c> - about half, and every widely used
/// European language among them. None produced a wrong value, which is the one merciful thing
/// about it: the defect always announced itself.
/// </para>
/// <para>
/// Every test here runs its solve on a dedicated thread. <c>CurrentCulture</c> is per-thread,
/// these tests run in parallel at method level, and the runner hands out pooled threads - so
/// setting it on the test's own thread could leak into whatever runs next on that thread. A
/// thread created for the purpose cannot leak anywhere.
/// </para>
/// <para>
/// Each culture test starts by asserting that the culture really does render its probe value
/// differently from the invariant culture. Under globalization-invariant mode every culture
/// falls back to invariant data, and without that check these tests would pass while
/// exercising nothing.
/// </para>
/// <para>
/// The solve, the model and the read back all happen on the foreign-culture thread, so these
/// cover the whole round trip rather than the write side alone.
/// </para>
/// </remarks>
[TestClass]
public class CultureInvarianceTests
{
    /// <summary>
    /// How long to wait for a solve running on a dedicated thread. Generous by design - it
    /// exists to turn a hang into a failure, not to police how long a solve should take.
    /// </summary>
    private static readonly TimeSpan SolveTimeout = TimeSpan.FromSeconds(30);

    [TestMethod]
    [DataRow("de-DE", DisplayName = "German - comma separator")]
    [DataRow("fr-FR", DisplayName = "French - comma separator")]
    [DataRow("ru-RU", DisplayName = "Russian - comma separator")]
    [DataRow("tr-TR", DisplayName = "Turkish - comma separator")]
    [DataRow("fa-IR", DisplayName = "Persian - U+066B decimal separator")]
    public void Solve_DoubleConstantUnderANonInvariantCulture_RoundTripsTheValue(string culture)
    {
        // Arrange: the case in #52's title. fa-IR is here because its separator is not a comma
        // either - the defect was never specific to one character.
        CultureInfo cultureInfo = RequireANonInvariantRendering(culture, 1.5);

        // Act
        double? value = SolveOn(cultureInfo, () =>
        {
            using var context = new Z3Context();
            return context.NewTheorem<Symbols<double, int>>()
                .Where(t => t.X1 == 1.5)
                .Solve()?.X1;
        });

        // Assert
        value.ShouldBe(1.5);
    }

    /// <summary>
    /// A negative real constant survives a culture whose negative sign is not ASCII.
    /// </summary>
    /// <remarks>
    /// These cultures render -1.5 with U+2212 MINUS SIGN rather than ASCII <c>-</c>. A fix that
    /// only swapped the decimal separator would still hand Z3 a sign it cannot parse, so this is
    /// the case that rules out the obvious wrong fix.
    /// </remarks>
    [TestMethod]
    [DataRow("sv-SE", DisplayName = "Swedish")]
    [DataRow("fi-FI", DisplayName = "Finnish")]
    public void Solve_NegativeDoubleConstantUnderACultureWithANonAsciiSign_RoundTripsTheValue(
        string culture)
    {
        // Arrange
        CultureInfo cultureInfo = RequireANonInvariantRendering(culture, -1.5);
        cultureInfo.NumberFormat.NegativeSign.ShouldNotBe("-");

        // Act
        double? value = SolveOn(cultureInfo, () =>
        {
            using var context = new Z3Context();
            return context.NewTheorem<Symbols<double, int>>()
                .Where(t => t.X1 == -1.5)
                .Solve()?.X1;
        });

        // Assert
        value.ShouldBe(-1.5);
    }

    [TestMethod]
    [DataRow("de-DE", DisplayName = "German")]
    [DataRow("sv-SE", DisplayName = "Swedish")]
    public void Solve_DecimalConstantUnderANonInvariantCulture_RoundTripsTheValue(string culture)
    {
        // Arrange: decimal shares the real sort with double and the same arm of the constant
        // switch, but it is a separate CLR formatter, so it gets its own case.
        CultureInfo cultureInfo = RequireANonInvariantRendering(culture, 1.5m);

        // Act
        decimal? value = SolveOn(cultureInfo, () =>
        {
            using var context = new Z3Context();
            return context.NewTheorem<Symbols<decimal, int>>()
                .Where(t => t.X1 == 1.5m)
                .Solve()?.X1;
        });

        // Assert
        value.ShouldBe(1.5m);
    }

    /// <summary>
    /// A <c>float</c> constant survives a foreign culture like the other two real types.
    /// </summary>
    /// <remarks>
    /// <c>float</c> takes the same arm of the constant switch as <c>double</c> and
    /// <c>decimal</c>, so without this the #52 fix would have a third of its call site untested.
    /// It could only assert an <see cref="ArgumentException"/> when it was written, because a
    /// float symbol could not round-trip at all: #54 parsed the model value into a double and
    /// reflection refused to write that to a float property. Now that #54 is fixed it says what
    /// it always wanted to.
    /// </remarks>
    [TestMethod]
    public void Solve_FloatConstantUnderANonInvariantCulture_RoundTripsTheValue()
    {
        // Arrange
        const string Culture = "de-DE";
        CultureInfo cultureInfo = RequireANonInvariantRendering(Culture, 1.5f);

        // Act
        float? value = SolveOn(cultureInfo, () =>
        {
            using var context = new Z3Context();
            return context.NewTheorem<Symbols<float, int>>()
                .Where(t => t.X1 == 1.5f)
                .Solve()?.X1;
        });

        // Assert
        value.ShouldBe(1.5f);
    }

    /// <summary>
    /// A <c>string</c> constant is unaffected by the culture.
    /// </summary>
    /// <remarks>
    /// The line that fixes #52 sits two above a <c>MkString(val.ToString())</c> that looks
    /// identical. That one is safe - <c>String.ToString()</c> returns the instance - and this
    /// test is here so the asymmetry is a recorded fact rather than an oversight waiting to be
    /// tidied up. tr-TR because its casing rules are the usual way a string path turns out to
    /// be culture-sensitive after all.
    /// </remarks>
    [TestMethod]
    public void Solve_StringConstantUnderANonInvariantCulture_RoundTripsTheValue()
    {
        // Arrange
        const string Culture = "tr-TR";
        CultureInfo cultureInfo = CultureInfo.GetCultureInfo(Culture);
        cultureInfo.TextInfo.ToLower("I").ShouldNotBe("i");

        // Act
        string? value = SolveOn(cultureInfo, () =>
        {
            using var context = new Z3Context();
            return context.NewTheorem<Symbols<string, int>>()
                .Where(t => t.X1 == "III")
                .Solve()?.X1;
        });

        // Assert
        value.ShouldBe("III");
    }

    [TestMethod]
    public void Solve_DoubleConstantUnderTheInvariantCulture_RoundTripsTheValue()
    {
        // Arrange: the control. It uses the same dedicated-thread mechanism as the tests above,
        // so they differ from it by culture and nothing else. This one passed on both sides of
        // the fix; if it ever fails, the problem is the harness rather than the culture.

        // Act
        double? value = SolveOn(CultureInfo.InvariantCulture, () =>
        {
            using var context = new Z3Context();
            return context.NewTheorem<Symbols<double, int>>()
                .Where(t => t.X1 == 1.5)
                .Solve()?.X1;
        });

        // Assert
        value.ShouldBe(1.5);
    }

    /// <summary>
    /// Asserts that <paramref name="culture"/> renders <paramref name="probe"/> differently from
    /// the invariant culture, so a test using it can detect the defect it exists for.
    /// </summary>
    /// <returns>The resolved culture, so the caller resolves it once.</returns>
    private static CultureInfo RequireANonInvariantRendering(string culture, IFormattable probe)
    {
        CultureInfo cultureInfo = CultureInfo.GetCultureInfo(culture);

        cultureInfo.NumberFormat.NumberDecimalSeparator.ShouldNotBe(
            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator,
            $"{culture} formats numbers exactly as the invariant culture does on this machine, "
            + "so this test cannot detect the defect it exists for - most likely the runtime is "
            + "in globalization-invariant mode, where every culture falls back to invariant "
            + "data");

        probe.ToString(null, cultureInfo)
            .ShouldNotBe(probe.ToString(null, CultureInfo.InvariantCulture));

        return cultureInfo;
    }

    /// <summary>
    /// Runs <paramref name="body"/> on a thread pinned to <paramref name="culture"/>, and returns
    /// what it returned or rethrows what it threw.
    /// </summary>
    /// <remarks>
    /// The exception is captured and rethrown rather than left to escape: one escaping a bare
    /// thread is unhandled, so it takes down the test host and the run reports "zero tests ran"
    /// rather than one failure.
    /// </remarks>
    private static T SolveOn<T>(CultureInfo culture, Func<T> body)
    {
        T result = default!;
        ExceptionDispatchInfo? failure = null;

        var thread = new Thread(() =>
        {
            CultureInfo.CurrentCulture = culture;

            try
            {
                result = body();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        })
        {
            // Foreground is the default, and a foreground thread that outlives the wait below
            // holds the whole test host open - measured: a process whose Main has returned does
            // not exit at all while one is still running. That turns the timeout from a clean
            // failure into a stalled run, which is the opposite of what it is here for.
            IsBackground = true,
        };

        thread.Start();

        // Bounded, so that a solver that never returns fails the test rather than hanging the
        // whole run with no indication of which test stopped. The work itself takes single-digit
        // milliseconds, so the limit is enormous slack rather than a tuned value.
        string name = culture.Name.Length == 0 ? "invariant" : culture.Name;
        thread.Join(SolveTimeout).ShouldBeTrue($"the solve on the {name} thread did not finish");

        failure?.Throw();
        return result;
    }
}
