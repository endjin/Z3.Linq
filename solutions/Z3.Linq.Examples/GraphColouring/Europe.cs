namespace Z3.Linq.Examples.GraphColouring;

using System.Linq;

/// <summary>
/// The land borders of 45 European countries, as a graph to colour.
/// </summary>
/// <remarks>
/// <para>
/// The data is the country list from "SAT/SMT by Example" by Dennis Yurichev, section 9.1.1,
/// https://smt.st. Each country lists its neighbours, so most borders appear twice; the
/// <see cref="Edges"/> property removes the duplicates, which leaves 78 distinct borders.
/// Cyprus, Iceland and Malta have no land border with any listed country.
/// </para>
/// <para>
/// The map needs four colours. Three are not enough: Belgium, France, Germany and Luxembourg all
/// border one another.
/// </para>
/// </remarks>
public static class Europe
{
    /// <summary>
    /// Gets each country with the countries it shares a land border with.
    /// </summary>
    public static (string Country, string[] Neighbours)[] Borders { get; } =
    [
        ("Albania", ["Greece", "Kosovo", "Macedonia", "Montenegro"]),
        ("Andorra", ["France", "Spain"]),
        ("Austria", ["CzechRepublic", "Germany", "Hungary", "Italy", "Liechtenstein", "Slovakia", "Slovenia", "Switzerland"]),
        ("Belarus", ["Latvia", "Lithuania", "Poland", "Ukraine"]),
        ("Belgium", ["France", "Germany", "Luxembourg", "Netherlands"]),
        ("BosniaHerzegovina", ["Croatia", "Montenegro", "Serbia"]),
        ("Bulgaria", ["Greece", "Macedonia", "Romania", "Serbia"]),
        ("Croatia", ["BosniaHerzegovina", "Hungary", "Montenegro", "Serbia", "Slovenia"]),
        ("Cyprus", []),
        ("CzechRepublic", ["Austria", "Germany", "Poland", "Slovakia"]),
        ("Denmark", ["Germany"]),
        ("Estonia", ["Latvia"]),
        ("Finland", ["Norway", "Sweden"]),
        ("France", ["Andorra", "Belgium", "Germany", "Italy", "Luxembourg", "Monaco", "Spain", "Switzerland"]),
        ("Germany", ["Austria", "Belgium", "CzechRepublic", "Denmark", "France", "Luxembourg", "Netherlands", "Poland", "Switzerland"]),
        ("Greece", ["Albania", "Bulgaria", "Macedonia"]),
        ("Hungary", ["Austria", "Croatia", "Romania", "Serbia", "Slovakia", "Slovenia", "Ukraine"]),
        ("Iceland", []),
        ("Ireland", ["UnitedKingdom"]),
        ("Italy", ["Austria", "France", "SanMarino", "Slovenia", "Switzerland", "VaticanCity"]),
        ("Kosovo", ["Albania", "Macedonia", "Montenegro", "Serbia"]),
        ("Latvia", ["Belarus", "Estonia", "Lithuania"]),
        ("Liechtenstein", ["Austria", "Switzerland"]),
        ("Lithuania", ["Belarus", "Latvia", "Poland"]),
        ("Luxembourg", ["Belgium", "France", "Germany"]),
        ("Macedonia", ["Albania", "Bulgaria", "Greece", "Kosovo", "Serbia"]),
        ("Malta", []),
        ("Moldova", ["Romania", "Ukraine"]),
        ("Monaco", ["France"]),
        ("Montenegro", ["Albania", "BosniaHerzegovina", "Croatia", "Kosovo", "Serbia"]),
        ("Netherlands", ["Belgium", "Germany"]),
        ("Norway", ["Finland", "Sweden"]),
        ("Poland", ["Belarus", "CzechRepublic", "Germany", "Lithuania", "Slovakia", "Ukraine"]),
        ("Portugal", ["Spain"]),
        ("Romania", ["Bulgaria", "Hungary", "Moldova", "Serbia", "Ukraine"]),
        ("SanMarino", ["Italy"]),
        ("Serbia", ["BosniaHerzegovina", "Bulgaria", "Croatia", "Hungary", "Kosovo", "Macedonia", "Montenegro", "Romania"]),
        ("Slovakia", ["Austria", "CzechRepublic", "Hungary", "Poland", "Ukraine"]),
        ("Slovenia", ["Austria", "Croatia", "Hungary", "Italy"]),
        ("Spain", ["Andorra", "France", "Portugal"]),
        ("Sweden", ["Finland", "Norway"]),
        ("Switzerland", ["Austria", "France", "Germany", "Italy", "Liechtenstein"]),
        ("Ukraine", ["Belarus", "Hungary", "Moldova", "Poland", "Romania", "Slovakia"]),
        ("UnitedKingdom", ["Ireland"]),
        ("VaticanCity", ["Italy"]),
    ];

    /// <summary>
    /// Gets the country names, in the order of <see cref="Borders"/>. A country's index here is
    /// its vertex number in <see cref="Edges"/>.
    /// </summary>
    public static string[] Countries { get; } = Borders.Select(entry => entry.Country).ToArray();

    /// <summary>
    /// Gets each distinct border once, as a pair of indexes into <see cref="Countries"/> with the
    /// smaller index first.
    /// </summary>
    public static (int A, int B)[] Edges { get; } = Borders
        .SelectMany(entry => entry.Neighbours.Select(neighbour => (A: IndexOf(entry.Country), B: IndexOf(neighbour))))
        .Select(edge => edge.A < edge.B ? edge : (A: edge.B, B: edge.A))
        .Distinct()
        .ToArray();

    /// <summary>
    /// Creates a theorem whose solutions are the valid colourings of the map.
    /// </summary>
    /// <param name="context">The Z3 context that owns the theorem.</param>
    /// <returns>The colouring theorem for <see cref="Edges"/>.</returns>
    public static Theorem<GraphColouring> Create(Z3Context context)
        => GraphColouring.Create(context, Countries.Length, Edges);

    private static int IndexOf(string country)
    {
        int index = Array.FindIndex(Borders, entry => entry.Country == country);
        return index >= 0 ? index : throw new InvalidOperationException($"Unknown country '{country}' in the border list.");
    }
}
