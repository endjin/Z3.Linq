namespace Z3.Linq.Tests;

/// <summary>
/// The shapes a theorem environment can take: the built-in <c>Symbols</c> family, anonymous
/// types, value tuples, records, ordinary classes and nested objects.
/// </summary>
/// <remarks>
/// <para>
/// Two code paths sit behind these. A compiler-generated type - an anonymous type - is created
/// uninitialised and populated by writing to the compiler's backing fields, matched by name
/// (Theorem.cs:607-648). Everything else is created with <c>Activator.CreateInstance</c> and
/// populated through its properties and fields (Theorem.cs:651-696).
/// </para>
/// <para>
/// The split matters because the two support different types. The anonymous path handles only
/// <c>bool</c> and <c>int</c> and throws on anything else, while the ordinary path handles the
/// full set. A value tuple looks like an anonymous type in source but is an ordinary framework
/// type, so it takes the second path and is not restricted.
/// </para>
/// <para>
/// Note that <c>Symbols&lt;T1, T2, T3, T4&gt;</c> does not exist - the family provides arities
/// 2, 3 and 5 only.
/// </para>
/// </remarks>
[TestClass]
public class EnvironmentTypeTests
{
    [TestMethod]
    public void Solve_SymbolsWithThreeSymbols_PopulatesEveryProperty()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int, int>>()
            .Where(t => t.X1 == 1)
            .Where(t => t.X2 == 2)
            .Where(t => t.X3 == 3)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        (result.X1, result.X2, result.X3).ShouldBe((1, 2, 3));
    }

    [TestMethod]
    public void Solve_SymbolsWithFiveSymbols_PopulatesEveryProperty()
    {
        // Arrange: the largest arity the family provides.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, int, int, int, int>>()
            .Where(t => t.X1 == 1)
            .Where(t => t.X2 == 2)
            .Where(t => t.X3 == 3)
            .Where(t => t.X4 == 4)
            .Where(t => t.X5 == 5)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        (result.X1, result.X2, result.X3, result.X4, result.X5).ShouldBe((1, 2, 3, 4, 5));
    }

    [TestMethod]
    public void Solve_SymbolsWithMixedTypes_PopulatesEveryProperty()
    {
        // Arrange: the Symbols family is generic, so the arity and the element types vary
        // independently.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<int, bool, string>>()
            .Where(t => t.X1 == 1)
            .Where(t => t.X2)
            .Where(t => t.X3 == "three")
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(1);
        result.X2.ShouldBeTrue();
        result.X3.ShouldBe("three");
    }

    [TestMethod]
    public void Solve_AnonymousTypeEnvironment_PopulatesEveryProperty()
    {
        // Arrange: anonymous types have get-only properties, so the solution is written to the
        // compiler-generated backing fields instead.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem(new { flag = default(bool), n = default(int) })
            .Where(t => t.flag)
            .Where(t => t.n == 9)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.flag.ShouldBeTrue();
        result.n.ShouldBe(9);
    }

    /// <summary>
    /// The anonymous-type path supports only <c>bool</c> and <c>int</c>.
    /// </summary>
    /// <remarks>
    /// Theorem.cs:645 throws for any other property type, so a double that is perfectly usable
    /// in a named environment cannot be used in an anonymous one. This is a real restriction
    /// rather than a defect - the branch never grew the other cases - but it is undocumented,
    /// and the failure names the property rather than explaining the restriction.
    /// </remarks>
    [TestMethod]
    public void Solve_AnonymousTypeWithDoubleProperty_ThrowsNotSupportedException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem(new { d = default(double) })
            .Where(t => t.d == 1.5);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => theorem.Solve());
    }

    [TestMethod]
    public void Solve_ValueTupleWithDouble_PopulatesEveryField()
    {
        // Arrange: the counterpart to the test above. A value tuple is written the same way in
        // source but is an ordinary framework type exposing public fields, so it takes the
        // non-anonymous path and the bool/int restriction does not apply.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<(int a, double b)>()
            .Where(t => t.a == 3)
            .Where(t => t.b == 2.5)
            .Solve();

        // Assert
        result.a.ShouldBe(3);
        result.b.ShouldBe(2.5);
    }

    [TestMethod]
    public void Solve_ValueTupleWithString_PopulatesEveryField()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<(int a, string s)>()
            .Where(t => t.a == 1)
            .Where(t => t.s == "hi")
            .Solve();

        // Assert
        result.a.ShouldBe(1);
        result.s.ShouldBe("hi");
    }

    [TestMethod]
    public void Solve_RecordEnvironment_PopulatesEveryProperty()
    {
        // Arrange: a record declared with a body rather than a positional parameter list has an
        // implicit parameterless constructor and settable properties, so it behaves as an
        // ordinary class here. RecordTheorem in the examples carries a comment saying this does
        // not work; it does, and the demo runs one unguarded.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem(new BodyRecord())
            .Where(t => t.X)
            .Where(t => !t.Y)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X.ShouldBeTrue();
        result.Y.ShouldBeFalse();
    }

    /// <summary>
    /// A positional record cannot be used as an environment.
    /// </summary>
    /// <remarks>
    /// Environments are constructed with <c>Activator.CreateInstance(t)</c> (Theorem.cs:653),
    /// and a positional record's only constructor takes its members, so there is nothing to
    /// call. This is the distinction the stale comment on RecordTheorem was probably reaching
    /// for: records work, positional records do not.
    /// </remarks>
    [TestMethod]
    public void Solve_PositionalRecordEnvironment_ThrowsMissingMethodException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<PositionalRecord>().Where(t => t.X);

        // Act & Assert
        Should.Throw<MissingMethodException>(() => theorem.Solve());
    }

    [TestMethod]
    public void Solve_NestedObjectEnvironment_PopulatesTheNestedProperties()
    {
        // Arrange: a property whose type is itself an environment recurses through GetSolution,
        // building a sub-environment per level.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<OuterEnvironment>()
            .Where(t => t.Inner.A == 4)
            .Where(t => t.Inner.B == 5)
            .Where(t => t.Top == 6)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Inner.ShouldNotBeNull();
        result.Inner.A.ShouldBe(4);
        result.Inner.B.ShouldBe(5);
        result.Top.ShouldBe(6);
    }

    [TestMethod]
    public void Solve_NestedObjectEnvironment_ConstrainsAcrossNestingLevels()
    {
        // Arrange: a constraint relating an inner symbol to an outer one, to show the levels
        // share a single set of Z3 constants rather than being solved independently.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<OuterEnvironment>()
            .Where(t => t.Inner.A == t.Top * 2)
            .Where(t => t.Top == 5)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Top.ShouldBe(5);
        result.Inner.A.ShouldBe(10);
    }

    [TestMethod]
    public void Solve_EnvironmentWithPrivateSetters_PopulatesThemAnyway()
    {
        // Arrange: the Symbols family declares private setters, so reflection has to be writing
        // through them. This pins that behaviour for a plain class, since a public-setter-only
        // implementation would break every built-in environment.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<PrivateSetterEnvironment>()
            .Where(t => t.A == 3)
            .Where(t => t.B == 4)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.A.ShouldBe(3);
        result.B.ShouldBe(4);
    }

    /// <summary>
    /// An anonymous environment with an unconstrained property is still populated.
    /// </summary>
    /// <remarks>
    /// The anonymous path writes to compiler-generated backing fields and evaluates the model
    /// through its own call, separate from the one every other environment shape uses. So it
    /// failed independently under #51 and it can regress independently: this is the only test
    /// covering that call. Only <c>flag</c> is asserted - <c>n</c> is free and takes whatever
    /// completion supplies.
    /// </remarks>
    [TestMethod]
    public void Solve_AnonymousTypeWithAnUnconstrainedProperty_PopulatesTheConstrainedOne()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem(new { flag = default(bool), n = default(int) })
            .Where(t => t.flag)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.flag.ShouldBeTrue();
    }

    /// <summary>
    /// A nested environment whose inner properties are unconstrained is still constructed.
    /// </summary>
    /// <remarks>
    /// The nested environment is built from the type, not from the constraints, so every inner
    /// symbol exists whether or not anything mentions it - and each is marshalled by the same
    /// recursion that handles a top-level one. Before #51 a single free leaf anywhere in the
    /// tree failed the whole solve.
    /// </remarks>
    [TestMethod]
    public void Solve_NestedObjectEnvironmentWithUnconstrainedInnerProperties_PopulatesTheOuterOne()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<OuterEnvironment>()
            .Where(t => t.Top == 6)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Inner.ShouldNotBeNull();
        result.Top.ShouldBe(6);
    }

    private sealed record BodyRecord
    {
        public bool X { get; set; }

        public bool Y { get; set; }
    }

    private sealed record PositionalRecord(bool X, bool Y);

    private sealed class InnerEnvironment
    {
        public int A { get; set; }

        public int B { get; set; }
    }

    private sealed class OuterEnvironment
    {
        public InnerEnvironment Inner { get; set; } = new();

        public int Top { get; set; }
    }

    private sealed class PrivateSetterEnvironment
    {
        public int A { get; private set; }

        public int B { get; private set; }
    }
}
