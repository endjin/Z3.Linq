namespace Z3.Linq.Examples.BitTwiddling;

/// <summary>
/// Bit-twiddling tricks proved correct, or shown to be wrong, by Z3 over 32- and 64-bit
/// bit-vectors.
/// </summary>
/// <remarks>
/// <para>
/// Adapted from "SAT/SMT by Example" by Dennis Yurichev (sections 3.7, 4.3, 4.4, 4.10, 4.11 and
/// 4.12), https://smt.st.
/// </para>
/// <para>
/// Each <c>...Counterexample</c> method states the negation of a claim: it asks Z3 for inputs
/// where the trick and a plain reference implementation disagree. Because <see cref="uint"/> and
/// <see cref="ulong"/> symbols are bit-vectors, Z3 reasons about every possible input at once,
/// with the same wrapping arithmetic as C#. If <see cref="Theorem{T}.TrySolve(out T, System.Threading.CancellationToken)"/>
/// returns <see langword="false"/>, no counterexample exists, and the claim is proved for all
/// 2^32 (or 2^64) inputs. If it returns <see langword="true"/>, the result is a concrete input
/// that breaks the claim.
/// </para>
/// <para>
/// The models avoid signed operators on <see cref="uint"/>: unary minus promotes to
/// <see cref="long"/>, so negation is written <c>0u - x</c>, the sign bit is tested with
/// <c>(x &gt;&gt; 31) == 1u</c>, and an arithmetic shift right by 31 is <c>0u - (x &gt;&gt; 31)</c>.
/// In C# <c>==</c> binds tighter than <c>^</c> and <c>&amp;</c>, so those expressions are
/// always in parentheses.
/// </para>
/// </remarks>
public static class BitTwiddlingTheorems
{
    /// <summary>
    /// The value from which <see cref="ModularInverseBookForm(Z3Context)"/> divides by 3.
    /// </summary>
    public const uint BookDividend = 0x1234567u * 3u;

    /// <summary>
    /// Looks for 32-bit values where swapping with three XORs does not swap them.
    /// </summary>
    /// <remarks>
    /// <c>x ^= y; y ^= x; x ^= y;</c> written in single-assignment form: each step gets its own
    /// symbol. No counterexample exists (book section 4.4.1).
    /// </remarks>
    public static Theorem<(uint x1, uint y1, uint x2, uint y3, uint x4)> XorSwapCounterexample(Z3Context context)
        => from t in context.NewTheorem<(uint x1, uint y1, uint x2, uint y3, uint x4)>()
           where t.x2 == (t.x1 ^ t.y1)
           where t.y3 == (t.y1 ^ t.x2)
           where t.x4 == (t.x2 ^ t.y3)
           where !(t.x4 == t.y1 && t.y3 == t.x1)
           select t;

    /// <summary>
    /// Looks for 64-bit values where swapping with three XORs does not swap them.
    /// </summary>
    /// <remarks>No counterexample exists.</remarks>
    public static Theorem<(ulong x1, ulong y1, ulong x2, ulong y3, ulong x4)> XorSwap64Counterexample(Z3Context context)
        => from t in context.NewTheorem<(ulong x1, ulong y1, ulong x2, ulong y3, ulong x4)>()
           where t.x2 == (t.x1 ^ t.y1)
           where t.y3 == (t.y1 ^ t.x2)
           where t.x4 == (t.x2 ^ t.y3)
           where !(t.x4 == t.y1 && t.y3 == t.x1)
           select t;

    /// <summary>
    /// Looks for 32-bit values where swapping with an addition and two subtractions does not
    /// swap them.
    /// </summary>
    /// <remarks>
    /// <c>x += y; y = x - y; x -= y;</c>. The addition can overflow, but the subtractions wrap
    /// back by the same amount, so no counterexample exists.
    /// </remarks>
    public static Theorem<(uint x1, uint y1, uint x2, uint y3, uint x4)> AddSubtractSwapCounterexample(Z3Context context)
        => from t in context.NewTheorem<(uint x1, uint y1, uint x2, uint y3, uint x4)>()
           where t.x2 == t.x1 + t.y1
           where t.y3 == t.x2 - t.y1
           where t.x4 == t.x2 - t.y3
           where !(t.x4 == t.y1 && t.y3 == t.x1)
           select t;

    /// <summary>
    /// Looks for 32-bit values where Dietz's formula <c>(x &amp; y) + ((x ^ y) &gt;&gt; 1)</c> is not
    /// the floor of their average.
    /// </summary>
    /// <remarks>
    /// The reference average is computed in 64 bits, where <c>x + y</c> cannot overflow. No
    /// counterexample exists (book section 4.3).
    /// </remarks>
    public static Theorem<(uint x, uint y)> DietzAverageCounterexample(Z3Context context)
        => from t in context.NewTheorem<(uint x, uint y)>()
           where (ulong)((t.x & t.y) + ((t.x ^ t.y) >> 1)) != (((ulong)t.x + (ulong)t.y) >> 1)
           select t;

    /// <summary>
    /// Looks for 32-bit values where the naive <c>(x + y) / 2</c> is not the floor of their
    /// average.
    /// </summary>
    /// <remarks>
    /// A counterexample exists: any pair whose sum is 2^32 or more overflows.
    /// </remarks>
    public static Theorem<(uint x, uint y)> NaiveAverageCounterexample(Z3Context context)
        => from t in context.NewTheorem<(uint x, uint y)>()
           where (ulong)((t.x + t.y) / 2u) != (((ulong)t.x + (ulong)t.y) >> 1)
           select t;

    /// <summary>
    /// Looks for 64-bit values where Dietz's formula differs from adding the two halves and
    /// the carry of their low bits.
    /// </summary>
    /// <remarks>
    /// There is no wider type to compute a 64-bit reference in, so this compares two
    /// overflow-free formulas with each other. No counterexample exists.
    /// </remarks>
    public static Theorem<(ulong x, ulong y)> DietzAverage64Counterexample(Z3Context context)
        => from t in context.NewTheorem<(ulong x, ulong y)>()
           where (t.x & t.y) + ((t.x ^ t.y) >> 1) != (t.x >> 1) + (t.y >> 1) + (t.x & t.y & 1UL)
           select t;

    /// <summary>
    /// Looks for a 32-bit two's complement value where the branchless absolute value
    /// <c>(x ^ y) - y</c>, with <c>y = x &gt;&gt; 31</c> (arithmetic shift), differs from the
    /// branching one.
    /// </summary>
    /// <remarks>
    /// The bit pattern is read as a signed value, so the absolute value of
    /// <see cref="int.MinValue"/> is itself, in both versions. No counterexample exists (book
    /// section 4.10).
    /// </remarks>
    public static Theorem<(uint x, uint y, uint reference, uint trick)> BranchlessAbsCounterexample(Z3Context context)
        => from t in context.NewTheorem<(uint x, uint y, uint reference, uint trick)>()
           where t.reference == ((t.x >> 31) == 1u ? 0u - t.x : t.x)
           where t.y == 0u - (t.x >> 31)
           where t.trick == (t.x ^ t.y) - t.y
           where t.reference != t.trick
           select t;

    /// <summary>
    /// Looks for 32-bit unsigned values where the branchless minimum
    /// <c>y ^ ((x ^ y) &amp; -(x &lt; y))</c> or maximum <c>x ^ ((x ^ y) &amp; -(x &lt; y))</c> is wrong.
    /// </summary>
    /// <remarks>
    /// <c>-(x &lt; y)</c> is a mask of all ones when <c>x &lt; y</c> and zero otherwise. No
    /// counterexample exists (book section 4.11).
    /// </remarks>
    public static Theorem<(uint x, uint y)> BranchlessMinMaxCounterexample(Z3Context context)
        => from t in context.NewTheorem<(uint x, uint y)>()
           where (t.y ^ ((t.x ^ t.y) & (0u - (t.x < t.y ? 1u : 0u)))) != (t.x < t.y ? t.x : t.y)
              || (t.x ^ ((t.x ^ t.y) & (0u - (t.x < t.y ? 1u : 0u)))) != (t.x < t.y ? t.y : t.x)
           select t;

    /// <summary>
    /// Looks for a 32-bit value where <c>((v - 0x01010101) &amp; ~v &amp; 0x80808080) != 0</c> does not
    /// agree with "one of the four bytes is zero".
    /// </summary>
    /// <remarks>
    /// <para>
    /// The search covers both directions, so finding nothing proves the expression is an exact
    /// test for a zero byte (book section 4.12).
    /// </para>
    /// <para>
    /// C# has no one-element value tuple, so <c>unused</c> is a free symbol that fills the second slot.
    /// </para>
    /// </remarks>
    public static Theorem<(uint v, int unused)> HasZeroByteCounterexample(Z3Context context)
        => from t in context.NewTheorem<(uint v, int unused)>()
           where (((t.v - 0x01010101u) & ~t.v & 0x80808080u) != 0u)
              != ((t.v & 0x000000FFu) == 0u || (t.v & 0x0000FF00u) == 0u || (t.v & 0x00FF0000u) == 0u || (t.v & 0xFF000000u) == 0u)
           select t;

    /// <summary>
    /// The same search as <see cref="HasZeroByteCounterexample(Z3Context)"/> for the expression
    /// without <c>~</c>, as the book's C listing prints it.
    /// </summary>
    /// <remarks>
    /// A counterexample exists: a byte of <c>0xC0</c> or more in every position, such as
    /// <c>0xC0C0C0C0</c>, makes the expression report a zero byte that is not there.
    /// </remarks>
    public static Theorem<(uint v, int unused)> MisprintedHasZeroByteCounterexample(Z3Context context)
        => from t in context.NewTheorem<(uint v, int unused)>()
           where (((t.v - 0x01010101u) & t.v & 0x80808080u) != 0u)
              != ((t.v & 0x000000FFu) == 0u || (t.v & 0x0000FF00u) == 0u || (t.v & 0x00FF0000u) == 0u || (t.v & 0xFF000000u) == 0u)
           select t;

    /// <summary>
    /// Evaluates the "has a zero byte" trick with the <c>~v</c> term, in plain C#.
    /// </summary>
    public static bool HasZeroByte(uint v) => ((v - 0x01010101u) & ~v & 0x80808080u) != 0u;

    /// <summary>
    /// Evaluates the "has a zero byte" trick without the <c>~v</c> term, in plain C#.
    /// </summary>
    public static bool MisprintedHasZeroByte(uint v) => ((v - 0x01010101u) & v & 0x80808080u) != 0u;

    /// <summary>
    /// The reference for the "has a zero byte" trick: checks each byte, in plain C#.
    /// </summary>
    public static bool HasZeroByteReference(uint v)
        => (v & 0x000000FFu) == 0u || (v & 0x0000FF00u) == 0u || (v & 0x00FF0000u) == 0u || (v & 0xFF000000u) == 0u;

    /// <summary>
    /// Finds the multiplicative inverse of <paramref name="divisor"/> modulo 2^32: the value
    /// that, multiplied by <paramref name="divisor"/>, wraps round to 1.
    /// </summary>
    /// <remarks>
    /// An inverse exists exactly when the divisor is odd. Multiplying a multiple of the divisor
    /// by the inverse divides it exactly, which is how compilers replace division by a constant
    /// with a multiplication (book section 3.7).
    /// </remarks>
    public static Theorem<(uint divisor, uint inverse)> ModularInverse(Z3Context context, uint divisor)
        => from t in context.NewTheorem<(uint divisor, uint inverse)>()
           where t.divisor == divisor
           where t.divisor * t.inverse == 1u
           select t;

    /// <summary>
    /// The book's form of the inverse search: a multiplier <c>m</c> that turns
    /// <see cref="BookDividend"/> into <see cref="BookDividend"/> / 3.
    /// </summary>
    public static Theorem<(uint m, int unused)> ModularInverseBookForm(Z3Context context)
        => from t in context.NewTheorem<(uint m, int unused)>()
           where BookDividend * t.m == BookDividend / 3u
           select t;

    /// <summary>
    /// Looks for a multiple of 3 that multiplying by <c>0xAAAAAAAB</c> does not divide by 3.
    /// </summary>
    /// <remarks>No counterexample exists.</remarks>
    public static Theorem<(uint x, uint inverse)> ModularInverseDividesCounterexample(Z3Context context)
        => from t in context.NewTheorem<(uint x, uint inverse)>()
           where t.inverse == 0xAAAAAAABu
           where (t.x * 3u) * t.inverse != t.x
           select t;

    /// <summary>
    /// Looks for an inverse of 3 modulo 2^32 other than <c>0xAAAAAAAB</c>.
    /// </summary>
    /// <remarks>No counterexample exists, so the inverse is unique.</remarks>
    public static Theorem<(uint inverse, int unused)> ModularInverseUniquenessCounterexample(Z3Context context)
        => from t in context.NewTheorem<(uint inverse, int unused)>()
           where 3u * t.inverse == 1u
           where t.inverse != 0xAAAAAAABu
           select t;
}
