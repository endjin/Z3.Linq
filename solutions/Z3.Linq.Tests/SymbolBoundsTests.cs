namespace Z3.Linq.Tests;

/// <summary>
/// The range of a symbol is the range of its type: a <c>short</c>, <c>int</c>, <c>long</c> or
/// <see cref="DateTime"/> symbol is bounded to what the CLR type can hold.
/// </summary>
/// <remarks>
/// <para>
/// Each of those travels through Z3 as an unbounded integer. Until #87 nothing told Z3 the
/// range, so a constraint no value of the type could satisfy still had a model, and the failure
/// came on the way out - an <see cref="OverflowException"/> from a checked read for
/// <c>short</c> and <see cref="DateTime"/>, and for <c>int</c> a <c>Z3Exception</c> from the
/// numeral accessor, <c>Numeral is not an int</c>. The bounds are asserted alongside the
/// constraints now, and such a theorem is unsatisfiable, which is the true answer.
/// </para>
/// <para>
/// The optimisation tests are the direct evidence, one per type: maximising or minimising a
/// symbol with no other bound on it returns the extreme of the type. Before #87 it returned
/// whatever Z3 supplied for an unbounded objective - zero, measured - so each of these fails by
/// itself if its type's row of the bounds is dropped.
/// </para>
/// <para>
/// Only scalars are bounded. A collection element is not, for the reasons given on the element
/// tests in <c>CollectionSymbolTests</c>, and is read with a checked conversion instead.
/// </para>
/// </remarks>
[TestClass]
public class SymbolBoundsTests
{
    [TestMethod]
    public void Optimize_ShortSymbolMaximised_ReturnsShortMaxValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<short, int>>()
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Maximize, t => t.X1);

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(short.MaxValue);
    }

    [TestMethod]
    public void Optimize_ShortSymbolMinimised_ReturnsShortMinValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<short, int>>()
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Minimize, t => t.X1);

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(short.MinValue);
    }

    [TestMethod]
    public void Optimize_IntSymbolMaximised_ReturnsIntMaxValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Maximize, t => t.X1);

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(int.MaxValue);
    }

    [TestMethod]
    public void Optimize_LongSymbolMinimised_ReturnsLongMinValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<long, int>>()
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Minimize, t => t.X1);

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(long.MinValue);
    }

    [TestMethod]
    public void Optimize_DateTimeSymbolMaximised_ReturnsDateTimeMaxValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Maximize, t => t.X1);

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(DateTime.MaxValue);
        result.X1.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [TestMethod]
    public void Optimize_DateTimeSymbolMinimised_ReturnsDateTimeMinValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Minimize, t => t.X1);

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(DateTime.MinValue);
    }

    /// <summary>
    /// An <c>int</c> symbol constrained beyond the range of <c>int</c> has no solution.
    /// </summary>
    /// <remarks>
    /// #87 recorded this route as unreachable for <c>int</c>, because a comparison against a
    /// <c>long</c> variable fell into the conversion catch-all. #76 made the widening a no-op,
    /// and the route opened: measured before this change, the theorem solved and the read threw
    /// <c>Z3Exception: Numeral is not an int</c>. Now it is unsatisfiable.
    /// </remarks>
    [TestMethod]
    public void Solve_IntSymbolConstrainedBeyondIntRange_IsUnsatisfiable()
    {
        // Arrange
        using var context = new Z3Context();
        long beyondIntRange = 3_000_000_000L;
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == beyondIntRange);

        // Act
        bool satisfiable = theorem.TrySolve(out _);

        // Assert
        satisfiable.ShouldBeFalse();
    }

    [TestMethod]
    public void Solve_ShortSymbolAtBothEndsOfItsRange_IsSatisfiable()
    {
        // Arrange: the bounds are inclusive - the extremes themselves are still values.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<short, short>>()
            .Where(t => t.X1 == short.MinValue)
            .Where(t => t.X2 == short.MaxValue)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(short.MinValue);
        result.X2.ShouldBe(short.MaxValue);
    }

    /// <summary>
    /// A symbol in a nested object is bounded like one at the top level.
    /// </summary>
    /// <remarks>
    /// The bounds are asserted by walking the environment, and a nested object is an environment
    /// of its own under the outer one, so the walk has to descend.
    /// </remarks>
    [TestMethod]
    public void Optimize_ShortSymbolInANestedObjectMaximised_ReturnsShortMaxValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<OuterEnvironment>()
            .Where(t => t.Top == 1)
            .Optimize(Optimization.Maximize, t => t.Inner.Value);

        // Assert
        result.ShouldNotBeNull();
        result.Inner.Value.ShouldBe(short.MaxValue);
    }

    /// <summary>
    /// A bounded symbol still reports itself unsatisfiable through <c>TryOptimize</c>, and a
    /// contradiction within the range is still a contradiction.
    /// </summary>
    [TestMethod]
    public void TryOptimize_ShortSymbolConstrainedOutsideItsRange_ReturnsFalse()
    {
        // Arrange
        using var context = new Z3Context();
        int beyondShortRange = 40000;
        var theorem = context.NewTheorem<Symbols<short, int>>()
            .Where(t => t.X1 > beyondShortRange);

        // Act
        bool satisfiable = theorem.TryOptimize(Optimization.Minimize, t => t.X1, out _);

        // Assert
        satisfiable.ShouldBeFalse();
    }

    private sealed class InnerEnvironment
    {
        public short Value { get; set; }
    }

    private sealed class OuterEnvironment
    {
        public InnerEnvironment Inner { get; set; } = new();

        public int Top { get; set; }
    }
}
