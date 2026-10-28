namespace Z3.Linq.Examples.Scheduling;

using System.Linq;

/// <summary>
/// A job-shop schedule: a start time for every task, and the makespan - the time by which every
/// job is finished.
/// </summary>
/// <remarks>
/// <para>
/// A job is a fixed sequence of tasks, and each task needs one machine for a whole number of
/// time units. A task cannot start until the previous task of its job has finished, and a
/// machine works on one task at a time. Ordering by <see cref="Makespan"/> asks Z3 for the
/// shortest schedule.
/// </para>
/// <para>
/// The tasks of all jobs are numbered in one sequence: job 0's tasks first, in order, then job
/// 1's, and so on. <see cref="Tasks"/> gives that numbering, and <see cref="Start"/> is indexed
/// by it.
/// </para>
/// <para>
/// Adapted from "SAT/SMT by Example" by Dennis Yurichev, section 22.8, https://smt.st. The
/// instance in <see cref="ThreeJobs"/> is the book's small example; the shortest schedule for it
/// takes 11 time units.
/// </para>
/// </remarks>
public class JobShop
{
    /// <summary>
    /// Gets the book's small instance: three jobs on three machines. Each task is a machine number
    /// and a duration.
    /// </summary>
    public static (int Machine, int Duration)[][] ThreeJobs { get; } =
    [
        [(0, 3), (1, 2), (2, 2)],
        [(0, 2), (2, 1), (1, 4)],
        [(1, 4), (2, 3)],
    ];

    /// <summary>
    /// Gets or sets the start time of each task, indexed as in <see cref="Tasks"/>.
    /// </summary>
    public int[] Start { get; set; } = [];

    /// <summary>
    /// Gets or sets the time by which every task has finished.
    /// </summary>
    public int Makespan { get; set; }

    /// <summary>
    /// Lists the tasks of all jobs in one sequence, which is the index order of
    /// <see cref="Start"/>.
    /// </summary>
    /// <param name="jobs">The jobs; each is a sequence of (machine, duration) tasks.</param>
    /// <returns>Every task with its job number and its position in that job.</returns>
    public static (int Job, int Step, int Machine, int Duration)[] Tasks((int Machine, int Duration)[][] jobs)
        => jobs
            .SelectMany((job, jobIndex) => job.Select((task, step) => (Job: jobIndex, Step: step, task.Machine, task.Duration)))
            .ToArray();

    /// <summary>
    /// Creates a theorem whose solutions are the valid schedules for some jobs.
    /// </summary>
    /// <param name="context">The Z3 context that owns the theorem.</param>
    /// <param name="jobs">The jobs; each is a sequence of (machine, duration) tasks.</param>
    /// <returns>
    /// A theorem that constrains every task to start at or after time zero and finish by the
    /// makespan, to follow the previous task of its job, and not to overlap any other task on its
    /// machine. Add <c>orderby t.Makespan</c> for the shortest schedule.
    /// </returns>
    public static Theorem<JobShop> Create(Z3Context context, (int Machine, int Duration)[][] jobs)
    {
        var tasks = Tasks(jobs);
        var theorem = context.NewTheorem(new JobShop { Start = new int[tasks.Length] });

        for (int i = 0; i < tasks.Length; i++)
        {
            int task = i;
            int duration = tasks[i].Duration;
            theorem = theorem.Where(t => t.Start[task] >= 0 && t.Start[task] + duration <= t.Makespan);

            // Tasks are numbered job by job, so the previous task of the same job is i - 1.
            if (tasks[i].Step > 0)
            {
                int previous = i - 1;
                int previousDuration = tasks[previous].Duration;
                theorem = theorem.Where(t => t.Start[task] >= t.Start[previous] + previousDuration);
            }
        }

        for (int i = 0; i < tasks.Length; i++)
        {
            for (int j = i + 1; j < tasks.Length; j++)
            {
                if (tasks[i].Machine != tasks[j].Machine)
                {
                    continue;
                }

                int a = i;
                int b = j;
                int durationA = tasks[i].Duration;
                int durationB = tasks[j].Duration;

                // One of the two tasks must finish before the other starts.
                theorem = theorem.Where(t => t.Start[a] + durationA <= t.Start[b] || t.Start[b] + durationB <= t.Start[a]);
            }
        }

        return theorem;
    }
}
