namespace Z3.Linq.Tests;

using Z3.Linq.Examples.Scheduling;

/// <summary>
/// Acceptance tests for the job-shop scheduling sample.
/// </summary>
/// <remarks>
/// <para>
/// The book's three-job instance ("SAT/SMT by Example", section 22.8) has a shortest schedule of
/// 11 time units, and several schedules reach it, so the tests pin the makespan and check the
/// returned start times against the rules in C# instead of pinning them. That 11 is optimal, and
/// not an artefact of the search, is shown separately: asking for a makespan of 10 or less is
/// unsatisfiable.
/// </para>
/// <para>
/// Two small instances whose answer is plain arithmetic check each rule on its own: a single job
/// is the sum of its durations (precedence), and two one-task jobs on one machine are the sum of
/// both (no overlap). Measured: each solve takes tens of milliseconds.
/// </para>
/// </remarks>
[TestClass]
public class JobShopTests
{
    [TestMethod]
    public void Solve_BookInstanceOrderedByMakespan_FindsElevenUnits()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = (from t in JobShop.Create(context, JobShop.ThreeJobs)
                      orderby t.Makespan
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Makespan.ShouldBe(11);
        AssertIsValidSchedule(result, JobShop.ThreeJobs);
    }

    [TestMethod]
    public void TrySolve_BookInstanceWithinTenUnits_IsUnsatisfiable()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool satisfiable = JobShop.Create(context, JobShop.ThreeJobs)
            .Where(t => t.Makespan <= 10)
            .TrySolve(out _);

        // Assert
        satisfiable.ShouldBeFalse();
    }

    [TestMethod]
    public void Solve_SingleJobOrderedByMakespan_TakesTheSumOfItsDurations()
    {
        // Arrange: one job, each task on its own machine - only precedence limits it.
        using var context = new Z3Context();
        (int Machine, int Duration)[][] jobs = [[(0, 3), (1, 2), (2, 4)]];

        // Act
        var result = (from t in JobShop.Create(context, jobs)
                      orderby t.Makespan
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Makespan.ShouldBe(9);
        result.Start.ShouldBe([0, 3, 5]);
    }

    [TestMethod]
    public void Solve_TwoJobsSharingOneMachine_RunOneAfterTheOther()
    {
        // Arrange: two independent one-task jobs - only the shared machine limits them.
        using var context = new Z3Context();
        (int Machine, int Duration)[][] jobs = [[(0, 3)], [(0, 4)]];

        // Act
        var result = (from t in JobShop.Create(context, jobs)
                      orderby t.Makespan
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Makespan.ShouldBe(7);
        AssertIsValidSchedule(result, jobs);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "No jobs")]
    [DataRow(2, DisplayName = "Two jobs with no tasks")]
    public void Solve_NoTasksOrderedByMakespan_TakesNoTime(int jobCount)
    {
        // Arrange: no task constrains the makespan, so only its own lower bound holds it at zero.
        using var context = new Z3Context();
        var jobs = Enumerable.Repeat<(int Machine, int Duration)[]>([], jobCount).ToArray();

        // Act
        var result = (from t in JobShop.Create(context, jobs)
                      orderby t.Makespan
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Makespan.ShouldBe(0);
        result.Start.ShouldBeEmpty();
    }

    [TestMethod]
    public void Tasks_BookInstance_NumbersTasksJobByJob()
    {
        // Arrange, Act
        var tasks = JobShop.Tasks(JobShop.ThreeJobs);

        // Assert
        tasks.Length.ShouldBe(8);
        tasks.Select(task => task.Job).ShouldBe([0, 0, 0, 1, 1, 1, 2, 2]);
        tasks.Select(task => task.Step).ShouldBe([0, 1, 2, 0, 1, 2, 0, 1]);
    }

    /// <summary>
    /// Asserts the three scheduling rules: every task runs between time zero and the makespan,
    /// follows the previous task of its job, and does not overlap another task on its machine.
    /// </summary>
    private static void AssertIsValidSchedule(JobShop schedule, (int Machine, int Duration)[][] jobs)
    {
        var tasks = JobShop.Tasks(jobs);
        schedule.Start.Length.ShouldBe(tasks.Length);

        for (int i = 0; i < tasks.Length; i++)
        {
            schedule.Start[i].ShouldBeGreaterThanOrEqualTo(0, $"task {i} starts before zero");
            (schedule.Start[i] + tasks[i].Duration).ShouldBeLessThanOrEqualTo(schedule.Makespan, $"task {i} ends after the makespan");

            if (tasks[i].Step > 0)
            {
                schedule.Start[i].ShouldBeGreaterThanOrEqualTo(
                    schedule.Start[i - 1] + tasks[i - 1].Duration,
                    $"task {i} starts before job {tasks[i].Job} step {tasks[i].Step - 1} ends");
            }

            for (int j = i + 1; j < tasks.Length; j++)
            {
                if (tasks[i].Machine == tasks[j].Machine)
                {
                    bool apart = schedule.Start[i] + tasks[i].Duration <= schedule.Start[j]
                              || schedule.Start[j] + tasks[j].Duration <= schedule.Start[i];
                    apart.ShouldBeTrue($"tasks {i} and {j} overlap on machine {tasks[i].Machine}");
                }
            }
        }
    }
}
