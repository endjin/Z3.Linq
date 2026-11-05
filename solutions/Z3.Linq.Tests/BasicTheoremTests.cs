namespace Z3.Linq.Tests;

/// <summary>
/// Smoke coverage of the theorem API: integer and boolean symbols over anonymous-type
/// environments, chained <c>Where</c> constraints, multi-symbol tuples,
/// <see cref="Z3Methods.Distinct{T}"/>, and <see cref="Theorem{T}.Optimize"/> in
/// both directions.
/// </summary>
/// <remarks>
/// <para>
/// These tests pin the baseline behaviour that feature PRs can rely on, and give
/// refactoring PRs a green floor to land on. They deliberately use the query syntax
/// against the public API only: no solver internals, no environment plumbing.
/// </para>
/// <para>
/// An optimum is unique in its objective value even where several assignments achieve it,
/// so the optimisation tests assert the objective rather than the particular vertex Z3
/// lands on; the ranges elsewhere are closed on both sides, so any in-range model passes.
/// </para>
/// </remarks>
[TestClass]
public class BasicTheoremTests
{
    [TestMethod]
    public void Solve_IntTheorem_ReturnsModelInRange()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem(new { x = default(int) })
                      where t.x > 3 && t.x < 10
                      select t;

        var solution = theorem.Solve();
        solution.ShouldNotBeNull();
        solution!.x.ShouldBeInRange(4, 9);
    }

    [TestMethod]
    public void Solve_ChainedWhereClauses_Conjoin()
    {
        using var ctx = new Z3Context();

        var theorem = ctx.NewTheorem(new { x = default(int) })
                         .Where(t => t.x >= 0)
                         .Where(t => t.x <= 5);

        var solution = theorem.Solve();
        solution.ShouldNotBeNull();
        solution!.x.ShouldBeInRange(0, 5);
    }

    [TestMethod]
    public void Solve_TupleTheorem_ReadsBackEverySymbol()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem<(int x, int y)>()
                      where t.x + t.y == 10 && t.x > 2 && t.x < 8
                      select t;

        var solution = theorem.Solve();
        (solution.x + solution.y).ShouldBe(10);
        solution.x.ShouldBeInRange(3, 7);
    }

    [TestMethod]
    public void Solve_BoolTheorem_SatisfiesProposition()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem(new { x = default(bool), y = default(bool) })
                      where t.x && !t.y
                      select t;

        var solution = theorem.Solve();
        solution.ShouldNotBeNull();
        solution!.x.ShouldBeTrue();
        solution.y.ShouldBeFalse();
    }

    [TestMethod]
    public void Solve_ContradictoryConstraints_ReturnsNull()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem(new { x = default(int) })
                      where t.x > 1 && t.x < 0
                      select t;

        theorem.Solve().ShouldBeNull();
    }

    [TestMethod]
    public void Solve_Distinct_ForcesDifferentValues()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem(new { x = default(int), y = default(int) })
                      where Z3Methods.Distinct(t.x, t.y) && t.x >= 0 && t.x <= 2 && t.y >= 0 && t.y <= 2
                      select t;

        var solution = theorem.Solve();
        solution.ShouldNotBeNull();
        solution!.x.ShouldNotBe(solution.y);
    }

    [TestMethod]
    public void Optimize_Maximize_ReturnsUpperBound()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem(new { x = default(int) })
                      where t.x >= 0 && t.x <= 10
                      select t;

        var optimum = theorem.Optimize(Optimization.Maximize, t => t.x);
        optimum.ShouldNotBeNull();
        optimum!.x.ShouldBe(10);
    }

    [TestMethod]
    public void Optimize_Minimize_ReturnsLowerBound()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem(new { x = default(int) })
                      where t.x >= 5 && t.x <= 100
                      select t;

        var optimum = theorem.Optimize(Optimization.Minimize, t => t.x);
        optimum.ShouldNotBeNull();
        optimum!.x.ShouldBe(5);
    }

    [TestMethod]
    public void Optimize_Minimize_OverTupleObjective_Solves()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem<(int x, int y)>()
                      where t.x >= 0 && t.y >= 0 && t.x + t.y >= 7
                      select t;

        var optimum = theorem.Optimize(Optimization.Minimize, t => t.x + t.y);
        (optimum.x + optimum.y).ShouldBe(7);
    }

    [TestMethod]
    public void NewTheorem_DummyOverload_SolvesLikeParameterless()
    {
        using var ctx = new Z3Context();

        var theorem = ctx.NewTheorem(new { x = default(int) });
        var solution = theorem.Where(t => t.x == 42).Solve();

        solution.ShouldNotBeNull();
        solution!.x.ShouldBe(42);
    }
}
