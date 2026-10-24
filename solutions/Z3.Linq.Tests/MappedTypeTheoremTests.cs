namespace Z3.Linq.Tests;

/// <summary>
/// A type carrying a <see cref="TheoremVariableTypeMappingAttribute"/> - a user type that stands
/// for one of the supported ones in a theorem, and reads back through its own constructor.
/// </summary>
/// <remarks>
/// <para>
/// The attribute is read from three places today: the environment builder declares the symbol
/// with the regular type's sort, the bounds asserter uses the same sort, and the marshaller reads
/// the value back through the mapped type's constructor. The expression visitor was the fourth
/// place that needed the same answer and did not ask for it, so the type behaved differently
/// depending on where it appeared in a constraint. A symbol of the mapped type was a real
/// symbol - it could be constrained, compared against another member, and put in arithmetic -
/// while a constant of the same type, reached through a cast, was looked up by its declared
/// <see cref="TypeCode.Object"/> and refused: there is no sort for "object", so the conversion
/// fell through to the catch-all <see cref="NotImplementedException"/>.
/// </para>
/// <para>
/// A constant of a mapped type passed as an instance rather than cast stays refused, and must
/// stay refused: the attribute promises a constructor from the regular type, not the conversion
/// the other way, so an arbitrary instance has no translation. That case is pinned separately
/// below because the bare message gave no way to guess the cast that does work.
/// </para>
/// <para>
/// The fixture type stands for <c>double</c> rather than for a <c>decimal</c> because a real
/// symbol is exact in Z3 whatever CLR type reads it back: 0.75 crosses the boundary without the
/// rounding a <c>float</c> would introduce, so the assertions can name the values.
/// </para>
/// </remarks>
[TestClass]
public class MappedTypeTheoremTests
{
    /// <summary>
    /// A constant cast to a mapped type is translated to the sort of the type it stands for.
    /// </summary>
    /// <remarks>
    /// The case that did not work: <c>(ExactReal)2.5</c> is a <c>Convert</c> node from
    /// <c>double</c> to a type whose <see cref="TypeCode"/> is <c>Object</c>, so the visitor
    /// found no target sort and threw, even though <c>t.x</c> - the same type, seen as a symbol -
    /// had been solved as a real two lines earlier. This is the assertion that fails without the
    /// fix and passes with it.
    /// </remarks>
    [TestMethod]
    public void Solve_ConstantCastToAMappedType_IsTranslated()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<ExactRealEnvironment>()
            .Where(t => t.x == (ExactReal)2.5)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.x.Value.ShouldBe(2.5);
    }

    /// <summary>
    /// A mapped constant takes part in arithmetic between two mapped symbols.
    /// </summary>
    /// <remarks>
    /// The conversion sits beside two symbols rather than alone, which is the shape the mapping
    /// exists for - a rational written as a fraction of two variables and compared against a
    /// literal. Two constraints pin both members, so a translation that dropped the constant and
    /// left the arithmetic free could not pass by completion alone.
    /// </remarks>
    [TestMethod]
    public void Solve_MappedTypeSymbolsInArithmeticWithAMappedConstant_RelateThem()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<ExactRealEnvironment>()
            .Where(t => t.x + t.y == (ExactReal)3.0)
            .Where(t => t.x - t.y == (ExactReal)1.0)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.x.Value.ShouldBe(2.0);
        result.y.Value.ShouldBe(1.0);
    }

    /// <summary>
    /// A symbol of a mapped type is read back through the mapped type's constructor.
    /// </summary>
    /// <remarks>
    /// The half that already worked, kept here as the control: without it, a fix that made the
    /// constant translate by treating the whole comparison as the regular type would look correct
    /// while quietly changing what <c>Solve</c> returns. The type has no parameterless
    /// constructor, so the solution can only be built through the <c>(double)</c> one.
    /// </remarks>
    [TestMethod]
    public void Solve_SymbolOfAMappedType_IsReadBackThroughItsConstructor()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<ExactRealEnvironment>()
            .Where(t => t.x > t.y)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.x.Value.ShouldBeGreaterThan(result.y.Value);
    }

    /// <summary>
    /// An instance of a mapped type passed as a constant is refused, and the message names both
    /// the type and the cast that does work.
    /// </summary>
    /// <remarks>
    /// A captured variable reaches the visitor as a constant of the mapped type itself, which has
    /// no translation: the attribute promises a constructor from the regular type, so the regular
    /// type can be read out of it, but nothing says how to build a term from an instance. The
    /// refusal is correct and stays; what the message adds is the way out, since a reader who
    /// wrote <c>t.x == value</c> has no reason to suspect that <c>t.x == (ExactReal)0.5</c> is
    /// the form that translates.
    /// </remarks>
    [TestMethod]
    public void Solve_InstanceOfAMappedTypeAsAConstant_RefusesAndNamesTheCast()
    {
        // Arrange: a captured variable rather than a literal, because a literal under an
        // explicit conversion is translated by the cast and never becomes a bare constant.
        using var context = new Z3Context();
        var value = new ExactReal(0.5);
        var theorem = context.NewTheorem<ExactRealEnvironment>().Where(t => t.x == value);

        // Act
        var exception = Should.Throw<NotSupportedException>(() => theorem.Solve());

        // Assert
        exception.Message.ShouldContain(nameof(ExactReal));
        exception.Message.ShouldContain("stands for");
    }

    /// <summary>
    /// A mapped type in an environment beside an ordinary symbol of its regular type.
    /// </summary>
    /// <remarks>
    /// The two members stand for the same sort by two different routes - one through the
    /// attribute, one through the built-in mapping - and the visitor has to relate them. This
    /// catches a fix that special-cases the conversion without asking the same question the
    /// environment builder asks.
    /// </remarks>
    [TestMethod]
    public void Solve_MappedTypeSymbolBesideItsRegularType_RelatesThem()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<MixedEnvironment>()
            .Where(t => t.exact == t.plain)
            .Where(t => t.plain == 1.25)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.exact.Value.ShouldBe(1.25);
        result.plain.ShouldBe(1.25);
    }

    private sealed class ExactRealEnvironment
    {
        public ExactReal x { get; set; }

        public ExactReal y { get; set; }
    }

    private sealed class MixedEnvironment
    {
        public ExactReal exact { get; set; }

        public double plain { get; set; }
    }

    /// <summary>
    /// A user type that stands for a <c>double</c> in a theorem.
    /// </summary>
    /// <remarks>
    /// Deliberately without a parameterless constructor, and with the arithmetic and comparison
    /// operators a caller would write in a constraint: the read-back path calls the constructor
    /// the attribute promises, and the operators keep the expressions in terms of the mapped type
    /// rather than widening them at the call site.
    /// </remarks>
    [TheoremVariableTypeMapping(typeof(double))]
    private readonly struct ExactReal
    {
        public ExactReal(double value) => this.Value = value;

        public double Value { get; }

        public static bool operator ==(ExactReal left, ExactReal right) => left.Value == right.Value;

        public static bool operator !=(ExactReal left, ExactReal right) => left.Value != right.Value;

        public static bool operator <(ExactReal left, ExactReal right) => left.Value < right.Value;

        public static bool operator >(ExactReal left, ExactReal right) => left.Value > right.Value;

        public static bool operator <=(ExactReal left, ExactReal right) => left.Value <= right.Value;

        public static bool operator >=(ExactReal left, ExactReal right) => left.Value >= right.Value;

        public static ExactReal operator +(ExactReal left, ExactReal right) => new(left.Value + right.Value);

        public static ExactReal operator -(ExactReal left, ExactReal right) => new(left.Value - right.Value);

        // So that a member of this type can be related to a member of the regular type without
        // the caller writing anything: the compiler inserts the conversion, and the visitor has
        // to reach the same answer for it that it reaches for the symbol.
        public static implicit operator ExactReal(double value) => new(value);

        public override bool Equals(object? obj) => obj is ExactReal other && this.Value.Equals(other.Value);

        public override int GetHashCode() => this.Value.GetHashCode();

        public override string ToString() => this.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }
}
