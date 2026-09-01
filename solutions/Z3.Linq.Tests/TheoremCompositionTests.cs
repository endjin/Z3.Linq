namespace Z3.Linq.Tests;

/// <summary>
/// Composition and diagnostic behaviour that does not depend on the solver: how
/// <see cref="Theorem{T}.Where"/> accumulates constraints, what <see cref="Theorem.ToString"/>
/// renders, and what <see cref="Z3Context.Log"/> receives.
/// </summary>
/// <remarks>
/// Most of these assert without solving at all, which makes them the cheapest tests in the
/// suite. <c>Where</c> returns a new <see cref="Theorem{T}"/> over a concatenated constraint
/// sequence (Theorem{T}.cs:69), so composition is immutable and a theorem can be reused.
/// </remarks>
[TestClass]
public class TheoremCompositionTests
{
    [TestMethod]
    public void Where_AppliedToTheorem_DoesNotMutateTheOriginal()
    {
        // Arrange
        using var context = new Z3Context();
        var original = context.NewTheorem<Symbols<int, int>>();

        // Act
        var constrained = original.Where(t => t.X1 > 5);

        // Assert
        constrained.ShouldNotBeSameAs(original);
        original.ToString().ShouldBeEmpty();
        constrained.ToString().ShouldNotBeEmpty();
    }

    [TestMethod]
    public void Where_ChainedCalls_AccumulateEveryConstraint()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 > 5)
            .Where(t => t.X2 < 10)
            .Where(t => t.X1 != t.X2);

        // Assert: ToString joins constraint bodies with ", " (Theorem.cs:65).
        var rendered = theorem.ToString();
        rendered.ShouldContain("X1");
        rendered.ShouldContain("X2");
        rendered.Split(", ").Length.ShouldBe(3);
    }

    [TestMethod]
    public void ToString_TheoremWithNoConstraints_ReturnsEmptyString()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var rendered = context.NewTheorem<Symbols<int, int>>().ToString();

        // Assert
        rendered.ShouldBeEmpty();
    }

    [TestMethod]
    public void Where_OriginalTheorem_RemainsIndependentlySolvable()
    {
        // Arrange: the parent must still solve under its own constraints only, proving the
        // child's extra constraint did not leak backwards.
        using var context = new Z3Context();
        var parent = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == 3);
        var child = parent.Where(t => t.X1 == 4);

        // Act
        var parentResult = parent.Solve();
        var childResult = child.Solve();

        // Assert
        parentResult.ShouldNotBeNull();
        parentResult.X1.ShouldBe(3);

        // The child contradicts the parent, so it must be unsatisfiable.
        childResult.ShouldBeNull();
    }

    [TestMethod]
    public void Log_WhenSet_ReceivesOneEntryPerAssertedConstraint()
    {
        // Arrange: Theorem.cs:178 writes each asserted constraint's SMT form to the log.
        // Constraints are kept small deliberately - Z3's printer wraps long expressions across
        // lines, so line counting is only meaningful for short ones.
        using var log = new StringWriter();
        using var context = new Z3Context { Log = log };

        // Act
        _ = (from t in context.NewTheorem<Symbols<int, int>>()
             where t.X1 > 1
             where t.X2 > 2
             select t).Solve();

        // Assert
        // Splitting on both line-ending characters keeps this independent of the platform the
        // tests run on. Environment.NewLine is not an option: this namespace is nested inside
        // Z3.Linq, which is searched before any using directive, so the bare name binds to
        // Z3.Linq.Environment and System.Environment would have to be qualified.
        string[] lines = log.ToString()
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        lines.Length.ShouldBe(2);
    }

    [TestMethod]
    public void Log_WhenSet_NamesTheSymbolsBeingConstrained()
    {
        // Arrange
        using var log = new StringWriter();
        using var context = new Z3Context { Log = log };

        // Act
        _ = (from t in context.NewTheorem<Symbols<int, int>>()
             where t.X1 > t.X2
             select t).Solve();

        // Assert: assert on structure, not the exact s-expression - the rendering is a
        // function of the native Z3 version.
        var logged = log.ToString();
        logged.ShouldContain("X1");
        logged.ShouldContain("X2");
    }

    [TestMethod]
    public void Log_WhenNotSet_SolvingDoesNotThrow()
    {
        // Arrange: Log is null by default and LogWriteLine guards on it (Z3Context.cs:89).
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where t.X1 == 1
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        context.Log.ShouldBeNull();
    }

    [TestMethod]
    public void Dispose_CalledOnContext_LeavesTheContextUsable()
    {
        // Arrange: Z3Context.Dispose is a no-op (Z3Context.cs:39). The wrapper holds only a
        // config dictionary; the native context is created and disposed per solve
        // (Theorem.cs:75). Demo/Program.cs:86 already relies on this being harmless.
        var context = new Z3Context();

        // Act
        context.Dispose();
        context.Dispose();

        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where t.X1 == 11
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.ShouldBe(11);
    }

    [TestMethod]
    public void NewTheorem_WithATemplateInstance_InfersTheEnvironmentType()
    {
        // Arrange: the instance overload exists so anonymous types can be used inline. Since #78
        // the instance is also the template for the solution, but only the lengths of its
        // collections are read; with none, as here, it does nothing but supply the type.
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem(new { x = default(int), y = default(int) })
                      where t.x == 5
                      where t.y == t.x * 2
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.x.ShouldBe(5);
        result.y.ShouldBe(10);
    }

    [TestMethod]
    public void SymbolsToString_PopulatedSymbols_RendersEveryPropertyAndValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = (from t in context.NewTheorem<Symbols<int, int>>()
                      where t.X1 == 1
                      where t.X2 == 2
                      select t).Solve();

        // Assert: Symbols.ToString reflects over the properties (Symbols.cs:17).
        result.ShouldNotBeNull();
        result.ToString().ShouldBe("{X1 = 1, X2 = 2}");
    }
}
