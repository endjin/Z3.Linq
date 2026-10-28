namespace Z3.Linq.Tests;

/// <summary>
/// <c>uint</c> and <c>ulong</c> symbols, which travel through Z3 as fixed-width bit-vectors and so
/// carry the operators a mathematical integer cannot: bitwise <c>&amp;</c> <c>|</c> <c>^</c>
/// <c>~</c>, shifts, and wrapping arithmetic with unsigned comparison.
/// </summary>
/// <remarks>
/// <para>
/// A <c>uint</c> maps to a 32-bit bit-vector and a <c>ulong</c> to a 64-bit one. <c>int</c>,
/// <c>long</c> and <c>short</c> stay unbounded mathematical integers, where those bit operators
/// have no meaning and are refused (see <c>ExpressionTranslationTests</c> and
/// <c>UnsupportedExpressionTests</c>). <c>byte</c> and <c>ushort</c> are deliberately not mapped:
/// C# promotes them to <c>int</c> in every expression, so such a symbol could never keep its
/// bit-vector sort through a constraint.
/// </para>
/// <para>
/// Constants must carry the unsigned suffix - <c>6u</c>, <c>6UL</c> - so C# keeps them
/// <c>uint</c>/<c>ulong</c> rather than promoting the whole expression to <c>long</c>; an int
/// literal that fits is implicitly typed to the symbol, so <c>t.X1 == 7</c> is fine, but mixing a
/// bit-vector symbol with an <c>int</c> that forces promotion is not supported.
/// </para>
/// <para>
/// Where a constraint does not pin a unique value, the assertion checks that the constraint holds
/// on the returned value rather than a specific model, per the suite's determinism rule.
/// </para>
/// </remarks>
[TestClass]
public class BitVectorSymbolTests
{
    [TestMethod]
    [DataRow(0u, DisplayName = "Zero")]
    [DataRow(42u, DisplayName = "Small")]
    [DataRow(uint.MaxValue, DisplayName = "UInt32.MaxValue")]
    public void Solve_UIntSymbol_RoundTripsTheValue(uint value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<uint, int>>()
            .Where(t => t.X1 == value)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(value);
    }

    [TestMethod]
    public void Solve_UIntBitwiseAnd_SatisfiesTheConstraint()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<uint, int>>()
            .Where(t => (t.X1 & 6u) == 4u)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert: any satisfying value has those bits, so the relation is model-independent.
        result.ShouldNotBeNull();
        (result.X1 & 6u).ShouldBe(4u);
    }

    [TestMethod]
    public void Solve_UIntBitwiseOr_SatisfiesTheConstraint()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<uint, int>>()
            .Where(t => (t.X1 | 1u) == 7u)
            .Where(t => t.X1 < 8u)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        (result.X1 | 1u).ShouldBe(7u);
    }

    [TestMethod]
    public void Solve_UIntBitwiseXor_PinsTheValue()
    {
        // Arrange: XOR against a constant has one solution for a pinned result.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<uint, int>>()
            .Where(t => (t.X1 ^ 5u) == 0u)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert: x ^ 5 == 0 has the single solution x == 5.
        result.ShouldNotBeNull();
        result.X1.ShouldBe(5u);
    }

    [TestMethod]
    public void Solve_UIntOnesComplement_PinsTheValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<uint, int>>()
            .Where(t => (~t.X1) == 0xFFFFFFF0u)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert: ~x == 0xFFFFFFF0 has the single solution x == 0x0000000F.
        result.ShouldNotBeNull();
        result.X1.ShouldBe(0x0000000Fu);
    }

    [TestMethod]
    public void Solve_UIntLeftShift_PinsTheValue()
    {
        // Arrange: the shift amount is an int in C#, converted to the value's width. The upper
        // bound removes the wrapped second solution, leaving a unique answer.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<uint, int>>()
            .Where(t => (t.X1 << 2) == 8u)
            .Where(t => t.X1 < 4u)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(2u);
    }

    [TestMethod]
    public void Solve_UIntRightShift_SatisfiesTheConstraint()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<uint, int>>()
            .Where(t => (t.X1 >> 1) == 5u)
            .Where(t => t.X1 < 12u)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        (result.X1 >> 1).ShouldBe(5u);
    }

    /// <summary>
    /// Bit-vector arithmetic wraps, as unsigned arithmetic does.
    /// </summary>
    /// <remarks>
    /// The defining difference from an <c>int</c> symbol, which is an unbounded integer and could
    /// never satisfy this: <c>x + 1 == 0</c> has a solution only because a 32-bit bit-vector wraps
    /// at its width, and that solution is <see cref="uint.MaxValue"/>.
    /// </remarks>
    [TestMethod]
    public void Solve_UIntArithmeticWraps_PinsMaxValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<uint, int>>()
            .Where(t => t.X1 + 1u == 0u)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(uint.MaxValue);
    }

    /// <summary>
    /// Comparison on a bit-vector symbol is unsigned.
    /// </summary>
    /// <remarks>
    /// The band 2,000,000,000 to 3,000,000,000 straddles the signed 32-bit boundary
    /// (0x7FFFFFFF ~ 2.147e9): 3,000,000,000 read as a signed <c>int</c> is negative. So under a
    /// <em>signed</em> comparison the two bounds contradict - nothing is both above a large
    /// positive and below a negative - and the theorem would be unsatisfiable. It is satisfiable
    /// only because the comparison is unsigned, which is what makes a signed-comparison mutation
    /// return <see langword="null"/> here rather than a coincidentally-passing model.
    /// </remarks>
    [TestMethod]
    public void Solve_UIntComparison_IsUnsigned()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<uint, int>>()
            .Where(t => t.X1 > 2_000_000_000u)
            .Where(t => t.X1 < 3_000_000_000u)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBeGreaterThan(2_000_000_000u);
        result.X1.ShouldBeLessThan(3_000_000_000u);
    }

    [TestMethod]
    public void Solve_UIntSymbolMaximised_ReturnsTheUpperBound()
    {
        // Arrange: the optimiser reads a bit-vector solution back through the same marshalling.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<uint, int>>()
            .Where(t => t.X1 < 100u)
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Maximize, t => t.X1);

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(99u);
    }

    /// <summary>
    /// A <c>ulong</c> symbol is a 64-bit bit-vector, so it holds values beyond <c>uint</c>.
    /// </summary>
    [TestMethod]
    public void Solve_ULongSymbol_RoundTripsAValueBeyondUInt()
    {
        // Arrange
        using var context = new Z3Context();
        const ulong value = 5_000_000_000UL;

        // Act
        var result = context.NewTheorem<Symbols<ulong, int>>()
            .Where(t => t.X1 == value)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(value);
    }

    [TestMethod]
    public void Solve_ULongBitwise_SatisfiesTheConstraint()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<ulong, int>>()
            .Where(t => (t.X1 & 6UL) == 4UL)
            .Where(t => t.X1 < 8UL)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        (result.X1 & 6UL).ShouldBe(4UL);
    }

    /// <summary>
    /// A <c>uint</c> collection round-trips, and its elements support bitwise operators.
    /// </summary>
    [TestMethod]
    public void Solve_UIntCollectionElement_SupportsBitwise()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<UIntArrayEnvironment>()
            .Where(t => (t.Values[0] & 6u) == 4u)
            .Where(t => t.Values[0] < 8u)
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.Length.ShouldBe(2);
        (result.Values[0] & 6u).ShouldBe(4u);
    }

    /// <summary>
    /// A shift count is masked to the value's width, as C# masks it.
    /// </summary>
    /// <remarks>
    /// C# uses only the low five bits of the count for a <c>uint</c> (the low six for a
    /// <c>ulong</c>), so <c>1u &lt;&lt; 32</c> is 1 and <c>1u &lt;&lt; -1</c> is <c>1u &lt;&lt; 31</c>. Z3 shifts
    /// by the whole count, so an unmasked translation would give 0 for both. The count is a symbol,
    /// so the solver cannot fold it away, and the expected value is computed by C# from the
    /// returned symbols rather than written down.
    /// </remarks>
    [TestMethod]
    [DataRow(1, DisplayName = "In range")]
    [DataRow(32, DisplayName = "Width")]
    [DataRow(33, DisplayName = "Beyond width")]
    [DataRow(-1, DisplayName = "Negative")]
    public void Solve_UIntShiftBySymbolicCount_MasksTheCountAsCSharpDoes(int count)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<uint, int, uint>>()
            .Where(t => t.X1 == 0x80000001u)
            .Where(t => t.X2 == count)
            .Where(t => t.X3 == (t.X1 << t.X2) + (t.X1 >> t.X2))
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X3.ShouldBe((result.X1 << result.X2) + (result.X1 >> result.X2));
    }

    /// <summary>
    /// A <c>ulong</c> shift count is masked to six bits.
    /// </summary>
    [TestMethod]
    [DataRow(1, DisplayName = "In range")]
    [DataRow(64, DisplayName = "Width")]
    [DataRow(65, DisplayName = "Beyond width")]
    [DataRow(-1, DisplayName = "Negative")]
    public void Solve_ULongShiftBySymbolicCount_MasksTheCountAsCSharpDoes(int count)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<ulong, int, ulong>>()
            .Where(t => t.X1 == 0x8000000000000001UL)
            .Where(t => t.X2 == count)
            .Where(t => t.X3 == (t.X1 << t.X2) + (t.X1 >> t.X2))
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X3.ShouldBe((result.X1 << result.X2) + (result.X1 >> result.X2));
    }

    /// <summary>
    /// A <c>uint</c> beside a <c>ulong</c> is zero-extended, as C# widens it.
    /// </summary>
    /// <remarks>
    /// C# converts the <c>uint</c> implicitly, so <c>t.X1 == t.X2</c> compares 64-bit values.
    /// <see cref="uint.MaxValue"/> is the value that tells zero-extension from sign-extension.
    /// </remarks>
    [TestMethod]
    [DataRow(5u, DisplayName = "Small")]
    [DataRow(uint.MaxValue, DisplayName = "UInt32.MaxValue")]
    public void Solve_UIntComparedWithULong_ZeroExtends(uint value)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<uint, ulong>>()
            .Where(t => t.X1 == value)
            .Where(t => t.X1 == t.X2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe((ulong)value);
    }

    /// <summary>
    /// An explicit <c>ulong</c> to <c>uint</c> cast keeps the low 32 bits, as an unchecked C# cast
    /// does.
    /// </summary>
    [TestMethod]
    public void Solve_ULongCastToUInt_KeepsTheLowBits()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<uint, ulong>>()
            .Where(t => t.X2 == 0x1_0000_0005UL)
            .Where(t => t.X1 == (uint)t.X2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(5u);
    }

    /// <summary>
    /// Checked bit-vector arithmetic is refused rather than silently wrapped.
    /// </summary>
    /// <remarks>
    /// Bit-vector arithmetic wraps, so translating <c>checked(x + 1u)</c> as a plain addition
    /// would return <see cref="uint.MaxValue"/> for <c>checked(x + 1u) == 0u</c> - a value for which
    /// the C# predicate throws <see cref="OverflowException"/> instead of holding. Refusing says
    /// that overflow checking is not supported, where a model would hide it.
    /// </remarks>
    [TestMethod]
    public void Solve_CheckedUIntArithmetic_IsRefused()
    {
        // Arrange
        using var context = new Z3Context();

        var add = context.NewTheorem<Symbols<uint, int>>().Where(t => checked(t.X1 + 1u) == 0u);
        var subtract = context.NewTheorem<Symbols<uint, int>>().Where(t => checked(t.X1 - 1u) == uint.MaxValue);
        var multiply = context.NewTheorem<Symbols<ulong, int>>().Where(t => checked(t.X1 * 2UL) == 0UL);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => add.Solve()).Message.ShouldContain("checked");
        Should.Throw<NotSupportedException>(() => subtract.Solve()).Message.ShouldContain("checked");
        Should.Throw<NotSupportedException>(() => multiply.Solve()).Message.ShouldContain("checked");
    }

    private sealed class UIntArrayEnvironment
    {
        public uint[] Values { get; set; } = new uint[2];

        public int Length { get; set; }
    }
}
