namespace Z3.Linq.Tests;

/// <summary>
/// The soft constraints of <see cref="Theorem{T}.SoftWhere"/>: weighted MaxSAT.
/// </summary>
/// <remarks>
/// A soft constraint is not required to hold. When it conflicts with the hard constraints or with a
/// heavier soft constraint, the optimizer leaves it unsatisfied and pays its weight, minimising the
/// total weight it leaves unsatisfied. Without it the DSL had only hard constraints, so "satisfy
/// this if you can, but prefer that one" could not be written at all: a violated constraint simply
/// made the theorem unsatisfiable.
/// </remarks>
[TestClass]
public class AssertSoftMaxSatTests
{
    /// <summary>Two flags, which a hard XOR forces apart.</summary>
    public class Flags
    {
        /// <summary>Gets or sets the first flag.</summary>
        public bool A { get; set; }

        /// <summary>Gets or sets the second flag.</summary>
        public bool B { get; set; }
    }

    /// <summary>One integer, for the numeric form.</summary>
    public class Cell
    {
        /// <summary>Gets or sets the value.</summary>
        public int X { get; set; }
    }

    [TestMethod]
    public void HigherWeightSoftConstraint_WinsConflict()
    {
        // The hard XOR admits exactly one of A and B, so the two soft constraints cannot both hold
        // and the weights decide which one is sacrificed.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Flags>()
            .Where(f => f.A ^ f.B)
            .SoftWhere(f => f.A, weight: 10)
            .SoftWhere(f => f.B, weight: 1)
            .Solve();

        result.ShouldNotBeNull();
        result!.A.ShouldBeTrue();
        result.B.ShouldBeFalse();
    }

    [TestMethod]
    public void FlippingWeights_FlipsOutcome()
    {
        // The same theorem with the weights swapped: the outcome follows the weight, not the order
        // the soft constraints were declared in.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Flags>()
            .Where(f => f.A ^ f.B)
            .SoftWhere(f => f.A, weight: 1)
            .SoftWhere(f => f.B, weight: 10)
            .Solve();

        result.ShouldNotBeNull();
        result!.A.ShouldBeFalse();
        result.B.ShouldBeTrue();
    }

    [TestMethod]
    public void CompatibleSoftConstraint_IsSatisfied()
    {
        // Nothing competes with it, so sacrificing it would buy nothing.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Flags>()
            .Where(f => f.A ^ f.B)
            .SoftWhere(f => f.A, weight: 5)
            .Solve();

        result.ShouldNotBeNull();
        result!.A.ShouldBeTrue();
    }

    [TestMethod]
    public void ImpossibleSoftConstraint_DoesNotMakeProblemUnsat()
    {
        // A soft constraint that contradicts a hard fact is paid for, and a model is still
        // returned: this is the property that makes the constraint soft.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Flags>()
            .Where(f => f.A)
            .SoftWhere(f => !f.A, weight: 100)
            .Solve();

        result.ShouldNotBeNull();
        result!.A.ShouldBeTrue();
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-3)]
    public void NonPositiveWeight_Throws(int weight)
    {
        // Z3 takes an unsigned weight, and a weight of zero would mean "free to violate" - a
        // programming error rather than a trade-off, so it fails at the call rather than on solve.
        using var ctx = new Z3Context();

        Should.Throw<ArgumentOutOfRangeException>(
            () => ctx.NewTheorem<Flags>().SoftWhere(f => f.A, weight));
    }

    [TestMethod]
    public void NumericSoftConstraints_HigherWeightTargetWins()
    {
        // Two values wanted by constraints of different weight, over the same symbol.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Cell>()
            .Where(c => c.X >= 0 && c.X <= 9)
            .SoftWhere(c => c.X == 3, weight: 2)
            .SoftWhere(c => c.X == 7, weight: 5)
            .Solve();

        result.ShouldNotBeNull();
        result!.X.ShouldBe(7);
    }

    [TestMethod]
    public void SoftWhereThenWhere_KeepsTheSoftConstraint()
    {
        // A hard constraint added after a soft one must not drop it: the two lists travel separately
        // through the Where chain, and losing one there would silently turn a trade-off into a
        // requirement.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Flags>()
            .SoftWhere(f => f.A, weight: 10)
            .SoftWhere(f => f.B, weight: 1)
            .Where(f => f.A ^ f.B)
            .Solve();

        result.ShouldNotBeNull();
        result!.A.ShouldBeTrue();
        result.B.ShouldBeFalse();
    }
}
