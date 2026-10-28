#:project ../solutions/Z3.Linq/Z3.Linq.csproj
#:project ../solutions/Z3.Linq.Examples/Z3.Linq.Examples.csproj
#:package Spectre.Console@0.57.2

// Bit-twiddling tricks proved correct for every input, or shown to be wrong, by Z3. A uint or
// ulong symbol is a bit-vector, so Z3 reasons about all 2^32 (or 2^64) values at once. Each
// theorem asks for an input where a trick and a plain reference implementation disagree: no
// answer is a proof, and an answer is a concrete counterexample. The last table finds the
// multiplicative inverse that compilers use to replace division by a constant.
//
// Adapted from "SAT/SMT by Example" (Dennis Yurichev), sections 3.7, 4.3, 4.4, 4.10, 4.11 and
// 4.12, https://smt.st
//
//   dotnet run demos/bit-twiddling.cs

using System.Diagnostics;

using Spectre.Console;

using Z3.Linq;
using Z3.Linq.Examples.BitTwiddling;

AnsiConsole.Write(new FigletText("Bit Twiddling").Color(Color.Fuchsia).Centered());
AnsiConsole.Write(new Rule("[fuchsia]tricks proved for every input[/]"));
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    "Testing a trick on sample values shows it works for those values. To prove it works for " +
    "[white]all[/] of them, state the opposite: ask Z3 for an input where the trick and a plain " +
    "reference [fuchsia]disagree[/]. If Z3 reports the theorem unsatisfiable, no such input exists " +
    "and the trick is [green]proved[/]. If Z3 finds one, it is a [red]counterexample[/] you can try in C#.")
    .Header("[white]The counterexample method[/]").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
AnsiConsole.WriteLine();

using var ctx = new Z3Context();

var proofs = new Table().Border(TableBorder.Rounded).BorderColor(Color.Fuchsia)
    .Title("[fuchsia]Proofs[/]")
    .AddColumn("[white]Trick[/]")
    .AddColumn("[white]Claim[/]")
    .AddColumn("[white]Verdict[/]")
    .AddColumn(new TableColumn("[white]Time[/]").RightAligned());

AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("fuchsia"))
    .Start("[fuchsia]Proving...[/]", _ =>
    {
        Prove(proofs, "XOR swap", "x ^= y; y ^= x; x ^= y swaps two uints",
            BitTwiddlingTheorems.XorSwapCounterexample(ctx), r => $"x=0x{r.x1:X8}, y=0x{r.y1:X8}");
        Prove(proofs, "XOR swap (64-bit)", "the same for two ulongs",
            BitTwiddlingTheorems.XorSwap64Counterexample(ctx), r => $"x=0x{r.x1:X16}, y=0x{r.y1:X16}");
        Prove(proofs, "Add/subtract swap", "x += y; y = x - y; x -= y swaps, even when x + y overflows",
            BitTwiddlingTheorems.AddSubtractSwapCounterexample(ctx), r => $"x=0x{r.x1:X8}, y=0x{r.y1:X8}");
        Prove(proofs, "Dietz's average", "(x & y) + ((x ^ y) >> 1) is the floor of (x + y) / 2",
            BitTwiddlingTheorems.DietzAverageCounterexample(ctx), r => $"x=0x{r.x:X8}, y=0x{r.y:X8}");
        Prove(proofs, "Naive average", "(x + y) / 2 is the floor of (x + y) / 2",
            BitTwiddlingTheorems.NaiveAverageCounterexample(ctx), r => $"x=0x{r.x:X8}, y=0x{r.y:X8}");
        Prove(proofs, "Dietz's average (64-bit)", "equals (x >> 1) + (y >> 1) + (x & y & 1)",
            BitTwiddlingTheorems.DietzAverage64Counterexample(ctx), r => $"x=0x{r.x:X16}, y=0x{r.y:X16}");
        Prove(proofs, "Branchless abs", "y = x >> 31 (arithmetic); (x ^ y) - y is |x|",
            BitTwiddlingTheorems.BranchlessAbsCounterexample(ctx), r => $"x=0x{r.x:X8}");
        Prove(proofs, "Branchless min/max", "y ^ ((x ^ y) & -(x < y)) is min, x ^ ... is max",
            BitTwiddlingTheorems.BranchlessMinMaxCounterexample(ctx), r => $"x=0x{r.x:X8}, y=0x{r.y:X8}");
        Prove(proofs, "Has a zero byte", "((v - 0x01010101) & ~v & 0x80808080) != 0 exactly when a byte is 0",
            BitTwiddlingTheorems.HasZeroByteCounterexample(ctx), r => $"v=0x{r.v:X8}");
        Prove(proofs, "Has a zero byte, no ~v", "the same without ~v, as the book's listing prints it",
            BitTwiddlingTheorems.MisprintedHasZeroByteCounterexample(ctx), r => $"v=0x{r.v:X8}");
        Prove(proofs, "Divide by 3 by multiplying", "(3 * x) * 0xAAAAAAAB == x for every x",
            BitTwiddlingTheorems.ModularInverseDividesCounterexample(ctx), r => $"x=0x{r.x:X8}");
        Prove(proofs, "Unique inverse of 3", "0xAAAAAAAB is the only m with 3 * m == 1",
            BitTwiddlingTheorems.ModularInverseUniquenessCounterexample(ctx), r => $"m=0x{r.inverse:X8}");
    });

AnsiConsole.Write(proofs);
AnsiConsole.MarkupLine("[grey]  The naive average overflows when x + y reaches 2^32.[/]");
AnsiConsole.MarkupLine("[grey]  Without ~v the zero-byte test reports a zero byte that is not there.[/]");
AnsiConsole.WriteLine();

var inverses = new Table().Border(TableBorder.Rounded).BorderColor(Color.Fuchsia)
    .Title("[fuchsia]Multiplicative inverse modulo 2^32[/]")
    .AddColumn(new TableColumn("[white]Divisor[/]").RightAligned())
    .AddColumn("[white]Inverse[/]")
    .AddColumn("[white]Check[/]")
    .AddColumn(new TableColumn("[white]Time[/]").RightAligned());

AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("fuchsia"))
    .Start("[fuchsia]Solving...[/]", _ =>
    {
        foreach (uint divisor in new uint[] { 3, 7, 10 })
        {
            var ((found, result), ms) = Time(() =>
            {
                bool ok = BitTwiddlingTheorems.ModularInverse(ctx, divisor).TrySolve(out var r);
                return (ok, r);
            });

            if (found)
            {
                inverses.AddRow(
                    $"{divisor}",
                    $"[green]0x{result.inverse:X8}[/]",
                    $"{divisor} * 0x{result.inverse:X8} wraps to {unchecked(divisor * result.inverse)}",
                    $"{ms:F1} ms");
            }
            else
            {
                inverses.AddRow($"{divisor}", "[red]none[/]", "even numbers have no inverse modulo 2^32", $"{ms:F1} ms");
            }
        }

        var ((bookFound, book), bookMs) = Time(() =>
        {
            bool ok = BitTwiddlingTheorems.ModularInverseBookForm(ctx).TrySolve(out var r);
            return (ok, r);
        });

        inverses.AddRow(
            "3",
            bookFound ? $"[green]0x{book.m:X8}[/]" : "[red]none[/]",
            $"book's form: 0x{BitTwiddlingTheorems.BookDividend:X8} * m == 0x{BitTwiddlingTheorems.BookDividend / 3u:X8}",
            $"{bookMs:F1} ms");
    });

AnsiConsole.Write(inverses);
AnsiConsole.MarkupLine("[grey]  Multiplying an exact multiple of a divisor by its inverse divides it:[/]");
AnsiConsole.MarkupLine("[grey]  when x is a multiple of 3, x / 3 == x * 0xAAAAAAAB.[/]");

// Solves a counterexample theorem and adds one row: green when Z3 finds none (proved), red
// with the input when it finds one.
static void Prove<T>(Table table, string trick, string claim, Theorem<T> counterexample, Func<T, string> describe)
{
    var ((found, result), ms) = Time(() =>
    {
        bool ok = counterexample.TrySolve(out var r);
        return (ok, r);
    });

    string verdict = found
        ? $"[red]counterexample: {Markup.Escape(describe(result!))}[/]"
        : "[green]proved - no counterexample[/]";

    table.AddRow(Markup.Escape(trick), Markup.Escape(claim), verdict, $"{ms:F1} ms");
}

static (T Result, double Ms) Time<T>(Func<T> solve)
{
    var sw = Stopwatch.StartNew();
    T result = solve();
    sw.Stop();
    return (result, sw.Elapsed.TotalMilliseconds);
}
