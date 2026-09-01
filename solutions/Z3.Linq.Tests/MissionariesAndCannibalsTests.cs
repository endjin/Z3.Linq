namespace Z3.Linq.Tests;

using Z3.Linq.Examples.RiverCrossing;

/// <summary>
/// Acceptance tests for the Missionaries and Cannibals sample: a planning problem expressed as a
/// theorem over two integer arrays, one entry per step.
/// </summary>
/// <remarks>
/// <para>
/// This is the sample that exercises collection symbols at scale - the theorem is built by
/// looping over <c>maxLength</c> and adding several constraints per step, so the constraint
/// count grows with the bound rather than being fixed.
/// </para>
/// <para>
/// <c>maxLength</c> is the search bound, not the answer. Measured: 11 and 12 are unsatisfiable,
/// and 13 upwards all yield the same optimum of 12. The tests use 15 - comfortably past the
/// boundary, and far cheaper than the 50 the demo uses, which costs 764ms for a plain solve
/// against 112ms at 15.
/// </para>
/// </remarks>
[TestClass]
public class MissionariesAndCannibalsTests
{
    private const int Population = 3;
    private const int BoatSize = 2;

    /// <summary>The measured optimum: everyone crosses in 12 states.</summary>
    private const int OptimalLength = 12;

    [TestMethod]
    public void Optimize_ClassicPuzzle_FindsTheShortestCrossing()
    {
        // Arrange: three missionaries, three cannibals, a boat holding two. The shortest
        // solution is a known property of the puzzle rather than an artefact of the search
        // bound, so it is safe to assert exactly.
        using var context = new Z3Context();

        // Act
        var result = (from t in MissionariesAndCannibals.Create(context, 15)
                      where t.MissionaryAndCannibalCount == Population
                      where t.SizeBoat == BoatSize
                      orderby t.Length
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBe(OptimalLength);
    }

    [TestMethod]
    [DataRow(13, DisplayName = "Smallest feasible bound")]
    [DataRow(15, DisplayName = "Comfortably past the boundary")]
    [DataRow(50, DisplayName = "The bound the demo uses")]
    public void Optimize_WithDifferentSearchBounds_FindsTheSameOptimum(int maxLength)
    {
        // Arrange: the optimum must not depend on how much slack the search is given. This is
        // what makes the reduced bound in the other tests legitimate rather than a shortcut.
        using var context = new Z3Context();

        // Act
        var result = (from t in MissionariesAndCannibals.Create(context, maxLength)
                      where t.MissionaryAndCannibalCount == Population
                      where t.SizeBoat == BoatSize
                      orderby t.Length
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBe(OptimalLength);
    }

    [TestMethod]
    [DataRow(11, DisplayName = "Bound below the optimum")]
    [DataRow(12, DisplayName = "Bound equal to the optimum")]
    public void Solve_WithSearchBoundBelowTheOptimum_ReturnsNull(int maxLength)
    {
        // Arrange: the goal requires Length < maxLength, so a bound equal to the optimum is one
        // short. Pinning both sides of the boundary documents that off-by-one, which is easy to
        // trip over when picking a bound.
        using var context = new Z3Context();

        // Act
        var result = (from t in MissionariesAndCannibals.Create(context, maxLength)
                      where t.MissionaryAndCannibalCount == Population
                      where t.SizeBoat == BoatSize
                      select t).Solve();

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void Solve_ClassicPuzzle_ProducesAValidCrossing()
    {
        // Arrange: a plain solve returns some crossing rather than the shortest, so the answer
        // is checked against the rules of the puzzle instead of a fixed length.
        using var context = new Z3Context();

        // Act
        var result = (from t in MissionariesAndCannibals.Create(context, 15)
                      where t.MissionaryAndCannibalCount == Population
                      where t.SizeBoat == BoatSize
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        AssertIsValidCrossing(result);
    }

    [TestMethod]
    public void Optimize_ClassicPuzzle_ProducesAValidCrossingAtTheOptimum()
    {
        // Arrange: the optimal answer must also be a legal one. Without this, an optimiser that
        // reached length 12 by violating the safety rule would satisfy the length assertion.
        using var context = new Z3Context();

        // Act
        var result = (from t in MissionariesAndCannibals.Create(context, 15)
                      where t.MissionaryAndCannibalCount == Population
                      where t.SizeBoat == BoatSize
                      orderby t.Length
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBe(OptimalLength);
        AssertIsValidCrossing(result);
    }

    [TestMethod]
    public void Optimize_WithABoatHoldingThree_CrossesInFewerSteps()
    {
        // Arrange: a bigger boat must not make the puzzle harder. Varying an input the sample
        // exposes checks the theorem is genuinely parameterised rather than solving one fixed
        // instance.
        using var context = new Z3Context();

        // Act
        var result = (from t in MissionariesAndCannibals.Create(context, 15)
                      where t.MissionaryAndCannibalCount == Population
                      where t.SizeBoat == 3
                      orderby t.Length
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBeLessThan(OptimalLength);
        AssertIsValidCrossing(result, boatSize: 3);
    }

    /// <summary>
    /// Asserts the rules of the puzzle: everyone starts on the near bank, everyone ends on the
    /// far bank, the boat capacity is respected, and missionaries are never outnumbered on
    /// either bank.
    /// </summary>
    private static void AssertIsValidCrossing(
        MissionariesAndCannibals result,
        int boatSize = BoatSize)
    {
        result.Missionaries.Length.ShouldBe(result.Length);
        result.Cannibals.Length.ShouldBe(result.Length);

        // Everyone starts on the near bank...
        result.Missionaries[0].ShouldBe(Population);
        result.Cannibals[0].ShouldBe(Population);

        // ...and nobody is left on it at the end.
        result.Missionaries[result.Length - 1].ShouldBe(0);
        result.Cannibals[result.Length - 1].ShouldBe(0);

        for (int step = 0; step < result.Length; step++)
        {
            int nearMissionaries = result.Missionaries[step];
            int nearCannibals = result.Cannibals[step];

            nearMissionaries.ShouldBeInRange(0, Population, $"missionaries at step {step}");
            nearCannibals.ShouldBeInRange(0, Population, $"cannibals at step {step}");

            // Missionaries are safe on a bank only if there are none of them, or they are not
            // outnumbered. Both banks have to hold.
            if (nearMissionaries > 0)
            {
                nearMissionaries.ShouldBeGreaterThanOrEqualTo(
                    nearCannibals, $"near bank at step {step}");
            }

            int farMissionaries = Population - nearMissionaries;
            int farCannibals = Population - nearCannibals;

            if (farMissionaries > 0)
            {
                farMissionaries.ShouldBeGreaterThanOrEqualTo(
                    farCannibals, $"far bank at step {step}");
            }

            // Each crossing moves at least one person, at most a boatful, and in the direction
            // the boat is actually travelling. The sample alternates by step parity - from an
            // even step the boat leaves the near bank, from an odd step it returns - so checking
            // only the magnitude would accept a plan that crossed twice the same way without
            // the boat ever coming back.
            if (step > 0)
            {
                int delta = (nearMissionaries + nearCannibals)
                    - (result.Missionaries[step - 1] + result.Cannibals[step - 1]);

                if ((step - 1) % 2 == 0)
                {
                    // Boat leaves the near bank, so its population falls.
                    delta.ShouldBeInRange(-boatSize, -1, $"crossing into step {step}");
                }
                else
                {
                    // Boat returns, so its population rises.
                    delta.ShouldBeInRange(1, boatSize, $"return into step {step}");
                }
            }
        }
    }
}
