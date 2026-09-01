namespace Z3.Linq.Tests;

/// <summary>
/// <c>byte</c>, <c>sbyte</c> and <c>ushort</c> symbols, which - like <c>short</c> - travel through
/// Z3 as mathematical integers bounded to the range of the type.
/// </summary>
/// <remarks>
/// <para>
/// These are the sub-<c>int</c> integer types. C# promotes them to <c>int</c> in every
/// expression, so - unlike <c>uint</c> and <c>ulong</c> - they cannot be bit-vectors: a promoted
/// operand would lose a bit-vector sort at the <c>Convert</c> the compiler inserts. Mapping them
/// to the integer sort instead makes that promotion a no-op in Z3 terms (the symbol is already an
/// integer), exactly as it is for <c>short</c>, so equality, ordering and arithmetic work. The
/// range is enforced the way #87 does it, so a value the type cannot hold makes the theorem
/// unsatisfiable rather than being read back wrong.
/// </para>
/// <para>
/// They do not support bitwise operators or shifts - the integer sort has none; that needs
/// <c>uint</c>/<c>ulong</c> and is covered by <c>BitVectorSymbolTests</c>.
/// </para>
/// </remarks>
[TestClass]
public class SmallIntegerSymbolTests
{
    [TestMethod]
    [DataRow((byte)0, DisplayName = "Zero")]
    [DataRow((byte)200, DisplayName = "Above sbyte range")]
    [DataRow(byte.MaxValue, DisplayName = "Byte.MaxValue")]
    public void Solve_ByteSymbol_RoundTripsTheValue(byte value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<byte, int>>()
            .Where(t => t.X1 == value)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(value);
    }

    [TestMethod]
    [DataRow((sbyte)0, DisplayName = "Zero")]
    [DataRow((sbyte)-100, DisplayName = "Negative")]
    [DataRow(sbyte.MinValue, DisplayName = "SByte.MinValue")]
    [DataRow(sbyte.MaxValue, DisplayName = "SByte.MaxValue")]
    public void Solve_SByteSymbol_RoundTripsTheValue(sbyte value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<sbyte, int>>()
            .Where(t => t.X1 == value)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(value);
    }

    [TestMethod]
    [DataRow((ushort)0, DisplayName = "Zero")]
    [DataRow((ushort)40000, DisplayName = "Above short range")]
    [DataRow(ushort.MaxValue, DisplayName = "UInt16.MaxValue")]
    public void Solve_UShortSymbol_RoundTripsTheValue(ushort value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<ushort, int>>()
            .Where(t => t.X1 == value)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(value);
    }

    [TestMethod]
    public void Solve_ByteSymbolsInArithmetic_RoundTripTheValues()
    {
        // Arrange: C# promotes both operands to int, which is a no-op against the int-sorted
        // symbols, so the arithmetic just works.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<byte, byte>>()
            .Where(t => t.X1 + t.X2 == 10)
            .Where(t => t.X1 == 4)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe((byte)4);
        result.X2.ShouldBe((byte)6);
    }

    [TestMethod]
    public void Optimize_ByteSymbolMaximised_ReturnsByteMaxValue()
    {
        // Arrange: the range bound is what makes the maximum the type's maximum rather than an
        // arbitrary large integer.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<byte, int>>()
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Maximize, t => t.X1);

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(byte.MaxValue);
    }

    [TestMethod]
    public void Optimize_SByteSymbolMinimised_ReturnsSByteMinValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<sbyte, int>>()
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Minimize, t => t.X1);

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(sbyte.MinValue);
    }

    [TestMethod]
    public void Optimize_UShortSymbolMaximised_ReturnsUShortMaxValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<ushort, int>>()
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Maximize, t => t.X1);

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(ushort.MaxValue);
    }

    /// <summary>
    /// A <c>byte</c> constrained outside its range has no solution.
    /// </summary>
    /// <remarks>
    /// The bound is enforced, so a constraint written through an <c>int</c> variable that no
    /// <c>byte</c> can satisfy is unsatisfiable - the true answer - rather than a value read back
    /// wrong. C# blocks the direct <c>t.X1 == 300</c>, so it takes a variable to reach.
    /// </remarks>
    [TestMethod]
    public void Solve_ByteSymbolConstrainedOutsideItsRange_IsUnsatisfiable()
    {
        // Arrange
        using var context = new Z3Context();
        int beyondByteRange = 300;
        var theorem = context.NewTheorem<Symbols<byte, int>>()
            .Where(t => t.X1 == beyondByteRange);

        // Act
        bool satisfiable = theorem.TrySolve(out _);

        // Assert
        satisfiable.ShouldBeFalse();
    }

    /// <summary>
    /// Bitwise operators are refused on a <c>byte</c> symbol.
    /// </summary>
    /// <remarks>
    /// A <c>byte</c> is an integer, not a bit-vector, and C# computes <c>byte &amp; byte</c> in
    /// <c>int</c> space - so this is a bitwise operation on integer operands, which the integer
    /// sort has no counterpart for. The refusal points at the bit-vector types.
    /// </remarks>
    [TestMethod]
    public void Solve_ByteBitwise_ThrowsNotSupported()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<byte, int>>()
            .Where(t => (t.X1 & 6) == 4);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => theorem.Solve());
    }

    [TestMethod]
    public void Solve_ByteArrayElement_RoundTripsTheValue()
    {
        // Arrange: the element read has its own arm, so byte has to work there too.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<ByteArrayEnvironment>()
            .Where(t => t.Values[0] == 42)
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.Length.ShouldBe(2);
        result.Values[0].ShouldBe((byte)42);
    }

    private sealed class ByteArrayEnvironment
    {
        public byte[] Values { get; set; } = new byte[2];

        public int Length { get; set; }
    }
}
