namespace Z3.Linq;

using Microsoft.Z3;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Controls how collection properties are modeled in Z3.
/// Constants = one Z3 constant per element (enables constraints on individual elements).
/// Array = Z3 array theory (single ArrayExpr with Select/Store).
/// </summary>
public enum CollectionHandling
{
    /// <summary>
    /// One Z3 constant per element (enables constraints on individual elements).
    /// </summary>
    Constants,

    /// <summary>
    /// Z3 array theory (single ArrayExpr with Select/Store).
    /// </summary>
    Array
}

/// <summary>
/// The Z3 handles standing in for the members of an environment type while a theorem is solved.
/// </summary>
/// <remarks>
/// Built once per solve by walking the environment type. A scalar member gets a constant of its
/// type's sort in <see cref="Expr"/>, a collection gets an array from <c>Int</c> to that sort,
/// and a nested object gets an <see cref="Environment"/> of its own under
/// <see cref="Properties"/>, with no <see cref="Expr"/>. Public because
/// the translator takes one; there is no reason to build one directly.
/// </remarks>
public class Environment
{
    /// <summary>
    /// Gets or sets the Z3 handle for a scalar or collection symbol, or <see langword="null"/>
    /// for a nested object, which has <see cref="Properties"/> instead.
    /// </summary>
    public Expr? Expr { get; set; }

    /// <summary>
    /// Gets or sets whether this environment describes a collection of objects, in which case
    /// each entry in <see cref="Properties"/> is an array indexed by position rather than a
    /// single handle.
    /// </summary>
    /// <remarks>
    /// Set by the environment builder and read by nothing: neither the translator nor the
    /// marshaller supports a collection of objects. See #89.
    /// </remarks>
    public bool IsArray { get; set; }

    /// <summary>
    /// Gets the environments of the members of a nested object, keyed by member.
    /// </summary>
    public Dictionary<MemberInfo, Environment> Properties { get; private set; } = new Dictionary<MemberInfo, Environment>();
}

/// <summary>
/// Environment for a collection modeled as individual Z3 constants (CollectionHandling.Constants).
/// Lazily creates sub-environments per element index on access.
/// </summary>
public class MultipleEnvironment : Environment
{
    /// <summary>
    /// Creates a new collection environment with the given prefix and element type.
    /// </summary>
    /// <param name="prefix">The name prefix for the element constants.</param>
    /// <param name="elementType">The type of each element in the collection.</param>
    public MultipleEnvironment(string prefix, Type elementType)
    {
        Prefix = prefix;
        ElementType = elementType;
    }

    /// <summary>
    /// Gets or sets the name prefix for the element constants.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// Gets or sets the type of each element in the collection.
    /// </summary>
    public Type ElementType { get; set; }

    /// <summary>
    /// Gets or sets the sub-environments, keyed by element index.
    /// </summary>
    public Dictionary<object, Environment> SubEnvironments { get; set; } = new Dictionary<object, Environment>();
}
