namespace Z3.Linq.Tests;

/// <summary>
/// A constant cast to a user type mapped by <see cref="TheoremVariableTypeMappingAttribute"/>
/// uses the same Z3 sort as a symbol of that type.
/// </summary>
[TestClass]
public class MappedTypeConversionTests
{
    [TestMethod]
    public void Solve_ConstantCastToMappedType_TranslatesUsingTheMappedSort()
    {
        using var context = new Z3Context();

        var result = context.NewTheorem<ExactRealEnvironment>()
            .Where(t => t.X == (ExactReal)2.5)
            .Solve();

        result.ShouldNotBeNull();
        result.X.Value.ShouldBe(2.5);
    }

    [TestMethod]
    public void Solve_MappedSymbolsAndConstantsInArithmetic_RelateThem()
    {
        using var context = new Z3Context();

        var result = context.NewTheorem<ExactRealEnvironment>()
            .Where(t => t.X + t.Y == (ExactReal)3.0)
            .Where(t => t.X - t.Y == (ExactReal)1.0)
            .Solve();

        result.ShouldNotBeNull();
        result.X.Value.ShouldBe(2.0);
        result.Y.Value.ShouldBe(1.0);
    }

    [TestMethod]
    public void Solve_MappedSymbol_ReadsBackThroughItsConstructor()
    {
        using var context = new Z3Context();

        var result = context.NewTheorem<ExactRealEnvironment>()
            .Where(t => t.X > t.Y)
            .Solve();

        result.ShouldNotBeNull();
        result.X.Value.ShouldBeGreaterThan(result.Y.Value);
    }

    [TestMethod]
    public void Solve_MappedInstanceAsConstant_RemainsUnsupported()
    {
        using var context = new Z3Context();
        var value = new ExactReal(0.5);
        var theorem = context.NewTheorem<ExactRealEnvironment>().Where(t => t.X == value);

        Should.Throw<NotSupportedException>(() => theorem.Solve());
    }

    private sealed class ExactRealEnvironment
    {
        public ExactReal X { get; set; }

        public ExactReal Y { get; set; }
    }

    [TheoremVariableTypeMapping(typeof(double))]
    private readonly struct ExactReal
    {
        public ExactReal(double value) => this.Value = value;

        public double Value { get; }

        public static implicit operator ExactReal(double value) => new(value);

        public static bool operator ==(ExactReal left, ExactReal right) => left.Value == right.Value;

        public static bool operator !=(ExactReal left, ExactReal right) => left.Value != right.Value;

        public static bool operator <(ExactReal left, ExactReal right) => left.Value < right.Value;

        public static bool operator >(ExactReal left, ExactReal right) => left.Value > right.Value;

        public static ExactReal operator +(ExactReal left, ExactReal right) => new(left.Value + right.Value);

        public static ExactReal operator -(ExactReal left, ExactReal right) => new(left.Value - right.Value);

        public override bool Equals(object? obj) => obj is ExactReal other && this.Value.Equals(other.Value);

        public override int GetHashCode() => this.Value.GetHashCode();
    }
}
