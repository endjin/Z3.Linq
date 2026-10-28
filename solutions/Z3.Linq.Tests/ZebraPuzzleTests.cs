namespace Z3.Linq.Tests;

using Z3.Linq.Examples.Puzzles;

/// <summary>
/// Tests that solve the Zebra puzzle from "SAT/SMT by Example" (section 8.2.1).
/// </summary>
/// <remarks>
/// <para>
/// The puzzle has exactly one solution, so every one of the 25 house numbers is pinned. One test
/// excludes that solution and checks that the theorem becomes unsatisfiable, which confirms the
/// uniqueness instead of assuming it. The environment uses nested classes (<c>t.Colour.Red</c>),
/// so these tests also cover member chains two levels deep at a realistic size.
/// </para>
/// <para>
/// The solve takes tens of milliseconds.
/// </para>
/// </remarks>
[TestClass]
public class ZebraPuzzleTests
{
    [TestMethod]
    public void Solve_ZebraPuzzle_AnswersBothQuestions()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = ZebraPuzzle.Create(context).Solve();

        // Assert: the Norwegian drinks water, and the Japanese owns the zebra.
        result.ShouldNotBeNull();
        result.Drink.Water.ShouldBe(result.Nation.Norwegian);
        result.Pet.Zebra.ShouldBe(result.Nation.Japanese);
    }

    [TestMethod]
    public void Solve_ZebraPuzzle_ReturnsTheKnownHouses()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = ZebraPuzzle.Create(context).Solve();

        // Assert
        result.ShouldNotBeNull();

        result.Colour.Yellow.ShouldBe(1);
        result.Colour.Blue.ShouldBe(2);
        result.Colour.Red.ShouldBe(3);
        result.Colour.Ivory.ShouldBe(4);
        result.Colour.Green.ShouldBe(5);

        result.Nation.Norwegian.ShouldBe(1);
        result.Nation.Ukrainian.ShouldBe(2);
        result.Nation.Englishman.ShouldBe(3);
        result.Nation.Spaniard.ShouldBe(4);
        result.Nation.Japanese.ShouldBe(5);

        result.Drink.Water.ShouldBe(1);
        result.Drink.Tea.ShouldBe(2);
        result.Drink.Milk.ShouldBe(3);
        result.Drink.OrangeJuice.ShouldBe(4);
        result.Drink.Coffee.ShouldBe(5);

        result.Smoke.Kools.ShouldBe(1);
        result.Smoke.Chesterfield.ShouldBe(2);
        result.Smoke.OldGold.ShouldBe(3);
        result.Smoke.LuckyStrike.ShouldBe(4);
        result.Smoke.Parliament.ShouldBe(5);

        result.Pet.Fox.ShouldBe(1);
        result.Pet.Horse.ShouldBe(2);
        result.Pet.Snails.ShouldBe(3);
        result.Pet.Dog.ShouldBe(4);
        result.Pet.Zebra.ShouldBe(5);
    }

    [TestMethod]
    public void TrySolve_ZebraPuzzleWithTheZebraElsewhere_IsUnsatisfiable()
    {
        // Arrange: the zebra's house follows from the clues, so forbidding it leaves no solution.
        // Together with the test above this shows the puzzle's answer is unique.
        using var context = new Z3Context();

        var theorem = ZebraPuzzle.Create(context).Where(t => t.Pet.Zebra != t.Nation.Japanese);

        // Act
        bool solved = theorem.TrySolve(out _);

        // Assert
        solved.ShouldBeFalse();
    }

    [TestMethod]
    public void TrySolve_ZebraPuzzleExcludingTheKnownSolution_IsUnsatisfiable()
    {
        // Arrange: within each group the five house numbers are distinct, so four of them fix
        // the fifth. Blocking the first four of every group blocks the whole known solution.
        using var context = new Z3Context();

        var theorem = ZebraPuzzle.Create(context).Where(t => !(
            t.Colour.Yellow == 1 && t.Colour.Blue == 2 && t.Colour.Red == 3 && t.Colour.Ivory == 4
            && t.Drink.Water == 1 && t.Drink.Tea == 2 && t.Drink.Milk == 3 && t.Drink.OrangeJuice == 4
            && t.Smoke.Kools == 1 && t.Smoke.Chesterfield == 2 && t.Smoke.OldGold == 3 && t.Smoke.LuckyStrike == 4
            && t.Pet.Fox == 1 && t.Pet.Horse == 2 && t.Pet.Snails == 3 && t.Pet.Dog == 4
            && t.Nation.Norwegian == 1 && t.Nation.Ukrainian == 2 && t.Nation.Englishman == 3 && t.Nation.Spaniard == 4));

        // Act
        bool solved = theorem.TrySolve(out _);

        // Assert
        solved.ShouldBeFalse();
    }

    [TestMethod]
    [DataRow(1, "Yellow", DisplayName = "House 1")]
    [DataRow(3, "Red", DisplayName = "House 3")]
    [DataRow(5, "Green", DisplayName = "House 5")]
    public void AttributeInHouse_SolvedColours_NamesTheColourOfThatHouse(int house, string expected)
    {
        // Arrange
        using var context = new Z3Context();
        var result = ZebraPuzzle.Create(context).Solve();
        result.ShouldNotBeNull();

        // Act
        string colour = ZebraPuzzle.AttributeInHouse(result.Colour, house);

        // Assert
        colour.ShouldBe(expected);
    }
}
