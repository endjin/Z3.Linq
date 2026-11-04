namespace Z3.Linq.Tests;

/// <summary>
/// Keystone tests for the resurrected <see cref="CollectionHandling"/> modes.
///
/// Both modes must solve the same constraint problem on a nested int[][] grid and produce a
/// satisfying assignment. The Array mode exercises Z3 array theory (a single ArrayExpr with
/// Select); the Constants mode exercises one Z3 constant per element (the classic endjin binding
/// model, lazily created on index access via <see cref="MultipleEnvironment"/>).
///
/// These tests are the proof that the previously-dead CollectionHandling enum is now wired end-to-end
/// (environment construction → constraint visiting → model extraction) in both modes.
/// </summary>
[TestClass]
public class CollectionHandlingTests
{
    /// <summary>
    /// 4x4 Sudoku grid modeled as int[][]: the smallest nested-array CSP that exercises both modes.
    /// </summary>
    public class SudokuGrid4
    {
        public int[][] Cells { get; set; } = new int[4][];

        public SudokuGrid4()
        {
            for (int i = 0; i < 4; i++)
            {
                Cells[i] = new int[4];
            }
        }
    }

    private static Theorem<SudokuGrid4> BuildSudoku4x4(Z3Context ctx)
    {
        var theorem = ctx.NewTheorem<SudokuGrid4>();

        // Each cell in [1,4].
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                int i1 = i, j1 = j;
                theorem = theorem.Where(g => g.Cells[i1][j1] >= 1 && g.Cells[i1][j1] <= 4);
            }
        }

        // Row distinctness (explicit args — the pattern supported by the Distinct rewriter).
        for (int r = 0; r < 4; r++)
        {
            int r1 = r;
            theorem = theorem.Where(g => Z3Methods.Distinct(
                g.Cells[r1][0], g.Cells[r1][1], g.Cells[r1][2], g.Cells[r1][3]));
        }

        // Column distinctness.
        for (int c = 0; c < 4; c++)
        {
            int c1 = c;
            theorem = theorem.Where(g => Z3Methods.Distinct(
                g.Cells[0][c1], g.Cells[1][c1], g.Cells[2][c1], g.Cells[3][c1]));
        }

        return theorem;
    }

    [TestMethod]
    public void Sudoku4x4_IsSolvable_InBothModes()
    {
        foreach (CollectionHandling mode in new[] { CollectionHandling.Array, CollectionHandling.Constants })
        {
            using var ctx = new Z3Context { DefaultCollectionHandling = mode };
            var solution = BuildSudoku4x4(ctx).Solve();

            solution.ShouldNotBeNull();
            foreach (var row in solution!.Cells)
            {
                foreach (var v in row)
                {
                    v.ShouldBeInRange(1, 4);
                }
            }

            // A valid 4x4 Latin square: each row and each column is a permutation of 1..4.
            for (int r = 0; r < 4; r++)
            {
                solution.Cells[r].OrderBy(v => v).ToArray()
                    .ShouldBe([1, 2, 3, 4], $"mode={mode} row {r}");
            }

            for (int c = 0; c < 4; c++)
            {
                Enumerable.Range(0, 4).Select(r => solution.Cells[r][c]).OrderBy(v => v).ToArray()
                    .ShouldBe([1, 2, 3, 4], $"mode={mode} col {c}");
            }
        }
    }

    [TestMethod]
    public void Sudoku4x4_RespectsHint_InBothModes()
    {
        foreach (CollectionHandling mode in new[] { CollectionHandling.Array, CollectionHandling.Constants })
        {
            using var ctx = new Z3Context { DefaultCollectionHandling = mode };
            var theorem = BuildSudoku4x4(ctx).Where(g => g.Cells[0][0] == 2);

            var solution = theorem.Solve();

            solution.ShouldNotBeNull();
            solution!.Cells[0][0].ShouldBe(2, $"mode={mode}");
        }
    }

    [TestMethod]
    public void ConstantsMode_DefaultsToArray_OnBareContext()
    {
        // Anti-regression guard: a context with no explicit mode must default to Array,
        // preserving the historical (pre-resurrection) behavior.
        using var ctx = new Z3Context();
        ctx.DefaultCollectionHandling.ShouldBe(CollectionHandling.Array);
    }

    /// <summary>
    /// Flat int[] (non-nested) collection in Constants mode — the simplest shape that must work
    /// now that MultipleEnvironment is wired. Three cells, all equal to 7.
    /// </summary>
    public class Triple
    {
        public int[] Values { get; set; } = new int[3];
    }

    [TestMethod]
    public void ConstantsMode_FlatIntArray_SolvesWithPerElementConstants()
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = CollectionHandling.Constants };

        var theorem = ctx.NewTheorem<Triple>()
            .Where(t => t.Values[0] == t.Values[1])
            .Where(t => t.Values[1] == t.Values[2])
            .Where(t => t.Values[0] == 7);

        var solution = theorem.Solve();

        solution.ShouldNotBeNull();
        solution!.Values[0].ShouldBe(7);
        solution.Values[1].ShouldBe(7);
        solution.Values[2].ShouldBe(7);
    }
}
