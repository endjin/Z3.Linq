namespace Z3.Linq;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

/// <summary>
/// Strongly-typed theorem type for use with LINQ syntax.
/// </summary>
/// <typeparam name="T">Environment type over which the theorem is defined.</typeparam>
public class Theorem<T> : Theorem, ISolveable<T>
{
    /// <summary>
    /// Creates a new theorem for the given Z3 context.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    internal Theorem(Z3Context context)
        : base(context)
    {
    }

    /// <summary>
    /// Creates a new pre-constrained theorem for the given Z3 context.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="constraints">Constraints to apply to the created theorem.</param>
    internal Theorem(Z3Context context, IEnumerable<LambdaExpression> constraints)
        : base(context, constraints)
    {
    }

    /// <summary>
    /// Solves the theorem.
    /// </summary>
    /// <returns>
    /// Environment type instance with properties set to theorem-satisfying values, or
    /// <c>default(T)</c> if the theorem cannot be satisfied.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Where <typeparamref name="T"/> is a value type - a value tuple, a struct, a record struct -
    /// <c>default(T)</c> is a populated instance with every symbol zero, which is also a perfectly
    /// good solution to some theorems. The two cannot be told apart here, and the result cannot
    /// even be compared against <see langword="null"/>. Use <see cref="TrySolve(out T)"/>, or
    /// <see cref="SolveableExtensions.SolveOrNull{TEnvironment}(ISolveable{TEnvironment})"/>, when
    /// the difference matters. See #57.
    /// </para>
    /// <para>
    /// A symbol that no constraint mentions is free: the theorem is still satisfiable, and Z3
    /// assigns the symbol an arbitrary value. Only the symbols the constraints pin down are
    /// determined by the query.
    /// </para>
    /// <para>
    /// A <see cref="DateTime"/> symbol travels through Z3 as an instant on the UTC timeline and
    /// comes back with <see cref="DateTimeKind.Utc"/>, whatever kind the constants constraining
    /// it had. A constant whose kind is <see cref="DateTimeKind.Unspecified"/> is read as UTC
    /// rather than as local time. The result is therefore the same on every machine; a caller
    /// working in local time needs <see cref="DateTime.ToLocalTime"/> on the way out.
    /// </para>
    /// </remarks>
    public T? Solve()
    {
        return base.Solve<T>();
    }

    /// <summary>
    /// Solves the theorem, reporting satisfiability separately from the solution.
    /// </summary>
    /// <param name="result">The solution, when the theorem could be satisfied.</param>
    /// <returns>
    /// <see langword="true"/> if the theorem was satisfiable; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// The only form that answers "is there a solution?" for every environment type. What is
    /// written into <paramref name="result"/> when this returns <see langword="true"/> is exactly
    /// what <see cref="Solve"/> would have returned.
    /// </remarks>
    public bool TrySolve([MaybeNullWhen(false)] out T result)
    {
        return base.TrySolve(out result);
    }

    /// <summary>
    /// Finds an optimal solution.
    /// </summary>
    /// <typeparam name="TResult">Type of the value being optimized.</typeparam>
    /// <param name="direction">The optimization goal, i.e. whether to minimize or maximize the solution.</param>
    /// <param name="lambda">Expression representing the value to minimize or maximize.</param>
    /// <returns>
    /// Environment type instance with properties set to theorem-satisfying values, or
    /// <c>default(T)</c> if the theorem cannot be satisfied.
    /// </returns>
    /// <remarks>
    /// Carries the same ambiguity as <see cref="Solve"/>, and answers it the same way - through
    /// <see cref="TryOptimize{TResult}(Optimization, Expression{Func{T, TResult}}, out T)"/>. As
    /// with <see cref="Solve"/>, a symbol that no constraint mentions is free and takes an
    /// arbitrary value; the objective determines only what it is written in terms of.
    /// </remarks>
    public T? Optimize<TResult>(Optimization direction, Expression<Func<T, TResult>> lambda)
    {
        return base.Optimize<T, TResult>(direction, lambda);
    }

    /// <summary>
    /// Finds an optimal solution, reporting satisfiability separately from the solution.
    /// </summary>
    /// <typeparam name="TResult">Type of the value being optimized.</typeparam>
    /// <param name="direction">The optimization goal, i.e. whether to minimize or maximize the solution.</param>
    /// <param name="lambda">Expression representing the value to minimize or maximize.</param>
    /// <param name="result">The optimal solution, when the theorem could be satisfied.</param>
    /// <returns>
    /// <see langword="true"/> if the theorem was satisfiable; otherwise <see langword="false"/>.
    /// </returns>
    public bool TryOptimize<TResult>(
        Optimization direction,
        Expression<Func<T, TResult>> lambda,
        [MaybeNullWhen(false)] out T result)
    {
        return base.TryOptimize(direction, lambda, out result);
    }

    /// <summary>
    /// Where query operator, used to add constraints to the theorem.
    /// </summary>
    /// <param name="constraint">Theorem constraint expression.</param>
    /// <returns>Theorem with the new constraint applied.</returns>
    public Theorem<T> Where(Expression<Func<T, bool>> constraint)
    {
        return new Theorem<T>(base.Context, base.Constraints.Concat(new List<LambdaExpression> { constraint }));
    }

    /// <summary>
    /// OrderBy query operator, used to optimize a solution using query expression syntax.
    /// </summary>
    /// <typeparam name="TResult">Type of the value being minimized.</typeparam>
    /// <param name="lambda">Expression representing the value to minimize.</param>
    /// <returns>
    /// A deferred minimization. Nothing reaches Z3 until <see cref="ISolveable{T}.Solve"/> or
    /// <see cref="ISolveable{T}.TrySolve(out T)"/> is called on the result.
    /// </returns>
    public ISolveable<T> OrderBy<TResult>(Expression<Func<T, TResult>> lambda)
        => new DeferredSolvable(() =>
            TryOptimize(Optimization.Minimize, lambda, out T? solution) ? (true, solution) : (false, default));

    /// <summary>
    /// OrderByDescending query operator, used to optimize a solution using query expression syntax.
    /// </summary>
    /// <typeparam name="TResult">Type of the value being maximized.</typeparam>
    /// <param name="lambda">Expression representing the value to maximize.</param>
    /// <returns>
    /// A deferred maximization. Nothing reaches Z3 until <see cref="ISolveable{T}.Solve"/> or
    /// <see cref="ISolveable{T}.TrySolve(out T)"/> is called on the result.
    /// </returns>
    public ISolveable<T> OrderByDescending<TResult>(Expression<Func<T, TResult>> lambda)
        => new DeferredSolvable(() =>
            TryOptimize(Optimization.Maximize, lambda, out T? solution) ? (true, solution) : (false, default));

    /// <summary>
    /// An optimisation that has not run yet.
    /// </summary>
    /// <remarks>
    /// The deferred call carries satisfiability alongside the solution rather than returning the
    /// solution alone, because for a value-type environment the solution on its own cannot say
    /// whether there was one. See #57.
    /// </remarks>
    private sealed class DeferredSolvable : ISolveable<T>
    {
        private readonly Func<(bool Satisfiable, T? Solution)> optimize;

        public DeferredSolvable(Func<(bool Satisfiable, T? Solution)> optimize)
        {
            this.optimize = optimize;
        }

        public T? Solve() => this.optimize().Solution;

        public bool TrySolve([MaybeNullWhen(false)] out T result)
        {
            (bool satisfiable, T? solution) = this.optimize();
            result = solution!;
            return satisfiable;
        }
    }
}
