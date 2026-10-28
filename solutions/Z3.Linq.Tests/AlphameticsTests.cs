namespace Z3.Linq.Tests;

using Z3.Linq.Examples.Puzzles;

/// <summary>
/// Tests that solve the SEND + MORE = MONEY alphametic from "SAT/SMT by Example" (section 8.10).
/// </summary>
/// <remarks>
/// <para>
/// With the rule that S and M cannot be zero, the puzzle has exactly one solution, 9567 + 1085 =
/// 10652. Uniqueness is not only assumed: one test excludes that assignment and checks that
/// nothing else satisfies the theorem. So the full assignment is safe to pin.
/// </para>
/// <para>
/// The solve and the uniqueness check each take tens of milliseconds.
/// </para>
/// </remarks>
[TestClass]
public class AlphameticsTests
{
    [TestMethod]
    public void Solve_SendMoreMoney_ReturnsTheKnownAssignment()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = Alphametics.SendMoreMoney(context).Solve();

        // Assert
        result.ShouldBe((S: 9, E: 5, N: 6, D: 7, M: 1, O: 0, R: 8, Y: 2));
    }

    [TestMethod]
    public void Solve_SendMoreMoney_SatisfiesTheSumWithDistinctDigits()
    {
        // Arrange: check the answer by the rules of the puzzle, not by a fixed value.
        using var context = new Z3Context();

        // Act
        var r = Alphametics.SendMoreMoney(context).Solve();

        // Assert
        int send = Alphametics.ToNumber(r.S, r.E, r.N, r.D);
        int more = Alphametics.ToNumber(r.M, r.O, r.R, r.E);
        int money = Alphametics.ToNumber(r.M, r.O, r.N, r.E, r.Y);

        (send + more).ShouldBe(money);
        new[] { r.S, r.E, r.N, r.D, r.M, r.O, r.R, r.Y }.Distinct().Count().ShouldBe(8);
        new[] { r.S, r.E, r.N, r.D, r.M, r.O, r.R, r.Y }.ShouldAllBe(digit => digit >= 0 && digit <= 9);
        r.S.ShouldNotBe(0);
        r.M.ShouldNotBe(0);
    }

    [TestMethod]
    public void TrySolve_SendMoreMoneyExcludingTheKnownAnswer_IsUnsatisfiable()
    {
        // Arrange: block the known assignment. If any other assignment existed, Z3 would find it.
        using var context = new Z3Context();

        var theorem = Alphametics.SendMoreMoney(context)
            .Where(t => !(t.S == 9 && t.E == 5 && t.N == 6 && t.D == 7 && t.M == 1 && t.O == 0 && t.R == 8 && t.Y == 2));

        // Act
        bool solved = theorem.TrySolve(out _);

        // Assert
        solved.ShouldBeFalse();
    }

    [TestMethod]
    [DataRow(new[] { 9, 5, 6, 7 }, 9567, DisplayName = "SEND")]
    [DataRow(new[] { 1, 0, 6, 5, 2 }, 10652, DisplayName = "MONEY")]
    [DataRow(new[] { 0 }, 0, DisplayName = "Single zero")]
    public void ToNumber_Digits_ReadsThemMostSignificantFirst(int[] digits, int expected)
    {
        // Act
        int number = Alphametics.ToNumber(digits);

        // Assert
        number.ShouldBe(expected);
    }
}
