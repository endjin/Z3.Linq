namespace Z3.Linq.Tests;

/// <summary>
/// Telling "there is no solution" apart from "there is a solution, and it is all zeroes".
/// </summary>
/// <remarks>
/// <para>
/// <c>Solve</c> reports no solution by returning <c>default(T)</c>. For a reference environment
/// that is <see langword="null"/> and reads correctly. For a value type - a value tuple, a struct,
/// a record struct - <c>default(T)</c> is a fully populated instance with every symbol zero, which
/// is also the answer to plenty of satisfiable theorems. The caller cannot tell the two apart and
/// cannot even write <c>result is null</c>: that is <c>error CS0037</c> on a non-nullable value
/// type. This file pins both halves - the ambiguity that remains on <c>Solve</c>, and the forms
/// added by #57 that resolve it.
/// </para>
/// <para>
/// Every test that asserts a negative is paired with one that asserts the positive over the same
/// value, because the whole difficulty is that the two look identical. A test that only checked
/// the unsatisfiable case would pass against an implementation that always answered "no solution".
/// </para>
/// </remarks>
[TestClass]
public class UnsatisfiabilityTests
{
    /// <summary>
    /// The ambiguity itself: two different theorems, the same result.
    /// </summary>
    /// <remarks>
    /// The reason #57 exists, kept as a test rather than as prose. One of these theorems has no
    /// solution and the other has exactly one; <c>Solve</c> answers both with <c>(0, 0)</c>. This
    /// is characterisation, not approval - the behaviour is deliberately left alone so that
    /// existing callers keep compiling, and the tests below cover the forms that answer properly.
    /// </remarks>
    [TestMethod]
    public void Solve_UnsatisfiableValueTupleTheorem_ReturnsWhatASatisfiableAllZeroTheoremReturns()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var noSolution = (from t in context.NewTheorem<(int a, int b)>()
                          where t.a == t.b && t.a > 4 && t.b < 2
                          select t).Solve();

        var oneSolution = (from t in context.NewTheorem<(int a, int b)>()
                           where t.a == 0 && t.b == 0
                           select t).Solve();

        // Assert
        noSolution.ShouldBe((0, 0));
        oneSolution.ShouldBe((0, 0));
        noSolution.ShouldBe(oneSolution);
    }

    [TestMethod]
    public void TrySolve_UnsatisfiableValueTupleTheorem_ReturnsFalse()
    {
        // Arrange: the original report's theorem, from #28.
        using var context = new Z3Context();
        var theorem = from t in context.NewTheorem<(int a, int b)>()
                      where t.a == t.b && t.a > 4 && t.b < 2
                      select t;

        // Act
        bool satisfiable = theorem.TrySolve(out (int a, int b) result);

        // Assert
        satisfiable.ShouldBeFalse();
        result.ShouldBe(default);
    }

    /// <summary>
    /// The satisfiable half of the same value.
    /// </summary>
    /// <remarks>
    /// The load-bearing test in this file. The result here is <c>(0, 0)</c>, exactly what the
    /// unsatisfiable theorem above produces, so the two are distinguished only by the return
    /// value of <c>TrySolve</c>. An implementation that inferred satisfiability from the solution
    /// - by comparing it against <c>default</c>, say - would pass every other test and fail this
    /// one.
    /// </remarks>
    [TestMethod]
    public void TrySolve_SatisfiableAllZeroValueTupleTheorem_ReturnsTrue()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = from t in context.NewTheorem<(int a, int b)>()
                      where t.a == 0 && t.b == 0
                      select t;

        // Act
        bool satisfiable = theorem.TrySolve(out (int a, int b) result);

        // Assert
        satisfiable.ShouldBeTrue();
        result.ShouldBe((0, 0));
    }

    [TestMethod]
    public void TrySolve_SatisfiableTheorem_ReturnsTheSolutionSolveWouldHaveReturned()
    {
        // Arrange: the two forms must not disagree about the answer, only about how they report
        // the absence of one.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 == 3 && t.X2 == 7);

        // Act
        bool satisfiable = theorem.TrySolve(out Symbols<int, int>? tried);
        var solved = theorem.Solve();

        // Assert
        satisfiable.ShouldBeTrue();
        tried.ShouldNotBeNull();
        solved.ShouldNotBeNull();
        tried.X1.ShouldBe(solved.X1);
        tried.X2.ShouldBe(solved.X2);
        tried.X1.ShouldBe(3);
    }

    [TestMethod]
    public void TrySolve_UnsatisfiableReferenceTypeTheorem_ReturnsFalse()
    {
        // Arrange: a reference environment could already report this by returning null, so this
        // is here to keep the two paths answering the same way rather than to fix anything.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 > 4 && t.X1 < 2);

        // Act
        bool satisfiable = theorem.TrySolve(out Symbols<int, int>? result);

        // Assert
        satisfiable.ShouldBeFalse();
        result.ShouldBeNull();
    }

    /// <summary>
    /// A struct environment behaves like a value tuple, because the problem is the value type
    /// rather than the tuple.
    /// </summary>
    /// <remarks>
    /// A value tuple is how the defect was first reported (#28), but nothing about it is
    /// tuple-specific: a plain struct and a record struct both come back as a populated all-zero
    /// instance from an unsatisfiable theorem. Covered here so that a fix which special-cased
    /// <c>ValueTuple</c> would not look complete.
    /// </remarks>
    [TestMethod]
    public void TrySolve_UnsatisfiableStructTheorems_ReturnFalse()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool structSatisfiable = context.NewTheorem<PointEnvironment>()
            .Where(t => t.X > 4 && t.X < 2)
            .TrySolve(out PointEnvironment fromStruct);

        bool recordSatisfiable = context.NewTheorem<PointRecord>()
            .Where(t => t.X > 4 && t.X < 2)
            .TrySolve(out PointRecord fromRecord);

        // Assert
        structSatisfiable.ShouldBeFalse();
        recordSatisfiable.ShouldBeFalse();

        // And the values they hand back are exactly the ones a caller could not have interpreted.
        fromStruct.X.ShouldBe(0);
        fromRecord.X.ShouldBe(0);
    }

    [TestMethod]
    public void SolveOrNull_UnsatisfiableValueTupleTheorem_ReturnsNull()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = from t in context.NewTheorem<(int a, int b)>()
                      where t.a == t.b && t.a > 4 && t.b < 2
                      select t;

        // Act
        (int a, int b)? result = theorem.SolveOrNull();

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// The satisfiable half, again over the value that used to be unreadable.
    /// </summary>
    /// <remarks>
    /// <c>SolveOrNull</c> lifts the environment to <see cref="System.Nullable{T}"/>, so
    /// <c>result is null</c> compiles and means what it says - the comparison that is
    /// <c>error CS0037</c> on the result of <c>Solve</c>. The value inside is still <c>(0, 0)</c>,
    /// which is the point: the wrapper carries the answer that the value cannot.
    /// </remarks>
    [TestMethod]
    public void SolveOrNull_SatisfiableAllZeroValueTupleTheorem_ReturnsTheAllZeroSolution()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = from t in context.NewTheorem<(int a, int b)>()
                      where t.a == 0 && t.b == 0
                      select t;

        // Act
        (int a, int b)? result = theorem.SolveOrNull();

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe((0, 0));
    }

    [TestMethod]
    public void TryOptimize_UnsatisfiableTheorem_ReturnsFalse()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<(int a, int b)>()
            .Where(t => t.a > 10 && t.a < 5);

        // Act
        bool satisfiable = theorem.TryOptimize(Optimization.Minimize, t => t.a, out (int a, int b) result);

        // Assert
        satisfiable.ShouldBeFalse();
        result.ShouldBe(default);
    }

    [TestMethod]
    public void TryOptimize_SatisfiableTheoremWhoseOptimumIsZero_ReturnsTrue()
    {
        // Arrange: the optimum is zero, so the solution is again indistinguishable from the
        // no-solution value - the same trap as Solve, on the optimiser's own path.
        using var context = new Z3Context();
        var theorem = context.NewTheorem<(int a, int b)>()
            .Where(t => t.a >= 0 && t.a <= 9 && t.b == 0);

        // Act
        bool satisfiable = theorem.TryOptimize(Optimization.Minimize, t => t.a, out (int a, int b) result);

        // Assert
        satisfiable.ShouldBeTrue();
        result.ShouldBe((0, 0));
    }

    [TestMethod]
    public void OptimizeOrNull_UnsatisfiableTheorem_ReturnsNull()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<(int a, int b)>()
            .Where(t => t.a > 10 && t.a < 5);

        // Act
        (int a, int b)? result = theorem.OptimizeOrNull(Optimization.Minimize, t => t.a);

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void OptimizeOrNull_SatisfiableTheorem_ReturnsTheOptimum()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<(int a, int b)>()
            .Where(t => t.a >= 5 && t.a <= 9 && t.b == 0);

        // Act
        (int a, int b)? result = theorem.OptimizeOrNull(Optimization.Minimize, t => t.a);

        // Assert
        result.ShouldNotBeNull();
        result.Value.a.ShouldBe(5);
    }

    /// <summary>
    /// The deferred <c>orderby</c> path answers the same way.
    /// </summary>
    /// <remarks>
    /// <c>OrderBy</c> and <c>OrderByDescending</c> return a deferred solvable with its own
    /// implementation of the interface, separate from the one on the theorem, so it can regress on
    /// its own. It has to carry satisfiability alongside the solution rather than returning the
    /// solution alone, which is the whole reason its shape changed under #57.
    /// </remarks>
    [TestMethod]
    public void TrySolve_OnADeferredOrderBy_ReportsSatisfiabilityEitherWay()
    {
        // Arrange
        using var context = new Z3Context();

        var unsatisfiable = from t in context.NewTheorem<(int a, int b)>()
                            where t.a > 10 && t.a < 5
                            orderby t.a
                            select t;

        var satisfiable = from t in context.NewTheorem<(int a, int b)>()
                          where t.a >= 0 && t.a <= 9 && t.b == 0
                          orderby t.a
                          select t;

        // Act
        bool unsatisfiableSolved = unsatisfiable.TrySolve(out (int a, int b) noSolution);
        bool satisfiableSolved = satisfiable.TrySolve(out (int a, int b) optimum);

        // Assert
        unsatisfiableSolved.ShouldBeFalse();
        satisfiableSolved.ShouldBeTrue();

        // Both values are (0, 0). Only the booleans differ.
        noSolution.ShouldBe(optimum);
        optimum.ShouldBe((0, 0));
    }

    [TestMethod]
    public void SolveOrNull_OnADeferredOrderByDescending_ReturnsTheOptimum()
    {
        // Arrange: the extension hangs off ISolveable, so it reaches the deferred form too.
        using var context = new Z3Context();
        var solveable = from t in context.NewTheorem<(int a, int b)>()
                        where t.a >= 5 && t.a <= 9 && t.b == 0
                        orderby t.a descending
                        select t;

        // Act
        (int a, int b)? result = solveable.SolveOrNull();

        // Assert
        result.ShouldNotBeNull();
        result.Value.a.ShouldBe(9);
    }

    /// <summary>
    /// The workaround suggested in the original report does not work.
    /// </summary>
    /// <remarks>
    /// #28 noted that declaring the environment as <c>(int a, int b)?</c> returns null for an
    /// unsatisfiable theorem, and offered it as a way round the ambiguity. It only appears to
    /// work because the satisfiable case was never tried: <see cref="System.Nullable{T}"/> exposes
    /// <c>HasValue</c> and <c>Value</c>, both get-only, so the moment there is a solution to write
    /// the marshalling layer fails on the property set. The unsatisfiable case never reaches that
    /// code, which is why it looked fine. Pinned so the record is unambiguous: before #57 there
    /// was no way to distinguish, not even the documented one.
    /// </remarks>
    [TestMethod]
    public void Solve_NullableValueTupleEnvironment_ThrowsWhenTheTheoremIsSatisfiable()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = from t in context.NewTheorem<(int a, int b)?>()
                      where t!.Value.a == 3 && t.Value.b == 7
                      select t;

        // Act
        ArgumentException exception = Should.Throw<ArgumentException>(() => theorem.Solve());

        // Assert
        exception.Message.ShouldContain("Property set method not found");
    }

    private struct PointEnvironment
    {
        public int X { get; set; }

        public int Y { get; set; }
    }

    private record struct PointRecord(int X, int Y);
}
