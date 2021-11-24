namespace Z3.Linq;

using System.Linq;

/// <summary>
/// Base class for symbol container types.
/// </summary>
public class Symbols
{
    /// <summary>
    /// Returns a friendly representation of the symbols and their values.
    /// Used to print the output of a theorem solving task.
    /// </summary>
    /// <returns>Friendly representation of the symbols and their values.</returns>
    public override string ToString()
    {
        var propertyValues = from prop in GetType().GetProperties()
                             let value = prop.GetValue(this, null)
                             select prop.Name + " = " + (value ?? "(null)");

        return "{" + string.Join(", ", propertyValues.ToArray()) + "}";
    }
}