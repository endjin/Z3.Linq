namespace Z3.Linq
{
    using System.Diagnostics.CodeAnalysis;

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
        /// <returns>
        /// Environment type instance with properties set to theorem-satisfying values, or
        /// <c>default(T)</c> if the theorem cannot be satisfied.
        /// </returns>
        /// <remarks>
        /// For a value-type environment <c>default(T)</c> is a populated all-zero instance and
        /// cannot be told apart from a solution in which every symbol is zero. Use
        /// <see cref="TrySolve(out T)"/>, or <c>SolveOrNull</c>, where that matters. See #57.
        /// </remarks>
        T? Solve();

        /// <summary>
        /// Solves the theorem, reporting satisfiability separately from the solution.
        /// </summary>
        /// <param name="result">The solution, when the theorem could be satisfied.</param>
        /// <returns>
        /// <see langword="true"/> if the theorem was satisfiable; otherwise <see langword="false"/>.
        /// </returns>
        bool TrySolve([MaybeNullWhen(false)] out T result);
    }
}
