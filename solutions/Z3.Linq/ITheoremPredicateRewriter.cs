namespace Z3.Linq;

using System.Linq.Expressions;

/// <summary>
/// Rewrites a method call inside a constraint into an expression the translator understands.
/// </summary>
/// <remarks>
/// Named on a method by <see cref="TheoremPredicateRewriterAttribute"/>. When the translator
/// meets a call to that method it hands the call to the rewriter and translates the result in
/// its place. The result has to differ from the input: a rewriter that returns the call it was
/// given is rejected, because translating that call would only invoke the rewriter again.
/// Implementations need a public parameterless constructor, since the library creates one when
/// the call is met.
/// </remarks>
public interface ITheoremPredicateRewriter
{
    /// <summary>
    /// Rewrites a call to the method the rewriter is registered on.
    /// </summary>
    /// <param name="call">The call as it appears in the constraint.</param>
    /// <returns>The expression to translate in its place, which must not be <paramref name="call"/> itself.</returns>
    MethodCallExpression Rewrite(MethodCallExpression call);
}
