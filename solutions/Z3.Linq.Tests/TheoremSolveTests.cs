namespace Z3.Linq.Tests;

/// <summary>
/// Core solve semantics: what <see cref="Theorem{T}.Solve"/> returns for satisfiable and
/// unsatisfiable theorems, and how the result is populated.
/// </summary>
/// <remarks>
/// <para>
/// Assertions here follow the suite's determinism rule: Z3 does not promise <em>which</em>
/// satisfying model it returns, so a test may only assert an exact value where the constraints
/// admit exactly one. Everywhere else it asserts a model-independent invariant. A Microsoft.Z3
/// version bump can legitimately change which model comes back, and Dependabot bumps NuGet
/// packages daily on this repository.
/// </para>
/// <para>
/// Unsatisfiable cases are deliberately kept trivial. <c>Solve</c> returns <c>default</c> for
/// both <c>Status.UNSATISFIABLE</c> and <c>Status.UNKNOWN</c> (Theorem.cs:85-87), so a theorem
/// that was merely slow would pass an "is unsatisfiable" assertion. Keeping these obviously
/// contradictory removes that risk.
/// </para>
/// </remarks>
[TestClass]
public class TheoremSolveTests
{
    [TestMethod]
    public void Solve_SatisfiableTheorem_ReturnsPopulatedResult()
    {
        // Arrange: X1 is pinned to exactly one value, so the assertion is model-independent.
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where t.X1 == 42
                      where t.X2 == t.X1 + 1
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(42);
        result.X2.ShouldBe(43);
    }

    [TestMethod]
    public void Solve_UnsatisfiableTheorem_ReturnsNull()
    {
        // Arrange: a direct contradiction - no model can satisfy both.
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where t.X1 > 10
                      where t.X1 < 5
                      select t).Solve();

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void Solve_ContradictoryBooleanTheorem_ReturnsNull()
    {
        // Arrange: 'x && !x' is unsatisfiable by construction.
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem(new { x = default(bool) })
                      where t.x && !t.x
                      select t).Solve();

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// KNOWN DEFECT: a symbol that no constraint mentions makes <c>Solve</c> throw.
    /// </summary>
    /// <remarks>
    /// Currently: <see cref="InvalidCastException"/>. Z3 only assigns model values to constants
    /// that appear in the asserted formulas, and <c>model.Eval(expr)</c> defaults to
    /// <c>completion: false</c>, so an unreferenced symbol evaluates to itself - an
    /// <c>IntExpr</c> rather than an <c>IntNum</c> - and the cast at Theorem.cs:516 fails.
    /// Should be: the theorem is satisfiable, so it should return a result with an arbitrary
    /// value for the free symbol. The fix is <c>model.Eval(expr, completion: true)</c> at all
    /// four call sites (Theorem.cs:445, 471, 507, 634).
    /// This test pins current behaviour and must be updated when the defect is fixed.
    /// </remarks>
    [TestMethod]
    public void Solve_TheoremWithUnconstrainedSymbol_ThrowsInvalidCastException()
    {
        // Arrange: X2 is never mentioned, so the model has no assignment for it.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>().Where(t => t.X1 == 1);

        // Act & Assert
        Should.Throw<InvalidCastException>(() => theorem.Solve());
    }

    /// <summary>
    /// KNOWN DEFECT: the same failure with no constraints at all.
    /// </summary>
    /// <remarks>
    /// An unconstrained theorem is trivially satisfiable and should return a result with
    /// arbitrary values. See <see cref="Solve_TheoremWithUnconstrainedSymbol_ThrowsInvalidCastException"/>
    /// for the mechanism. Pins current behaviour.
    /// </remarks>
    [TestMethod]
    public void Solve_TheoremWithNoConstraints_ThrowsInvalidCastException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>();

        // Act & Assert
        Should.Throw<InvalidCastException>(() => theorem.Solve());
    }

    [TestMethod]
    public void Solve_EverySymbolConstrained_ReturnsResult()
    {
        // Arrange: the working counterpart to the two pins above - once every symbol is
        // referenced by a constraint, the model assigns all of them and materialisation works.
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where t.X1 >= 0
                      where t.X2 >= 0
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBeGreaterThanOrEqualTo(0);
        result.X2.ShouldBeGreaterThanOrEqualTo(0);
    }

    [TestMethod]
    public void Solve_ConstraintsAdmittingManyModels_SatisfiesEveryConstraint()
    {
        // Arrange: many models satisfy this, so assert the constraints hold rather than
        // pinning the particular assignment Z3 happens to choose.
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where t.X1 < t.X2 + 1
                      where t.X1 > 2
                      where t.X1 != t.X2
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBeGreaterThan(2);
        result.X1.ShouldBeLessThan(result.X2);
    }

    [TestMethod]
    public void Solve_LinearSystemWithForcedDifference_ForcesTheSameValuesInEveryModel()
    {
        // Arrange: Bart De Smet's TechEd Europe 2012 example. X1-X2 must be even and within
        // [1,3], which leaves only 2; that in turn forces X3 to 1. Both hold in EVERY model,
        // so they are safe to assert exactly even though X1 and X2 themselves are free.
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem<Symbols<int, int, int>>()
                      where t.X1 - t.X2 >= 1
                      where t.X1 - t.X2 <= 3
                      where t.X1 == (2 * t.X3) + t.X2
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        (result.X1 - result.X2).ShouldBe(2);
        result.X3.ShouldBe(1);
    }

    [TestMethod]
    public void Solve_CalledTwiceOnTheSameTheorem_ReturnsEquivalentResults()
    {
        // Arrange: a theorem is immutable and re-solvable; Solve builds a fresh native context
        // each time (Theorem.cs:75). Constraints pin a single model so the two runs are
        // directly comparable.
        using var context = new Z3Context();
        var theorem = from t in context.NewTheorem<Symbols<int, int>>()
                      where t.X1 == 7
                      where t.X2 == 9
                      select t;

        // Act
        var first = theorem.Solve();
        var second = theorem.Solve();

        // Assert
        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        second.X1.ShouldBe(first.X1);
        second.X2.ShouldBe(first.X2);
    }

    [TestMethod]
    [DataRow(3, 4, DisplayName = "Small positive values")]
    [DataRow(0, 0, DisplayName = "Zero values")]
    [DataRow(-5, -2, DisplayName = "Negative values")]
    [DataRow(int.MaxValue, int.MinValue, DisplayName = "Int32 boundary values")]
    public void Solve_IntegerSymbolsPinnedToValues_RoundTripsThoseValues(int x1, int x2)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where t.X1 == x1
                      where t.X2 == x2
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(x1);
        result.X2.ShouldBe(x2);
    }
}
