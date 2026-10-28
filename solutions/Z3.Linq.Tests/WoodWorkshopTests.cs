namespace Z3.Linq.Tests;

/// <summary>
/// Tests that solve Kantorovich's wood-workshop problem from "SAT/SMT by Example" (Dennis
/// Yurichev, section 3.3, https://smt.st): cut 6x13 workpieces so as to make 800 pieces of type A
/// and 400 of type B from as few workpieces as possible.
/// </summary>
/// <remarks>
/// <para>
/// The model is restated here from <c>demos/wood-workshop.cs</c>. Each cut pattern yields a fixed
/// number of A and B pieces: (3, 1), (2, 6), (1, 9) and (0, 13).
/// </para>
/// <para>
/// The optimum is 275 workpieces: 250 cut the first way and 25 the second. It is unique, and the
/// tests prove both halves of that claim with Z3: no plan uses 274 workpieces or fewer, and no
/// other plan uses 275. Each test takes a few tens of milliseconds.
/// </para>
/// </remarks>
[TestClass]
public class WoodWorkshopTests
{
    [TestMethod]
    public void Optimize_FewestWorkpieces_FindsTheUniqueCuttingPlan()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = (from t in CuttingPlans(context)
                      orderby t.cutA + t.cutB + t.cutC + t.cutD
                      select t).SolveOrNull();

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe((250, 25, 0, 0));

        // The plan must make exactly the pieces asked for.
        var (a, b, c, d) = result.Value;
        ((3 * a) + (2 * b) + c).ShouldBe(800);
        (a + (6 * b) + (9 * c) + (13 * d)).ShouldBe(400);
    }

    [TestMethod]
    public void TrySolve_FewerWorkpiecesThanTheOptimum_IsUnsatisfiable()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = CuttingPlans(context).Where(t => t.cutA + t.cutB + t.cutC + t.cutD < 275);

        // Act
        bool found = theorem.TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }

    [TestMethod]
    public void TrySolve_AnotherPlanWithTheOptimalCount_IsUnsatisfiable()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = CuttingPlans(context)
            .Where(t => t.cutA + t.cutB + t.cutC + t.cutD == 275)
            .Where(t => !(t.cutA == 250 && t.cutB == 25 && t.cutC == 0 && t.cutD == 0));

        // Act
        bool found = theorem.TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }

    private static Theorem<(int cutA, int cutB, int cutC, int cutD)> CuttingPlans(Z3Context context)
        => from t in context.NewTheorem<(int cutA, int cutB, int cutC, int cutD)>()
           where t.cutA >= 0 && t.cutB >= 0 && t.cutC >= 0 && t.cutD >= 0
           where (3 * t.cutA) + (2 * t.cutB) + t.cutC == 800
           where t.cutA + (6 * t.cutB) + (9 * t.cutC) + (13 * t.cutD) == 400
           select t;
}
