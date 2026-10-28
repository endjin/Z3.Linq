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
    /// Creates a new theorem for the given Z3 context, from a template instance.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="template">
    /// An instance of <typeparamref name="T"/> whose collections supply a length to the
    /// solution's. See #78.
    /// </param>
    internal Theorem(Z3Context context, T template)
        : base(context, [], template)
    {
    }

    /// <summary>
    /// Creates a new pre-constrained theorem for the given Z3 context.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="constraints">Constraints to apply to the created theorem.</param>
    /// <param name="template">The template instance of the theorem being extended, or null.</param>
    internal Theorem(Z3Context context, IEnumerable<LambdaExpression> constraints, object? template)
        : base(context, constraints, template)
    {
    }

    /// <summary>
    /// Solves the theorem.
    /// </summary>
    /// <param name="cancellationToken">A token that interrupts the solve.</param>
    /// <returns>
    /// Environment type instance with properties set to theorem-satisfying values, or
    /// <c>default(T)</c> if the theorem cannot be satisfied.
    /// </returns>
    /// <exception cref="TheoremUndecidedException">
    /// Z3 stopped without deciding: the <see cref="Z3Context.Timeout"/> or
    /// <see cref="Z3Context.ResourceLimit"/> was reached, or Z3 gave up. Without a limit, a theorem
    /// Z3 cannot decide runs until the process is killed. See #85.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <remarks>
    /// <para>
    /// Where <typeparamref name="T"/> is a value type - a value tuple, a struct, a record struct -
    /// <c>default(T)</c> is a populated instance with every symbol zero, which is also a perfectly
    /// good solution to some theorems. The two cannot be told apart here, and the result cannot
    /// even be compared against <see langword="null"/>. Use
    /// <see cref="TrySolve(out T, CancellationToken)"/>, or
    /// <see cref="SolveableExtensions.SolveOrNull{TEnvironment}(ISolveable{TEnvironment}, CancellationToken)"/>,
    /// when the difference matters. See #57.
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
    /// <para>
    /// A <c>short</c>, <c>int</c>, <c>long</c> or <see cref="DateTime"/> symbol is bounded to the
    /// range of its type, so a constraint no value of the type can satisfy makes the theorem
    /// unsatisfiable, and an optimisation with no other bound on such a symbol returns the
    /// extreme of the type. Elements of a collection are not bounded; one constrained beyond its
    /// type is read with a checked conversion and throws. See #87.
    /// </para>
    /// </remarks>
    public T? Solve(CancellationToken cancellationToken = default)
    {
        return base.Solve<T>(cancellationToken);
    }

    /// <summary>
    /// Solves the theorem, reporting satisfiability separately from the solution.
    /// </summary>
    /// <param name="result">The solution, when the theorem could be satisfied.</param>
    /// <param name="cancellationToken">A token that interrupts the solve.</param>
    /// <returns>
    /// <see langword="true"/> if the theorem was satisfiable; otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="TheoremUndecidedException">
    /// Z3 stopped without deciding: the <see cref="Z3Context.Timeout"/> or
    /// <see cref="Z3Context.ResourceLimit"/> was reached, or Z3 gave up. See #85.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <remarks>
    /// The only form that answers "is there a solution?" for every environment type. What is
    /// written into <paramref name="result"/> when this returns <see langword="true"/> is exactly
    /// what <see cref="Solve"/> would have returned. <see langword="false"/> means the theorem was
    /// proved to have no solution - a solve that stopped without deciding throws instead.
    /// </remarks>
    public bool TrySolve([MaybeNullWhen(false)] out T result, CancellationToken cancellationToken = default)
    {
        return base.TrySolve(out result, cancellationToken);
    }

    /// <summary>
    /// Finds an optimal solution.
    /// </summary>
    /// <typeparam name="TResult">Type of the value being optimized.</typeparam>
    /// <param name="direction">The optimization goal, i.e. whether to minimize or maximize the solution.</param>
    /// <param name="lambda">Expression representing the value to minimize or maximize.</param>
    /// <param name="cancellationToken">A token that interrupts the optimisation.</param>
    /// <returns>
    /// Environment type instance with properties set to theorem-satisfying values, or
    /// <c>default(T)</c> if the theorem cannot be satisfied.
    /// </returns>
    /// <exception cref="TheoremUndecidedException">
    /// Z3 stopped without deciding: the <see cref="Z3Context.Timeout"/> or
    /// <see cref="Z3Context.ResourceLimit"/> was reached, or Z3 gave up. See #85.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <remarks>
    /// Carries the same ambiguity as <see cref="Solve"/>, and answers it the same way - through
    /// <see cref="TryOptimize{TResult}(Optimization, Expression{Func{T, TResult}}, out T, CancellationToken)"/>.
    /// As with <see cref="Solve"/>, a symbol that no constraint mentions is free and takes an
    /// arbitrary value; the objective determines only what it is written in terms of.
    /// </remarks>
    public T? Optimize<TResult>(Optimization direction, Expression<Func<T, TResult>> lambda, CancellationToken cancellationToken = default)
    {
        return base.Optimize<T, TResult>(direction, lambda, cancellationToken);
    }

    /// <summary>
    /// Finds an optimal solution, reporting satisfiability separately from the solution.
    /// </summary>
    /// <typeparam name="TResult">Type of the value being optimized.</typeparam>
    /// <param name="direction">The optimization goal, i.e. whether to minimize or maximize the solution.</param>
    /// <param name="lambda">Expression representing the value to minimize or maximize.</param>
    /// <param name="result">The optimal solution, when the theorem could be satisfied.</param>
    /// <param name="cancellationToken">A token that interrupts the optimisation.</param>
    /// <returns>
    /// <see langword="true"/> if the theorem was satisfiable; otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="TheoremUndecidedException">
    /// Z3 stopped without deciding: the <see cref="Z3Context.Timeout"/> or
    /// <see cref="Z3Context.ResourceLimit"/> was reached, or Z3 gave up. See #85.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public bool TryOptimize<TResult>(
        Optimization direction,
        Expression<Func<T, TResult>> lambda,
        [MaybeNullWhen(false)] out T result,
        CancellationToken cancellationToken = default)
    {
        return base.TryOptimize(direction, lambda, out result, cancellationToken);
    }

    /// <summary>
    /// Where query operator, used to add constraints to the theorem.
    /// </summary>
    /// <param name="constraint">Theorem constraint expression.</param>
    /// <returns>Theorem with the new constraint applied.</returns>
    public Theorem<T> Where(Expression<Func<T, bool>> constraint)
    {
        return new Theorem<T>(base.Context, base.Constraints.Concat([constraint]), base.Template);
    }

    /// <summary>
    /// OrderBy query operator, used to optimize a solution using query expression syntax.
    /// </summary>
    /// <typeparam name="TResult">Type of the value being minimized.</typeparam>
    /// <param name="lambda">Expression representing the value to minimize.</param>
    /// <returns>
    /// A deferred minimization. Nothing reaches Z3 until <see cref="ISolveable{T}.Solve"/> or
    /// <see cref="ISolveable{T}.TrySolve(out T, CancellationToken)"/> is called on the result.
    /// </returns>
    public ISolveable<T> OrderBy<TResult>(Expression<Func<T, TResult>> lambda)
        => new DeferredSolvable(cancellationToken =>
            TryOptimize(Optimization.Minimize, lambda, out T? solution, cancellationToken) ? (true, solution) : (false, default));

    /// <summary>
    /// OrderByDescending query operator, used to optimize a solution using query expression syntax.
    /// </summary>
    /// <typeparam name="TResult">Type of the value being maximized.</typeparam>
    /// <param name="lambda">Expression representing the value to maximize.</param>
    /// <returns>
    /// A deferred maximization. Nothing reaches Z3 until <see cref="ISolveable{T}.Solve"/> or
    /// <see cref="ISolveable{T}.TrySolve(out T, CancellationToken)"/> is called on the result.
    /// </returns>
    public ISolveable<T> OrderByDescending<TResult>(Expression<Func<T, TResult>> lambda)
        => new DeferredSolvable(cancellationToken =>
            TryOptimize(Optimization.Maximize, lambda, out T? solution, cancellationToken) ? (true, solution) : (false, default));

    /// <summary>
    /// An optimisation that has not run yet.
    /// </summary>
    /// <remarks>
    /// The deferred call carries satisfiability alongside the solution rather than returning the
    /// solution alone, because for a value-type environment the solution on its own cannot say
    /// whether there was one. See #57. The token is the caller's, supplied when the deferred
    /// solve is finally asked for, so it reaches the optimizer like any other. See #85.
    /// </remarks>
    private sealed class DeferredSolvable : ISolveable<T>
    {
        private readonly Func<CancellationToken, (bool Satisfiable, T? Solution)> optimize;

        public DeferredSolvable(Func<CancellationToken, (bool Satisfiable, T? Solution)> optimize)
        {
            this.optimize = optimize;
        }

        public T? Solve(CancellationToken cancellationToken = default) => this.optimize(cancellationToken).Solution;

        public bool TrySolve([MaybeNullWhen(false)] out T result, CancellationToken cancellationToken = default)
        {
            (bool satisfiable, T? solution) = this.optimize(cancellationToken);
            result = solution!;
            return satisfiable;
        }
    }
}
