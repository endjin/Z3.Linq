namespace Z3.Linq.Tests;

using System.Linq.Expressions;

/// <summary>
/// The two rewriter extension points: a global rewriter attached to an environment type, which
/// gets the whole constraint list before anything is translated, and a predicate rewriter
/// attached to a method, which replaces a call the visitor would otherwise reject.
/// </summary>
/// <remarks>
/// <para>
/// Both are public API and both were entirely unverified. They are also the only way to extend
/// the translator without changing it, which makes them worth holding still.
/// </para>
/// <para>
/// The tests assert what a rewrite <em>did</em>, not merely that one ran. A rewriter whose
/// returned constraints were discarded, or whose replacement expression was built and then
/// ignored, would satisfy a "was it called" check perfectly well while doing nothing.
/// </para>
/// </remarks>
[TestClass]
public class RewriterTests
{
    [TestMethod]
    public void Solve_EnvironmentWithGlobalRewriter_AppliesTheRewrittenConstraints()
    {
        // Arrange: the rewriter appends a constraint pinning Second to 99, which no constraint
        // in the query mentions. If the returned sequence were ignored, Second would be free
        // and this would fail.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<GloballyRewrittenEnvironment>()
            .Where(t => t.First == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.First.ShouldBe(1);
        result.Second.ShouldBe(99);
    }

    [TestMethod]
    public void Solve_GlobalRewriterThatDropsConstraints_ChangesSatisfiability()
    {
        // Arrange: the complementary direction. The query is a direct contradiction, and the
        // rewriter discards everything, so a theorem that could not otherwise be satisfied is.
        // A rewriter whose output was ignored would leave this unsatisfiable.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<ConstraintDroppingEnvironment>()
            .Where(t => t.First > 10)
            .Where(t => t.First < 5)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.First.ShouldBe(7);
    }

    [TestMethod]
    public void Solve_GlobalRewriterTypeNotImplementingTheInterface_ThrowsInvalidOperationException()
    {
        // Arrange: the attribute takes a bare Type, so nothing at compile time requires it to be
        // a rewriter. The check happens on the way in (Theorem.cs:151).
        using var context = new Z3Context();
        var theorem = context.NewTheorem<BadlyRewrittenEnvironment>().Where(t => t.First == 1);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => theorem.Solve());
    }

    [TestMethod]
    public void Solve_MethodWithPredicateRewriter_TranslatesTheRewrittenCall()
    {
        // Arrange: AllDifferent has no meaning to the visitor by itself - its own body throws.
        // The rewriter turns the call into Z3Methods.Distinct over the same arguments, which
        // the visitor does understand.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => AllDifferent(t.X1, t.X2))
            .Where(t => t.X1 == 1)
            .Where(t => t.X2 == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(1);
        result.X2.ShouldBe(2);
    }

    [TestMethod]
    public void Solve_MethodWithPredicateRewriter_AppliesTheRewrittenMeaning()
    {
        // Arrange: the test above shows the rewritten call is accepted; this one shows it means
        // something. Both symbols are pinned to 3, so the Distinct the rewriter produced cannot
        // hold and the theorem must be unsatisfiable. A rewrite that was accepted and then
        // dropped would return a result here.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => AllDifferent(t.X1, t.X2))
            .Where(t => t.X1 == 3)
            .Where(t => t.X2 == 3)
            .Solve();

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void Solve_PredicateRewriterThatReturnsItsInput_ThrowsTheProgressGuard()
    {
        // Arrange: the visitor re-visits whatever the rewriter returns, so a rewriter returning
        // its own input would recurse forever. That is detected by reference equality and
        // rejected (ExpressionVisitor.cs:191) - a guard worth keeping, since the alternative is
        // a hung build rather than a failed one.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => NeverRewritten(t.X1, t.X2));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => theorem.Solve());
    }

    [TestMethod]
    public void Solve_PredicateRewriterTypeNotImplementingTheInterface_ThrowsInvalidOperationException()
    {
        // Arrange: as with the global rewriter, the attribute cannot enforce this at compile
        // time (ExpressionVisitor.cs:179).
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => WrongRewriterType(t.X1, t.X2));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => theorem.Solve());
    }

    [TestMethod]
    public void Solve_EnvironmentWithoutARewriter_IsUnaffected()
    {
        // Arrange: the control. Rewriting is opt-in by attribute, so an ordinary environment
        // must behave as though the machinery were not there.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<PlainEnvironment>()
            .Where(t => t.First == 1)
            .Where(t => t.Second == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.First.ShouldBe(1);
        result.Second.ShouldBe(2);
    }

    [TheoremPredicateRewriter(typeof(DistinctPredicateRewriter))]
    private static bool AllDifferent(int first, int second)
        => throw new NotSupportedException("This method should only be used in query expressions.");

    [TheoremPredicateRewriter(typeof(IdentityPredicateRewriter))]
    private static bool NeverRewritten(int first, int second)
        => throw new NotSupportedException("This method should only be used in query expressions.");

    [TheoremPredicateRewriter(typeof(NotARewriter))]
    private static bool WrongRewriterType(int first, int second)
        => throw new NotSupportedException("This method should only be used in query expressions.");

    /// <summary>Implements neither rewriter interface, which is the point.</summary>
    private sealed class NotARewriter
    {
    }

    private sealed class DistinctPredicateRewriter : ITheoremPredicateRewriter
    {
        public MethodCallExpression Rewrite(MethodCallExpression call)
        {
            var distinct = typeof(Z3Methods).GetMethod(nameof(Z3Methods.Distinct))!
                .MakeGenericMethod(typeof(int));

            return Expression.Call(distinct, Expression.NewArrayInit(typeof(int), call.Arguments));
        }
    }

    private sealed class IdentityPredicateRewriter : ITheoremPredicateRewriter
    {
        public MethodCallExpression Rewrite(MethodCallExpression call) => call;
    }

    private sealed class AppendConstraintRewriter : ITheoremGlobalRewriter
    {
        public IEnumerable<LambdaExpression> Rewrite(IEnumerable<LambdaExpression> constraints)
        {
            Expression<Func<GloballyRewrittenEnvironment, bool>> extra = t => t.Second == 99;

            return constraints.Concat<LambdaExpression>([extra]);
        }
    }

    private sealed class DropAllConstraintsRewriter : ITheoremGlobalRewriter
    {
        public IEnumerable<LambdaExpression> Rewrite(IEnumerable<LambdaExpression> constraints)
        {
            Expression<Func<ConstraintDroppingEnvironment, bool>> replacement = t => t.First == 7;

            return [replacement];
        }
    }

    [TheoremGlobalRewriter(typeof(AppendConstraintRewriter))]
    private sealed class GloballyRewrittenEnvironment
    {
        public int First { get; set; }

        public int Second { get; set; }
    }

    [TheoremGlobalRewriter(typeof(DropAllConstraintsRewriter))]
    private sealed class ConstraintDroppingEnvironment
    {
        public int First { get; set; }
    }

    [TheoremGlobalRewriter(typeof(NotARewriter))]
    private sealed class BadlyRewrittenEnvironment
    {
        public int First { get; set; }
    }

    private sealed class PlainEnvironment
    {
        public int First { get; set; }

        public int Second { get; set; }
    }
}
