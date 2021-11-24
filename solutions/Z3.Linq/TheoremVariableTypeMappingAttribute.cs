namespace Z3.Linq;

using System;

public class TheoremVariableTypeMappingAttribute : Attribute
{
    public TheoremVariableTypeMappingAttribute(Type regularType)
    {
        this.RegularType = regularType;
    }

    public Type RegularType { get; }
}