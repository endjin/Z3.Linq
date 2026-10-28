namespace Z3.Linq;

using System;

/// <summary>
/// Declares that a type used for a symbol stands for one of the supported types, and is
/// converted back from it when the solution is read.
/// </summary>
/// <remarks>
/// Applied to the type of a symbol. The symbol is declared and solved as
/// <see cref="RegularType"/>, and when the solution is built the solved value is passed to a
/// public constructor of the declaring type that takes a single <see cref="RegularType"/>
/// argument. A type without such a constructor fails when the solution is read, not when the
/// theorem is created.
/// </remarks>
public class TheoremVariableTypeMappingAttribute : Attribute
{
    /// <summary>
    /// Creates the attribute.
    /// </summary>
    /// <param name="regularType">The supported type the declaring type stands for in the theorem.</param>
    public TheoremVariableTypeMappingAttribute(Type regularType)
    {
        this.RegularType = regularType;
    }

    /// <summary>
    /// Gets the supported type the declaring type stands for in the theorem.
    /// </summary>
    public Type RegularType { get; }
}
