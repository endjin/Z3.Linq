namespace Z3.Linq;

/// <summary>
/// Pre-defined environment with five symbols.
/// </summary>
/// <typeparam name="T1">Type of the first symbol.</typeparam>
/// <typeparam name="T2">Type of the second symbol.</typeparam>
/// <typeparam name="T3">Type of the third symbol.</typeparam>
/// <typeparam name="T4">Type of the fourth symbol.</typeparam>
/// <typeparam name="T5">Type of the fifth symbol.</typeparam>
public sealed class Symbols<T1, T2, T3, T4, T5> : Symbols
{
    /// <summary>
    /// Provides access to the first symbol.
    /// </summary>
    /// <remarks>
    /// Used within definition of theorem constraints and to print the theorem prover's result.
    /// </remarks>
    public T1 X1 { get; private set; } = default!;

    /// <summary>
    /// Provides access to the second symbol.
    /// </summary>
    /// <remarks>
    /// Used within definition of theorem constraints and to print the theorem prover's result.
    /// </remarks>
    public T2 X2 { get; private set; } = default!;

    /// <summary>
    /// Provides access to the third symbol.
    /// </summary>
    /// <remarks>
    /// Used within definition of theorem constraints and to print the theorem prover's result.
    /// </remarks>
    public T3 X3 { get; private set; } = default!;

    /// <summary>
    /// Provides access to the fourth symbol.
    /// </summary>
    /// <remarks>
    /// Used within definition of theorem constraints and to print the theorem prover's result.
    /// </remarks>
    public T4 X4 { get; private set; } = default!;

    /// <summary>
    /// Provides access to the fifth symbol.
    /// </summary>
    /// <remarks>
    /// Used within definition of theorem constraints and to print the theorem prover's result.
    /// </remarks>
    public T5 X5 { get; private set; } = default!;
}