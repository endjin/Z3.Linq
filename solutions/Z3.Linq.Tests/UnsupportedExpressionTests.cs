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
    public void Solve_ConditionalExpression_ThrowsNotSupportedException()
    {
        // Arrange: a ternary is a Conditional node, which the visitor has no case for. Z3 can
        // express if-then-else, so this is a gap rather than a fundamental limit.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => (t.X1 > 0 ? t.X1 : 0) == 1);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => theorem.Solve());
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

    [TestMethod]
    public void Solve_UnsupportedCast_ThrowsNotImplementedException()
    {
        // Arrange: the Convert case handles conversions to double, int and char only. Widening
        // an int symbol to long is unremarkable C# but has no case, so it falls through to the
        // catch-all (ExpressionVisitor.cs:141). Note this one is NotImplementedException rather
        // than NotSupportedException - the throw sites are not consistent about which they use.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => (long)t.X1 == 1L);

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
    public void Solve_UnsignedIntProperty_ThrowsNotSupportedException()
    {
        // Arrange: uint is likewise absent. Z3 integers are unbounded, so the omission is about
        // the type switch rather than anything Z3 cannot represent.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<UnsignedEnvironment>().Where(t => t.Other == 1);

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
    /// which the sort mapping has never handled, so it stops at the same guard that rejects a
    /// bare <c>byte</c> or <c>uint</c>.
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
        // satisfying-looking answer to a different question.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 > 0)
            .Where(t => (t.X1 > 5 ? t.X1 : 0) == 6);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => theorem.Solve());
    }

    private static int Increment(int value) => value + 1;

    private sealed class CharEnvironment
    {
        public char Value { get; set; }

        public int Other { get; set; }
    }

    private sealed class UnsignedEnvironment
    {
        public uint Value { get; set; }

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
