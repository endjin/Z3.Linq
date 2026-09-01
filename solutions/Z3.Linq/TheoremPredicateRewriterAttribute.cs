namespace Z3.Linq;

using System;

/// <summary>
/// Names the <see cref="ITheoremPredicateRewriter"/> that rewrites calls to the method it is
/// applied to. Applied to a method that is called inside a constraint.
/// </summary>
public class TheoremPredicateRewriterAttribute : Attribute
{
    /// <summary>
    /// Creates the attribute.
    /// </summary>
    /// <param name="rewriterType">
    /// A type implementing <see cref="ITheoremPredicateRewriter"/> with a public parameterless
    /// constructor. A type that does not implement the interface is rejected when a call to the
    /// method is translated, not when the attribute is applied.
    /// </param>
    public TheoremPredicateRewriterAttribute(Type rewriterType)
    {
        this.RewriterType = rewriterType;
    }

    /// <summary>
    /// Gets the type of the rewriter to apply.
    /// </summary>
    public Type RewriterType { get; }
}
