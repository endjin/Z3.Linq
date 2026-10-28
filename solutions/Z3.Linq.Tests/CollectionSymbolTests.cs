namespace Z3.Linq.Tests;

/// <summary>
/// Collection-typed symbols: arrays and generic collections whose elements are constrained
/// individually and read back with an indexed <c>MkSelect</c>.
/// </summary>
/// <remarks>
/// <para>
/// A collection symbol is declared as a Z3 array from <c>Int</c> to the element type's sort -
/// the same sort a scalar of that type gets, from the one mapping the two share. Elements are
/// always read back with an integer index, and the number read is the <c>Count</c> of the
/// collection already on the instance - so an environment must pre-size its collections, and a
/// solution never changes their length.
/// </para>
/// <para>
/// Every element type a scalar supports now round-trips through a collection too. Until #64
/// only <c>int</c> did: collections carried a sort mapping of their own, and every row but the
/// <c>int</c> one declared a domain or range that contradicted how the elements were constrained
/// or read. Where a case failed depended on the constraint - one naming a constant of the
/// element type failed during translation, while elements left free solved cleanly and failed
/// in the marshalling loop instead - which is why the tests below cover both shapes. The Sudoku
/// examples are all <c>int</c>, which is why the limitation went unnoticed.
/// </para>
/// <para>
/// None of this reaches a collection whose elements are objects. The library builds one array
/// per property of the element type for those, but neither the visitor nor the marshaller can
/// use what it built, so a <c>Holder[]</c> symbol fails in every shape - #89. Nothing here
/// covers that path.
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
    /// A <c>long</c> collection round-trips values no <c>int</c> could hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first of the element types #64 fixed. Its range was declared as a 64-bit bit-vector
    /// while a constrained value translated to an integer term, so Z3 rejected the comparison
    /// outright - <c>Sorts (_ BitVec 64) and Int are incompatible</c>. The range is now the same
    /// <c>Int</c> a scalar <c>long</c> has always used.
    /// </para>
    /// <para>
    /// The values are beyond <c>Int32</c> range on purpose: a round-trip that silently narrowed
    /// the element read would only pass by accident.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_LongArraySymbol_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<LongArrayEnvironment>()
            .Where(t => t.Values[0] == 9_000_000_000L)
            .Where(t => t.Values[1] == -9_000_000_000L)
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([9_000_000_000L, -9_000_000_000L]);
    }

    [TestMethod]
    public void Solve_LongListSymbol_RoundTripsEveryElement()
    {
        // Arrange: the generic-collection branch, reconstructed through the constructor rather
        // than ToArray.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<LongListEnvironment>()
            .Where(t => t.Values[0] == 9_000_000_000L)
            .Where(t => t.Values[1] == 1L)
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([9_000_000_000L, 1L]);
    }

    /// <summary>
    /// A <c>bool</c> collection round-trips each element.
    /// </summary>
    /// <remarks>
    /// The case where the <em>domain</em> was wrong rather than the range: a <c>bool</c>
    /// collection was declared as an array indexed <em>by</em> <c>Bool</c>, and every element is
    /// read with an integer index - <c>domain sort Int and parameter Bool do not match</c>. It
    /// failed the same way whether or not the constraint named a constant, because the index is
    /// supplied by the library rather than by the constraint. The domain is now <c>Int</c> for
    /// every element type.
    /// </remarks>
    [TestMethod]
    public void Solve_BoolArraySymbol_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<BoolArrayEnvironment>()
            .Where(t => t.Values[0])
            .Where(t => !t.Values[1])
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([true, false]);
    }

    [TestMethod]
    public void Solve_BoolListSymbol_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<BoolListEnvironment>()
            .Where(t => !t.Values[0])
            .Where(t => t.Values[1])
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([false, true]);
    }

    /// <summary>
    /// A <c>double</c> collection round-trips each element.
    /// </summary>
    /// <remarks>
    /// The range was a floating-point sort while a constrained value translated to a real -
    /// <c>Sorts (_ FloatingPoint 11 53) and Real are incompatible</c>. A scalar <c>double</c> has
    /// always been a <c>Real</c>, and its collection now is too. The values are chosen to be
    /// exactly representable, so the assertion is about the sort and not about rounding.
    /// </remarks>
    [TestMethod]
    public void Solve_DoubleArraySymbol_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<DoubleArrayEnvironment>()
            .Where(t => t.Values[0] == 1.5)
            .Where(t => t.Values[1] == -2.25)
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([1.5, -2.25]);
    }

    [TestMethod]
    public void Solve_DoubleArrayWithRelationalConstraints_SatisfiesThemAll()
    {
        // Arrange: elements related to each other rather than pinned, over a real-sorted
        // collection - the shape the int tests above use, now available to every element type.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<DoubleArrayEnvironment>()
            .Where(t => t.Values[0] > 2.5)
            .Where(t => t.Values[1] == t.Values[0] * 2)
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values[0].ShouldBeGreaterThan(2.5);
        result.Values[1].ShouldBe(result.Values[0] * 2);
    }

    /// <summary>
    /// A <c>float</c> collection round-trips each element - the array half of #54, observable for
    /// the first time.
    /// </summary>
    /// <remarks>
    /// The element loop has had its own <c>TypeCode.Single</c> arm, corrected alongside the
    /// scalar one when #54 was fixed, and until #64 nothing could reach it: a constrained
    /// <c>float</c> collection failed during translation exactly as a <c>double</c> one did, and
    /// a free one got no further than the cast on the line before the parse. This is the first
    /// test that exercises that arm, and <c>0.1f</c> is here because it is not exactly
    /// representable - the parse has to produce the same <c>float</c> the constraint named.
    /// </remarks>
    [TestMethod]
    public void Solve_FloatArraySymbol_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<FloatArrayEnvironment>()
            .Where(t => t.Values[0] == 1.5f)
            .Where(t => t.Values[1] == 0.1f)
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([1.5f, 0.1f]);
    }

    /// <summary>
    /// A <c>decimal</c> collection round-trips each element.
    /// </summary>
    /// <remarks>
    /// This is the shape #64 listed for <c>decimal</c> - a constraint naming a decimal constant -
    /// and the reason it concluded the element loop was unreachable. The range it declared was a
    /// 32-bit floating-point sort, which would not have held a <c>decimal</c> even if the sorts
    /// had agreed; see <see cref="Solve_DecimalArraySymbol_RoundTripsAtFullPrecision"/> for the
    /// case that proves the range is now a <c>Real</c>.
    /// </remarks>
    [TestMethod]
    public void Solve_DecimalArraySymbol_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<DecimalArrayEnvironment>()
            .Where(t => t.Values[0] == 1.5m)
            .Where(t => t.Values[1] == 0.1m)
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([1.5m, 0.1m]);
    }

    /// <summary>
    /// A <c>decimal</c> element survives at full precision.
    /// </summary>
    /// <remarks>
    /// <c>decimal.MaxValue</c> has 29 significant digits. No floating-point sort Z3 offers could
    /// return it exactly, so this passing is direct evidence that the element range is the same
    /// unbounded <c>Real</c> a scalar <c>decimal</c> uses, rather than a wider float that would
    /// pass the test above and lose precision quietly on larger values.
    /// </remarks>
    [TestMethod]
    public void Solve_DecimalArraySymbol_RoundTripsAtFullPrecision()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<DecimalArrayEnvironment>()
            .Where(t => t.Values[0] == decimal.MaxValue)
            .Where(t => t.Values[1] == 0.0000000000000000000000000001m)
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([decimal.MaxValue, 0.0000000000000000000000000001m]);
    }

    [TestMethod]
    public void Solve_DecimalListSymbol_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<DecimalListEnvironment>()
            .Where(t => t.Values[0] == 2.5m)
            .Where(t => t.Values[1] == 1.5m)
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([2.5m, 1.5m]);
    }

    /// <summary>
    /// A <c>string</c> collection round-trips each element.
    /// </summary>
    /// <remarks>
    /// The other domain case, alongside <c>bool</c>: a <c>string</c> collection was declared as
    /// indexed by <c>String</c>, with a 16-bit bit-vector range that could not have held a
    /// string either. Both halves were wrong, and both now come from the shared mapping - an
    /// <c>Int</c> domain and the <c>String</c> sort a scalar uses. The empty string is included
    /// because it is what completion supplies for a free element, so a test that pinned only
    /// non-empty values could not tell a constrained element from an unconstrained one.
    /// </remarks>
    [TestMethod]
    public void Solve_StringArraySymbol_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<StringArrayEnvironment>()
            .Where(t => t.Values[0] == "abc")
            .Where(t => t.Values[1] == "")
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe(["abc", ""]);
    }

    /// <summary>
    /// A <c>DateTime</c> collection round-trips each instant exactly, as UTC.
    /// </summary>
    /// <remarks>
    /// <c>DateTime</c> shared the <c>long</c> row of the old mapping and failed the same two ways.
    /// #56 had already corrected the element read to <c>FromFileTimeUtc</c>, so once the range
    /// is <c>Int</c> nothing else on this path needs to change - the comment on #64 that
    /// measured exactly that is what this test pins.
    /// </remarks>
    [TestMethod]
    public void Solve_DateTimeArraySymbol_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();
        DateTime first = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        DateTime second = new(2026, 6, 2, 9, 30, 0, DateTimeKind.Utc);

        // Act
        var result = context.NewTheorem<DateTimeArrayEnvironment>()
            .Where(t => t.Values[0] == first)
            .Where(t => t.Values[1] == second)
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([first, second]);
        result.Values[0].Kind.ShouldBe(DateTimeKind.Utc);
        result.Values[1].Kind.ShouldBe(DateTimeKind.Utc);
    }

    [TestMethod]
    public void Solve_DateTimeListSymbol_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();
        DateTime instant = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = context.NewTheorem<DateTimeListEnvironment>()
            .Where(t => t.Values[0] == instant)
            .Where(t => t.Values[1] == instant)
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([instant, instant]);
    }

    /// <summary>
    /// A <c>DateTime</c> collection with free elements reads back as UTC.
    /// </summary>
    /// <remarks>
    /// The instants are completion's to choose and are not asserted - measured, they are
    /// <c>1601-01-01T00:00:00Z</c>, file time zero, which is the earliest instant the encoding
    /// can express (#83). The kind is fixed by the read path and can be asserted where the value
    /// cannot, as the scalar family in <c>SymbolTypeMarshallingTests</c> does.
    /// </remarks>
    [TestMethod]
    public void Solve_DateTimeArrayWithFreeElements_ReadsBackAsUtc()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<DateTimeArrayEnvironment>()
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.Length.ShouldBe(2);
        result.Values[0].Kind.ShouldBe(DateTimeKind.Utc);
        result.Values[1].Kind.ShouldBe(DateTimeKind.Utc);
    }

    /// <summary>
    /// A <c>short</c> collection round-trips each element.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>short</c> collection had three defects stacked, and this test is the first to reach
    /// the third. The range was a 16-bit bit-vector, so the widened comparison failed in the
    /// visitor (#64); behind that sat the <c>Convert</c> node #63 fixed for scalars, which the
    /// guard there handles for a select just as well; and behind <em>that</em> the element loop
    /// still shared its <c>Int16</c> arm with <c>Int32</c> and handed reflection an <c>int</c>.
    /// The last was recorded on #64 when #63 was fixed, because it could not be tested until this
    /// change made it reachable.
    /// </para>
    /// <para>
    /// The arithmetic form is here rather than in a separate test because it is the one that
    /// reaches the #63 guard: a bare equality against a constant is folded to a comparison of
    /// the select with a literal, while an addition is where C# inserts the widening.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_ShortArraySymbol_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<ShortArrayEnvironment>()
            .Where(t => t.Values[0] + t.Values[1] == 10)
            .Where(t => t.Values[0] == 4)
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([(short)4, (short)6]);
    }

    /// <summary>
    /// A <c>short</c> element whose model value no <c>short</c> can hold fails loudly.
    /// </summary>
    /// <remarks>
    /// The element read is a checked cast, as the scalar arm has been since #63 and for the same
    /// reason: the array's range is an unbounded <c>Int</c>, so a constraint written against the
    /// widened <c>int</c> can be satisfied by a value the element cannot hold, and an unchecked
    /// cast would wrap it into a plausible wrong answer. #87 - bounding the symbol so Z3 cannot
    /// pick such a value - applies to elements exactly as it does to scalars.
    /// </remarks>
    [TestMethod]
    public void Solve_ShortArrayElementConstrainedOutsideShortRange_ThrowsOverflowException()
    {
        // Arrange
        using var context = new Z3Context();
        int beyondShortRange = 40000;
        var theorem = context.NewTheorem<ShortArrayEnvironment>()
            .Where(t => t.Values[0] == beyondShortRange)
            .Where(t => t.Length == 2);

        // Act & Assert
        Should.Throw<OverflowException>(() => theorem.Solve());
    }

    /// <summary>
    /// A <c>decimal</c> collection with free elements is materialised at its initialised length.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shape that reaches the element loop without naming a decimal constant, and the one
    /// that observed #55 while #64 still stood: the decimal arm evaluated the whole array
    /// constant instead of the element the loop had just selected, and cast the result to
    /// <see cref="Microsoft.Z3.RatNum"/>, which an array expression can never satisfy. Before
    /// #64 the fix could only be seen in which type the failing cast named; now the loop
    /// completes, and reverting #55 fails this test outright.
    /// </para>
    /// <para>
    /// The element values come from model completion and are not asserted.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_DecimalArrayWithFreeElements_ReturnsTheInitialisedLength()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<DecimalArrayEnvironment>()
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.Length.ShouldBe(2);
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
    public void Solve_DecimalListWithFreeElements_ReturnsTheInitialisedLength()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<DecimalListEnvironment>()
            .Where(t => t.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.Count.ShouldBe(2);
    }

    /// <summary>
    /// Elements of a <c>decimal</c> collection can be related to each other.
    /// </summary>
    /// <remarks>
    /// Relating two elements puts both selects in the formula while naming no constant, which
    /// is the shape that reached the element loop under #64 and now yields a value. The lower
    /// bound is there so the equality cannot be satisfied trivially by the completion default.
    /// </remarks>
    [TestMethod]
    public void Solve_DecimalArrayWithElementsConstrainedToEachOther_ReturnsEqualElements()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<DecimalArrayEnvironment>()
            .Where(t => t.Values[0] == t.Values[1])
            .Where(t => t.Values[0] > 1.25m)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values[0].ShouldBeGreaterThan(1.25m);
        result.Values[1].ShouldBe(result.Values[0]);
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
    /// A field is no different from a property for element types other than <c>int</c>.
    /// </summary>
    /// <remarks>
    /// This was pinned as a failure while #64 stood, so that the #53 fix could not be mistaken
    /// for having widened the element types a field supports. It is kept as the positive case
    /// for the same reason in reverse: the sort mapping is chosen before the member kind matters,
    /// so a field gets the corrected declaration exactly as a property does.
    /// </remarks>
    [TestMethod]
    public void Solve_LongArrayInAPublicField_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<LongFieldCollectionEnvironment>()
            .Where(t => t.Values[0] == 9_000_000_000L)
            .Where(t => t.Values[1] == 2L)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldBe([9_000_000_000L, 2L]);
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

    private sealed class LongListEnvironment
    {
        public List<long> Values { get; set; } = [0L, 0L];

        public int Length { get; set; }
    }

    private sealed class BoolListEnvironment
    {
        public List<bool> Values { get; set; } = [false, false];

        public int Length { get; set; }
    }

    private sealed class StringArrayEnvironment
    {
        public string[] Values { get; set; } = new string[2];

        public int Length { get; set; }
    }

    private sealed class DateTimeArrayEnvironment
    {
        public DateTime[] Values { get; set; } = new DateTime[2];

        public int Length { get; set; }
    }

    private sealed class DateTimeListEnvironment
    {
        public List<DateTime> Values { get; set; } = [default, default];

        public int Length { get; set; }
    }

    private sealed class ShortArrayEnvironment
    {
        public short[] Values { get; set; } = new short[2];

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
