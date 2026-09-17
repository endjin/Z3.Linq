namespace Z3.Linq.Tests;

using System;
using Xunit;

/// <summary>
/// Smoke coverage of the theorem API as it exists on <c>main</c> today: integer and
/// boolean symbols over anonymous-type environments, chained <c>Where</c> constraints,
/// multi-symbol tuples, <see cref="Z3Methods.Distinct{T}"/>, and
/// <see cref="Theorem{T}.Optimize{TResult}"/> in both directions. These tests pin the
/// behaviour feature PRs can rely on, and give refactoring PRs a green floor to land on.
/// </summary>
public class BasicTheoremTests
{
    [Fact]
    public void Solve_IntTheorem_ReturnsModelInRange()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem(new { x = default(int) })
                      where t.x > 3 && t.x < 10
                      select t;

        var solution = theorem.Solve();

        Assert.NotNull(solution);
        Assert.InRange(solution!.x, 4, 9);
    }

    [Fact]
    public void Solve_ChainedWhereClauses_Conjoin()
    {
        using var ctx = new Z3Context();

        var theorem = ctx.NewTheorem(new { x = default(int) })
                         .Where(t => t.x >= 0)
                         .Where(t => t.x <= 5);

        var solution = theorem.Solve();

        Assert.NotNull(solution);
        Assert.InRange(solution!.x, 0, 5);
    }

    [Fact]
    public void Solve_TupleTheorem_ReadsBackEverySymbol()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem<(int x, int y)>()
                      where t.x + t.y == 10 && t.x > 2 && t.x < 8
                      select t;

        var solution = theorem.Solve();

        Assert.NotNull(solution);
        Assert.Equal(10, solution!.x + solution.y);
        Assert.InRange(solution.x, 3, 7);
    }

    [Fact]
    public void Solve_BoolTheorem_SatisfiesProposition()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem(new { x = default(bool), y = default(bool) })
                      where t.x && !t.y
                      select t;

        var solution = theorem.Solve();

        Assert.NotNull(solution);
        Assert.True(solution!.x);
        Assert.False(solution.y);
    }

    [Fact]
    public void Solve_ContradictoryConstraints_ReturnsNull()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem(new { x = default(int) })
                      where t.x > 1 && t.x < 0
                      select t;

        Assert.Null(theorem.Solve());
    }

    [Fact]
    public void Solve_Distinct_ForcesDifferentValues()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem(new { x = default(int), y = default(int) })
                      where Z3Methods.Distinct(t.x, t.y) && t.x >= 0 && t.x <= 2 && t.y >= 0 && t.y <= 2
                      select t;

        var solution = theorem.Solve();

        Assert.NotNull(solution);
        Assert.NotEqual(solution!.x, solution.y);
    }

    [Fact]
    public void Optimize_Maximize_ReturnsUpperBound()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem(new { x = default(int) })
                      where t.x >= 0 && t.x <= 10
                      select t;

        var optimum = theorem.Optimize(Optimization.Maximize, t => t.x);

        Assert.Equal(10, optimum.x);
    }

    [Fact]
    public void Optimize_Minimize_ReturnsLowerBound()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem(new { x = default(int) })
                      where t.x >= 5 && t.x <= 100
                      select t;

        var optimum = theorem.Optimize(Optimization.Minimize, t => t.x);

        Assert.Equal(5, optimum.x);
    }

    [Fact]
    public void Optimize_Minimize_OverTupleObjective_Solves()
    {
        using var ctx = new Z3Context();

        var theorem = from t in ctx.NewTheorem<(int x, int y)>()
                      where t.x >= 0 && t.y >= 0 && t.x + t.y >= 7
                      select t;

        var optimum = theorem.Optimize(Optimization.Minimize, t => t.x + t.y);

        Assert.Equal(7, optimum.x + optimum.y);
    }

    [Fact]
    public void NewTheorem_DummyOverload_SolvesLikeParameterless()
    {
        using var ctx = new Z3Context();

        var theorem = ctx.NewTheorem(new { x = default(int) });
        var solution = theorem.Where(t => t.x == 42).Solve();

        Assert.NotNull(solution);
        Assert.Equal(42, solution!.x);
    }
}
