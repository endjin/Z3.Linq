namespace Z3.Linq.Tests;

/// <summary>
/// Bounding a solve: <see cref="Z3Context.Timeout"/>, <see cref="Z3Context.ResourceLimit"/> and
/// the <see cref="CancellationToken"/> every solve and optimisation accepts.
/// </summary>
/// <remarks>
/// <para>
/// Nonlinear integer arithmetic is undecidable in general, and the theorem these tests use -
/// three integers whose cubes sum to 42 - is the one #85 measured as still searching after two
/// minutes. Before #85 nothing in the API could stop it. Every test that runs it carries a
/// <see cref="TimeoutAttribute"/>, so a regression fails the test rather than hanging the suite.
/// </para>
/// <para>
/// How Z3 stops is reported by exception, never by <see langword="false"/>: a cancelled token is
/// <see cref="OperationCanceledException"/>, and everything else - a limit reached, or Z3 giving
/// up - is <see cref="TheoremUndecidedException"/>. That settles the wrinkle #57 deferred: the
/// <see langword="false"/> from <c>TrySolve</c> means "proved to have no solution" and nothing
/// else. Z3 describes why it stopped as a string, and the strings differ between the solver and
/// the optimizer for the same limit, so no test asserts on them beyond their presence.
/// </para>
/// <para>
/// The limits are small - half a second, a modest resource budget - because the point is that
/// they are reached, not how long that takes. Nothing here asserts on elapsed time.
/// </para>
/// </remarks>
[TestClass]
public class SolveLimitTests
{
    private const int HangGuardMilliseconds = 60_000;

    [TestMethod]
    [Timeout(HangGuardMilliseconds)]
    public void Solve_UndecidableTheoremWithATimeout_ThrowsTheoremUndecidedException()
    {
        // Arrange
        using var context = new Z3Context { Timeout = TimeSpan.FromMilliseconds(500) };
        var theorem = SumOfThreeCubes(context);

        // Act
        TheoremUndecidedException exception = Should.Throw<TheoremUndecidedException>(() => theorem.Solve());

        // Assert
        exception.Reason.ShouldNotBeNullOrEmpty();
        exception.Message.ShouldContain(exception.Reason);
    }

    /// <summary>
    /// A solve that stopped without deciding throws rather than returning <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// The wrinkle #57 raised and #85 deferred: <c>TrySolve</c> used to return
    /// <see langword="false"/> for <c>Status.UNKNOWN</c> as well as for unsatisfiable, which was
    /// defensible only while nothing could produce an unknown. Now something can, and
    /// <see langword="false"/> has to mean what it says.
    /// </remarks>
    [TestMethod]
    [Timeout(HangGuardMilliseconds)]
    public void TrySolve_UndecidableTheoremWithATimeout_ThrowsRatherThanReturningFalse()
    {
        // Arrange
        using var context = new Z3Context { Timeout = TimeSpan.FromMilliseconds(500) };
        var theorem = SumOfThreeCubes(context);

        // Act & Assert
        Should.Throw<TheoremUndecidedException>(() => theorem.TrySolve(out _));
    }

    /// <summary>
    /// A resource limit stops the same theorem, deterministically.
    /// </summary>
    /// <remarks>
    /// Z3's <c>rlimit</c> counts work rather than time, so the same theorem does the same amount
    /// of it on every machine. Measured, this budget is exhausted in tens of milliseconds.
    /// </remarks>
    [TestMethod]
    [Timeout(HangGuardMilliseconds)]
    public void Solve_UndecidableTheoremWithAResourceLimit_ThrowsTheoremUndecidedException()
    {
        // Arrange
        using var context = new Z3Context { ResourceLimit = 200_000 };
        var theorem = SumOfThreeCubes(context);

        // Act & Assert
        Should.Throw<TheoremUndecidedException>(() => theorem.Solve());
    }

    [TestMethod]
    public void Solve_SatisfiableTheoremWithATimeout_StillSolves()
    {
        // Arrange: a limit changes nothing for a theorem Z3 decides within it.
        using var context = new Z3Context { Timeout = TimeSpan.FromMilliseconds(500) };

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == 3)
            .Where(t => t.X2 == t.X1 + 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(3);
        result.X2.ShouldBe(4);
    }

    [TestMethod]
    public void TrySolve_UnsatisfiableTheoremWithATimeout_StillReturnsFalse()
    {
        // Arrange: false still means proved unsatisfiable, limit or no limit.
        using var context = new Z3Context { Timeout = TimeSpan.FromMilliseconds(500) };
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == 3)
            .Where(t => t.X1 == 4);

        // Act
        bool satisfiable = theorem.TrySolve(out _);

        // Assert
        satisfiable.ShouldBeFalse();
    }

    /// <summary>
    /// The context stays usable after a solve is cut short.
    /// </summary>
    /// <remarks>
    /// The limit is applied to each solve, not baked into the native context, so an undecided
    /// solve leaves nothing behind - the next theorem on the same context solves normally.
    /// </remarks>
    [TestMethod]
    [Timeout(HangGuardMilliseconds)]
    public void Solve_AfterAnUndecidedSolveOnTheSameContext_StillSolves()
    {
        // Arrange
        using var context = new Z3Context { Timeout = TimeSpan.FromMilliseconds(500) };
        Should.Throw<TheoremUndecidedException>(() => SumOfThreeCubes(context).Solve());

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == 7)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(7);
    }

    /// <summary>
    /// An already-cancelled token throws before any work is done.
    /// </summary>
    /// <remarks>
    /// Measured on the raw API: an interrupt issued before the check starts is lost, so a token
    /// that is cancelled on the way in has to be inspected rather than relied on to fire. The
    /// theorem here is trivially satisfiable and would solve in milliseconds; it must not.
    /// </remarks>
    [TestMethod]
    public void Solve_WithAnAlreadyCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var context = new Z3Context();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var theorem = context.NewTheorem<Symbols<int, int>>().Where(t => t.X1 == 3);

        // Act
        OperationCanceledException exception =
            Should.Throw<OperationCanceledException>(() => theorem.Solve(cancellation.Token));

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [TestMethod]
    [Timeout(HangGuardMilliseconds)]
    public void Solve_CancelledDuringTheSolve_ThrowsOperationCanceledException()
    {
        // Arrange: no limit on the context, so only the token can end this.
        using var context = new Z3Context();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var theorem = SumOfThreeCubes(context);

        // Act
        OperationCanceledException exception =
            Should.Throw<OperationCanceledException>(() => theorem.Solve(cancellation.Token));

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [TestMethod]
    [Timeout(HangGuardMilliseconds)]
    public void Optimize_UndecidableTheoremWithATimeout_ThrowsTheoremUndecidedException()
    {
        // Arrange: the optimizer has its own check, and reports the limit with a different
        // string from the solver - which is why the library decides by what it asked for.
        using var context = new Z3Context { Timeout = TimeSpan.FromMilliseconds(500) };
        var theorem = SumOfThreeCubes(context);

        // Act
        TheoremUndecidedException exception =
            Should.Throw<TheoremUndecidedException>(() => theorem.Optimize(Optimization.Minimize, t => t.X1));

        // Assert
        exception.Reason.ShouldNotBeNullOrEmpty();
    }

    [TestMethod]
    [Timeout(HangGuardMilliseconds)]
    public void Optimize_CancelledDuringTheSolve_ThrowsOperationCanceledException()
    {
        // Arrange
        using var context = new Z3Context();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var theorem = SumOfThreeCubes(context);

        // Act
        OperationCanceledException exception = Should.Throw<OperationCanceledException>(
            () => theorem.TryOptimize(Optimization.Minimize, t => t.X1, out _, cancellation.Token));

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    /// <summary>
    /// The deferred form an <c>orderby</c> query returns takes the token when it is finally
    /// solved.
    /// </summary>
    [TestMethod]
    [Timeout(HangGuardMilliseconds)]
    public void OrderBy_CancelledDuringTheDeferredSolve_ThrowsOperationCanceledException()
    {
        // Arrange
        using var context = new Z3Context();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        ISolveable<Symbols<int, int, int>> deferred = SumOfThreeCubes(context).OrderBy(t => t.X1);

        // Act
        OperationCanceledException exception =
            Should.Throw<OperationCanceledException>(() => deferred.TrySolve(out _, cancellation.Token));

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    /// <summary>
    /// The <c>Nullable</c>-lifting extension passes the token through.
    /// </summary>
    [TestMethod]
    [Timeout(HangGuardMilliseconds)]
    public void SolveOrNull_CancelledDuringTheSolve_ThrowsOperationCanceledException()
    {
        // Arrange
        using var context = new Z3Context();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var theorem = context.NewTheorem<(int a, int b, int c)>()
            .Where(t => (t.a * t.a * t.a) + (t.b * t.b * t.b) + (t.c * t.c * t.c) == 42);

        // Act & Assert
        Should.Throw<OperationCanceledException>(() => theorem.SolveOrNull(cancellation.Token));
    }

    [TestMethod]
    [DataRow(0L, DisplayName = "Zero")]
    [DataRow(-1L, DisplayName = "Negative")]
    public void Timeout_SetToANonPositiveValue_ThrowsArgumentOutOfRangeException(long ticks)
    {
        // Arrange
        using var context = new Z3Context();

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => context.Timeout = TimeSpan.FromTicks(ticks));
    }

    [TestMethod]
    public void ResourceLimit_SetToZero_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var context = new Z3Context();

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => context.ResourceLimit = 0);
    }

    [TestMethod]
    public void Timeout_SetToNull_ClearsTheLimit()
    {
        // Arrange
        using var context = new Z3Context { Timeout = TimeSpan.FromSeconds(1) };

        // Act
        context.Timeout = null;

        // Assert
        context.Timeout.ShouldBeNull();
    }

    private static Theorem<Symbols<int, int, int>> SumOfThreeCubes(Z3Context context)
    {
        // x^3 + y^3 + z^3 == 42 over the integers. A solution exists - it was found in 2019 and
        // has eighteen digits - but Z3 has no way to reach it, and no way to prove there is none.
        return context.NewTheorem<Symbols<int, int, int>>()
            .Where(t => (t.X1 * t.X1 * t.X1) + (t.X2 * t.X2 * t.X2) + (t.X3 * t.X3 * t.X3) == 42);
    }
}
