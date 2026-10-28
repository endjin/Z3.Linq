namespace Z3.Linq.Examples.GraphColouring;

using System.Collections.Generic;
using System.Linq.Expressions;

/// <summary>
/// A vertex colouring of an undirected graph: one colour per vertex, so that the two ends of
/// every edge get different colours.
/// </summary>
/// <remarks>
/// <para>
/// Colours are the integers <c>0</c> to <c>Colours - 1</c>. <see cref="Colours"/> is itself a
/// symbol, so ordering by it asks Z3 for the chromatic number: the fewest colours the graph
/// needs. The same model covers map colouring (vertices are countries, edges are borders) and
/// timetabling (vertices are meetings, edges join meetings that share a person, and a colour is
/// a time slot).
/// </para>
/// <para>
/// Adapted from "SAT/SMT by Example" by Dennis Yurichev, sections 9.1 and 9.2,
/// https://smt.st.
/// </para>
/// </remarks>
public class GraphColouring
{
    /// <summary>
    /// Gets or sets the colour of each vertex, indexed by vertex number.
    /// </summary>
    public int[] Colour { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of colours in use. Every entry of <see cref="Colour"/> is less
    /// than this.
    /// </summary>
    public int Colours { get; set; }

    /// <summary>
    /// Creates a theorem whose solutions are the valid colourings of a graph.
    /// </summary>
    /// <param name="context">The Z3 context that owns the theorem.</param>
    /// <param name="vertexCount">The number of vertices, numbered from zero.</param>
    /// <param name="edges">The edges, as pairs of vertex numbers.</param>
    /// <returns>
    /// A theorem with one range constraint per vertex and one inequality per edge. Add
    /// <c>orderby t.Colours</c> to find the fewest colours, or <c>where t.Colours == k</c> to fix
    /// the palette.
    /// </returns>
    public static Theorem<GraphColouring> Create(Z3Context context, int vertexCount, IEnumerable<(int A, int B)> edges)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(vertexCount);

        // The vertex constraints below bound Colours from below, but an empty graph has none, and
        // minimising an unbounded symbol would return the library's implicit int.MinValue.
        var theorem = context.NewTheorem(new GraphColouring { Colour = new int[vertexCount] })
            .Where(t => t.Colours >= 0);

        for (int vertex = 0; vertex < vertexCount; vertex++)
        {
            int v = vertex;
            theorem = theorem.Where(t => t.Colour[v] >= 0 && t.Colour[v] < t.Colours);
        }

        foreach ((int a, int b) in edges)
        {
            if (a < 0 || a >= vertexCount || b < 0 || b >= vertexCount)
            {
                throw new ArgumentException($"Edge ({a}, {b}) refers to a vertex outside 0..{vertexCount - 1}.", nameof(edges));
            }

            int from = a;
            int to = b;
            theorem = theorem.Where(t => t.Colour[from] != t.Colour[to]);
        }

        return theorem;
    }

    /// <summary>
    /// Builds the objective "how many vertices have this colour", for use with
    /// <see cref="Theorem{T}.Optimize{TResult}(Optimization, Expression{Func{T, TResult}}, CancellationToken)"/>.
    /// </summary>
    /// <param name="vertexCount">The number of vertices.</param>
    /// <param name="colour">The colour to count.</param>
    /// <returns>
    /// A lambda equivalent to <c>t =&gt; (t.Colour[0] == colour ? 1 : 0) + (t.Colour[1] == colour ? 1 : 0) + ...</c>.
    /// It is built as an expression tree because Z3.Linq cannot translate <c>Enumerable.Count</c>.
    /// </returns>
    public static Expression<Func<GraphColouring, int>> CountOfColour(int vertexCount, int colour)
    {
        var t = Expression.Parameter(typeof(GraphColouring), "t");
        var colours = Expression.Property(t, typeof(GraphColouring).GetProperty(nameof(Colour))!);
        var one = Expression.Constant(1);
        var zero = Expression.Constant(0);

        Expression sum = zero;
        for (int vertex = 0; vertex < vertexCount; vertex++)
        {
            var cell = Expression.ArrayIndex(colours, Expression.Constant(vertex));
            var isColour = Expression.Equal(cell, Expression.Constant(colour));
            sum = Expression.Add(sum, Expression.Condition(isColour, one, zero));
        }

        return Expression.Lambda<Func<GraphColouring, int>>(sum, t);
    }
}
