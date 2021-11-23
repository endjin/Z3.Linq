namespace Z3.Linq;

using System;

public class TheoremPredicateRewriterAttribute : Attribute
{
    public TheoremPredicateRewriterAttribute(Type rewriterType)
    {
        this.RewriterType = rewriterType;
    }

    public Type RewriterType { get; }
}