namespace Z3.Linq.Tests;

/// <summary>
/// Tests for two knapsack-style problems from "SAT/SMT by Example" (Dennis Yurichev,
/// https://smt.st): the popsicle problem (section 10.1) and XKCD 287, "NP-Complete" (section 3.2).
/// </summary>
/// <remarks>
/// <para>
/// The models are restated here from <c>demos/knapsack.cs</c>.
/// </para>
/// <para>
/// Popsicles: with $8, single popsicles at $1, 3-packs at $2 and 5-packs at $3, the most
/// popsicles is 13 (one 3-pack and two 5-packs). That optimum is unique, and the tests prove it:
/// nothing gives 14, and nothing else gives 13.
/// </para>
/// <para>
/// XKCD 287: exactly two orders from the menu cost $15.05 - seven mixed fruit, or one mixed fruit,
/// two hot wings and one sampler plate. The test enumerates every solution by blocking each one
/// found, then asserts the set rather than the order Z3 found them in. Each test takes a few tens
/// of milliseconds.
/// </para>
/// </remarks>
[TestClass]
public class KnapsackTests
{
    [TestMethod]
    public void Optimize_EightDollarsOfPopsicles_BuysThirteen()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = (from t in Purchases(context)
                      orderby t.singles + (3 * t.threes) + (5 * t.fives) descending
                      select t).SolveOrNull();

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe((0, 1, 2));
        (result.Value.singles + (3 * result.Value.threes) + (5 * result.Value.fives)).ShouldBe(13);
    }

    [TestMethod]
    public void TrySolve_MorePopsiclesThanTheOptimum_IsUnsatisfiable()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = Purchases(context).Where(t => t.singles + (3 * t.threes) + (5 * t.fives) > 13);

        // Act
        bool found = theorem.TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }

    [TestMethod]
    public void TrySolve_AnotherWayToBuyThirteen_IsUnsatisfiable()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = Purchases(context)
            .Where(t => t.singles + (3 * t.threes) + (5 * t.fives) == 13)
            .Where(t => !(t.singles == 0 && t.threes == 1 && t.fives == 2));

        // Act
        bool found = theorem.TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }

    [TestMethod]
    public void TrySolve_EveryXkcdOrderCostingFifteenOhFive_FindsExactlyTwo()
    {
        // Arrange
        using var context = new Z3Context();
        var orders = from t in context.NewTheorem<(int fruit, int fries, int salad, int wings, int sticks, int plate)>()
                     where t.fruit >= 0 && t.fries >= 0 && t.salad >= 0 && t.wings >= 0 && t.sticks >= 0 && t.plate >= 0
                     where (215 * t.fruit) + (275 * t.fries) + (335 * t.salad) + (355 * t.wings) + (420 * t.sticks) + (580 * t.plate) == 1505
                     select t;

        var found = new List<(int fruit, int fries, int salad, int wings, int sticks, int plate)>();

        // Act: block each order once found, until none is left. The cap stops a regression that
        // failed to block from looping for ever.
        while (found.Count < 10 && orders.TrySolve(out var order))
        {
            found.Add(order);
            var seen = order;
            orders = orders.Where(t => !(t.fruit == seen.fruit && t.fries == seen.fries && t.salad == seen.salad
                                      && t.wings == seen.wings && t.sticks == seen.sticks && t.plate == seen.plate));
        }

        // Assert
        found.ShouldBe(
            [
                (7, 0, 0, 0, 0, 0),
                (1, 0, 0, 2, 0, 1),
            ],
            ignoreOrder: true);
    }

    private static Theorem<(int singles, int threes, int fives)> Purchases(Z3Context context)
        => from t in context.NewTheorem<(int singles, int threes, int fives)>()
           where t.singles >= 0 && t.threes >= 0 && t.fives >= 0
           where t.singles + (2 * t.threes) + (3 * t.fives) == 8
           select t;
}
