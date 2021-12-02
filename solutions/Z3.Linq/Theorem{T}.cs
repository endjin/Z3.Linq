namespace Z3.Linq;

using System;
using System.Collections.Generic;
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
    /// <returns>Environment type instance with properties set to theorem-satisfying values.</returns>
    public T? Solve()
    {
        return base.Solve<T>();
    }

    /// <summary>
    /// Finds an optimal solution.
    /// </summary>
    /// <param name="direction">The optimization goal, i.e. whether to minimize or maximize the solution.</param>
    /// <param name="lambda">Expression representing the value to minimize or maximize.</param>
    /// <returns>Environment type instance with properties set to theorem-satisfying values.</returns>
    public T Optimize<TResult>(Optimization direction, Expression<Func<T, TResult>> lambda)
    {
        return base.Optimize<T, TResult>(direction, lambda);
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
    /// <param name="lambda">Expression representing the value to minimize.</param>
    /// <returns>Environment type instance with properties set to theorem-satisfying values.</returns>
    public ISolveable<T> OrderBy<TResult>(Expression<Func<T, TResult>> lambda)
        => new DeferredSolvable(() => Optimize(Optimization.Minimize, lambda));

    /// <summary>
    /// OrderBy query operator, used to optimize a solution using query expression syntax.
    /// </summary>
    /// <param name="lambda">Expression representing the value to maximize.</param>
    /// <returns>Environment type instance with properties set to theorem-satisfying values.</returns>
    public ISolveable<T> OrderByDescending<TResult>(Expression<Func<T, TResult>> lambda)
        => new DeferredSolvable(() => Optimize(Optimization.Maximize, lambda));

    private class DeferredSolvable : ISolveable<T>
    {
        private readonly Func<T> solve;

        public DeferredSolvable(Func<T> solve)
        {
            this.solve = solve;
        }

        public T? Solve() => this.solve();
    }
}