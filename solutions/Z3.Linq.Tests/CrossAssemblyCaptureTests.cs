namespace Z3.Linq.Tests;

using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

/// <summary>
/// A local captured from an earlier scripting submission - a notebook cell or a REPL line - is
/// read from the submission that declared it.
/// </summary>
/// <remarks>
/// <para>
/// A scripting host such as Roslyn scripting, which .NET Interactive runs each cell through,
/// compiles every submission to its own class, <c>Submission#N</c>, in its own dynamic assembly. A
/// top-level local becomes a field on its submission. A later submission that uses it holds a
/// compiler-generated field referring to the earlier submission, so inside an expression-tree
/// lambda the local reaches the visitor as a chain of two fields that starts at a constant: the
/// later submission's instance, then the field to the earlier submission, then the local.
/// </para>
/// <para>
/// The tests emit that shape with Reflection.Emit rather than run a scripting host: two
/// collectible assemblies, one per submission, so the second field's type comes from another
/// assembly just as it does in a notebook. The local is an instance field on the first
/// submission, which the later submission's instance does not have.
/// </para>
/// <para>
/// Before #118 the visitor read every link of a captured chain from the first constant, so the
/// local was read from the later submission and threw <see cref="ArgumentException"/> - the
/// failure reported on #43. Each link is now read from the value of the one before it.
/// </para>
/// </remarks>
[TestClass]
public class CrossAssemblyCaptureTests
{
    [TestMethod]
    public void Solve_LocalCapturedAcrossDynamicAssemblies_IsReadFromTheDeclaringSubmission()
    {
        // Arrange
        using var context = new Z3Context();
        Submissions submissions = EmitSubmissions();
        submissions.Local.SetValue(submissions.Declaring, 5);

        // Act
        var result = context.NewTheorem<Bag>()
            .Where(EqualsCapturedLocal(submissions))
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.I.ShouldBe(5);
    }

    /// <summary>
    /// A local from an earlier submission is read when the theorem is solved, not when the
    /// constraint is written.
    /// </summary>
    /// <remarks>
    /// The same as a captured local in compiled code: a cell that changes the local between
    /// <c>Where</c> and <c>Solve</c> is seen.
    /// </remarks>
    [TestMethod]
    public void Solve_LocalCapturedAcrossDynamicAssemblies_IsReadWhenSolved()
    {
        // Arrange
        using var context = new Z3Context();
        Submissions submissions = EmitSubmissions();
        submissions.Local.SetValue(submissions.Declaring, 1);
        var theorem = context.NewTheorem<Bag>()
            .Where(EqualsCapturedLocal(submissions));
        submissions.Local.SetValue(submissions.Declaring, 8);

        // Act
        var result = theorem.Solve();

        // Assert
        result.ShouldNotBeNull();
        result.I.ShouldBe(8);
    }

    /// <summary>
    /// A chain whose link to the earlier submission is null is refused by naming that link.
    /// </summary>
    [TestMethod]
    public void Solve_CapturedChainWithANullSubmissionLink_ThrowsNamingTheExpression()
    {
        // Arrange
        using var context = new Z3Context();
        Submissions submissions = EmitSubmissions();
        submissions.Previous.SetValue(submissions.Using, null);
        Expression<Func<Bag, bool>> constraint = EqualsCapturedLocal(submissions);
        Expression previousLink = ((MemberExpression)((BinaryExpression)constraint.Body).Right).Expression!;
        var theorem = context.NewTheorem<Bag>()
            .Where(constraint);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => theorem.Solve()).Message.ShouldContain($"'{previousLink}' is null");
    }

    /// <summary>
    /// Builds <c>x => x.I == local</c> as a scripting host compiles it when <c>local</c> was
    /// declared in an earlier submission.
    /// </summary>
    /// <param name="submissions">The emitted submissions.</param>
    /// <returns>The constraint.</returns>
    private static Expression<Func<Bag, bool>> EqualsCapturedLocal(Submissions submissions)
    {
        ParameterExpression x = Expression.Parameter(typeof(Bag), "x");

        // Constant(using submission).previous.b
        MemberExpression local = Expression.Field(
            Expression.Field(Expression.Constant(submissions.Using), submissions.Previous),
            submissions.Local);

        return Expression.Lambda<Func<Bag, bool>>(
            Expression.Equal(Expression.Property(x, nameof(Bag.I)), local),
            x);
    }

    /// <summary>
    /// Emits two submissions, each in its own collectible assembly, and links the later one to the
    /// earlier.
    /// </summary>
    /// <returns>The two instances and the fields that link them.</returns>
    private static Submissions EmitSubmissions()
    {
        TypeBuilder declaringBuilder = DefineSubmissionType("Submission0");
        FieldInfo local = declaringBuilder.DefineField("b", typeof(int), FieldAttributes.Public);
        Type declaringType = declaringBuilder.CreateType();

        TypeBuilder usingBuilder = DefineSubmissionType("Submission1");
        usingBuilder.DefineField("previous", declaringType, FieldAttributes.Public);
        Type usingType = usingBuilder.CreateType();

        // The fields of the created types, not the builders' FieldBuilders, which cannot be read.
        local = declaringType.GetField(local.Name)!;
        FieldInfo previous = usingType.GetField("previous")!;

        object declaring = Activator.CreateInstance(declaringType)!;
        object usingSubmission = Activator.CreateInstance(usingType)!;
        previous.SetValue(usingSubmission, declaring);

        return new Submissions(declaring, usingSubmission, local, previous);
    }

    /// <summary>
    /// Defines a public class with a public default constructor in a new collectible assembly.
    /// </summary>
    /// <param name="name">The type's name.</param>
    /// <returns>The type builder, ready for fields.</returns>
    private static TypeBuilder DefineSubmissionType(string name)
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"{name}{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.RunAndCollect);

        TypeBuilder builder = assembly.DefineDynamicModule("Module").DefineType(name, TypeAttributes.Public | TypeAttributes.Class);
        builder.DefineDefaultConstructor(MethodAttributes.Public);
        return builder;
    }

    /// <summary>
    /// Two emitted submissions: <paramref name="Declaring"/> declares the local, and
    /// <paramref name="Using"/> refers to it through <paramref name="Previous"/>.
    /// </summary>
    /// <param name="Declaring">The earlier submission's instance.</param>
    /// <param name="Using">The later submission's instance, whose lambda captures the local.</param>
    /// <param name="Local">The local, an instance field on the earlier submission.</param>
    /// <param name="Previous">The later submission's field to the earlier one.</param>
    private sealed record Submissions(object Declaring, object Using, FieldInfo Local, FieldInfo Previous);

    private sealed class Bag
    {
        public int I { get; set; }
    }
}
