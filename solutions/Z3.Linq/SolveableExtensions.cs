namespace Z3.Linq;

using System;
using System.Linq.Expressions;

/// <summary>
/// Solve and optimize forms that lift a value-type environment to <see cref="Nullable{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// For a reference-type environment, <c>Solve</c> already returns <see langword="null"/> when a
/// theorem cannot be satisfied and nothing here is needed. For a value type it returns
/// <c>default(T)</c> - a populated all-zero instance that cannot be told apart from a solution in
/// which every symbol is zero, and cannot be compared against <see langword="null"/> at all. These
/// methods give that case the same shape the reference-type case already has. See #57.
/// </para>
/// <para>
/// They are extension methods rather than members because the <c>struct</c> constraint that makes
/// <c>T?</c> mean <see cref="Nullable{T}"/> cannot be applied to a member of <c>Theorem&lt;T&gt;</c>,
/// whose <c>T</c> is unconstrained.
/// </para>
/// </remarks>
public static class SolveableExtensions
{
    /// <summary>
    /// Solves the theorem, returning <see langword="null"/> if it cannot be satisfied.
    /// </summary>
    /// <typeparam name="TEnvironment">Value-type environment over which the theorem is defined.</typeparam>
    /// <param name="solveable">The theorem, or a deferred optimisation over one.</param>
    /// <returns>The solution, or <see langword="null"/> if the theorem cannot be satisfied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="solveable"/> is null.</exception>
    public static TEnvironment? SolveOrNull<TEnvironment>(this ISolveable<TEnvironment> solveable)
        where TEnvironment : struct
    {
        ArgumentNullException.ThrowIfNull(solveable);

        return solveable.TrySolve(out TEnvironment solution) ? solution : null;
    }

    /// <summary>
    /// Finds an optimal solution, returning <see langword="null"/> if the theorem cannot be
    /// satisfied.
    /// </summary>
    /// <typeparam name="TEnvironment">Value-type environment over which the theorem is defined.</typeparam>
    /// <typeparam name="TResult">Type of the value being optimized.</typeparam>
    /// <param name="theorem">The theorem to optimize over.</param>
    /// <param name="direction">The optimization goal, i.e. whether to minimize or maximize the solution.</param>
    /// <param name="lambda">Expression representing the value to minimize or maximize.</param>
    /// <returns>The optimal solution, or <see langword="null"/> if the theorem cannot be satisfied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theorem"/> is null.</exception>
    public static TEnvironment? OptimizeOrNull<TEnvironment, TResult>(
        this Theorem<TEnvironment> theorem,
        Optimization direction,
        Expression<Func<TEnvironment, TResult>> lambda)
        where TEnvironment : struct
    {
        ArgumentNullException.ThrowIfNull(theorem);

        return theorem.TryOptimize(direction, lambda, out TEnvironment solution) ? solution : null;
    }
}
