namespace Z3.Linq.Tests;

/// <summary>
/// Collection-typed symbols: arrays and generic collections whose elements are constrained
/// individually and read back with an indexed <c>MkSelect</c>.
/// </summary>
/// <remarks>
/// <para>
/// A collection symbol is declared as a Z3 array with a domain (index) sort and a range
/// (element) sort chosen per element type (Theorem.cs:205-235). Elements are always read with
/// an integer index (Theorem.cs:446), and the number read is the <c>Count</c> of the collection
/// already on the instance - so an environment must pre-size its collections, and a solution
/// never changes their length.
/// </para>
/// <para>
/// Only <c>int</c> elements work. Every other element type declares a domain or range that
/// contradicts how the elements are constrained or read, and throws during translation; those
/// cases are pinned below against #64. The Sudoku examples are all <c>int</c>, which is why the
/// limitation has gone unnoticed.
/// </para>
/// </remarks>
[TestClass]
public class CollectionSymbolTests
{
    [TestMethod]
    public void Solve_IntArraySymbol_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<IntArrayEnvironment>()
            .Where(t => t.Values[0] == 10)
            .Where(t => t.Values[1] == 20)
            .Where(t => t.Values[2] == 30)
            .Where(t => t.Length == 3)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([10, 20, 30]);
    }

    [TestMethod]
    public void Solve_IntArraySymbol_PreservesTheInitialisedLength()
    {
        // Arrange: the element count comes from the collection already on the instance, so the
        // result has exactly the length the environment was constructed with - a solution can
        // never grow or shrink it.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<IntArrayEnvironment>()
            .Where(t => t.Values[0] == 1)
            .Where(t => t.Values[1] == 2)
            .Where(t => t.Values[2] == 3)
            .Where(t => t.Length == 3)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.Length.ShouldBe(3);
    }

    [TestMethod]
    public void Solve_IntArrayWithRelationalConstraints_SatisfiesThemAll()
    {
        // Arrange: constraints relating elements to each other rather than pinning them, which
        // is how the Sudoku theorems actually use collection symbols.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<IntArrayEnvironment>()
            .Where(t => t.Values[0] > 0)
            .Where(t => t.Values[1] == t.Values[0] * 2)
            .Where(t => t.Values[2] == t.Values[1] + t.Values[0])
            .Where(t => t.Values[0] < 5)
            .Where(t => t.Length == 3)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values[0].ShouldBeGreaterThan(0);
        result.Values[0].ShouldBeLessThan(5);
        result.Values[1].ShouldBe(result.Values[0] * 2);
        result.Values[2].ShouldBe(result.Values[1] + result.Values[0]);
    }

    [TestMethod]
    public void Solve_IntArrayWithContradictoryElementConstraints_ReturnsNull()
    {
        // Arrange: one element cannot hold two values at once.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<IntArrayEnvironment>()
            .Where(t => t.Values[0] == 1)
            .Where(t => t.Values[0] == 2)
            .Solve();

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void Solve_GenericIntCollectionSymbol_RoundTripsEveryElement()
    {
        // Arrange: the non-array branch, reached for a generic type implementing IEnumerable.
        // It is reconstructed by passing the element array to the collection's constructor
        // (Theorem.cs:490), which List<int> supports.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<IntListEnvironment>()
            .Where(t => t.Values[0] == 7)
            .Where(t => t.Values[1] == 8)
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([7, 8]);
    }

    /// <summary>
    /// KNOWN DEFECT (#64): no collection element type other than <c>int</c> can be translated.
    /// </summary>
    /// <remarks>
    /// Each case declares a domain or range sort that contradicts either the constrained values
    /// or the integer index used to read elements back, so Z3 rejects the term outright. The
    /// message differs per type, so the assertion is on the exception rather than its text.
    /// These pin current behaviour and must be updated when the defect is fixed.
    /// </remarks>
    [TestMethod]
    public void Solve_LongArraySymbol_ThrowsZ3Exception()
    {
        // Arrange: range is BitVec 64 while the constrained value is an integer term.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<LongArrayEnvironment>()
            .Where(t => t.Values[0] == 1L)
            .Where(t => t.Length == 2);

        // Act & Assert
        Should.Throw<Microsoft.Z3.Z3Exception>(() => theorem.Solve());
    }

    /// <summary>KNOWN DEFECT (#64). See <see cref="Solve_LongArraySymbol_ThrowsZ3Exception"/>.</summary>
    [TestMethod]
    public void Solve_BoolArraySymbol_ThrowsZ3Exception()
    {
        // Arrange: the domain - the index sort - is declared Bool, but elements are read with an
        // integer index.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<BoolArrayEnvironment>()
            .Where(t => t.Values[0])
            .Where(t => t.Length == 2);

        // Act & Assert
        Should.Throw<Microsoft.Z3.Z3Exception>(() => theorem.Solve());
    }

    /// <summary>KNOWN DEFECT (#64). See <see cref="Solve_LongArraySymbol_ThrowsZ3Exception"/>.</summary>
    [TestMethod]
    public void Solve_DoubleArraySymbol_ThrowsZ3Exception()
    {
        // Arrange: range is a floating-point sort while the constrained value is a real.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<DoubleArrayEnvironment>()
            .Where(t => t.Values[0] == 1.5)
            .Where(t => t.Length == 2);

        // Act & Assert
        Should.Throw<Microsoft.Z3.Z3Exception>(() => theorem.Solve());
    }

    /// <summary>
    /// KNOWN DEFECT (#64), and the reason #55 cannot currently be observed.
    /// </summary>
    /// <remarks>
    /// #55 describes the decimal element branch evaluating the whole array expression instead of
    /// the selected element (Theorem.cs:471-474), which would give every element the same value.
    /// That code is still wrong, but unreachable: a decimal array never survives translation.
    /// </remarks>
    [TestMethod]
    public void Solve_DecimalArraySymbol_ThrowsZ3Exception()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<DecimalArrayEnvironment>()
            .Where(t => t.Values[0] == 1.5m)
            .Where(t => t.Length == 2);

        // Act & Assert
        Should.Throw<Microsoft.Z3.Z3Exception>(() => theorem.Solve());
    }

    /// <summary>
    /// KNOWN DEFECT (#53): a collection held in a public field cannot be marshalled.
    /// </summary>
    /// <remarks>
    /// Determining how many elements to read casts the reflection object itself rather than the
    /// value it holds - <c>((ICollection)info1).Count</c> where <c>info1</c> is the
    /// <see cref="System.Reflection.FieldInfo"/> (Theorem.cs:439). The property branch beside it
    /// correctly calls <c>GetValue</c> first, which is why an array property works and an array
    /// field does not.
    /// This test pins current behaviour and must be updated when the defect is fixed.
    /// </remarks>
    [TestMethod]
    public void Solve_CollectionInAPublicField_ThrowsInvalidCastException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<FieldCollectionEnvironment>()
            .Where(t => t.Values[0] == 1)
            .Where(t => t.Length == 2);

        // Act & Assert
        Should.Throw<InvalidCastException>(() => theorem.Solve());
    }

    /// <summary>
    /// An array symbol that no constraint touches is still materialised, at its initialised
    /// length.
    /// </summary>
    /// <remarks>
    /// The collection form of #51, and the only test covering the element-select evaluation.
    /// With no element constrained, the array constant itself has no interpretation, so every
    /// <c>MkSelect</c> against it evaluated to the select term rather than to a numeral. The
    /// element values come from model completion and are not asserted; the length is, because
    /// it comes from the instance rather than from the model.
    /// </remarks>
    [TestMethod]
    public void Solve_IntArraySymbolWithNoElementConstraints_ReturnsTheInitialisedLength()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<IntArrayEnvironment>()
            .Where(t => t.Length == 3)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.Length.ShouldBe(3);
        result.Length.ShouldBe(3);
    }

    /// <summary>
    /// Constraining one element of an array leaves the rest resolvable.
    /// </summary>
    /// <remarks>
    /// This one passed before #51 as well, and is here for the semantics it records rather than
    /// as proof of the fix. Once any element is constrained the array constant has an
    /// interpretation, and a free element resolves through that array's else-value rather than
    /// being assigned independently - measured as <c>[10, 10, 10]</c> here. So free elements are
    /// not independently arbitrary, which is why the blast radius of #51 on arrays was smaller
    /// than it looks. Only the constrained element is asserted; the else-value is Z3's choice.
    /// </remarks>
    [TestMethod]
    public void Solve_IntArrayWithSomeElementsConstrained_KeepsTheConstrainedElements()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<IntArrayEnvironment>()
            .Where(t => t.Values[0] == 10)
            .Where(t => t.Length == 3)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.Length.ShouldBe(3);
        result.Values[0].ShouldBe(10);
    }

    private sealed class IntArrayEnvironment
    {
        public int[] Values { get; set; } = new int[3];

        public int Length { get; set; }
    }

    private sealed class IntListEnvironment
    {
        public List<int> Values { get; set; } = [0, 0];

        public int Length { get; set; }
    }

    private sealed class LongArrayEnvironment
    {
        public long[] Values { get; set; } = new long[2];

        public int Length { get; set; }
    }

    private sealed class BoolArrayEnvironment
    {
        public bool[] Values { get; set; } = new bool[2];

        public int Length { get; set; }
    }

    private sealed class DoubleArrayEnvironment
    {
        public double[] Values { get; set; } = new double[2];

        public int Length { get; set; }
    }

    private sealed class DecimalArrayEnvironment
    {
        public decimal[] Values { get; set; } = new decimal[2];

        public int Length { get; set; }
    }

    private sealed class FieldCollectionEnvironment
    {
        public int[] Values = new int[2];

        public int Length { get; set; }
    }
}
