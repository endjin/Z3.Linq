namespace Z3.Linq.Tests;

/// <summary>
/// Optimisation: <see cref="Theorem{T}.Optimize"/> and the <c>orderby</c> query operators that
/// delegate to it.
/// </summary>
/// <remarks>
/// <para>
/// <c>Optimize</c> takes the same constraints as <c>Solve</c> but asserts them against an
/// <c>Optimize</c> solver with an objective attached, so the model returned is the best one
/// rather than any satisfying one (Theorem.cs:99-129). <c>OrderBy</c> maps to
/// <c>Minimize</c> and <c>OrderByDescending</c> to <c>Maximize</c>, each wrapped in a deferred
/// solvable so nothing runs until <c>Solve</c> is called.
/// </para>
/// <para>
/// An optimum is unique in its objective value even where several assignments achieve it, so
/// these tests assert the objective rather than the particular vertex Z3 lands on - except
/// where the constraints pin the variables outright.
/// </para>
/// </remarks>
[TestClass]
public class OptimizationTests
{
    [TestMethod]
    public void Optimize_Minimize_ReturnsTheSmallestSatisfyingValue()
    {
        // Arrange: the range is closed on both sides, so the minimum is exactly 5.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 >= 5)
            .Where(t => t.X1 <= 9)
            .Optimize(Optimization.Minimize, t => t.X1);

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(5);
    }

    [TestMethod]
    public void Optimize_Maximize_ReturnsTheLargestSatisfyingValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 >= 5)
            .Where(t => t.X1 <= 9)
            .Optimize(Optimization.Maximize, t => t.X1);

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(9);
    }

    [TestMethod]
    public void Optimize_MinimizeAndMaximizeOverTheSameTheorem_ReturnOppositeBounds()
    {
        // Arrange: the two directions applied to one theorem. A mapping that sent both to the
        // same Z3 objective would still pass each of the two tests above in isolation only if
        // it happened to pick the right one; this fails whichever way it was collapsed.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 >= 5)
            .Where(t => t.X1 <= 9);

        // Act
        var minimum = theorem.Optimize(Optimization.Minimize, t => t.X1);
        var maximum = theorem.Optimize(Optimization.Maximize, t => t.X1);

        // Assert
        minimum.ShouldNotBeNull();
        maximum.ShouldNotBeNull();
        minimum.X1.ShouldBe(5);
        maximum.X1.ShouldBe(9);
        minimum.X1.ShouldBeLessThan(maximum.X1);
    }

    [TestMethod]
    public void OrderBy_ProducesTheSameResultAsMinimize()
    {
        // Arrange: OrderBy is documented as sugar for Optimize(Minimize, ...), so the two must
        // agree - and this test costs nothing beyond stating that.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 >= 3)
            .Where(t => t.X1 <= 8);

        // Act
        var viaOptimize = theorem.Optimize(Optimization.Minimize, t => t.X1);
        var viaOrderBy = theorem.OrderBy(t => t.X1).Solve();

        // Assert
        viaOptimize.ShouldNotBeNull();
        viaOrderBy.ShouldNotBeNull();
        viaOrderBy.X1.ShouldBe(viaOptimize.X1);
        viaOrderBy.X1.ShouldBe(3);
    }

    [TestMethod]
    public void OrderByDescending_ProducesTheSameResultAsMaximize()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 >= 3)
            .Where(t => t.X1 <= 8);

        // Act
        var viaOptimize = theorem.Optimize(Optimization.Maximize, t => t.X1);
        var viaOrderBy = theorem.OrderByDescending(t => t.X1).Solve();

        // Assert
        viaOptimize.ShouldNotBeNull();
        viaOrderBy.ShouldNotBeNull();
        viaOrderBy.X1.ShouldBe(viaOptimize.X1);
        viaOrderBy.X1.ShouldBe(8);
    }

    [TestMethod]
    public void OrderBy_BeforeSolveIsCalled_DoesNotSolveAnything()
    {
        // Arrange: OrderBy returns a deferred solvable (Theorem{T}.cs:78), so building the query
        // must not touch Z3. The log is the observable proof - Theorem.cs:178 writes a line per
        // asserted constraint, so an eager implementation would have written to it already.
        using var log = new StringWriter();
        using var context = new Z3Context { Log = log };
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 >= 3);

        // Act
        var deferred = theorem.OrderBy(t => t.X1);

        // Assert
        log.ToString().ShouldBeEmpty();

        // ...and solving is what actually runs it.
        deferred.Solve();
        log.ToString().ShouldNotBeEmpty();
    }

    [TestMethod]
    public void OrderByDescending_BeforeSolveIsCalled_DoesNotSolveAnything()
    {
        // Arrange
        using var log = new StringWriter();
        using var context = new Z3Context { Log = log };
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 >= 3);

        // Act
        var deferred = theorem.OrderByDescending(t => t.X1);

        // Assert
        log.ToString().ShouldBeEmpty();

        deferred.Solve();
        log.ToString().ShouldNotBeEmpty();
    }

    [TestMethod]
    public void Optimize_ObjectiveOverSeveralSymbols_MinimizesTheirCombination()
    {
        // Arrange: the objective need not be a single symbol. Both are free within their ranges
        // and only their sum is minimised, so the sum is what can be asserted.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 >= 2)
            .Where(t => t.X2 >= 3)
            .Where(t => t.X1 + t.X2 >= 10)
            .Optimize(Optimization.Minimize, t => t.X1 + t.X2);

        // Assert
        result.ShouldNotBeNull();
        (result.X1 + result.X2).ShouldBe(10);
        result.X1.ShouldBeGreaterThanOrEqualTo(2);
        result.X2.ShouldBeGreaterThanOrEqualTo(3);
    }

    [TestMethod]
    public void Optimize_LinearProgramOverDoubles_FindsTheKnownOptimum()
    {
        // Arrange: the oil purchase problem from the README, whose optimum is a single vertex -
        // buy 2200 barrels from Saudi Arabia and 3100 from Venezuela for $90,500. An LP optimum
        // is unique in its objective value, so the cost is safe to assert exactly.
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem<(double sa, double vz)>()
                      where (0.3 * t.sa) + (0.4 * t.vz) >= 1900   // gasolene
                      where (0.4 * t.sa) + (0.2 * t.vz) >= 1500   // jet fuel
                      where (0.2 * t.sa) + (0.3 * t.vz) >= 500    // lubricant
                      where 0 <= t.sa && t.sa <= 9000
                      where 0 <= t.vz && t.vz <= 6000
                      orderby (20.0 * t.sa) + (15.0 * t.vz)
                      select t).Solve();

        // Assert
        double cost = (20.0 * result.sa) + (15.0 * result.vz);
        cost.ShouldBe(90_500.0);

        // The constraints must still hold at the optimum - a "minimum" that violated them would
        // otherwise satisfy the cost assertion by being infeasible.
        ((0.3 * result.sa) + (0.4 * result.vz)).ShouldBeGreaterThanOrEqualTo(1900);
        ((0.4 * result.sa) + (0.2 * result.vz)).ShouldBeGreaterThanOrEqualTo(1500);
        ((0.2 * result.sa) + (0.3 * result.vz)).ShouldBeGreaterThanOrEqualTo(500);
    }

    [TestMethod]
    public void Optimize_MinimizingIsNotJustTheFirstSatisfyingModel()
    {
        // Arrange: this is the test that distinguishes optimising from solving. A plain Solve
        // over the same constraints may return any value in [5, 100]; minimising must return 5.
        // Without it, an Optimize that quietly ignored its objective would still look correct
        // wherever Z3 happened to pick the boundary.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 >= 5)
            .Where(t => t.X1 <= 100);

        // Act
        var minimised = theorem.Optimize(Optimization.Minimize, t => t.X1);
        var maximised = theorem.Optimize(Optimization.Maximize, t => t.X1);

        // Assert
        minimised.ShouldNotBeNull();
        maximised.ShouldNotBeNull();
        minimised.X1.ShouldBe(5);
        maximised.X1.ShouldBe(100);
    }

    [TestMethod]
    public void Optimize_CalledTwiceOnTheSameTheorem_LeavesItReusable()
    {
        // Arrange: each call builds its own native context and optimiser, so an earlier
        // optimisation must not leave an objective attached to the theorem.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 >= 1)
            .Where(t => t.X1 <= 4);

        // Act
        var first = theorem.Optimize(Optimization.Minimize, t => t.X1);
        var second = theorem.Optimize(Optimization.Minimize, t => t.X1);
        var solved = theorem.Solve();

        // Assert
        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        first.X1.ShouldBe(1);
        second.X1.ShouldBe(1);

        // The plain solve is unconstrained by the objective and only has to satisfy the range.
        solved.ShouldNotBeNull();
        solved.X1.ShouldBeInRange(1, 4);
    }

    [TestMethod]
    public void Optimize_WithUnrecognisedDirection_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: Optimization has two members, and the switch rejects anything else rather
        // than defaulting to one of them (Theorem.cs:118).
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == 1);

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(
            () => theorem.Optimize((Optimization)42, t => t.X1));
    }

    /// <summary>
    /// An unsatisfiable optimisation returns null through a signature that says so.
    /// </summary>
    /// <remarks>
    /// <c>Optimize</c> returned <c>default!</c> through a non-nullable <c>T</c> until #58 - for a
    /// reference environment, a null the compiler had been told could not happen, so the caller
    /// dereferenced it and got a <see cref="NullReferenceException"/> far from the cause. The
    /// value has not changed; what changed is that the declaration now admits it, which turned
    /// ten unchecked dereferences across six tests in this very file into compiler errors.
    /// </remarks>
    [TestMethod]
    public void Optimize_WithUnsatisfiableTheorem_ReturnsNull()
    {
        // Arrange: a direct contradiction, so the optimiser has nothing to optimise over.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 > 10)
            .Where(t => t.X1 < 5);

        // Act
        var result = theorem.Optimize(Optimization.Minimize, t => t.X1);

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void OrderBy_OnAnUnsatisfiableTheorem_ReturnsNull()
    {
        // Arrange: the query-syntax route to the same place. This one was always well-typed,
        // since ISolveable<T>.Solve has always been declared to return T?.
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where t.X1 > 10
                      where t.X1 < 5
                      orderby t.X1
                      select t).Solve();

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void Optimize_AfterAdditionalWhere_RespectsTheNarrowedConstraints()
    {
        // Arrange: Where returns a new theorem, so optimising the child must see the extra
        // constraint while the parent still does not.
        using var context = new Z3Context();
        var parent = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 >= 1)
            .Where(t => t.X1 <= 10);
        var child = parent.Where(t => t.X1 >= 6);

        // Act
        var parentMinimum = parent.Optimize(Optimization.Minimize, t => t.X1);
        var childMinimum = child.Optimize(Optimization.Minimize, t => t.X1);

        // Assert
        parentMinimum.ShouldNotBeNull();
        childMinimum.ShouldNotBeNull();
        parentMinimum.X1.ShouldBe(1);
        childMinimum.X1.ShouldBe(6);
    }

    /// <summary>
    /// An unconstrained symbol does not stop an optimisation returning its optimum.
    /// </summary>
    /// <remarks>
    /// <c>Optimize</c> reads its answer from the optimiser's model rather than a solver's, so it
    /// needs its own coverage of #51 - every test in this file constrained X2 purely to avoid
    /// the defect. The minimum over a closed range is unique, so asserting it exactly is safe;
    /// X2 is free and is not asserted.
    /// </remarks>
    [TestMethod]
    public void Optimize_WithAnUnconstrainedSymbol_ReturnsTheOptimum()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 >= 5)
            .Where(t => t.X1 <= 9)
            .Optimize(Optimization.Minimize, t => t.X1);

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(5);
    }

    [TestMethod]
    public void OrderBy_WithAnUnconstrainedSymbol_ReturnsTheOptimum()
    {
        // Arrange: the query-syntax route to the same code path, kept for the symmetry this
        // file maintains between Optimize and OrderBy.
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where t.X1 >= 5
                      where t.X1 <= 9
                      orderby t.X1
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(5);
    }

    /// <summary>
    /// An optimisation over a <see cref="DateTime"/> symbol returns the latest instant in range.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A DateTime is encoded as an integer - its ticks - so ordering it is ordering that integer -
    /// which is only the same thing as ordering the instants because every constant on the way in
    /// is converted the same way, whatever kind it carried. This test puts one bound in local time
    /// and one in UTC to say so: they are half an hour apart on the timeline, not the seven-and-a-
    /// half hours their wall-clock readings suggest in some zones.
    /// </para>
    /// <para>
    /// The range is closed, so the maximum is exactly the upper bound and can be asserted
    /// outright. That the answer comes back as UTC is #56; that it is the right instant is what
    /// this file is for.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void OrderByDescending_DateTimeSymbolInAClosedRange_ReturnsTheLatestInstant()
    {
        // Arrange
        using var context = new Z3Context();
        var earliest = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Local);
        DateTime latest = earliest.ToUniversalTime().AddMinutes(30);

        // Act
        var result = (from t in context.NewTheorem<Symbols<DateTime, int>>()
                      where t.X1 >= earliest && t.X1 <= latest && t.X2 == 0
                      orderby t.X1 descending
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(latest);
        result.X1.Kind.ShouldBe(DateTimeKind.Utc);
    }
}
