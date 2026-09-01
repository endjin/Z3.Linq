namespace Z3.Linq;

/// <summary>
/// The direction of an optimisation: which way <c>Optimize</c> moves its objective.
/// </summary>
public enum Optimization
{
    /// <summary>
    /// Find a solution in which the objective is as large as the constraints allow.
    /// </summary>
    Maximize,

    /// <summary>
    /// Find a solution in which the objective is as small as the constraints allow.
    /// </summary>
    Minimize
}
