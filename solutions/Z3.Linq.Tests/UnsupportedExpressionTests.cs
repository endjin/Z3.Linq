namespace Z3.Linq.Tests;

/// <summary>
/// The library's "we do not support that" contract: the expressions and environment shapes that
/// are rejected, and what they are rejected with.
/// </summary>
/// <remarks>
/// <para>
/// These throw sites are the boundary of the translator. They matter as much as the supported
/// cases, because a change that quietly stops rejecting something does not fail loudly - it
/// produces a theorem that means something other than what was written. Pinning them also makes
/// the boundary discoverable, since none of it is documented anywhere else.
/// </para>
/// <para>
/// Assertions are on exception type rather than message. The messages are informative but
/// several are misspelled or malformed, and pinning them would turn a typo fix into a test
/// failure.
/// </para>
/// </remarks>
[TestClass]
public class UnsupportedExpressionTests
{
    [TestMethod]
    public void Distinct_CalledOutsideAQueryExpression_ThrowsNotSupportedException()
    {
        // Arrange: Z3Methods.Distinct exists to be recognised by the visitor, never executed.
        // Its body throws so that calling it directly cannot silently return a meaningless
        // bool (Z3Methods.cs:19).

        // Act & Assert
        Should.Throw<NotSupportedException>(() => Z3Methods.Distinct(1, 2, 3));
    }

    [TestMethod]
    public void Solve_CallToAnUnrecognisedMethod_ThrowsNotSupportedException()
    {
        // Arrange: only Z3Methods.Distinct, indexed property getters and methods carrying a
        // predicate rewriter attribute are understood. An ordinary method that depends on a
        // theorem symbol cannot be evaluated away, so it reaches the visitor and is rejected
        // (ExpressionVisitor.cs:278).
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => Increment(t.X1) == 2);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => theorem.Solve());
    }

    /// <summary>
    /// A cast to a type the sort mapping does not know falls through to the catch-all.
    /// </summary>
    /// <remarks>
    /// This pinned <c>(long)t.X1</c> until #76, when the Convert case chose by target type and
    /// had no arm for <c>long</c>. Conversions are now decided by the sorts involved, using the
    /// same mapping the symbols are declared with, so what is unsupported is a target type that
    /// mapping has no row for - <c>byte</c> here. Note this one is NotImplementedException
    /// rather than NotSupportedException - the throw sites are not consistent about which they
    /// use.
    /// </remarks>
    [TestMethod]
    public void Solve_CastToAnUnmappedType_ThrowsNotImplementedException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => (byte)t.X1 == 1);

        // Act & Assert
        Should.Throw<NotImplementedException>(() => theorem.Solve());
    }

    [TestMethod]
    public void Solve_CharProperty_ThrowsNotSupportedException()
    {
        // Arrange: char has no branch in the symbol declaration switch (Theorem.cs:342). The
        // message names the mangled symbol - "CharEnvironment_Value" - rather than the type,
        // which is worth knowing when diagnosing one of these.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<CharEnvironment>().Where(t => t.Other == 1);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => theorem.Solve());
    }

    [TestMethod]
    public void Solve_ByteProperty_ThrowsNotSupportedException()
    {
        // Arrange: byte and ushort are deliberately not mapped, even though uint and ulong are
        // bit-vectors - because C# promotes byte and ushort to int in every expression, so such a
        // symbol could never keep a bit-vector sort through a constraint. See the bit-vector work
        // and BitVectorSymbolTests.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<ByteEnvironment>().Where(t => t.Other == 1);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => theorem.Solve());
    }

    [TestMethod]
    public void Solve_NullableProperty_ThrowsArgumentException()
    {
        // Arrange: a Nullable<int> property is TypeCode.Object, so it is treated as a nested
        // environment and recursed into. Nullable<T> has no settable properties, so the failure
        // surfaces from reflection as "Property set method not found" rather than as anything
        // naming nullability.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<NullableEnvironment>().Where(t => t.Other == 1);

        // Act & Assert
        Should.Throw<ArgumentException>(() => theorem.Solve());
    }

    /// <summary>
    /// An enum whose underlying type is not one the environment builder maps is rejected by
    /// name, before anything is translated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An enum is only ever as supported as its underlying type. #63 fixed the <c>int</c>-backed
    /// case - <see cref="DayOfWeek"/> and friends - which is now round-tripped in
    /// <c>SymbolTypeMarshallingTests</c>. A <c>byte</c>-backed enum is <c>TypeCode.Byte</c>,
    /// which the sort mapping does not handle, so it stops at the same guard that rejects a
    /// bare <c>byte</c> or <c>ushort</c>.
    /// </para>
    /// <para>
    /// This is the outcome #63 asked for where a type genuinely is not supported: a
    /// <see cref="NotSupportedException"/> naming the member, rather than an
    /// <see cref="InvalidCastException"/> from inside the visitor. Pinned so the distinction
    /// between the two enum cases does not quietly collapse.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_EnumPropertyWithAnUnsupportedUnderlyingType_ThrowsNotSupportedException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<ByteEnumEnvironment>()
            .Where(t => t.Size == ByteBackedEnum.Large)
            .Where(t => t.Other == 1);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => theorem.Solve())
            .Message.ShouldContain("Size");
    }

    [TestMethod]
    public void Solve_TwoLevelsOfObjectCollections_ThrowsNotSupportedException()
    {
        // Arrange: a collection of objects that themselves hold collections. The environment
        // builder rejects this explicitly rather than producing something wrong
        // (Theorem.cs:385), and its message names the offending prefix - one of the better
        // diagnostics in the library.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<TwoLevelCollectionEnvironment>().Where(t => t.Other == 2);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => theorem.Solve());
    }

    [TestMethod]
    public void Solve_SupportedExpressionsAlongsideRejectedOnes_StillRejects()
    {
        // Arrange: an unsupported constraint is not skipped in favour of the ones that do
        // translate. Worth pinning, because silently dropping a constraint would produce a
        // satisfying-looking answer to a different question. The rejected constraint is an
        // integer bitwise operation, which Z3's integer sort has no counterpart for.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 > 0)
            .Where(t => (t.X1 & 2) == 0);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => theorem.Solve());
    }

    private static int Increment(int value) => value + 1;

    private sealed class CharEnvironment
    {
        public char Value { get; set; }

        public int Other { get; set; }
    }

    private sealed class ByteEnvironment
    {
        public byte Value { get; set; }

        public int Other { get; set; }
    }

    private sealed class NullableEnvironment
    {
        public int? Value { get; set; }

        public int Other { get; set; }
    }

    private enum ByteBackedEnum : byte
    {
        Small = 1,
        Large = 2,
    }

    private sealed class ByteEnumEnvironment
    {
        public ByteBackedEnum Size { get; set; }

        public int Other { get; set; }
    }

    private sealed class InnerCollectionHolder
    {
        public int[] Values { get; set; } = new int[2];
    }

    private sealed class TwoLevelCollectionEnvironment
    {
        public InnerCollectionHolder[] Items { get; set; } = [new(), new()];

        public int Other { get; set; }
    }
}
