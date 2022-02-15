namespace Z3.LinqDemo;

using System;
using System.Diagnostics;
using System.Globalization;

using Z3.Linq;
using Z3.Linq.Examples;
using Z3.Linq.Examples.RiverCrossing;
using Z3.Linq.Examples.Sudoku;

public static class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("==== Missionaries & Cannibals using Solve() ====");
        Console.WriteLine();

        using (var ctx = new Z3Context())
        {
            var theorem = from t in MissionariesAndCannibals.Create(ctx, 50)
                          where t.MissionaryAndCannibalCount == 3
                          where t.SizeBoat == 2
                          select t;

            var sw = Stopwatch.StartNew();
            MissionariesAndCannibals? result = theorem.Solve();
            sw.Stop();

            Console.WriteLine(result);
            Console.WriteLine($"Time to solution: {sw.Elapsed.TotalMilliseconds} ms");
            Console.WriteLine();

            Console.WriteLine("==== Missionaries & Cannibals using Optimize() ====");
            Console.WriteLine();

            sw = Stopwatch.StartNew();
            var minimal = theorem.Optimize(Optimization.Minimize, t => t.Length);
            sw.Stop();

            Console.WriteLine(minimal);
            Console.WriteLine($"Time to optimized solution: {sw.Elapsed.TotalMilliseconds} ms");
            Console.WriteLine();

            Console.WriteLine("==== Missionaries & Cannibals using orderby clause ====");
            Console.WriteLine();

            sw = Stopwatch.StartNew();
            minimal = (from t in theorem
                       orderby t.Length
                       select t).Solve();
            sw.Stop();

            Console.WriteLine(minimal);
            Console.WriteLine($"Time to optimized solution: {sw.Elapsed.TotalMilliseconds} ms");
        }

        Console.WriteLine();
        Console.WriteLine("==== t.x ^ t.y using Anonymous Types ====");
        Console.WriteLine();

        using (var ctx = new Z3Context())
        {
            var theorem = from t in ctx.NewTheorem(new { x = default(bool), y = default(bool) })
                          where t.x ^ t.y
                          select t;

            var result = theorem.Solve();

            Console.WriteLine(result);
        }

        Console.WriteLine();
        Console.WriteLine("==== t.x ^ t.y using ValueTuples ====");
        Console.WriteLine();

        using (var ctx = new Z3Context())
        {
            var theorem = from t in ctx.NewTheorem<(bool x, bool y)>()
                          where t.x ^ t.y
                          select t;

            var result = theorem.Solve();

            Console.WriteLine(result);
            ctx.Dispose();
        }

        Console.WriteLine();
        Console.WriteLine("==== t.x ^ t.y using Custom Theorem using Record type ====");
        Console.WriteLine();

        using (var ctx = new Z3Context())
        {
            var theorem = from t in ctx.NewTheorem(new RecordTheorem<bool, bool>())
                          where t.X ^ t.Y
                          select t;

            var result = theorem.Solve();

            Console.WriteLine(result);
        }

        Console.WriteLine();
        Console.WriteLine("==== Bart's example from TechEd Europe 2012 ====");
        Console.WriteLine();

        using (var ctx = new Z3Context())
        {
            var theorem = from t in ctx.NewTheorem<Symbols<int, int, int>>()
                          where t.X1 - t.X2 >= 1
                          where t.X1 - t.X2 <= 3
                          where t.X1 == (2 * t.X3) + t.X2
                          select t;

            var result = theorem.Solve();

            Console.WriteLine(result);
        }

        Console.WriteLine();
        Console.WriteLine("==== Bart's example from TechEd Europe 2012 using ValueTuples ====");
        Console.WriteLine();

        using (var ctx = new Z3Context())
        {
            var theorem = from t in ctx.NewTheorem<(int x, int y, int z)>()
                          where t.x - t.y >= 1
                          where t.x - t.y <= 3
                          where t.x == (2 * t.z) + t.y
                          select t;

            var result = theorem.Solve();

            Console.WriteLine(result);
        }

        Console.WriteLine();
        Console.WriteLine("====  Example using Symbols<T1, T2> ====");
        Console.WriteLine();

        using (var ctx = new Z3Context())
        {
            var theorem = from t in ctx.NewTheorem<Symbols<int, int>>()
                          where t.X1 < t.X2 + 1
                          where t.X1 > 2
                          where t.X1 != t.X2
                          select t;

            var result = theorem.Solve();

            Console.WriteLine(result);
        }

        Console.WriteLine();
        Console.WriteLine("====  SudokuTheorem Example ====");
        Console.WriteLine();

        using (var ctx = new Z3Context())
        {
            var theorem = from t in SudokuTheorem.Create(ctx)
                          where t.Cell13 == 2 && t.Cell16 == 1 && t.Cell18 == 6
                          where t.Cell23 == 7 && t.Cell26 == 4
                          where t.Cell31 == 5 && t.Cell37 == 9
                          where t.Cell42 == 1 && t.Cell44 == 3
                          where t.Cell51 == 8 && t.Cell55 == 5 && t.Cell59 == 4
                          where t.Cell66 == 6 && t.Cell68 == 2
                          where t.Cell73 == 6 && t.Cell79 == 7
                          where t.Cell84 == 8 && t.Cell87 == 3
                          where t.Cell92 == 4 && t.Cell94 == 9 && t.Cell97 == 2
                          select t;

            var result = theorem.Solve();

            Console.WriteLine(result);
        }

        Console.WriteLine("====  SudokuTheorem Example from https://sandiway.arizona.edu/sudoku/examples.html ====");
        Console.WriteLine();

        using (var ctx = new Z3Context())
        {
            var theorem = from t in SudokuTheorem.Create(ctx)
                          where t.Cell14 == 2 && t.Cell15 == 6 && t.Cell17 == 7 && t.Cell19 == 1
                          where t.Cell21 == 6 && t.Cell22 == 8 && t.Cell25 == 7 && t.Cell28 == 9
                          where t.Cell31 == 1 && t.Cell32 == 9 && t.Cell36 == 4 && t.Cell37 == 5
                          where t.Cell41 == 8 && t.Cell42 == 2 && t.Cell44 == 1 && t.Cell48 == 4
                          where t.Cell53 == 4 && t.Cell54 == 6 && t.Cell56 == 2 && t.Cell57 == 9
                          where t.Cell62 == 5 && t.Cell66 == 3 && t.Cell68 == 2 && t.Cell69 == 8
                          where t.Cell73 == 9 && t.Cell74 == 3 && t.Cell78 == 7 && t.Cell79 == 4
                          where t.Cell82 == 4 && t.Cell85 == 5 && t.Cell88 == 3 && t.Cell89 == 6
                          where t.Cell91 == 7 && t.Cell93 == 3 && t.Cell95 == 1 && t.Cell96 == 8
                          select t;

            var result = theorem.Solve();

            Console.WriteLine(result);
        }

        Console.WriteLine("====  Oil Purchase Problem. ====");
        Console.WriteLine();

        using (var ctx = new Z3Context())
        {
            var solveable = from t in ctx.NewTheorem<(double vz, double sa)>()
                            where 0.3 * t.sa + 0.4 * t.vz >= 1900
                            where 0.4 * t.sa + 0.2 * t.vz >= 1500
                            where 0.2 * t.sa + 0.3 * t.vz >= 500
                            where 0 <= t.sa && t.sa <= 9000
                            where 0 <= t.vz && t.vz <= 6000
                            orderby 20.0 * t.sa + 15.0 * t.vz
                            select t;

            var result = solveable.Solve();

            Console.WriteLine(string.Create(CultureInfo.CreateSpecificCulture("en-US"), $"Saudia Arabia: {result.sa} barrels ({(result.sa * 20):C}), Venezuela: {result.vz} barrels ({(result.vz * 15):C})"));
        }

        Console.WriteLine();
        Console.WriteLine("====  Warehouse Logistics Problem ====");
        Console.WriteLine();

        using (var ctx = new Z3Context())
        {
            var theorem =
                from t in ctx.NewTheorem<(double w1c1, double w1c2, double w1c3, double w1c4, double w2c1, double w2c2, double w2c3, double w2c4)>()
                where t.w1c1 + t.w1c2 + t.w1c3 + t.w1c4 <= 60_000 // Warehouse 1 Product Availability
                where t.w2c1 + t.w2c2 + t.w2c3 + t.w2c4 <= 80_000 // Warehouse 2 Product Availability
                where t.w1c1 + t.w2c1 == 35_000 && (t.w1c1 >= 0 && t.w2c1 >= 0) // Customer 1 Orders
                where t.w1c2 + t.w2c2 == 22_000 && (t.w1c2 >= 0 && t.w2c2 >= 0) // Customer 2 Orders
                where t.w1c3 + t.w2c3 == 18_000 && (t.w1c3 >= 0 && t.w2c3 >= 0) // Customer 3 Orders
                where t.w1c4 + t.w2c4 == 30_000 && (t.w1c4 >= 0 && t.w2c4 >= 0) // Customer 4 Orders
                orderby (1.00 * t.w1c1) + (3.00 * t.w1c2) + (0.50 * t.w1c3) + (4.00 * t.w1c4) +
                        (2.50 * t.w2c1) + (5.00 * t.w2c2) + (1.50 * t.w2c3) + (2.50 * t.w2c4) // Optimize for Total Shipping Cost
                select t;

            var result = theorem.Solve();

            Console.WriteLine($"|                     | Customer 1 | Customer 2  | Customer 3 | Customer 4 |");
            Console.WriteLine($"|---------------------|------------|-------------|------------|------------|");
            Console.WriteLine($"| Warehouse 1         | {result.w1c1}      | {result.w1c2}       |  {result.w1c3}      | {result.w1c4}          |");
            Console.WriteLine($"| Warehouse 2         | {result.w2c1}          | {result.w2c2}           | {result.w2c3}      | {result.w2c4}      |");
            Console.WriteLine();
            Console.WriteLine(string.Create(CultureInfo.CreateSpecificCulture("en-US"), $"Total Cost: {1 * result.w1c1 + 3 * result.w1c2 + 0.5 * result.w1c3 + 4 * result.w1c4 + 2.5 * result.w2c1 + 5 * result.w2c2 + 1.5 * result.w2c3 + 2.5 * result.w2c4:C}"));
        }

        // AllSamplesWithLogging();

        Console.ReadKey();
    }

    private static void AllSamplesWithLogging()
    {
        using (var ctx = new Z3Context())
        {
            ctx.Log = Console.Out; // see internal logging

            Solve(from t in ctx.NewTheorem(new { x = default(bool) })
                  where t.x && !t.x
                  select t);

            Solve(from t in ctx.NewTheorem(new { x = default(bool), y = default(bool) })
                  where t.x ^ t.y
                  select t);

            Solve(from t in ctx.NewTheorem(new { x = default(int), y = default(int) })
                  where t.x < t.y + 1
                  where t.x > 2
                  select t);

            Solve(from t in ctx.NewTheorem<Symbols<int, int>>()
                  where t.X1 < t.X2 + 1
                  where t.X1 > 2
                  where t.X1 != t.X2
                  select t);

            Solve(from t in ctx.NewTheorem<Symbols<int, int, int, int, int>>()
                  where t.X1 - t.X2 >= 1
                  where t.X1 - t.X2 <= 3
                  where t.X1 == (2 * t.X3) + t.X5
                  where t.X3 == t.X5
                  where t.X2 == 6 * t.X4
                  select t);

            Solve(from t in ctx.NewTheorem<Symbols<int, int>>()
                  where Z3Methods.Distinct(t.X1, t.X2)
                  select t);

            Solve(from t in SudokuTheorem.Create(ctx)
                  where t.Cell13 == 2 && t.Cell16 == 1 && t.Cell18 == 6
                  where t.Cell23 == 7 && t.Cell26 == 4
                  where t.Cell31 == 5 && t.Cell37 == 9
                  where t.Cell42 == 1 && t.Cell44 == 3
                  where t.Cell51 == 8 && t.Cell55 == 5 && t.Cell59 == 4
                  where t.Cell66 == 6 && t.Cell68 == 2
                  where t.Cell73 == 6 && t.Cell79 == 7
                  where t.Cell84 == 8 && t.Cell87 == 3
                  where t.Cell92 == 4 && t.Cell94 == 9 && t.Cell97 == 2
                  select t);
        }
    }

    private static void Solve<T>(Theorem<T> t) where T : class
    {
        Console.WriteLine(t);
        var res = t.Solve();
        Console.WriteLine(res == null ? "none" : res.ToString());
        Console.WriteLine();
    }
}