namespace Z3.Linq;

using Microsoft.Z3;
using System.Collections.Generic;
using System.Reflection;

public class Environment
{
    public Expr? Expr { get; set; }

    public bool IsArray { get; set; }
        
    public Dictionary<MemberInfo, Environment> Properties { get; private set; } = new Dictionary<MemberInfo, Environment>();
}