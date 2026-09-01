namespace Z3.Linq;

using System.Collections.Generic;
using System.Linq.Expressions;

/// <summary>
/// Rewrites the whole set of a theorem's constraints before they are translated to Z3.
/// </summary>
/// <remarks>
/// Named on an environment type by <see cref="TheoremGlobalRewriterAttribute"/>. The rewriter
/// sees every constraint the theorem has accumulated and returns the constraints to assert in
/// their place, so it can add to them, drop them or replace them outright. Implementations need
/// a public parameterless constructor, since the library creates one when the theorem is solved.
/// </remarks>
public interface ITheoremGlobalRewriter
{
    /// <summary>
    /// Rewrites the constraints of a theorem.
    /// </summary>
    /// <param name="constraints">The constraints as written, in the order they were added.</param>
    /// <returns>The constraints to assert instead.</returns>
    IEnumerable<LambdaExpression> Rewrite(IEnumerable<LambdaExpression> constraints);
}
