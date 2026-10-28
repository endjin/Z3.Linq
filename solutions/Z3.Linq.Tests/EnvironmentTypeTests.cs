namespace Z3.Linq.Tests;

/// <summary>
/// The shapes a theorem environment can take: the built-in <c>Symbols</c> family, anonymous
/// types, value tuples, records, ordinary classes and nested objects.
/// </summary>
/// <remarks>
/// <para>
/// Two code paths sit behind these. A compiler-generated type - an anonymous type - is created
/// uninitialised and populated by writing to the compiler's backing fields, matched by name.
/// Everything else is created with <c>Activator.CreateInstance</c> and populated through its
/// properties and fields.
/// </para>
/// <para>
/// Since #75 the two share one marshaller, so an anonymous environment supports every type a
/// named one does, nested objects included. Until then the anonymous path carried a marshaller
/// of its own that handled <c>bool</c> and <c>int</c> only, and evaluated a property's handle
/// before checking its type - so a nested object, whose handle is null, reached Z3 and came back
/// as a bare <c>NullReferenceException</c>. The one shape still refused is a collection: the
/// instance passed to <c>NewTheorem</c> is discarded, so nothing can pre-size it, and it is
/// rejected by name. A value tuple looks like an anonymous type in source but is an ordinary
/// framework type, so it takes the second path.
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
    /// An anonymous environment supports the types a named one does.
    /// </summary>
    /// <remarks>
    /// This pinned a <see cref="NotSupportedException"/> until #75: the anonymous path had a
    /// marshaller of its own that handled <c>bool</c> and <c>int</c> and refused everything
    /// else, so a double that was perfectly usable in a named environment could not be used in
    /// an anonymous one. It now goes through the same <c>ConvertZ3Expression</c> as every other
    /// shape, and the restriction is gone with the code that imposed it.
    /// </remarks>
    [TestMethod]
    public void Solve_AnonymousTypeWithDoubleProperty_PopulatesIt()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem(new { d = default(double) })
            .Where(t => t.d == 1.5)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.d.ShouldBe(1.5);
    }

    [TestMethod]
    public void Solve_AnonymousTypeWithEveryScalarType_PopulatesEachOne()
    {
        // Arrange: one property of each type the sort mapping supports, in a single anonymous
        // environment, so a regression to a marshaller that knows some of them would show up as
        // a named failure rather than pass on the two it did handle.
        using var context = new Z3Context();
        DateTime instant = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = context.NewTheorem(new
            {
                flag = default(bool),
                n = default(int),
                s = default(short),
                l = default(long),
                f = default(float),
                d = default(double),
                m = default(decimal),
                text = default(string),
                when = default(DateTime),
            })
            .Where(t => t.flag)
            .Where(t => t.n == 1)
            .Where(t => t.s == 2)
            .Where(t => t.l == 9_000_000_000L)
            .Where(t => t.f == 1.5f)
            .Where(t => t.d == -2.25)
            .Where(t => t.m == 0.1m)
            .Where(t => t.text == "abc")
            .Where(t => t.when == instant)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.flag.ShouldBeTrue();
        result.n.ShouldBe(1);
        result.s.ShouldBe((short)2);
        result.l.ShouldBe(9_000_000_000L);
        result.f.ShouldBe(1.5f);
        result.d.ShouldBe(-2.25);
        result.m.ShouldBe(0.1m);
        result.text.ShouldBe("abc");
        result.when.ShouldBe(instant);
        result.when.Kind.ShouldBe(DateTimeKind.Utc);
    }

    /// <summary>
    /// The case in #75: an anonymous environment holding a nested object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the issue's repro verbatim - the nested object is not even constrained - and it
    /// threw a bare <see cref="NullReferenceException"/> from inside the native interop. A nested
    /// environment has no handle of its own, only children, and the anonymous branch evaluated
    /// the handle before looking at the type, so the null went straight to <c>Model.Eval</c>.
    /// The three evaluation sites in <c>ConvertZ3Expression</c> guarded against exactly that;
    /// this fourth one did not, and now no longer exists.
    /// </para>
    /// <para>
    /// The inner values come from completion and are not asserted; that the inner object is
    /// constructed at all is the point.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_AnonymousTypeWithAnUnconstrainedNestedObject_ConstructsIt()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem(new { inner = new InnerEnvironment(), n = default(int) })
            .Where(t => t.n == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.inner.ShouldNotBeNull();
        result.n.ShouldBe(1);
    }

    [TestMethod]
    public void Solve_AnonymousTypeWithANestedObject_PopulatesTheNestedProperties()
    {
        // Arrange: the same shape with the inner symbols constrained through the anonymous root,
        // which is what a caller would actually write. Translation already handled this - the
        // solve completed before the marshalling failure - so the assertion is on the values.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem(new { inner = new InnerEnvironment(), n = default(int) })
            .Where(t => t.inner.A == 5)
            .Where(t => t.inner.B == t.n * 2)
            .Where(t => t.n == 4)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.inner.A.ShouldBe(5);
        result.inner.B.ShouldBe(8);
        result.n.ShouldBe(4);
    }

    [TestMethod]
    public void Solve_AnonymousTypeWithANestedAnonymousType_PopulatesIt()
    {
        // Arrange: an anonymous type inside an anonymous type. The recursion re-enters the same
        // branch for the inner one, so both levels are written through backing fields.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem(new { inner = new { a = default(int) }, n = default(int) })
            .Where(t => t.inner.a == 4)
            .Where(t => t.n == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.inner.a.ShouldBe(4);
        result.n.ShouldBe(1);
    }

    /// <summary>
    /// A collection in an anonymous environment is rejected by name.
    /// </summary>
    /// <remarks>
    /// The one shape the shared marshaller cannot serve here. It reads the element count from
    /// the collection already on the instance, and an anonymous instance is created uninitialised
    /// - the one passed to <c>NewTheorem</c> is discarded - so there is nothing to read. Without
    /// the guard this would be #78's <see cref="NullReferenceException"/> on the count; with it,
    /// the message says which member and why. Pinned because dropping the guard passes every
    /// other test in this file.
    /// </remarks>
    [TestMethod]
    public void Solve_AnonymousTypeWithACollectionProperty_ThrowsNotSupportedException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem(new { v = new int[2], n = default(int) })
            .Where(t => t.v[0] == 3)
            .Where(t => t.n == 1);

        // Act
        NotSupportedException exception = Should.Throw<NotSupportedException>(() => theorem.Solve());

        // Assert
        exception.Message.ShouldContain("v");
        exception.Message.ShouldContain("pre-sized");
    }

    [TestMethod]
    public void Solve_ValueTupleWithDouble_PopulatesEveryField()
    {
        // Arrange: a value tuple is written the same way in source as an anonymous type but is
        // an ordinary framework type exposing public fields, so it takes the non-anonymous path.
        // Kept beside the anonymous double case because until #75 the two behaved differently.
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
    /// Environments are constructed with <c>Activator.CreateInstance(t)</c> (Theorem.cs:656),
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
    /// The anonymous path writes to compiler-generated backing fields, and until #75 it also
    /// evaluated the model through a call of its own, separate from the one every other shape
    /// uses - so it failed independently under #51, and this was the only test covering that
    /// call. It now marshals through <c>ConvertZ3Expression</c> like everything else; the test
    /// stays because the backing-field write is still this branch's own. Only <c>flag</c> is
    /// asserted - <c>n</c> is free and takes whatever completion supplies.
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
