namespace Z3.Linq.Tests;

/// <summary>
/// Tests that solve the assignment problem from "SAT/SMT by Example" (Dennis Yurichev, section
/// 14.5, https://smt.st): give each of three workers a different job at the least total cost.
/// </summary>
/// <remarks>
/// <para>
/// The model is restated here from <c>demos/assignment.cs</c>, as the oil-purchase linear program
/// is in <see cref="OptimizationTests"/>: it is small enough to read in one go.
/// </para>
/// <para>
/// There are only 3! = 6 assignments, so the expected answer is checked against a brute-force
/// search in C# as well as pinned. The optimum - worker 0 on job 1, worker 1 on job 2, worker 2
/// on job 0, for 950 - is unique: every other assignment costs at least 1000. Each test takes a
/// few tens of milliseconds.
/// </para>
/// </remarks>
[TestClass]
public class AssignmentProblemTests
{
    // Rows are workers, columns are jobs.
    private static readonly int[,] Costs =
    {
        { 250, 400, 350 },
        { 400, 600, 350 },
        { 200, 400, 250 },
    };

    [TestMethod]
    public void Optimize_ThreeWorkersThreeJobs_FindsTheUniqueCheapestAssignment()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem<(int w0, int w1, int w2)>()
                      where t.w0 >= 0 && t.w0 <= 2 && t.w1 >= 0 && t.w1 <= 2 && t.w2 >= 0 && t.w2 <= 2
                      where Z3Methods.Distinct(t.w0, t.w1, t.w2)
                      orderby (t.w0 == 0 ? 250 : t.w0 == 1 ? 400 : 350)
                            + (t.w1 == 0 ? 400 : t.w1 == 1 ? 600 : 350)
                            + (t.w2 == 0 ? 200 : t.w2 == 1 ? 400 : 250)
                      select t).SolveOrNull();

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe((1, 2, 0));
        (Costs[0, result.Value.w0] + Costs[1, result.Value.w1] + Costs[2, result.Value.w2]).ShouldBe(950);
    }

    [TestMethod]
    public void BruteForce_EveryAssignment_ConfirmsTheOptimumIsUnique()
    {
        // Arrange: the six permutations of the three jobs, checked in C# with no solver. This pins
        // the expected answer independently of Z3.
        int[][] permutations = [[0, 1, 2], [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0]];

        // Act
        var costs = permutations
            .Select(p => (Assignment: p, Cost: Costs[0, p[0]] + Costs[1, p[1]] + Costs[2, p[2]]))
            .OrderBy(x => x.Cost)
            .ToArray();

        // Assert
        costs[0].Assignment.ShouldBe([1, 2, 0]);
        costs[0].Cost.ShouldBe(950);
        costs[1].Cost.ShouldBeGreaterThan(950);
    }

    [TestMethod]
    public void TrySolve_CheaperThanTheOptimum_IsUnsatisfiable()
    {
        // Arrange: no assignment costs less than 950, so asking for 949 or less must fail.
        using var context = new Z3Context();
        var theorem = from t in context.NewTheorem<(int w0, int w1, int w2)>()
                      where t.w0 >= 0 && t.w0 <= 2 && t.w1 >= 0 && t.w1 <= 2 && t.w2 >= 0 && t.w2 <= 2
                      where Z3Methods.Distinct(t.w0, t.w1, t.w2)
                      where (t.w0 == 0 ? 250 : t.w0 == 1 ? 400 : 350)
                          + (t.w1 == 0 ? 400 : t.w1 == 1 ? 600 : 350)
                          + (t.w2 == 0 ? 200 : t.w2 == 1 ? 400 : 250) < 950
                      select t;

        // Act
        bool found = theorem.TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }
}
