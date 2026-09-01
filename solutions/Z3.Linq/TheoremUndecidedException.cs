namespace Z3.Linq;

using System;

/// <summary>
/// Z3 stopped without deciding whether the theorem is satisfiable.
/// </summary>
/// <remarks>
/// <para>
/// Thrown when a solve or optimisation ends with Z3 reporting <c>unknown</c>: the
/// <see cref="Z3Context.Timeout"/> or <see cref="Z3Context.ResourceLimit"/> was reached, or Z3
/// gave up on a problem it cannot decide. The theorem has been proved neither satisfiable nor
/// unsatisfiable, which is why this is an exception rather than a <see langword="false"/> from
/// <c>TrySolve</c> - <see langword="false"/> means the theorem has no solution, and this means
/// nobody knows. See #57 and #85.
/// </para>
/// <para>
/// A solve cut short by a <see cref="System.Threading.CancellationToken"/> throws
/// <see cref="OperationCanceledException"/> instead, as everywhere else in .NET.
/// </para>
/// </remarks>
public class TheoremUndecidedException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="reason">Z3's own account of why it stopped.</param>
    public TheoremUndecidedException(string reason)
        : base($"Z3 could not decide whether the theorem is satisfiable ({reason}). A timeout or resource limit set on the Z3Context was reached, or Z3 gave up; the theorem has been proved neither satisfiable nor unsatisfiable.")
    {
        this.Reason = reason;
    }

    /// <summary>
    /// Gets Z3's own account of why it stopped - <c>timeout</c>, <c>canceled</c> and
    /// <c>interrupted</c> are the usual ones, and which of them a given limit produces differs
    /// between the solver and the optimiser.
    /// </summary>
    public string Reason { get; }
}
