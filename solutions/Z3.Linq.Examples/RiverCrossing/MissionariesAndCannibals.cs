namespace Z3.Linq.Examples.RiverCrossing;
 
using System.Text;

/// <summary>
/// This class solves the famous <see href="https://en.wikipedia.org/wiki/Missionaries_and_cannibals_problem">Missionaries and cannibals problem</see>
/// "Three missionaries and three cannibals must cross a river using a boat which can carry at most two people, under the constraint that, for both banks, 
/// if there are missionaries present on the bank, they cannot be outnumbered by cannibals (if they were, the cannibals would eat the missionaries). 
/// The boat cannot cross the river by itself with no people on board. The class contains the state representation for the problem and embeds the rules into a Z3 solving theorem
/// </summary>
/// <remarks>
/// <para>
/// Unlike, say, Sudoku, where the end state is the solution, here the end state is known up front: it's the state
/// where everyone is on the far bank. The information we want Z3 to generate is the series of steps that get from
/// the start state to that known end state.
/// </para>
/// </remarks>
public class MissionariesAndCannibals
{
    /// <summary>
    /// Gets or sets the number of missionaries and cannibals.
    /// </summary>
    /// <remarks>
    /// <para>3 in the original problem.</para>
    /// <para>This should be specified with a 'where' constraint.</para>
    /// </remarks>
    public int MissionaryAndCannibalCount { get; set; }

    /// <summary>
    /// The maximum total number of missionaries and cannibals that the boat can hold.
    /// </summary>
    /// <remarks>
    /// <para>2 in the original problem.</para>
    /// <para>This should be specified with a 'where' constraint.</para>
    /// </remarks>
    public int SizeBoat { get; set; }

    /// <summary>
    /// Gets or sets the number of steps in the solution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When using this theorem type, this is effectively an output - Z3 tells us how many
    /// steps its solution has by setting this. To get the best solution, we can ask Z3
    /// to optimize for minimizing this property.
    /// </para>
    /// <para>
    /// Internally, we also use this to express the constraints for the final state, e.g.
    /// <c>t.Missionares[t.Length - 1] == 0</c>.
    /// </para>
    /// </remarks>
    public int Length
    {
        get => this.Missionaries.Length;
        set
        {
            // When length is computed by Z3, we initialize arrays to retrieve values
            Missionaries = new int[value];
            Cannibals = new int[value];
        }
    }

    /// <summary>
    /// Gets or sets an array that contains the number of Missionaries on the starting bank
    /// at each step.
    /// </summary>
    public int[] Missionaries { get; set; } = default!;

    /// <summary>
    /// Gets or sets an array that contains the number of Cannibals on the starting bank
    /// at each step.
    /// </summary>
    public int[] Cannibals { get; set; } = default!;

    /// <summary>
    /// An easy to read representation of the proposed solution
    /// </summary>
    /// <returns>A string where each line represents the environment state for a step</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Cannibals.Length; i++)
        {
            sb.AppendLine($"{i + 1} - (({Missionaries[i]}M, {Cannibals[i]}C, {1 - i % 2}), ({i % 2}, {MissionaryAndCannibalCount - Missionaries[i]}M, {MissionaryAndCannibalCount - Cannibals[i]}C))");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a theorem with the rules of the game, and the starting parameters initialized from this instance
    /// </summary>
    /// <param name="context">A wrapping Z3 context used to interpret c# Lambda into Z3 constraints</param>
    /// <param name="maxLength">The maximum number of steps explore.</param>
    /// <returns>A typed theorem to be solved</returns>
    public static Theorem<MissionariesAndCannibals> Create(Z3Context context, int maxLength)
    {
        var theorem = context.NewTheorem<MissionariesAndCannibals>();
            
        // Initial state
        theorem = theorem.Where(t => t.Missionaries[0] == t.MissionaryAndCannibalCount && t.Cannibals[0] == t.MissionaryAndCannibalCount);

        // Transition model: We filter each step according to legal moves
        for (int iclosure = 0; iclosure < maxLength; iclosure++)
        {
            var i = iclosure;
            //The 2 banks cannot have more people than the initial population
            theorem = theorem.Where(t => t.Cannibals[i] >= 0
                                      && t.Cannibals[i] <= t.MissionaryAndCannibalCount
                                      && t.Missionaries[i] >= 0
                                      && t.Missionaries[i] <= t.MissionaryAndCannibalCount);
            if (i % 2 == 0)
            {
                // On even steps, the starting bank loses between 1 and SizeBoat people 
                theorem = theorem.Where(t => t.Cannibals[i + 1] <= t.Cannibals[i]
                                          && t.Missionaries[i + 1] <= t.Missionaries[i]
                                          && t.Cannibals[i + 1] + t.Missionaries[i + 1] - t.Cannibals[i] - t.Missionaries[i] < 0
                                          && t.Cannibals[i + 1] + t.Missionaries[i + 1] - t.Cannibals[i] - t.Missionaries[i] >= -t.SizeBoat);

            }
            else
            {
                // On odd steps, the starting bank gains between 1 and SizeBoat people
                theorem = theorem.Where(t => t.Cannibals[i + 1] >= t.Cannibals[i]
                                          && t.Missionaries[i + 1] >= t.Missionaries[i]
                                          && t.Cannibals[i + 1] + t.Missionaries[i + 1] - t.Cannibals[i] - t.Missionaries[i] > 0
                                          && t.Cannibals[i + 1] + t.Missionaries[i + 1] - t.Cannibals[i] - t.Missionaries[i] <= t.SizeBoat);

            }

            //Never less missionaries than cannibals on any bank
            theorem = theorem.Where(t => (t.Missionaries[i] == 0 || (t.Missionaries[i] >= t.Cannibals[i]))
                                      && (t.Missionaries[i] == t.MissionaryAndCannibalCount || ((t.MissionaryAndCannibalCount - t.Missionaries[i]) >= (t.MissionaryAndCannibalCount - t.Cannibals[i]))));
        }

        // Goal state
        // When finished, No more people on the starting bank
        theorem = theorem.Where(t => t.Length > 0
                                  && t.Length < maxLength
                                  && t.Missionaries[t.Length - 1] == 0
                                  && t.Cannibals[t.Length - 1] == 0);

        return theorem;
    }
}