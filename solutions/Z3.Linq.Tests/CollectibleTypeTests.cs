namespace Z3.Linq.Tests;

using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

/// <summary>
/// Solving with an environment type from a collectible assembly does not keep that assembly
/// alive.
/// </summary>
/// <remarks>
/// The library caches per-type reflection - mapping attributes, public properties and fields - for
/// the life of the process. A cache keyed strongly on <see cref="Type"/> holds every environment
/// type it has seen, so a host that loads environments from a collectible assembly (a plugin it
/// reloads, a script it compiles) could never unload one. The environment type here is emitted into
/// a <see cref="AssemblyBuilderAccess.RunAndCollect"/> assembly, solved once, and then every
/// reference the test holds is dropped; the assembly must then be collectable.
/// </remarks>
[TestClass]
public class CollectibleTypeTests
{
    [TestMethod]
    public void Solve_EnvironmentFromACollectibleAssembly_LetsTheAssemblyBeCollected()
    {
        // Arrange & Act
        WeakReference environment = SolveWithACollectibleEnvironment();

        for (int i = 0; i < 10 && environment.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // Assert
        environment.IsAlive.ShouldBeFalse();
    }

    // Not inlined, so no reference to the emitted type or its assembly survives on this frame.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference SolveWithACollectibleEnvironment()
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"CollectibleEnvironment{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.RunAndCollect);

        TypeBuilder builder = assembly.DefineDynamicModule("Module").DefineType("Environment", TypeAttributes.Public | TypeAttributes.Class);
        builder.DefineField("X", typeof(int), FieldAttributes.Public);
        builder.DefineDefaultConstructor(MethodAttributes.Public);
        Type environment = builder.CreateType();

        object solution = Solve(environment);

        // The solve really ran against the emitted type, so its members went through the caches.
        environment.GetField("X")!.GetValue(solution).ShouldBe(42);

        // The type, not the AssemblyBuilder: the builder is a wrapper that can be collected while
        // the runtime assembly behind it stays loaded.
        return new WeakReference(environment);
    }

    private static object Solve(Type environment)
    {
        using var context = new Z3Context();

        // t => t.X == 42, built by hand because the type does not exist at compile time.
        ParameterExpression t = Expression.Parameter(environment, "t");
        LambdaExpression constraint = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(environment, typeof(bool)),
            Expression.Equal(Expression.Field(t, "X"), Expression.Constant(42)),
            t);

        object theorem = typeof(Z3Context)
            .GetMethod(nameof(Z3Context.NewTheorem), Type.EmptyTypes)!
            .MakeGenericMethod(environment)
            .Invoke(context, null)!;

        object constrained = theorem.GetType().GetMethod("Where")!.Invoke(theorem, [constraint])!;

        return constrained.GetType().GetMethod("Solve")!.Invoke(constrained, [default(CancellationToken)])!;
    }
}
