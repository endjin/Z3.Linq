namespace Z3.Linq.Tests;

/// <summary>
/// Record (constructor-initialised) theorem environments.
/// </summary>
/// <remarks>
/// A solution used to be reconstructed either through the anonymous-type branch (uninitialised
/// object plus backing-field assignment) or through the named branch (<c>Activator.CreateInstance</c>
/// followed by property setters). Neither handles a positional record: it has no public
/// parameterless constructor, so <c>Activator.CreateInstance</c> throws, and its members are
/// init-only, so a setter cannot be used even if one were reached. The third branch evaluates
/// each constructor parameter from the model and invokes the primary constructor.
/// </remarks>
[TestClass]
public class RecordEnvTheoryTests
{
    [TestMethod]
    public void PositionalRecord_IsConstructedFromModel()
    {
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Point>()
            .Where(p => p.X + p.Y == 12)
            .Where(p => p.X == 2 * p.Y)
            .Solve();

        result.ShouldNotBeNull();
        (result!.X + result.Y).ShouldBe(12);
        result.X.ShouldBe(2 * result.Y);

        // X = 8, Y = 4 is the unique solution.
        result.X.ShouldBe(8);
        result.Y.ShouldBe(4);
    }

    [TestMethod]
    public void PositionalRecord_MixedScalarTypes_AreReadBack()
    {
        // A constructor mixing an int and a bool: each parameter goes through the same read as the
        // matching member would, so the two types must both survive the constructor path.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Mixed>()
            .Where(m => m.Count > 3 && m.Count < 6)
            .Where(m => m.Flag)
            .Solve();

        result.ShouldNotBeNull();
        result!.Count.ShouldBeInRange(4, 5);
        result.Flag.ShouldBeTrue();
    }

    [TestMethod]
    public void PositionalRecord_ThreeParameters_KeepConstructorOrder()
    {
        // Three parameters show that they are matched by name and passed positionally, rather than
        // in whatever order the environment happens to hold its members.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Triple>()
            .Where(t => t.A == 1)
            .Where(t => t.B == 2)
            .Where(t => t.C == 3)
            .Solve();

        result.ShouldNotBeNull();
        result!.A.ShouldBe(1);
        result.B.ShouldBe(2);
        result.C.ShouldBe(3);
    }

    [TestMethod]
    public void UnsatRecordTheorem_ReturnsNull()
    {
        // The constructor must not run when the theorem has no solution.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Point>()
            .Where(p => p.X > 0)
            .Where(p => p.X < 0)
            .Solve();

        result.ShouldBeNull();
    }

    /// <summary>A positional record with no parameterless constructor.</summary>
    /// <param name="X">The first coordinate.</param>
    /// <param name="Y">The second coordinate.</param>
    public record Point(int X, int Y);

    /// <summary>A record mixing two scalar constructor parameters of different types.</summary>
    /// <param name="Count">The integer member.</param>
    /// <param name="Flag">The boolean member.</param>
    public record Mixed(int Count, bool Flag);

    /// <summary>A record with three parameters, for name matching.</summary>
    /// <param name="A">The first member.</param>
    /// <param name="B">The second member.</param>
    /// <param name="C">The third member.</param>
    public record Triple(int A, int B, int C);
}
