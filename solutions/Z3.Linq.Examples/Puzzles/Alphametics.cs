namespace Z3.Linq.Examples.Puzzles;

/// <summary>
/// Alphametic (cryptarithm) puzzles, where each letter stands for a different decimal digit.
/// </summary>
/// <remarks>
/// <para>
/// Ported from "SAT/SMT by Example" by Dennis Yurichev, section 8.10, https://smt.st.
/// </para>
/// <para>
/// Each letter is an integer symbol in the range 0-9, all letters are distinct, and the
/// arithmetic of the puzzle is written out column by column as one linear equation. The
/// leading letter of a multi-digit word cannot be zero; without that rule SEND + MORE = MONEY
/// has more than one answer.
/// </para>
/// </remarks>
public static class Alphametics
{
    /// <summary>
    /// Builds the theorem for SEND + MORE = MONEY.
    /// </summary>
    /// <param name="context">The Z3 context that owns the theorem.</param>
    /// <returns>
    /// A theorem whose only solution is S=9, E=5, N=6, D=7, M=1, O=0, R=8, Y=2
    /// (9567 + 1085 = 10652).
    /// </returns>
    public static Theorem<(int S, int E, int N, int D, int M, int O, int R, int Y)> SendMoreMoney(Z3Context context)
    {
        return from t in context.NewTheorem<(int S, int E, int N, int D, int M, int O, int R, int Y)>()
               where Z3Methods.Distinct(t.S, t.E, t.N, t.D, t.M, t.O, t.R, t.Y)
               where t.S >= 0 && t.S <= 9 && t.E >= 0 && t.E <= 9 && t.N >= 0 && t.N <= 9 && t.D >= 0 && t.D <= 9
               where t.M >= 0 && t.M <= 9 && t.O >= 0 && t.O <= 9 && t.R >= 0 && t.R <= 9 && t.Y >= 0 && t.Y <= 9
               where t.S != 0 && t.M != 0
               where (1000 * t.S) + (100 * t.E) + (10 * t.N) + t.D
                     + (1000 * t.M) + (100 * t.O) + (10 * t.R) + t.E
                     == (10000 * t.M) + (1000 * t.O) + (100 * t.N) + (10 * t.E) + t.Y
               select t;
    }

    /// <summary>
    /// Reads a sequence of digits as a decimal number, most significant digit first.
    /// </summary>
    /// <param name="digits">The digits of the number.</param>
    /// <returns>The number the digits spell.</returns>
    public static int ToNumber(params int[] digits)
    {
        int number = 0;

        foreach (int digit in digits)
        {
            number = (number * 10) + digit;
        }

        return number;
    }
}
