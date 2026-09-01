namespace Z3.Linq.Tests;

/// <summary>
/// Collection-typed symbols: arrays and generic collections whose elements are constrained
/// individually and read back with an indexed <c>MkSelect</c>.
/// </summary>
/// <remarks>
/// <para>
/// A collection symbol is declared as a Z3 array with a domain (index) sort and a range
/// (element) sort chosen per element type (Theorem.cs:205-235). Elements are always read with
/// an integer index (Theorem.cs:448), and the number read is the <c>Count</c> of the collection
/// already on the instance - so an environment must pre-size its collections, and a solution
/// never changes their length.
/// </para>
/// <para>
/// Only <c>int</c> elements work. Every other element type declares a domain or range that
/// contradicts how the elements are constrained or read; those cases are pinned below against
/// #64. Where they fail depends on the constraint - one naming a constant of the element type
/// fails during translation, while elements left free solve cleanly and fail in the marshalling
/// loop instead. The Sudoku examples are all <c>int</c>, which is why the limitation has gone
/// unnoticed.
/// </para>
/// <para>
/// A collection symbol can be a property or a public field, and since #53 the two behave
/// identically. A collection the environment leaves null throws either way, which is #78 - and
/// unavoidable for a ValueTuple, whose elements are fields it has no way to pre-size.
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
        // (Theorem.cs:496), which List<int> supports.
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
    /// KNOWN DEFECT (#64), and the reason the array half of #54 cannot be observed.
    /// </summary>
    /// <remarks>
    /// The element loop has its own <c>TypeCode.Single</c> arm with the same defect #54 fixed on
    /// the scalar path, and it was corrected in the same commit. Nothing reaches the parse it
    /// contains. The shape below fails during translation, because a float array declares a
    /// floating-point range against a real-valued constraint, exactly as a double array does;
    /// leaving the elements free instead gets past translation but no further than the cast on
    /// the line before the parse, as
    /// <see cref="Solve_DecimalArrayWithFreeElements_CastsTheElementNotTheArray"/> shows for the
    /// neighbouring arm.
    /// This test pins current behaviour and must be updated when the defect is fixed.
    /// </remarks>
    [TestMethod]
    public void Solve_FloatArraySymbol_ThrowsZ3Exception()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<FloatArrayEnvironment>()
            .Where(t => t.Values[0] == 1.5f)
            .Where(t => t.Length == 2);

        // Act & Assert
        Should.Throw<Microsoft.Z3.Z3Exception>(() => theorem.Solve());
    }

    /// <summary>
    /// KNOWN DEFECT (#64): a decimal array constrained against a decimal constant does not survive
    /// translation.
    /// </summary>
    /// <remarks>
    /// This is the shape #64 lists, and the reason it concluded the element loop was unreachable
    /// for a decimal. It is not the only shape: leaving the elements free reaches the loop, which
    /// is where the three tests below observe the #55 fix.
    /// This test pins current behaviour and must be updated when the defect is fixed.
    /// </remarks>
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
    /// The defect in #55: the decimal arm of the element loop evaluated the whole array constant
    /// instead of the element the loop had just selected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every other arm of that loop reads <c>numValExpr</c>, the result of
    /// <c>MkSelect(arrVal, MkInt(i))</c>. The decimal arm called <c>Eval</c> a second time on
    /// <c>subEnv.Expr</c> - the array itself - and cast the result to
    /// <see cref="Microsoft.Z3.RatNum"/>, which an array expression can never satisfy. The issue
    /// describes the symptom as every element taking the same value or the cast failing; measured,
    /// it is always the cast, in every shape a decimal collection can take.
    /// </para>
    /// <para>
    /// A decimal collection still cannot be solved, because #64 declares its range as a
    /// floating-point sort, so the assertion here is about which expression the cast rejects
    /// rather than about a value. The load-bearing half is that the message no longer names an
    /// array: that is #55, and reverting the fix fails on it. The element type it does name is
    /// #64's sort mapping, and that half must be updated when #64 is fixed.
    /// </para>
    /// <para>
    /// #64 records this code as unreachable. That holds only for a constraint naming a decimal
    /// constant, which fails during translation; with the elements left free the theorem solves
    /// and the element loop runs.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_DecimalArrayWithFreeElements_CastsTheElementNotTheArray()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<DecimalArrayEnvironment>()
            .Where(t => t.Length == 2);

        // Act
        InvalidCastException exception = Should.Throw<InvalidCastException>(() => theorem.Solve());

        // Assert
        exception.Message.ShouldNotContain("ArrayExpr");
        exception.Message.ShouldContain("FPNum");
    }

    /// <summary>
    /// A generic collection takes the same element loop as an array, so the #55 fix reaches it
    /// too.
    /// </summary>
    /// <remarks>
    /// Worth its own case because the collection branch materialises the result differently -
    /// <c>Activator.CreateInstance</c> against the constructed type rather than
    /// <c>ToArray</c> - and only the element read is shared.
    /// </remarks>
    [TestMethod]
    public void Solve_DecimalListWithFreeElements_CastsTheElementNotTheArray()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<DecimalListEnvironment>()
            .Where(t => t.Length == 2);

        // Act
        InvalidCastException exception = Should.Throw<InvalidCastException>(() => theorem.Solve());

        // Assert
        exception.Message.ShouldNotContain("ArrayExpr");
        exception.Message.ShouldContain("FPNum");
    }

    /// <summary>
    /// The element loop is reached by a theorem that genuinely constrains the elements, not only
    /// by one that leaves them out of the constraints altogether.
    /// </summary>
    /// <remarks>
    /// Relating two elements to each other puts both selects in the formula while naming no
    /// decimal constant, so nothing forces the sort mismatch #64 describes and translation
    /// succeeds. Without this the fix could be dismissed as only mattering to empty theorems.
    /// </remarks>
    [TestMethod]
    public void Solve_DecimalArrayWithElementsConstrainedToEachOther_CastsTheElementNotTheArray()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<DecimalArrayEnvironment>()
            .Where(t => t.Values[0] == t.Values[1]);

        // Act
        InvalidCastException exception = Should.Throw<InvalidCastException>(() => theorem.Solve());

        // Assert
        exception.Message.ShouldNotContain("ArrayExpr");
        exception.Message.ShouldContain("FPNum");
    }

    /// <summary>
    /// A collection held in a public field round-trips exactly as one held in a property does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The case in #53. Working out how many elements to read cast the reflection object itself -
    /// <c>((ICollection)info1).Count</c>, where <c>info1</c> was the
    /// <see cref="System.Reflection.FieldInfo"/> - so every collection field threw
    /// <see cref="InvalidCastException"/>, while the property branch beside it, which calls
    /// <c>GetValue</c> first, worked.
    /// </para>
    /// <para>
    /// Fixing it made the whole element-materialisation loop reachable through a field for the
    /// first time, so the tests below walk that path rather than stopping at this one case.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_IntArrayInAPublicField_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<FieldCollectionEnvironment>()
            .Where(t => t.Values[0] == 1)
            .Where(t => t.Values[1] == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([1, 2]);
    }

    [TestMethod]
    public void Solve_GenericIntCollectionInAPublicField_RoundTripsEveryElement()
    {
        // Arrange: the non-array branch of the same block, which reconstructs the collection
        // through its constructor rather than materialising an array.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<FieldListEnvironment>()
            .Where(t => t.Values[0] == 7)
            .Where(t => t.Values[1] == 8)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([7, 8]);
    }

    /// <summary>
    /// A field collection and a property collection in the same environment both round-trip.
    /// </summary>
    /// <remarks>
    /// The direct evidence that the two branches now agree: one environment, one solve, and each
    /// arm produces its own answer rather than one of them throwing.
    /// </remarks>
    [TestMethod]
    public void Solve_CollectionsInAFieldAndAPropertySideBySide_RoundTripBoth()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<FieldAndPropertyCollectionEnvironment>()
            .Where(t => t.FieldValues[0] == 1)
            .Where(t => t.PropertyValues[0] == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.FieldValues.ShouldBe([1]);
        result.PropertyValues.ShouldBe([2]);
    }

    [TestMethod]
    public void Solve_CollectionInAPublicFieldOfANestedObject_RoundTripsEveryElement()
    {
        // Arrange: the count is read from the object the field lives on, which for a nested
        // environment is the inner instance rather than the one being returned. Worth its own
        // case, because that argument is exactly what the defect got wrong.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<NestedFieldCollectionEnvironment>()
            .Where(t => t.Inner.Values[0] == 9)
            .Where(t => t.Inner.Values[1] == 10)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Inner.Values.ShouldBe([9, 10]);
    }

    /// <summary>
    /// A <c>readonly</c> collection field is written anyway.
    /// </summary>
    /// <remarks>
    /// The solution is assigned with <c>FieldInfo.SetValue</c>, which the runtime permits on an
    /// initonly instance field, so the modifier buys no protection here - the field ends up
    /// holding a different array from the one its initialiser created. Recorded because it is the
    /// opposite of what the declaration suggests, and because <c>readonly</c> is a natural thing
    /// to write on a collection that only exists to be pre-sized.
    /// </remarks>
    [TestMethod]
    public void Solve_CollectionInAReadOnlyField_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<ReadOnlyFieldCollectionEnvironment>()
            .Where(t => t.Values[0] == 3)
            .Where(t => t.Values[1] == 4)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([3, 4]);
    }

    [TestMethod]
    public void Solve_EmptyCollectionInAPublicField_ReturnsAnEmptyCollection()
    {
        // Arrange: the boundary at the other end - a count of zero, so the element loop never
        // runs and the field is assigned an empty array.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<EmptyFieldCollectionEnvironment>().Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBeEmpty();
    }

    /// <summary>
    /// A collection field no constraint touches keeps the length it was initialised with.
    /// </summary>
    /// <remarks>
    /// The length comes from the instance rather than from the model, so it survives a theorem
    /// that says nothing at all. The element values come from model completion (#51) and are not
    /// asserted.
    /// </remarks>
    [TestMethod]
    public void Solve_UnconstrainedCollectionInAPublicField_PreservesTheInitialisedLength()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<FieldCollectionEnvironment>().Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.Length.ShouldBe(2);
    }

    [TestMethod]
    public void OrderByDescending_OverAnElementOfACollectionField_ReturnsTheOptimum()
    {
        // Arrange: the optimiser reads its solution back through the same marshalling code, so
        // the fix has to hold on that path too.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<FieldCollectionEnvironment>()
            .Where(t => t.Values[0] > 0)
            .Where(t => t.Values[0] < 10)
            .OrderByDescending(t => t.Values[0])
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values[0].ShouldBe(9);
    }

    /// <summary>
    /// KNOWN DEFECT (#64): a field is no better off than a property for element types other than
    /// <c>int</c>.
    /// </summary>
    /// <remarks>
    /// Translation fails before any marshalling happens, so this failed identically before #53 was
    /// fixed. It is here so the fix cannot be mistaken for having widened the element types a
    /// field supports. See <see cref="Solve_LongArraySymbol_ThrowsZ3Exception"/>.
    /// This test pins current behaviour and must be updated when the defect is fixed.
    /// </remarks>
    [TestMethod]
    public void Solve_LongArrayInAPublicField_ThrowsZ3Exception()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<LongFieldCollectionEnvironment>()
            .Where(t => t.Values[0] == 1L);

        // Act & Assert
        Should.Throw<Microsoft.Z3.Z3Exception>(() => theorem.Solve());
    }

    /// <summary>
    /// KNOWN DEFECT (#78): a collection that is null on a freshly constructed environment throws
    /// <see cref="NullReferenceException"/>.
    /// </summary>
    /// <remarks>
    /// The count is read straight off the value the member holds, with no null check, so an
    /// environment that declares a collection without initialising it fails with nothing naming
    /// the member at fault. This predates #53 - the property form below has always behaved this
    /// way - so fixing #53 gave fields the same behaviour rather than introducing it.
    /// These tests pin current behaviour and must be updated when the defect is fixed.
    /// </remarks>
    [TestMethod]
    public void Solve_NullCollectionInAPublicField_ThrowsNullReferenceException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<NullFieldCollectionEnvironment>();

        // Act & Assert
        Should.Throw<NullReferenceException>(() => theorem.Solve());
    }

    /// <summary>
    /// KNOWN DEFECT (#78). The property form, which behaved this way before #53 was fixed too.
    /// </summary>
    [TestMethod]
    public void Solve_NullCollectionInAPublicProperty_ThrowsNullReferenceException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<NullPropertyCollectionEnvironment>();

        // Act & Assert
        Should.Throw<NullReferenceException>(() => theorem.Solve());
    }

    /// <summary>
    /// KNOWN DEFECT (#78): a collection in a tuple environment can never work.
    /// </summary>
    /// <remarks>
    /// A <c>ValueTuple</c> exposes its elements as public fields, so a tuple environment takes the
    /// branch #53 fixed. It still cannot hold a collection: the length comes from the instance,
    /// the instance is created with <c>Activator.CreateInstance</c>, and a tuple has nowhere to
    /// put an initialiser - so the element is always null. #78 is structural for tuples rather
    /// than a matter of remembering to initialise something.
    /// This test pins current behaviour and must be updated when the defect is fixed.
    /// </remarks>
    [TestMethod]
    public void Solve_CollectionInAValueTupleEnvironment_ThrowsNullReferenceException()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<(int[] Values, int Other)>().Where(t => t.Other == 1);

        // Act & Assert
        Should.Throw<NullReferenceException>(() => theorem.Solve());
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

    private sealed class FloatArrayEnvironment
    {
        public float[] Values { get; set; } = new float[2];

        public int Length { get; set; }
    }

    private sealed class DecimalArrayEnvironment
    {
        public decimal[] Values { get; set; } = new decimal[2];

        public int Length { get; set; }
    }

    private sealed class DecimalListEnvironment
    {
        public List<decimal> Values { get; set; } = [0m, 0m];

        public int Length { get; set; }
    }

    private sealed class FieldCollectionEnvironment
    {
        public int[] Values = new int[2];
    }

    private sealed class FieldListEnvironment
    {
        public List<int> Values = [0, 0];
    }

    private sealed class FieldAndPropertyCollectionEnvironment
    {
        public int[] FieldValues = new int[1];

        public int[] PropertyValues { get; set; } = new int[1];
    }

    private sealed class FieldCollectionHolder
    {
        public int[] Values = new int[2];
    }

    private sealed class NestedFieldCollectionEnvironment
    {
        public FieldCollectionHolder Inner { get; set; } = new();
    }

    private sealed class ReadOnlyFieldCollectionEnvironment
    {
        public readonly int[] Values = new int[2];
    }

    private sealed class EmptyFieldCollectionEnvironment
    {
        public int[] Values = [];
    }

    private sealed class LongFieldCollectionEnvironment
    {
        public long[] Values = new long[2];
    }

    private sealed class NullFieldCollectionEnvironment
    {
        public int[]? Values = null;
    }

    private sealed class NullPropertyCollectionEnvironment
    {
        public int[]? Values { get; set; }
    }
}
