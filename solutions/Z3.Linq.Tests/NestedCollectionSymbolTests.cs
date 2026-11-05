namespace Z3.Linq.Tests;

/// <summary>
/// Nested collection symbols: a collection whose elements are themselves collections, such as
/// <c>int[][]</c>, <c>List&lt;int[]&gt;</c> or <c>List&lt;List&lt;int&gt;&gt;</c>, constrained
/// element by element and read back row by row.
/// </summary>
/// <remarks>
/// <para>
/// A nested collection is declared as a Z3 array from <c>Int</c> to the sort of its element
/// type, derived recursively: an <c>int[][]</c> is an array from <c>Int</c> to an array from
/// <c>Int</c> to <c>Int</c>. A constraint such as <c>t.Cells[i][j]</c> is then two selects, the
/// first of which is itself an array - so the visitor needs nothing new, and an index can be a
/// symbol as well as a literal.
/// </para>
/// <para>
/// Before #43 was addressed none of this worked. The environment builder saw that the element type of an
/// <c>int[][]</c> - <c>int[]</c> - has a <see cref="TypeCode"/> of <c>Object</c>, took it for a
/// collection of objects (#89) and walked the public properties of <see cref="Array"/>, failing
/// at <c>SyncRoot</c> with "Only one level of object collections is currently supported". A
/// collection of objects that hold collections still fails that way.
/// </para>
/// <para>
/// Lengths come from instances exactly as they do for a flat collection (#53, #78): the outer
/// collection on the template passed to <c>NewTheorem</c>, or failing that its initialiser, and
/// each row from that same outer collection - so a template sets the row lengths too, rows may
/// differ in length, and a row that is null is rejected by name. Free elements are filled by
/// model completion like flat ones (#51), and elements are not bounded to their type's range,
/// so a <c>short</c> element read back out of range is loud rather than wrapped.
/// </para>
/// <para>
/// A multidimensional array such as <c>int[,]</c> is not a nested collection and is still not
/// supported.
/// </para>
/// </remarks>
[TestClass]
public class NestedCollectionSymbolTests
{
    [TestMethod]
    public void Solve_JaggedIntArraySymbol_RoundTripsEveryElement()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<JaggedIntEnvironment>()
            .Where(t => t.Cells[0][0] == 1)
            .Where(t => t.Cells[0][1] == 2)
            .Where(t => t.Cells[1][0] == 3)
            .Where(t => t.Cells[1][1] == 4)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Cells.Length.ShouldBe(2);
        result.Cells[0].ShouldBe([1, 2]);
        result.Cells[1].ShouldBe([3, 4]);
    }

    [TestMethod]
    public void Solve_JaggedIntArrayWithRelationalConstraints_SatisfiesThemAll()
    {
        // Arrange: elements related to each other across rows rather than pinned, so more than
        // one model satisfies them and only the relations are asserted.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<JaggedIntEnvironment>()
            .Where(t => t.Cells[0][0] > 0)
            .Where(t => t.Cells[0][0] < 5)
            .Where(t => t.Cells[0][1] == t.Cells[0][0] * 2)
            .Where(t => t.Cells[1][0] == t.Cells[0][1] + 1)
            .Where(t => t.Cells[1][1] == t.Cells[1][0] - t.Cells[0][0])
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Cells[0][0].ShouldBeGreaterThan(0);
        result.Cells[0][0].ShouldBeLessThan(5);
        result.Cells[0][1].ShouldBe(result.Cells[0][0] * 2);
        result.Cells[1][0].ShouldBe(result.Cells[0][1] + 1);
        result.Cells[1][1].ShouldBe(result.Cells[1][0] - result.Cells[0][0]);
    }

    [TestMethod]
    public void Solve_JaggedIntArrayWithFreeElements_ReturnsTheInitialisedShape()
    {
        // Arrange: no constraint mentions the symbol, so the model does not interpret it and
        // every element comes from completion (#51). The values are Z3's choice; only the shape
        // - three rows of lengths 1, 2 and 3 - is asserted.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<RaggedIntEnvironment>()
            .Where(t => t.Other == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Cells.Select(static row => row.Length).ShouldBe([1, 2, 3]);
    }

    [TestMethod]
    public void Solve_JaggedIntArrayWithSomeElementsConstrained_KeepsTheConstrainedElements()
    {
        // Arrange: the other elements are free and their values are Z3's choice, so only the
        // constrained ones and the shape are asserted.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<RaggedIntEnvironment>()
            .Where(t => t.Cells[0][0] == 10)
            .Where(t => t.Cells[2][2] == 30)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Cells.Select(static row => row.Length).ShouldBe([1, 2, 3]);
        result.Cells[0][0].ShouldBe(10);
        result.Cells[2][2].ShouldBe(30);
    }

    [TestMethod]
    public void Solve_JaggedIntArrayWithContradictoryElementConstraints_ReturnsNull()
    {
        // Arrange: one element cannot hold two values at once.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<JaggedIntEnvironment>()
            .Where(t => t.Cells[1][0] == 1)
            .Where(t => t.Cells[1][0] == 2)
            .Solve();

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// A 2x2 Latin square over a jagged array has exactly one solution once a corner is fixed.
    /// </summary>
    /// <remarks>
    /// The shape a grid puzzle such as Sudoku takes over a nested collection: rows and columns
    /// distinct, every cell in range. With the values 1 and 2 and <c>Cells[0][0] == 1</c>, the
    /// only square is <c>[[1, 2], [2, 1]]</c>, so the exact values are asserted.
    /// </remarks>
    [TestMethod]
    public void Solve_LatinSquareOverAJaggedArray_ReturnsTheUniqueSolution()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<JaggedIntEnvironment>()
            .Where(t => t.Cells[0][0] >= 1 && t.Cells[0][0] <= 2)
            .Where(t => t.Cells[0][1] >= 1 && t.Cells[0][1] <= 2)
            .Where(t => t.Cells[1][0] >= 1 && t.Cells[1][0] <= 2)
            .Where(t => t.Cells[1][1] >= 1 && t.Cells[1][1] <= 2)
            .Where(t => Z3Methods.Distinct(t.Cells[0][0], t.Cells[0][1]))
            .Where(t => Z3Methods.Distinct(t.Cells[1][0], t.Cells[1][1]))
            .Where(t => Z3Methods.Distinct(t.Cells[0][0], t.Cells[1][0]))
            .Where(t => Z3Methods.Distinct(t.Cells[0][1], t.Cells[1][1]))
            .Where(t => t.Cells[0][0] == 1)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Cells[0].ShouldBe([1, 2]);
        result.Cells[1].ShouldBe([2, 1]);
    }

    [TestMethod]
    public void Solve_ListOfIntArraysSymbol_RoundTripsEveryElement()
    {
        // Arrange: the outer collection is generic, so its index arrives as a get_Item call
        // rather than an array index, and it is rebuilt through its constructor.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<ListOfArraysEnvironment>()
            .Where(t => t.Cells[0][0] == 5)
            .Where(t => t.Cells[0][1] == 6)
            .Where(t => t.Cells[1][0] == 7)
            .Where(t => t.Cells[1][1] == 8)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Cells.Count.ShouldBe(2);
        result.Cells[0].ShouldBe([5, 6]);
        result.Cells[1].ShouldBe([7, 8]);
    }

    [TestMethod]
    public void Solve_ListOfListsSymbol_RoundTripsEveryElement()
    {
        // Arrange: generic at both levels, so both indexes are get_Item calls and both levels are
        // rebuilt through the constructor.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<ListOfListsEnvironment>()
            .Where(t => t.Cells[0][0] == 1)
            .Where(t => t.Cells[0][1] == 2)
            .Where(t => t.Cells[1][0] == 3)
            .Where(t => t.Cells[1][1] == 4)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Cells.Count.ShouldBe(2);
        result.Cells[0].ShouldBe([1, 2]);
        result.Cells[1].ShouldBe([3, 4]);
    }

    [TestMethod]
    public void Solve_ThreeLevelIntArraySymbol_RoundTripsEveryElement()
    {
        // Arrange: the sort and the read-back are both recursive, so a third level needs nothing
        // a second does not.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<ThreeLevelIntEnvironment>()
            .Where(t => t.Cells[0][0][0] == 11)
            .Where(t => t.Cells[0][0][1] == 12)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Cells.Length.ShouldBe(1);
        result.Cells[0].Length.ShouldBe(1);
        result.Cells[0][0].ShouldBe([11, 12]);
    }

    [TestMethod]
    public void Solve_JaggedDoubleArraySymbol_RoundTripsEveryElement()
    {
        // Arrange: the innermost range is a real, from the same mapping a scalar double uses.
        // The values are exactly representable, so the assertion is about the sort and not about
        // rounding.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<JaggedDoubleEnvironment>()
            .Where(t => t.Cells[0][0] == 1.5)
            .Where(t => t.Cells[0][1] == -2.25)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Cells[0].ShouldBe([1.5, -2.25]);
    }

    [TestMethod]
    public void Solve_JaggedShortArrayElementConstrainedOutsideShortRange_ThrowsOverflowException()
    {
        // Arrange: elements are not bounded to their type's range, at any depth, so the checked
        // read at the leaf is what makes an out-of-range value loud rather than wrapped.
        using var context = new Z3Context();
        int beyondShortRange = 40000;
        var theorem = context.NewTheorem<JaggedShortEnvironment>()
            .Where(t => t.Cells[0][1] == beyondShortRange);

        // Act & Assert
        Should.Throw<OverflowException>(() => theorem.Solve());
    }

    [TestMethod]
    public void Solve_JaggedArrayWithANullRow_ThrowsNotSupportedExceptionNamingTheRow()
    {
        // Arrange: the outer collection has a length but its rows do not, so the row is named
        // with its index rather than failing on the null.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<NullRowEnvironment>()
            .Where(t => t.Other == 1);

        // Act & Assert
        NotSupportedException exception = Should.Throw<NotSupportedException>(() => theorem.Solve());
        exception.Message.ShouldStartWith("Collection symbol Cells[0] has no length");
    }

    [TestMethod]
    public void Solve_NullJaggedArray_IsSizedByTheTemplate()
    {
        // Arrange: the type never initialises the collection, so the instance passed in sets both
        // the number of rows and the length of each.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem(new NullJaggedPropertyEnvironment { Cells = [new int[1], new int[3]] })
            .Where(t => t.Cells![1][2] == 9)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Cells.ShouldNotBeNull();
        result.Cells.Select(static row => row.Length).ShouldBe([1, 3]);
        result.Cells[1][2].ShouldBe(9);
    }

    /// <summary>
    /// A template's row lengths beat the type's own initialiser.
    /// </summary>
    /// <remarks>
    /// <c>JaggedIntEnvironment</c> initialises two rows of two; the instance passed in has rows
    /// of three and one. The rows are read from the same collection as the outer length, so the
    /// template wins for the rows as it does for the outer collection (#78).
    /// </remarks>
    [TestMethod]
    public void Solve_TemplateRowLengths_TakePrecedenceOverTheInitialiser()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem(new JaggedIntEnvironment { Cells = [new int[3], new int[1]] })
            .Where(t => t.Cells[0][2] == 7)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Cells.Select(static row => row.Length).ShouldBe([3, 1]);
        result.Cells[0][2].ShouldBe(7);
    }

    [TestMethod]
    public void OrderByDescending_OverANestedElement_ReturnsTheOptimum()
    {
        // Arrange: the optimiser reads its solution back through the same marshalling code.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<JaggedIntEnvironment>()
            .Where(t => t.Cells[1][0] > 0)
            .Where(t => t.Cells[1][0] < 10)
            .OrderByDescending(t => t.Cells[1][0])
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Cells[1][0].ShouldBe(9);
    }

    [TestMethod]
    public void Solve_JaggedIntArrayIndexedByASymbol_SelectsThatRow()
    {
        // Arrange: the row index is itself a symbol, which only a nested array sort - not a set
        // of constants per element - can express. Only one row can hold 5 in its first element,
        // so Z3 has to choose that row.
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<JaggedIntEnvironment>()
            .Where(t => t.Row >= 0 && t.Row <= 1)
            .Where(t => t.Cells[0][0] == 4)
            .Where(t => t.Cells[1][0] == 5)
            .Where(t => t.Cells[t.Row][0] == 5)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Row.ShouldBe(1);
        result.Cells[result.Row][0].ShouldBe(5);
    }

    private sealed class JaggedIntEnvironment
    {
        public int[][] Cells { get; set; } = [new int[2], new int[2]];

        public int Row { get; set; }
    }

    private sealed class RaggedIntEnvironment
    {
        public int[][] Cells { get; set; } = [new int[1], new int[2], new int[3]];

        public int Other { get; set; }
    }

    private sealed class ListOfArraysEnvironment
    {
        public List<int[]> Cells { get; set; } = [new int[2], new int[2]];
    }

    private sealed class ListOfListsEnvironment
    {
        public List<List<int>> Cells { get; set; } = [[0, 0], [0, 0]];
    }

    private sealed class ThreeLevelIntEnvironment
    {
        public int[][][] Cells { get; set; } = [[new int[2]]];
    }

    private sealed class JaggedDoubleEnvironment
    {
        public double[][] Cells { get; set; } = [new double[2]];
    }

    private sealed class JaggedShortEnvironment
    {
        public short[][] Cells { get; set; } = [new short[2]];
    }

    private sealed class NullRowEnvironment
    {
        public int[][] Cells { get; set; } = new int[2][];

        public int Other { get; set; }
    }

    private sealed class NullJaggedPropertyEnvironment
    {
        public int[][]? Cells { get; set; }
    }
}
