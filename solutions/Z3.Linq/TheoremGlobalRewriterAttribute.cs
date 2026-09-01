namespace Z3.Linq;

using System;

/// <summary>
/// Names the <see cref="ITheoremGlobalRewriter"/> that rewrites a theorem's constraints before
/// they are translated. Applied to the environment type.
/// </summary>
public class TheoremGlobalRewriterAttribute : Attribute
{
    /// <summary>
    /// Creates the attribute.
    /// </summary>
    /// <param name="rewriterType">
    /// A type implementing <see cref="ITheoremGlobalRewriter"/> with a public parameterless
    /// constructor. A type that does not implement the interface is rejected when the theorem
    /// is solved, not when the attribute is applied.
    /// </param>
    public TheoremGlobalRewriterAttribute(Type rewriterType)
    {
        this.RewriterType = rewriterType;
    }

    /// <summary>
    /// Gets the type of the rewriter to apply.
    /// </summary>
    public Type RewriterType { get; }
}
