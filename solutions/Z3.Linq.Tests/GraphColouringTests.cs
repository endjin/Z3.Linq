namespace Z3.Linq.Tests;

using Z3.Linq.Examples.GraphColouring;

/// <summary>
/// Acceptance tests for the graph-colouring sample: the map of Europe, committee scheduling, and
/// a few small graphs whose chromatic number is known by inspection.
/// </summary>
/// <remarks>
/// <para>
/// A colouring is almost never unique - any permutation of the colours gives another - so these
/// tests pin only numbers that every optimal solution shares: the fewest colours, and the fewest
/// and most countries one colour can cover. Each returned colouring is then checked in C# against
/// the graph, so a mistranslated constraint cannot pass by producing the right count with an
/// invalid colouring.
/// </para>
/// <para>
/// The Europe figures match the book ("SAT/SMT by Example", sections 9.1 and 9.1.1): four
/// colours, three is unsatisfiable (Belgium, France, Germany and Luxembourg all border one
/// another), and with four colours one colour covers at least 3 and at most 22 of the 45
/// countries. Measured: each Europe solve or optimisation takes between about 30 and 150 ms.
/// </para>
/// </remarks>
[TestClass]
public class GraphColouringTests
{
    [TestMethod]
    public void Europe_BorderData_HasFortyFiveCountriesAndSeventyEightDistinctBorders()
    {
        // Arrange, Act
        var countries = Europe.Countries;
        var edges = Europe.Edges;

        // Assert
        countries.Length.ShouldBe(45);
        countries.Distinct().Count().ShouldBe(45);
        edges.Length.ShouldBe(78);
        edges.ShouldAllBe(edge => edge.A < edge.B);
    }

    [TestMethod]
    public void Solve_EuropeOrderedByColours_NeedsFourColours()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = (from t in Europe.Create(context)
                      orderby t.Colours
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Colours.ShouldBe(4);
        AssertIsValidColouring(result, Europe.Countries.Length, Europe.Edges);
    }

    [TestMethod]
    public void TrySolve_EuropeWithThreeColours_IsUnsatisfiable()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool satisfiable = Europe.Create(context)
            .Where(t => t.Colours == 3)
            .TrySolve(out _);

        // Assert
        satisfiable.ShouldBeFalse();
    }

    [TestMethod]
    [DataRow(Optimization.Minimize, 3, DisplayName = "Fewest countries in colour 0")]
    [DataRow(Optimization.Maximize, 22, DisplayName = "Most countries in colour 0")]
    public void Optimize_EuropeCountOfOneColourWithFourColours_FindsTheBooksBound(Optimization direction, int expected)
    {
        // Arrange
        using var context = new Z3Context();
        int countryCount = Europe.Countries.Length;

        // Act
        var result = Europe.Create(context)
            .Where(t => t.Colours == 4)
            .Optimize(direction, GraphColouring.CountOfColour(countryCount, 0));

        // Assert
        result.ShouldNotBeNull();
        result.Colours.ShouldBe(4);
        result.Colour.Count(colour => colour == 0).ShouldBe(expected);
        AssertIsValidColouring(result, countryCount, Europe.Edges);
    }

    [TestMethod]
    public void Solve_CommitteesOrderedByColours_FitsInThreeHours()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = (from t in Committees.Create(context)
                      orderby t.Colours
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Colours.ShouldBe(3);
        AssertIsValidColouring(result, Committees.Membership.Length, Committees.Clashes);
    }

    [TestMethod]
    public void Committees_Clashes_JoinExactlyThePairsThatShareAMember()
    {
        // Arrange: worked out by hand from the membership lists. S1 = {1,2,3} shares with S2, S3
        // and S6; the other pairs follow the same pattern round the ring of members.
        (int A, int B)[] expected = [(0, 1), (0, 2), (0, 5), (1, 2), (1, 3), (2, 3), (2, 4), (3, 4), (3, 5), (4, 5)];

        // Act
        var clashes = Committees.Clashes;

        // Assert
        clashes.ShouldBe(expected, ignoreOrder: true);
    }

    [TestMethod]
    [DataRow(1, "", 1, DisplayName = "Single vertex needs 1 colour")]
    [DataRow(4, "0-1,1-2,2-3", 2, DisplayName = "Path needs 2 colours")]
    [DataRow(4, "0-1,1-2,2-3,3-0", 2, DisplayName = "Even cycle needs 2 colours")]
    [DataRow(3, "0-1,1-2,2-0", 3, DisplayName = "Triangle needs 3 colours")]
    [DataRow(5, "0-1,1-2,2-3,3-4,4-0", 3, DisplayName = "Odd cycle needs 3 colours")]
    [DataRow(4, "0-1,0-2,0-3,1-2,1-3,2-3", 4, DisplayName = "K4 needs 4 colours")]
    public void Solve_SmallGraphOrderedByColours_FindsTheChromaticNumber(int vertexCount, string edgeList, int expected)
    {
        // Arrange
        using var context = new Z3Context();
        var edges = ParseEdges(edgeList);

        // Act
        var result = (from t in GraphColouring.Create(context, vertexCount, edges)
                      orderby t.Colours
                      select t).Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Colours.ShouldBe(expected);
        AssertIsValidColouring(result, vertexCount, edges);
    }

    [TestMethod]
    public void Create_EdgeToMissingVertex_Throws()
    {
        // Arrange
        using var context = new Z3Context();

        // Act, Assert
        Should.Throw<ArgumentException>(() => GraphColouring.Create(context, 2, [(0, 2)]));
    }

    private static (int A, int B)[] ParseEdges(string edgeList)
        => edgeList.Length == 0
            ? []
            : edgeList.Split(',')
                .Select(edge => edge.Split('-'))
                .Select(ends => (int.Parse(ends[0]), int.Parse(ends[1])))
                .ToArray();

    /// <summary>
    /// Asserts that every vertex has a colour in range and that the ends of every edge differ.
    /// </summary>
    private static void AssertIsValidColouring(GraphColouring colouring, int vertexCount, (int A, int B)[] edges)
    {
        colouring.Colour.Length.ShouldBe(vertexCount);
        colouring.Colour.ShouldAllBe(colour => colour >= 0 && colour < colouring.Colours);

        foreach (var (a, b) in edges)
        {
            colouring.Colour[a].ShouldNotBe(colouring.Colour[b], $"edge {a}-{b}");
        }
    }
}
