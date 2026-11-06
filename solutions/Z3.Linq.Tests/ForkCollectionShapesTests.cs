namespace Z3.Linq.Tests;

/// <summary>
/// Constraint shapes ported from the downstream fork (MyIntelligenceAgency/Z3.Linq) onto this
/// branch, covering axes this branch's own suite does not exercise: flat-array distinctness
/// (a 4x4 sudoku on <c>int[16]</c>), boolean element constraints, an interior index gap on a
/// flat array, and capture chains that reach a value through more than one member access.
/// </summary>
/// <remarks>
/// <para>
/// The fork's suite runs these shapes against two collection lowerings - one array constant, or
/// one constant per element - behind a <c>CollectionHandling</c> switch. This branch has a
/// single lowering, so the mode axis is dropped here and only the constraint shapes are
/// ported: if the branch satisfies them all, the fork's per-element mode and the branch's array
/// mode agree on these problems.
/// </para>
/// <para>
/// Provenance: the sudoku and gap shapes come from the fork's collection work
/// (<c>CollectionHandlingTests</c>), the capture chains from its closure reduction
/// (<c>ClosureChainTests</c>). No library file is touched by this addition.
/// </para>
/// </remarks>
[TestClass]
public class ForkCollectionShapesTests
{
    /// <summary>
    /// A 4x4 sudoku held in a flat <c>int[16]</c>, cell (r, c) at index <c>4 * r + c</c>: the
    /// smallest CSP that exercises distinctness over a flat array's elements.
    /// </summary>
    public class FlatSudokuGrid4
    {
        /// <summary>Gets or sets the sixteen cells, row-major.</summary>
        public int[] Cells { get; set; } = new int[16];
    }

    private static Theorem<FlatSudokuGrid4> BuildFlatSudoku4x4(Z3Context context)
    {
        var theorem = context.NewTheorem<FlatSudokuGrid4>();

        for (int i = 0; i < 16; i++)
        {
            int cell = i;
            theorem = theorem.Where(g => g.Cells[cell] >= 1 && g.Cells[cell] <= 4);
        }

        for (int r = 0; r < 4; r++)
        {
            int first = 4 * r;
            theorem = theorem.Where(g => Z3Methods.Distinct(
                g.Cells[first], g.Cells[first + 1], g.Cells[first + 2], g.Cells[first + 3]));
        }

        for (int c = 0; c < 4; c++)
        {
            int first = c;
            theorem = theorem.Where(g => Z3Methods.Distinct(
                g.Cells[first], g.Cells[first + 4], g.Cells[first + 8], g.Cells[first + 12]));
        }

        return theorem;
    }

    [TestMethod]
    public void Solve_FlatInt16SudokuWithDistinct_IsSolvable()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = BuildFlatSudoku4x4(context).Solve();

        // Assert: every row and every column is a permutation of 1..4.
        result.ShouldNotBeNull();
        for (int r = 0; r < 4; r++)
        {
            int row = r;
            Enumerable.Range(0, 4).Select(c => result.Cells[4 * row + c]).OrderBy(static v => v).ShouldBe([1, 2, 3, 4]);
        }

        for (int c = 0; c < 4; c++)
        {
            int column = c;
            Enumerable.Range(0, 4).Select(r => result.Cells[4 * r + column]).OrderBy(static v => v).ShouldBe([1, 2, 3, 4]);
        }
    }

    [TestMethod]
    public void Solve_FlatInt16Sudoku_RespectsHint()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = BuildFlatSudoku4x4(context).Where(g => g.Cells[4 * 1 + 2] == 3).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Cells[6].ShouldBe(3);
    }

    /// <summary>
    /// A flat <c>int[]</c> whose constraints reference elements 0 and 2 but never element 1:
    /// the unreferenced interior index must come back fully materialised - referenced elements
    /// round-trip and the gap does not surface as a hole or a crash.
    /// </summary>
    /// <remarks>
    /// The value of the unreferenced element differs by design: this branch fills it by model
    /// completion, as documented on the jagged free-element case, while the downstream fork's
    /// per-element mode returns the initialiser's value for it. Only the materialisation is
    /// asserted here, so the test holds under either policy.
    /// </remarks>
    public class Triple
    {
        /// <summary>Gets or sets three integer values.</summary>
        public int[] Values { get; set; } = new int[3];
    }

    [TestMethod]
    public void Solve_FlatIntArrayWithInteriorIndexGap_RoundTripsReferencedElements()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Triple>()
            .Where(t => t.Values[0] == 1)
            .Where(t => t.Values[2] == 3)
            .Solve();

        // Assert: extraction returns every element, referenced or not - the fork's original
        // regression was a null leaking through this exact gap.
        result.ShouldNotBeNull();
        result.Values.Length.ShouldBe(3);
        result.Values[0].ShouldBe(1);
        result.Values[2].ShouldBe(3);
    }

    /// <summary>
    /// Three boolean flags, so element constraints and the read-back run through boolean
    /// elements rather than integers.
    /// </summary>
    public class BoolTriple
    {
        /// <summary>Gets or sets three flags.</summary>
        public bool[] Flags { get; set; } = new bool[3];
    }

    [TestMethod]
    public void Solve_BoolArrayElements_RoundTrip()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<BoolTriple>()
            .Where(t => t.Flags[0])
            .Where(t => !t.Flags[1])
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Flags[0].ShouldBeTrue();
        result.Flags[1].ShouldBeFalse();
    }

    /// <summary>A captured instance whose property a constraint reads.</summary>
    public class Limit
    {
        /// <summary>Gets or sets the bound.</summary>
        public int Value { get; set; } = 4;
    }

    private static Theorem<Triple> BuildFloorPerElement(Z3Context context, int floor)
    {
        var theorem = context.NewTheorem<Triple>();

        for (int i = 0; i < 3; i++)
        {
            // `position` is captured per iteration, `floor` once for the method: two nesting
            // levels of display class in the expression tree.
            int position = i;
            theorem = theorem.Where(t => t.Values[position] >= floor);
        }

        return theorem;
    }

    [TestMethod]
    public void CapturedMethodParameter_InsideLoop_IsReducedThroughTwoDisplayClassLevels()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = BuildFloorPerElement(context, 5)
            .Where(t => t.Values[0] <= 9 && t.Values[1] <= 9 && t.Values[2] <= 9)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values.ShouldAllBe(v => v >= 5 && v <= 9);
    }

    [TestMethod]
    public void CapturedInstance_PropertyChain_IsReduced()
    {
        // Arrange: 7 is above the captured bound, so the theorem is unsatisfiable - the point is
        // that the bound is read at all, and read as 4 rather than as something else.
        using var context = new Z3Context();
        var limit = new Limit { Value = 4 };

        // Act
        var result = context.NewTheorem<Triple>()
            .Where(t => t.Values[0] == 7)
            .Where(t => t.Values[0] <= limit.Value)
            .Solve();

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void CapturedInstance_PropertyChain_ConstrainsTheSolution()
    {
        // Arrange
        using var context = new Z3Context();
        var limit = new Limit { Value = 4 };

        // Act
        var result = context.NewTheorem<Triple>()
            .Where(t => t.Values[0] >= 1)
            .Where(t => t.Values[0] == limit.Value)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values[0].ShouldBe(4);
    }
}
