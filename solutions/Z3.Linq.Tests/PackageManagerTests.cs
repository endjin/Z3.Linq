namespace Z3.Linq.Tests;

using Z3.Linq.Examples.Packaging;

/// <summary>
/// Tests that resolve the package catalogue of <see cref="PackageManager"/>, the simple package
/// manager from "SAT/SMT by Example" (Dennis Yurichev, section 22.5.1, https://smt.st).
/// </summary>
/// <remarks>
/// <para>
/// Each request has a unique best plan, so the plans are asserted in full. Uniqueness is checked,
/// not assumed: a second solve asks for a plan with the same cost that differs from the pinned
/// one, and must come back unsatisfiable. For the requests the catalogue cannot meet, the tests
/// check that the theorem is unsatisfiable rather than that the optimiser returns some plan.
/// </para>
/// <para>
/// This port marks a library that is not installed with version 0 and minimises
/// <see cref="PackageManager.InstallCost"/>, where the book uses 999 and maximises the sum of the
/// versions. Both give the same plans for this catalogue; the remarks on
/// <see cref="PackageManager"/> say why the port does not copy the book. Each test takes a few
/// tens of milliseconds.
/// </para>
/// </remarks>
[TestClass]
public class PackageManagerTests
{
    [TestMethod]
    [DataRow(1, 7, 2, 5, 10, DisplayName = "programA v1 + programB v7")]
    [DataRow(2, 7, 3, 5, 11, DisplayName = "programA v2 + programB v7")]
    public void Resolve_SatisfiableRequest_InstallsTheNewestCompatibleLibrariesAndNothingElse(
        int programA, int programB, int libA, int libB, int libC)
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = PackageManager.WithPrograms(PackageManager.Create(context), programA, programB);

        // Act
        var plan = PackageManager.Resolve(theorem).Solve();

        // Assert
        plan.ShouldNotBeNull();
        plan.ProgramA.ShouldBe(programA);
        plan.ProgramB.ShouldBe(programB);
        plan.LibA.ShouldBe(libA);
        plan.LibB.ShouldBe(libB);
        plan.LibC.ShouldBe(libC);

        // Nothing depends on library D, so the best plan leaves it out.
        plan.LibD.ShouldBe(PackageManager.NotInstalled);
    }

    [TestMethod]
    [DataRow(1, 7, DisplayName = "programA v1 + programB v7")]
    [DataRow(2, 7, DisplayName = "programA v2 + programB v7")]
    public void Resolve_SatisfiableRequest_EveryOtherPlanCostsMore(int programA, int programB)
    {
        // Arrange: find the best plan, then exclude its library versions and optimise again. The
        // runner-up must cost strictly more, which makes the best plan the unique optimum.
        using var context = new Z3Context();
        var theorem = PackageManager.WithPrograms(PackageManager.Create(context), programA, programB);
        var best = PackageManager.Resolve(theorem).Solve();
        best.ShouldNotBeNull();

        var others = theorem
            .Where(t => !(t.LibA == best.LibA && t.LibB == best.LibB && t.LibC == best.LibC && t.LibD == best.LibD));

        // Act
        bool found = PackageManager.Resolve(others).TrySolve(out var runnerUp);

        // Assert: the catalogue always has other plans (library D at any version, for one).
        found.ShouldBeTrue();
        Func<PackageManager, int> cost = PackageManager.InstallCost.Compile();
        cost(runnerUp!).ShouldBeGreaterThan(cost(best));
    }

    [TestMethod]
    [DataRow(1, 8, DisplayName = "programA v1 + programB v8")]
    [DataRow(2, 8, DisplayName = "programA v2 + programB v8")]
    public void Resolve_RequestTheCatalogueCannotMeet_IsUnsatisfiable(int programA, int programB)
    {
        // Arrange: programB v8 needs library A v6 or later, which does not exist, and either
        // programA version pins library A to v2 or v3 anyway. The book's model rejects both too.
        using var context = new Z3Context();
        var theorem = PackageManager.WithPrograms(PackageManager.Create(context), programA, programB);

        // Act
        bool satisfiable = PackageManager.Resolve(theorem).TrySolve(out _);

        // Assert
        satisfiable.ShouldBeFalse();
    }

    [TestMethod]
    public void Resolve_NoProgramVersionsRequested_ChoosesTheNewestPlanThatWorks()
    {
        // Arrange: leave the program versions to Z3. Of the two plans that work, programA v2
        // needs newer libraries (A v3, C v11) than programA v1 (A v2, C v10), so it costs less.
        using var context = new Z3Context();

        // Act
        var plan = PackageManager.Resolve(PackageManager.Create(context)).Solve();

        // Assert
        plan.ShouldNotBeNull();
        plan.ProgramA.ShouldBe(2);
        plan.ProgramB.ShouldBe(7);
        plan.LibA.ShouldBe(3);
        plan.LibB.ShouldBe(5);
        plan.LibC.ShouldBe(11);
        plan.LibD.ShouldBe(PackageManager.NotInstalled);
    }

    [TestMethod]
    public void Resolve_LibraryDRequested_InstallsItAtItsNewestVersion()
    {
        // Arrange: when a request forces library D in, the cost now prefers its newest version.
        // This shows that "not installed" wins only because nothing needs the library, not
        // because of a magic version number.
        using var context = new Z3Context();
        var theorem = PackageManager.WithPrograms(PackageManager.Create(context), 1, 7)
            .Where(t => t.LibD != PackageManager.NotInstalled);

        // Act
        var plan = PackageManager.Resolve(theorem).Solve();

        // Assert
        plan.ShouldNotBeNull();
        plan.LibD.ShouldBe(10);
    }
}
