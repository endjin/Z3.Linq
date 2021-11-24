namespace Z3.Linq;

using System;

public class TheoremGlobalRewriterAttribute : Attribute
{
    public TheoremGlobalRewriterAttribute(Type rewriterType)
    {
        this.RewriterType = rewriterType;
    }

    public Type RewriterType { get; }
}