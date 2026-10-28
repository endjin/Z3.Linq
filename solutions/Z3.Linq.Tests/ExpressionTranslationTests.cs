namespace Z3.Linq.Tests;

/// <summary>
/// Translation of the operators the <c>ExpressionVisitor</c> turns into Z3 terms, focused on the
/// cases that were added or corrected: the ternary conditional, and the operators C# spells the
/// same way for two different meanings.
/// </summary>
/// <remarks>
/// <para>
/// The visitor dispatches on the expression node, but two of C#'s operators are overloaded across
/// sorts the node cannot distinguish: <c>&amp;</c>/<c>|</c>/<c>^</c> mean Boolean logic on
/// <c>bool</c> and bitwise arithmetic on integers, and <c>%</c> means integer remainder on
/// integers and has no counterpart on reals. Those used to be translated by an unconditional cast
/// that succeeded for one meaning and threw <see cref="InvalidCastException"/> from inside Z3 for
/// the other. They now choose - or refuse - by the operands' sort, so the Boolean and integer
/// cases keep working and the unsupported ones say why.
/// </para>
/// <para>
/// The ternary <c>?:</c> was simply unsupported before; it now maps onto Z3's if-then-else.
/// </para>
/// </remarks>
[TestClass]
public class ExpressionTranslationTests
{
    /// <summary>
    /// A ternary chooses its true branch when the test holds.
    /// </summary>
    /// <remarks>
    /// <c>ExpressionType.Conditional</c> had no case and threw; it now translates to
    /// <c>MkITE</c>. Here <c>X1</c> is pinned positive, so the value read from the conditional is
    /// the true branch, <c>X2</c>, which the equality then pins to 5.
    /// </remarks>
    [TestMethod]
    public void Solve_TernaryWhoseTestHolds_TakesTheTrueBranch()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => (t.X1 > 0 ? t.X2 : 0) == 5)
            .Where(t => t.X1 == 3)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(3);
        result.X2.ShouldBe(5);
    }

    /// <summary>
    /// A ternary chooses its false branch when the test fails.
    /// </summary>
    /// <remarks>
    /// The mirror of the test above: <c>X1</c> is pinned non-positive, so the conditional yields
    /// its false branch, the constant 9, and the constraint <c>== 9</c> is satisfied without
    /// pinning <c>X2</c> at all. Only <c>X1</c> is asserted; <c>X2</c> is free.
    /// </remarks>
    [TestMethod]
    public void Solve_TernaryWhoseTestFails_TakesTheFalseBranch()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => (t.X1 > 0 ? t.X2 : 9) == 9)
            .Where(t => t.X1 == -1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(-1);
    }

    [TestMethod]
    public void Solve_TernaryOverRealBranches_Translates()
    {
        // Arrange: both branches are real-sorted, so the if-then-else is well-sorted.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<double, int>>()
            .Where(t => (t.X1 > 1.0 ? 2.5 : 0.5) > 1.0)
            .Where(t => t.X1 == 2.0)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(2.0);
    }

    /// <summary>
    /// A ternary nested inside arithmetic translates too.
    /// </summary>
    /// <remarks>
    /// The conditional is a sub-term of an addition rather than the whole constraint, so the
    /// visitor meets it below a binary node - the case that would break if the conditional were
    /// only handled at the top level.
    /// </remarks>
    [TestMethod]
    public void Solve_TernaryNestedInArithmetic_Translates()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => ((t.X1 > 0 ? 10 : 20) + t.X2) == 15)
            .Where(t => t.X1 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X2.ShouldBe(5);
    }

    /// <summary>
    /// A bitwise operator on integer symbols is refused with a message that names the limitation.
    /// </summary>
    /// <remarks>
    /// C# spells integer bitwise <c>&amp;</c> with the same node as Boolean <c>&amp;</c>, and the
    /// old code cast both to a Boolean term - so this threw <see cref="InvalidCastException"/>
    /// from inside Z3, naming neither the operator nor the reason. Z3's integer sort has no
    /// bitwise operators (those need a bit-vector), so the honest answer is a clear refusal.
    /// </remarks>
    [TestMethod]
    [DataRow("&", DisplayName = "Bitwise AND")]
    [DataRow("|", DisplayName = "Bitwise OR")]
    [DataRow("^", DisplayName = "Bitwise XOR")]
    public void Solve_BitwiseOperatorOnIntegerSymbols_ThrowsNotSupportedNamingIt(string op)
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = op switch
        {
            "&" => context.NewTheorem<Symbols<int, int>>().Where(t => (t.X1 & 6) == 4),
            "|" => context.NewTheorem<Symbols<int, int>>().Where(t => (t.X1 | 1) == 7),
            _ => context.NewTheorem<Symbols<int, int>>().Where(t => (t.X1 ^ 2) == 3),
        };

        // Act
        NotSupportedException exception = Should.Throw<NotSupportedException>(() => theorem.Solve());

        // Assert
        exception.Message.ShouldContain(op);
        exception.Message.ShouldContain("bit-vector");
    }

    /// <summary>
    /// The modulo operator on real symbols is refused with a message that names the limitation.
    /// </summary>
    /// <remarks>
    /// <c>%</c> translated to Z3's integer remainder unconditionally, so a real modulo threw
    /// <see cref="InvalidCastException"/>. Z3 has no remainder on reals, so this is refused
    /// clearly rather than crashing in the cast.
    /// </remarks>
    [TestMethod]
    public void Solve_ModuloOnRealSymbols_ThrowsNotSupportedNamingIt()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<double, int>>()
            .Where(t => t.X1 % 2.5 == 0.5);

        // Act
        NotSupportedException exception = Should.Throw<NotSupportedException>(() => theorem.Solve());

        // Assert
        exception.Message.ShouldContain("modulo");
    }

    /// <summary>
    /// Boolean <c>&amp;</c>, <c>|</c> and <c>^</c> still translate, unchanged by the sort-aware
    /// dispatch.
    /// </summary>
    /// <remarks>
    /// The load-bearing regression guard: making the operators refuse the integer case must not
    /// disturb the Boolean case, which is the one the operators were written for.
    /// </remarks>
    [TestMethod]
    public void Solve_BooleanBitwiseOperators_StillTranslate()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<bool, bool>>()
            .Where(t => (t.X1 & t.X2) ^ (t.X1 | t.X2))
            .Where(t => t.X1)
            .Solve();

        // Assert: X1 & X2 differs from X1 | X2 exactly when the two differ, and X1 is true, so X2
        // must be false.
        result.ShouldNotBeNull();
        result.X1.ShouldBeTrue();
        result.X2.ShouldBeFalse();
    }

    [TestMethod]
    public void Solve_ModuloOnIntegerSymbols_StillTranslates()
    {
        // Arrange: the integer remainder the operator was always able to do.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 % 3 == 1)
            .Where(t => t.X1 > 3)
            .Where(t => t.X1 < 7)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(4);
    }
}
