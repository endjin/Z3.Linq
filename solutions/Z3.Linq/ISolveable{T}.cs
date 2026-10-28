namespace Z3.Linq
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;

    /// <summary>
    /// Enables optimization constraints as expressed by OrderBy to be deferred, just like
    /// everything else.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    public interface ISolveable<T>
    {
        /// <summary>
        /// Solves the theorem.
        /// </summary>
        /// <param name="cancellationToken">A token that interrupts the solve.</param>
        /// <returns>
        /// Environment type instance with properties set to theorem-satisfying values, or
        /// <c>default(T)</c> if the theorem cannot be satisfied.
        /// </returns>
        /// <exception cref="TheoremUndecidedException">
        /// Z3 stopped without deciding: a limit on the <see cref="Z3Context"/> was reached, or it
        /// gave up. See #85.
        /// </exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        /// <remarks>
        /// For a value-type environment <c>default(T)</c> is a populated all-zero instance and
        /// cannot be told apart from a solution in which every symbol is zero. Use
        /// <see cref="TrySolve(out T, CancellationToken)"/>, or <c>SolveOrNull</c>, where that
        /// matters. See #57.
        /// </remarks>
        T? Solve(CancellationToken cancellationToken = default);

        /// <summary>
        /// Solves the theorem, reporting satisfiability separately from the solution.
        /// </summary>
        /// <param name="result">The solution, when the theorem could be satisfied.</param>
        /// <param name="cancellationToken">A token that interrupts the solve.</param>
        /// <returns>
        /// <see langword="true"/> if the theorem was satisfiable; otherwise <see langword="false"/>.
        /// A solve that stopped without deciding throws rather than returning
        /// <see langword="false"/>.
        /// </returns>
        /// <exception cref="TheoremUndecidedException">
        /// Z3 stopped without deciding: a limit on the <see cref="Z3Context"/> was reached, or it
        /// gave up. See #85.
        /// </exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        bool TrySolve([MaybeNullWhen(false)] out T result, CancellationToken cancellationToken = default);
    }
}
