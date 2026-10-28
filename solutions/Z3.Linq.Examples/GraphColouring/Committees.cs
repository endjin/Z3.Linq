namespace Z3.Linq.Examples.GraphColouring;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Committee scheduling as a graph-colouring problem: six committees must each meet for an
/// hour, and two committees that share a member cannot meet in the same hour.
/// </summary>
/// <remarks>
/// <para>
/// Each committee is a vertex, an edge joins two committees with a member in common, and a colour
/// is an hour. The fewest colours is the fewest hours that fit every meeting. For the data below
/// that is three; there are several three-hour timetables, so only the count is fixed.
/// </para>
/// <para>
/// The data is from "SAT/SMT by Example" by Dennis Yurichev, section 9.2, https://smt.st.
/// </para>
/// </remarks>
public static class Committees
{
    /// <summary>
    /// Gets the committees. Each one is a name and the numbers of its members.
    /// </summary>
    public static (string Name, int[] Members)[] Membership { get; } =
    [
        ("S1", [1, 2, 3]),
        ("S2", [2, 3, 4]),
        ("S3", [3, 4, 5]),
        ("S4", [4, 5, 6]),
        ("S5", [5, 6, 7]),
        ("S6", [1, 6, 7]),
    ];

    /// <summary>
    /// Gets each pair of committees that share at least one member, as indexes into
    /// <see cref="Membership"/> with the smaller index first.
    /// </summary>
    public static (int A, int B)[] Clashes { get; } = FindClashes().ToArray();

    /// <summary>
    /// Creates a theorem whose solutions are the valid timetables. A colour is an hour.
    /// </summary>
    /// <param name="context">The Z3 context that owns the theorem.</param>
    /// <returns>The colouring theorem for <see cref="Clashes"/>.</returns>
    public static Theorem<GraphColouring> Create(Z3Context context)
        => GraphColouring.Create(context, Membership.Length, Clashes);

    private static IEnumerable<(int A, int B)> FindClashes()
    {
        for (int a = 0; a < Membership.Length; a++)
        {
            for (int b = a + 1; b < Membership.Length; b++)
            {
                if (Membership[a].Members.Intersect(Membership[b].Members).Any())
                {
                    yield return (a, b);
                }
            }
        }
    }
}
