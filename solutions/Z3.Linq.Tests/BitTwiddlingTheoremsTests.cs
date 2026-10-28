namespace Z3.Linq.Tests;

using Z3.Linq.Examples.BitTwiddling;

/// <summary>
/// Tests that pin the verdicts of the bit-twiddling proofs from "SAT/SMT by Example".
/// </summary>
/// <remarks>
/// <para>
/// Each proof is a search for a counterexample, so a proved trick is asserted by
/// <c>TrySolve</c> returning <see langword="false"/>. That is the only safe way to check an
/// unsatisfiable value-tuple theorem: <c>Solve</c> returns <c>default</c>, which for a tuple of
/// zeros cannot be told apart from a real model.
/// </para>
/// <para>
/// Where a counterexample exists it is not unique (the naive average fails for about half of
/// all pairs, and the zero-byte test without <c>~v</c> for many values), so those tests do not pin
/// the input Z3 returns. They re-evaluate the trick and the reference in plain C# on that input
/// and assert that the two really disagree. The inverse of 3 modulo 2^32 is unique (a separate
/// test proves it), so its value is pinned.
/// </para>
/// <para>
/// Measured with Microsoft.Z3 5.3.2, each theorem solves in 8-150 ms.
/// </para>
/// </remarks>
[TestClass]
public class BitTwiddlingTheoremsTests
{
    [TestMethod]
    public void TrySolve_XorSwapCounterexample_FindsNone()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.XorSwapCounterexample(context).TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }

    [TestMethod]
    public void TrySolve_XorSwap64Counterexample_FindsNone()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.XorSwap64Counterexample(context).TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }

    [TestMethod]
    public void TrySolve_AddSubtractSwapCounterexample_FindsNone()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.AddSubtractSwapCounterexample(context).TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }

    [TestMethod]
    public void TrySolve_DietzAverageCounterexample_FindsNone()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.DietzAverageCounterexample(context).TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }

    [TestMethod]
    public void TrySolve_DietzAverage64Counterexample_FindsNone()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.DietzAverage64Counterexample(context).TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }

    [TestMethod]
    public void TrySolve_NaiveAverageCounterexample_FindsAPairWhoseSumOverflows()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.NaiveAverageCounterexample(context).TrySolve(out var result);

        // Assert
        found.ShouldBeTrue();
        ((ulong)result.x + result.y).ShouldBeGreaterThanOrEqualTo(1UL << 32);
        unchecked((result.x + result.y) / 2u).ShouldNotBe((uint)(((ulong)result.x + result.y) >> 1));
    }

    [TestMethod]
    public void TrySolve_BranchlessAbsCounterexample_FindsNone()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.BranchlessAbsCounterexample(context).TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }

    [TestMethod]
    public void TrySolve_BranchlessMinMaxCounterexample_FindsNone()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.BranchlessMinMaxCounterexample(context).TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }

    [TestMethod]
    public void TrySolve_HasZeroByteCounterexample_FindsNone()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.HasZeroByteCounterexample(context).TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }

    [TestMethod]
    public void TrySolve_HasZeroByteWithoutNotCounterexample_FindsAValueWhereTrickAndReferenceDisagree()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.HasZeroByteWithoutNotCounterexample(context).TrySolve(out var result);

        // Assert
        found.ShouldBeTrue();
        BitTwiddlingTheorems.HasZeroByteWithoutNot(result.v).ShouldNotBe(
            BitTwiddlingTheorems.HasZeroByteReference(result.v),
            $"v = 0x{result.v:X8}");
    }

    [TestMethod]
    [DataRow(0x00000000u, DisplayName = "all bytes zero")]
    [DataRow(0x12003456u, DisplayName = "one zero byte")]
    [DataRow(0x01010101u, DisplayName = "no zero byte, low values")]
    [DataRow(0xC0C0C0C0u, DisplayName = "no zero byte, high values")]
    [DataRow(0xFFFFFFFFu, DisplayName = "no zero byte, all ones")]
    public void HasZeroByte_SampleValue_AgreesWithTheReference(uint v)
    {
        // Act
        bool trick = BitTwiddlingTheorems.HasZeroByte(v);

        // Assert: a spot check that the C# helpers say the same as the proved theorem.
        trick.ShouldBe(BitTwiddlingTheorems.HasZeroByteReference(v));
    }

    [TestMethod]
    public void TrySolve_ModularInverseOfThree_IsAAAAAAAB()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.ModularInverse(context, 3u).TrySolve(out var result);

        // Assert
        found.ShouldBeTrue();
        result.inverse.ShouldBe(0xAAAAAAABu);
    }

    [TestMethod]
    [DataRow(3u, DisplayName = "3")]
    [DataRow(5u, DisplayName = "5")]
    [DataRow(7u, DisplayName = "7")]
    public void TrySolve_ModularInverseOfOddDivisor_MultipliesToOne(uint divisor)
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.ModularInverse(context, divisor).TrySolve(out var result);

        // Assert
        found.ShouldBeTrue();
        result.divisor.ShouldBe(divisor);
        unchecked(divisor * result.inverse).ShouldBe(1u);
    }

    [TestMethod]
    public void TrySolve_ModularInverseOfTen_FindsNone()
    {
        // Arrange: an even number shares the factor 2 with 2^32, so it has no inverse.
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.ModularInverse(context, 10u).TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }

    [TestMethod]
    public void TrySolve_ModularInverseBookForm_IsAAAAAAAB()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.ModularInverseBookForm(context).TrySolve(out var result);

        // Assert
        found.ShouldBeTrue();
        result.m.ShouldBe(0xAAAAAAABu);
    }

    [TestMethod]
    public void TrySolve_ModularInverseDividesCounterexample_FindsNone()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.ModularInverseDividesCounterexample(context).TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }

    [TestMethod]
    public void TrySolve_ModularInverseUniquenessCounterexample_FindsNone()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        bool found = BitTwiddlingTheorems.ModularInverseUniquenessCounterexample(context).TrySolve(out _);

        // Assert
        found.ShouldBeFalse();
    }
}
