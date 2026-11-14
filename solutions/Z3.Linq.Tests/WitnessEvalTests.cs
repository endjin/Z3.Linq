namespace Z3.Linq.Tests;

/// <summary>
/// Witness generation through <c>Theorem&lt;T&gt;.Solve(inspect)</c>.
/// </summary>
/// <remarks>
/// A plain solve computes a model, populates the result object and then discards the model, so a
/// value that is not itself a member of the result type - the derived quantity <c>t.X + t.Y</c>, or
/// a relational fact - could not be read back at all. The callback threads the live model out, and
/// the witness evaluates arbitrary sub-expressions under the very model that produced the solution.
/// </remarks>
[TestClass]
public class WitnessEvalTests
{
    [TestMethod]
    public void Witness_EvaluatesDerivedQuantity_ConsistentWithSolution()
    {
        using var ctx = new Z3Context();

        int? witnessSum = null;

        var solution = ctx.NewTheorem<Pair>()
            .Where(p => p.X == 4)
            .Where(p => p.Y == 5)
            .Solve(w => witnessSum = w.Eval(p => p.X + p.Y));

        solution.ShouldNotBeNull();
        solution!.X.ShouldBe(4);
        solution.Y.ShouldBe(5);
        witnessSum.ShouldNotBeNull();
        witnessSum!.Value.ShouldBe(9);

        // The witness and the solution members are read under the same model, so they agree.
        witnessSum.Value.ShouldBe(solution.X + solution.Y);
    }

    [TestMethod]
    public void Witness_EvaluatesBooleanFact()
    {
        using var ctx = new Z3Context();

        bool? xLessThanY = null;

        ctx.NewTheorem<Pair>()
            .Where(p => p.X == 2)
            .Where(p => p.Y == 7)
            .Solve(w => xLessThanY = w.Eval(p => p.X < p.Y));

        xLessThanY.ShouldNotBeNull();
        xLessThanY!.Value.ShouldBeTrue();
    }

    [TestMethod]
    public void Witness_NotInvoked_OnUnsat()
    {
        // There is no model to witness, so the callback must not run - an unconditional call would
        // read values Z3 never chose.
        using var ctx = new Z3Context();

        bool invoked = false;

        var solution = ctx.NewTheorem<Pair>()
            .Where(p => p.X == 1)
            .Where(p => p.X == 2)
            .Solve(_ => invoked = true);

        solution.ShouldBeNull();
        invoked.ShouldBeFalse();
    }

    /// <summary>A pair of integers.</summary>
    public class Pair
    {
        /// <summary>Gets or sets the first integer.</summary>
        public int X { get; set; }

        /// <summary>Gets or sets the second integer.</summary>
        public int Y { get; set; }
    }
}
