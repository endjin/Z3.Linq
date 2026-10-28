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
/// <para>
/// A symbol the returned model does not interpret takes an arbitrary value supplied by Z3's
/// model completion (#51). That value is not part of any contract, so the tests here assert
/// that the solve completed and that the constrained symbols survived - never what the free
/// one came back as.
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
    /// A symbol that no constraint mentions does not stop the theorem being solved.
    /// </summary>
    /// <remarks>
    /// The theorem is satisfiable, so X2 takes whatever value Z3's model completion supplies.
    /// That value is not part of any contract and is deliberately not asserted; what matters is
    /// that the solve completes and the constrained symbol is untouched. Fixed by #51 - this
    /// previously threw <see cref="InvalidCastException"/>, because a symbol the model does not
    /// interpret evaluates to itself rather than to a numeral.
    /// </remarks>
    [TestMethod]
    public void Solve_TheoremWithAnUnconstrainedSymbol_ReturnsAResultAndKeepsTheConstrainedValue()
    {
        // Arrange: X2 is never mentioned, so the solver has no reason to assign it. This is the
        // repro from #51 verbatim.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>().Where(t => t.X1 == 1);

        // Act
        var result = theorem.Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(1);
    }

    /// <summary>
    /// A theorem with no constraints at all is trivially satisfiable and returns a result.
    /// </summary>
    /// <remarks>
    /// The degenerate case of the test above: every symbol is free, so there is nothing to
    /// assert beyond the solve completing. This is the only test that checks a solver is asked
    /// to solve without a single assertion.
    /// </remarks>
    [TestMethod]
    public void Solve_TheoremWithNoConstraints_ReturnsAResult()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>();

        // Act
        var result = theorem.Solve();

        // Assert
        result.ShouldNotBeNull();
    }

    /// <summary>
    /// A symbol mentioned only by a constraint the solver discards is still populated.
    /// </summary>
    /// <remarks>
    /// "Mentioned by a constraint" is not the condition that matters - "interpreted by the
    /// returned model" is. A tautology is reduced to true as it is asserted, so X1 is never
    /// created and has no interpretation, despite the query naming it twice. Before #51 this
    /// threw for exactly the same reason an unmentioned symbol did, which is why the fix is
    /// model completion rather than an inspection of the constraints.
    /// </remarks>
    [TestMethod]
    public void Solve_SymbolMentionedOnlyInATautology_ReturnsAResult()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where t.X1 > 0 || t.X1 <= 0
                      where t.X2 == 0
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe(0);
    }

    [TestMethod]
    public void Solve_EverySymbolConstrained_ReturnsResult()
    {
        // Arrange: the control for #51. Constraining every symbol was the workaround for that
        // defect, and it must keep behaving identically now that it is no longer necessary.
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
