namespace Z3.Linq.Examples.Puzzles;

using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// The Zebra puzzle (also called Einstein's puzzle): five houses in a row, each with a different
/// colour, owner nationality, drink, cigarette brand and pet.
/// </summary>
/// <remarks>
/// <para>
/// Based on a problem in "SAT/SMT by Example" by Dennis Yurichev, section 8.2.1, https://smt.st. The clues
/// are the ones published in Life International in 1962.
/// </para>
/// <para>
/// Every attribute is an integer symbol holding the number (1-5) of the house it belongs to. So
/// "the Englishman lives in the red house" becomes <c>t.Nation.Englishman == t.Colour.Red</c>, and
/// "next to" becomes "the house numbers differ by one". Each group of five attributes is
/// <c>Distinct</c>, because no two houses share a colour, a nationality, and so on.
/// </para>
/// <para>
/// The puzzle has exactly one solution: the Norwegian drinks water, and the Japanese owns the
/// zebra.
/// </para>
/// </remarks>
public class ZebraPuzzle
{
    /// <summary>
    /// The number of houses in the row.
    /// </summary>
    public const int HouseCount = 5;

    public Colours Colour { get; set; } = new();

    public Nations Nation { get; set; } = new();

    public Drinks Drink { get; set; } = new();

    public Smokes Smoke { get; set; } = new();

    public Pets Pet { get; set; } = new();

    /// <summary>
    /// Builds the theorem for the puzzle: the rules that make it a five-house assignment, then
    /// the fourteen clues.
    /// </summary>
    /// <param name="context">The Z3 context that owns the theorem.</param>
    /// <returns>A theorem with exactly one solution.</returns>
    public static Theorem<ZebraPuzzle> Create(Z3Context context)
    {
        var theorem = context.NewTheorem<ZebraPuzzle>()
            .Where(t => Z3Methods.Distinct(t.Colour.Yellow, t.Colour.Blue, t.Colour.Red, t.Colour.Ivory, t.Colour.Green))
            .Where(t => Z3Methods.Distinct(t.Nation.Norwegian, t.Nation.Ukrainian, t.Nation.Englishman, t.Nation.Spaniard, t.Nation.Japanese))
            .Where(t => Z3Methods.Distinct(t.Drink.Water, t.Drink.Tea, t.Drink.Milk, t.Drink.OrangeJuice, t.Drink.Coffee))
            .Where(t => Z3Methods.Distinct(t.Smoke.Kools, t.Smoke.Chesterfield, t.Smoke.OldGold, t.Smoke.LuckyStrike, t.Smoke.Parliament))
            .Where(t => Z3Methods.Distinct(t.Pet.Fox, t.Pet.Horse, t.Pet.Snails, t.Pet.Dog, t.Pet.Zebra));

        theorem = WithHouseNumbers(theorem);

        return theorem
            .Where(t => t.Nation.Englishman == t.Colour.Red)
            .Where(t => t.Nation.Spaniard == t.Pet.Dog)
            .Where(t => t.Drink.Coffee == t.Colour.Green)
            .Where(t => t.Nation.Ukrainian == t.Drink.Tea)
            .Where(t => t.Colour.Green == t.Colour.Ivory + 1)
            .Where(t => t.Smoke.OldGold == t.Pet.Snails)
            .Where(t => t.Smoke.Kools == t.Colour.Yellow)
            .Where(t => t.Drink.Milk == 3)
            .Where(t => t.Nation.Norwegian == 1)
            .Where(t => t.Smoke.Chesterfield == t.Pet.Fox + 1 || t.Smoke.Chesterfield == t.Pet.Fox - 1)
            .Where(t => t.Smoke.Kools == t.Pet.Horse + 1 || t.Smoke.Kools == t.Pet.Horse - 1)
            .Where(t => t.Smoke.LuckyStrike == t.Drink.OrangeJuice)
            .Where(t => t.Nation.Japanese == t.Smoke.Parliament)
            .Where(t => t.Nation.Norwegian == t.Colour.Blue + 1 || t.Nation.Norwegian == t.Colour.Blue - 1);
    }

    /// <summary>
    /// Gets the name of the attribute in <paramref name="group"/> that is assigned to a house.
    /// </summary>
    /// <param name="group">One attribute group of a solved puzzle, such as <see cref="Colour"/>.</param>
    /// <param name="house">The house number, 1 to 5.</param>
    /// <returns>The attribute's property name, for example <c>"OrangeJuice"</c>.</returns>
    public static string AttributeInHouse(object group, int house)
    {
        return group.GetType().GetProperties()
            .Single(property => (int)property.GetValue(group)! == house)
            .Name;
    }

    /// <summary>
    /// Adds <c>1 &lt;= attribute &lt;= 5</c> for each of the 25 attributes. The constraints are
    /// built as expression trees over the nested properties, so a new attribute cannot be left
    /// out by accident.
    /// </summary>
    private static Theorem<ZebraPuzzle> WithHouseNumbers(Theorem<ZebraPuzzle> theorem)
    {
        ParameterExpression t = Expression.Parameter(typeof(ZebraPuzzle), "t");

        foreach (PropertyInfo group in typeof(ZebraPuzzle).GetProperties())
        {
            foreach (PropertyInfo attribute in group.PropertyType.GetProperties())
            {
                MemberExpression house = Expression.Property(Expression.Property(t, group), attribute);

                BinaryExpression body = Expression.AndAlso(
                    Expression.GreaterThanOrEqual(house, Expression.Constant(1)),
                    Expression.LessThanOrEqual(house, Expression.Constant(HouseCount)));

                theorem = theorem.Where(Expression.Lambda<Func<ZebraPuzzle, bool>>(body, t));
            }
        }

        return theorem;
    }
}

/// <summary>
/// The house number of each colour.
/// </summary>
public class Colours
{
    public int Yellow { get; set; }

    public int Blue { get; set; }

    public int Red { get; set; }

    public int Ivory { get; set; }

    public int Green { get; set; }
}

/// <summary>
/// The house number of each nationality.
/// </summary>
public class Nations
{
    public int Norwegian { get; set; }

    public int Ukrainian { get; set; }

    public int Englishman { get; set; }

    public int Spaniard { get; set; }

    public int Japanese { get; set; }
}

/// <summary>
/// The house number of each drink.
/// </summary>
public class Drinks
{
    public int Water { get; set; }

    public int Tea { get; set; }

    public int Milk { get; set; }

    public int OrangeJuice { get; set; }

    public int Coffee { get; set; }
}

/// <summary>
/// The house number of each cigarette brand.
/// </summary>
public class Smokes
{
    public int Kools { get; set; }

    public int Chesterfield { get; set; }

    public int OldGold { get; set; }

    public int LuckyStrike { get; set; }

    public int Parliament { get; set; }
}

/// <summary>
/// The house number of each pet.
/// </summary>
public class Pets
{
    public int Fox { get; set; }

    public int Horse { get; set; }

    public int Snails { get; set; }

    public int Dog { get; set; }

    public int Zebra { get; set; }
}
