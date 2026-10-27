namespace Z3.Linq.Tests;

/// <summary>
/// Members that are not symbols but values: a static field or property, and a chain of members
/// on a captured variable. Each is read when the theorem is solved and becomes a constant.
/// </summary>
/// <remarks>
/// <para>
/// A <c>const</c> is folded by the compiler, so <c>int.MaxValue</c> reaches the visitor as a
/// constant. A <c>static readonly</c> field is not - <c>DateTime.MaxValue</c> is one - and nor is
/// a static property, so the expression tree carries a member access with no instance. The visitor
/// had no case for that, and threw <c>Unknown parameter encountered</c> for every static member.
/// The DateTime tests had stayed clear of it by copying the boundary into a local first.
/// </para>
/// <para>
/// A captured variable is a field on a compiler-generated closure, so <c>holder.In.Value</c> is a
/// chain of three members starting at a constant. The visitor walked the chain but read every
/// member from the closure rather than from the previous member's value, so any chain longer than
/// one threw <see cref="System.Reflection.TargetException"/>.
/// </para>
/// </remarks>
[TestClass]
public class ConstantMemberTests
{
    private static readonly int StaticReadonlyLimit = 5;

    private static int mutableLimit;

    private static int StaticPropertyLimit => 6;

    [TestMethod]
    public void Solve_DateTimeMaxValueInline_RoundTrips()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 == DateTime.MaxValue)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(DateTime.MaxValue);
    }

    [TestMethod]
    public void Solve_DateTimeMinValueInline_RoundTrips()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 == DateTime.MinValue)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(DateTime.MinValue);
    }

    [TestMethod]
    public void Solve_DateTimeUnixEpochInline_RoundTrips()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 == DateTime.UnixEpoch)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(DateTime.UnixEpoch);
    }

    /// <summary>
    /// A DateTime beyond <see cref="DateTime.MaxValue"/> has no solution.
    /// </summary>
    /// <remarks>
    /// The symbol is bounded to the DateTime range in ticks (#98), so asking for a value beyond
    /// the top of the range is unsatisfiable rather than a model the marshaller would then have to
    /// refuse. Before the fix this threw on <c>MaxValue</c> before Z3 was asked anything.
    /// </remarks>
    [TestMethod]
    public void TrySolve_DateTimeBeyondMaxValueInline_HasNoSolution()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 > DateTime.MaxValue);

        // Act
        bool satisfiable = theorem.TrySolve(out _);

        // Assert
        satisfiable.ShouldBeFalse();
    }

    [TestMethod]
    public void Solve_StaticReadonlyField_IsReadAsAConstant()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == StaticReadonlyLimit)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(5);
    }

    [TestMethod]
    public void Solve_StaticProperty_IsReadAsAConstant()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == StaticPropertyLimit)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(6);
    }

    /// <summary>
    /// A static member is read when the theorem is solved, not when the constraint is written.
    /// </summary>
    /// <remarks>
    /// The same as a captured local: the constraint holds an expression that names the member, and
    /// the member is read when that expression is translated. A change between <c>Where</c> and
    /// <c>Solve</c> is therefore seen.
    /// </remarks>
    [TestMethod]
    public void Solve_MutableStaticField_IsReadWhenSolved()
    {
        // Arrange
        using var context = new Z3Context();
        mutableLimit = 1;
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == mutableLimit)
            .Where(t => t.X2 == 1);
        mutableLimit = 8;

        // Act
        var result = theorem.Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(8);
    }

    [TestMethod]
    public void Solve_MemberChainOnACapturedVariable_ReadsEachMemberFromTheLast()
    {
        // Arrange
        using var context = new Z3Context();
        var holder = new Holder();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == holder.In.Value)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(7);
    }

    [TestMethod]
    public void Solve_MemberChainOnAStaticMember_ReadsEachMemberFromTheLast()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == Holder.Shared.In.Value)
            .Where(t => t.X2 == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(7);
    }

    /// <summary>
    /// A member chain that reaches null is refused by name rather than with a null reference.
    /// </summary>
    [TestMethod]
    public void Solve_MemberChainThroughANull_ThrowsNamingTheExpression()
    {
        // Arrange
        using var context = new Z3Context();
        var holder = new Holder { In = null! };
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == holder.In.Value);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => theorem.Solve()).Message.ShouldContain("holder.In");
    }

    private sealed class Holder
    {
        public static Holder Shared { get; } = new();

        public Inner In { get; set; } = new();
    }

    private sealed class Inner
    {
        public int Value { get; set; } = 7;
    }
}
