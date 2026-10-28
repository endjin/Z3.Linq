namespace Z3.Linq.Examples.Packaging;

using System.Linq.Expressions;

/// <summary>
/// One installation plan for a small package catalogue: the version chosen for each of two
/// programs and the four libraries they may depend on. Z3 picks the versions; the rules come from
/// <see cref="Create(Z3Context)"/>.
/// </summary>
/// <remarks>
/// <para>
/// Ported from the simple package manager in "SAT/SMT by Example" by Dennis Yurichev, section
/// 22.5.1, https://smt.st. The catalogue (versions and dependencies) is the book's.
/// </para>
/// <para>
/// The book encodes "not installed" as version 999 and maximises the sum of the library versions,
/// so an unneeded library is pulled up to 999 and a needed one to its newest compatible version.
/// That gives the right answer for this catalogue, but for the wrong reason: the objective
/// rewards "not installed" because 999 is a large number, and 999 also satisfies any "at least
/// version N" rule, so a program could appear to have its dependency met by a library that is not
/// there. This port uses <see cref="NotInstalled"/> (0) instead, which no "at least" rule accepts,
/// and minimises <see cref="InstallCost"/>, an objective that says what it means: install as few
/// libraries as possible, and of those, the newest versions that work together.
/// </para>
/// </remarks>
public class PackageManager
{
    /// <summary>
    /// The version number that means the library is not installed.
    /// </summary>
    public const int NotInstalled = 0;

    /// <summary>
    /// The cost of installing a library, before the discount for a newer version. It is larger
    /// than the sum of every library's newest version, so leaving one library out always saves
    /// more than any combination of upgrades.
    /// </summary>
    public const int InstallPenalty = 100;

    /// <summary>
    /// Gets or sets the version of library A: 1 to 5, or <see cref="NotInstalled"/>.
    /// </summary>
    public int LibA { get; set; }

    /// <summary>
    /// Gets or sets the version of library B: 1, 4 or 5, or <see cref="NotInstalled"/>.
    /// </summary>
    public int LibB { get; set; }

    /// <summary>
    /// Gets or sets the version of library C: 10, 11 or 14, or <see cref="NotInstalled"/>.
    /// </summary>
    public int LibC { get; set; }

    /// <summary>
    /// Gets or sets the version of library D: 1 to 10, or <see cref="NotInstalled"/>. Nothing in
    /// the catalogue depends on it.
    /// </summary>
    public int LibD { get; set; }

    /// <summary>
    /// Gets or sets the version of program A: 1 or 2. Programs are always installed.
    /// </summary>
    public int ProgramA { get; set; }

    /// <summary>
    /// Gets or sets the version of program B: 7 or 8. Programs are always installed.
    /// </summary>
    public int ProgramB { get; set; }

    /// <summary>
    /// Gets the objective to minimise: each installed library costs
    /// <see cref="InstallPenalty"/> less its version number, and a library that is not installed
    /// costs nothing.
    /// </summary>
    /// <remarks>
    /// Every version is far below <see cref="InstallPenalty"/>, so the objective works in two
    /// tiers. First it installs no library that the programs do not need; then, among the
    /// libraries that must be installed, it prefers newer versions.
    /// </remarks>
    public static Expression<Func<PackageManager, int>> InstallCost { get; } =
        t => (t.LibA == NotInstalled ? 0 : InstallPenalty - t.LibA)
           + (t.LibB == NotInstalled ? 0 : InstallPenalty - t.LibB)
           + (t.LibC == NotInstalled ? 0 : InstallPenalty - t.LibC)
           + (t.LibD == NotInstalled ? 0 : InstallPenalty - t.LibD);

    /// <summary>
    /// Creates a theorem holding the whole catalogue: which versions exist, and what each version
    /// needs.
    /// </summary>
    /// <param name="context">The Z3 context to create the theorem in.</param>
    /// <returns>A theorem whose solutions are the consistent installation plans.</returns>
    public static Theorem<PackageManager> Create(Z3Context context)
    {
        return context.NewTheorem<PackageManager>()

            // The versions that exist.
            .Where(t => (t.LibA >= 1 && t.LibA <= 5) || t.LibA == NotInstalled)
            .Where(t => t.LibB == 1 || t.LibB == 4 || t.LibB == 5 || t.LibB == NotInstalled)
            .Where(t => t.LibC == 10 || t.LibC == 11 || t.LibC == 14 || t.LibC == NotInstalled)
            .Where(t => (t.LibD >= 1 && t.LibD <= 10) || t.LibD == NotInstalled)
            .Where(t => t.ProgramA == 1 || t.ProgramA == 2)
            .Where(t => t.ProgramB == 7 || t.ProgramB == 8)

            // Library C depends on library A: v10 works with A v1 to v3 only, v11 needs at least
            // A v3, and v14 needs at least A v5.
            .Where(t => t.LibC != 10 || (t.LibA >= 1 && t.LibA <= 3))
            .Where(t => t.LibC != 11 || t.LibA >= 3)
            .Where(t => t.LibC != 14 || t.LibA >= 5)

            // Program A v1 needs exactly A v2, B v4 or v5, and C v10; v2 needs A v3, B v5, C v11.
            .Where(t => t.ProgramA != 1 || (t.LibA == 2 && (t.LibB == 4 || t.LibB == 5) && t.LibC == 10))
            .Where(t => t.ProgramA != 2 || (t.LibA == 3 && t.LibB == 5 && t.LibC == 11))

            // Program B v7 needs at least A v2 and C v10; v8 needs at least A v6 and C v11.
            .Where(t => t.ProgramB != 7 || (t.LibA >= 2 && t.LibC >= 10))
            .Where(t => t.ProgramB != 8 || (t.LibA >= 6 && t.LibC >= 11));
    }

    /// <summary>
    /// Asks for particular versions of the two programs.
    /// </summary>
    /// <param name="theorem">The catalogue theorem from <see cref="Create(Z3Context)"/>.</param>
    /// <param name="programA">The version of program A to install: 1 or 2.</param>
    /// <param name="programB">The version of program B to install: 7 or 8.</param>
    /// <returns>The theorem, restricted to plans that install those versions.</returns>
    public static Theorem<PackageManager> WithPrograms(Theorem<PackageManager> theorem, int programA, int programB)
    {
        ArgumentNullException.ThrowIfNull(theorem);

        return theorem.Where(t => t.ProgramA == programA && t.ProgramB == programB);
    }

    /// <summary>
    /// Finds the best plan for a theorem: the fewest libraries, at the newest versions that work
    /// together.
    /// </summary>
    /// <param name="theorem">The catalogue theorem, with any requests added.</param>
    /// <returns>A deferred minimisation of <see cref="InstallCost"/>.</returns>
    public static ISolveable<PackageManager> Resolve(Theorem<PackageManager> theorem)
    {
        ArgumentNullException.ThrowIfNull(theorem);

        return theorem.OrderBy(InstallCost);
    }

    /// <summary>
    /// Returns the plan as text, for display and debugging.
    /// </summary>
    /// <returns>The version of each package, with "-" for a library that is not installed.</returns>
    public override string ToString()
    {
        return $"programA v{ProgramA}, programB v{ProgramB}, libA {Show(LibA)}, libB {Show(LibB)}, libC {Show(LibC)}, libD {Show(LibD)}";

        static string Show(int version) => version == NotInstalled ? "-" : $"v{version}";
    }
}
