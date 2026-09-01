namespace Z3.Linq;
 
using Microsoft.Z3;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>
/// Representation of a theorem with its constraints.
/// </summary>
public class Theorem
{
    /// <summary>
    /// Theorem constraints.
    /// </summary>
    private readonly IEnumerable<LambdaExpression> constraints;

    /// <summary>
    /// The instance passed to <c>NewTheorem</c>, if any, whose collections give theirs a length.
    /// </summary>
    private readonly object? template;

    /// <summary>
    /// Z3 context under which the theorem is solved.
    /// </summary>
    private readonly Z3Context context;

    /// <summary>
    /// Creates a new theorem for the given Z3 context.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    protected Theorem(Z3Context context)
        : this(context, new List<LambdaExpression>(), null)
    {
    }

    /// <summary>
    /// Creates a new pre-constrained theorem for the given Z3 context.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="constraints">Constraints to apply to the created theorem.</param>
    protected Theorem(Z3Context context, IEnumerable<LambdaExpression> constraints)
        : this(context, constraints, null)
    {
    }

    /// <summary>
    /// Creates a new pre-constrained theorem for the given Z3 context, with a template instance.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="constraints">Constraints to apply to the created theorem.</param>
    /// <param name="template">
    /// An instance of the environment type whose collections supply a length to the solution's,
    /// or <see langword="null"/>. See #78.
    /// </param>
    protected Theorem(Z3Context context, IEnumerable<LambdaExpression> constraints, object? template)
    {
        this.context = context;
        this.constraints = constraints;
        this.template = template;
    }

    /// <summary>
    /// Gets the constraints of the theorem.
    /// </summary>
    protected IEnumerable<LambdaExpression> Constraints => constraints;

    /// <summary>
    /// Gets the template instance the theorem was created from, if any.
    /// </summary>
    protected object? Template => template;

    /// <summary>
    /// Gets the Z3 context under which the theorem is solved.
    /// </summary>
    protected Z3Context Context => context;

    /// <summary>
    /// Returns a comma-separated representation of the constraints embodied in the theorem.
    /// </summary>
    /// <returns>Comma-separated string representation of the theorem's constraints.</returns>
    public override string ToString()
    {
        return string.Join(", ", (from c in constraints select c.Body.ToString()).ToArray());
    }

    /// <summary>
    /// Solves the theorem using Z3.
    /// </summary>
    /// <typeparam name="T">Theorem environment type.</typeparam>
    /// <returns>Result of solving the theorem; <c>default(T)</c> if the theorem cannot be satisfied.</returns>
    /// <param name="cancellationToken">A token that interrupts the solve.</param>
    /// <remarks>
    /// For a value-type environment <c>default(T)</c> is a real, populated instance - all zeroes -
    /// and so cannot be told apart from a solution in which every symbol happens to be zero. Use
    /// <see cref="TrySolve{T}(out T, CancellationToken)"/> where that matters. See #57.
    /// </remarks>
    protected T? Solve<T>(CancellationToken cancellationToken)
    {
        return this.TrySolve<T>(out T? result, cancellationToken) ? result : default;
    }

    /// <summary>
    /// Solves the theorem using Z3, reporting satisfiability separately from the solution.
    /// </summary>
    /// <typeparam name="T">Theorem environment type.</typeparam>
    /// <param name="result">The solution, when the theorem could be satisfied.</param>
    /// <param name="cancellationToken">A token that interrupts the solve.</param>
    /// <returns><see langword="true"/> if the theorem was satisfiable; otherwise <see langword="false"/>.</returns>
    /// <exception cref="TheoremUndecidedException">Z3 stopped without deciding - a limit on the <see cref="Z3Context"/> was reached, or it gave up.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    protected bool TrySolve<T>([MaybeNullWhen(false)] out T result, CancellationToken cancellationToken)
    {
        using Context ctx = this.context.CreateContext();
        var environment = GetEnvironment(ctx, typeof(T));

        // Solver solver = context.MkSimpleSolver();
        Solver solver = ctx.MkSolver();

        AssertConstraints<T>(ctx, solver, environment);

        Status status = this.Check(ctx, solver, cancellationToken);

        if (status != Status.SATISFIABLE)
        {
            result = default;
            return false;
        }

        result = GetSolution<T>(ctx, solver.Model, environment, this.template);
        return true;
    }

    /// <summary>
    /// Finds an optimal solution using Z3.
    /// </summary>
    /// <typeparam name="T">Theorem environment type.</typeparam>
    /// <typeparam name="TResult">The Theorem Result.</typeparam>
    /// <param name="direction">The optimization goal, i.e. whether to minimize or maximize the solution.</param>
    /// <param name="lambda">Expression representing the value to minimize or maximize.</param>
    /// <returns>Result of solving the theorem; <c>default(T)</c> if the theorem cannot be satisfied.</returns>
    /// <param name="cancellationToken">A token that interrupts the optimisation.</param>
    /// <remarks>
    /// Carries the same ambiguity as <see cref="Solve{T}(CancellationToken)"/> for a value-type
    /// environment. Use
    /// <see cref="TryOptimize{T, TResult}(Optimization, Expression{Func{T, TResult}}, out T, CancellationToken)"/>
    /// where that matters. See #57.
    /// </remarks>
    protected T? Optimize<T, TResult>(Optimization direction, Expression<Func<T, TResult>> lambda, CancellationToken cancellationToken)
    {
        return this.TryOptimize<T, TResult>(direction, lambda, out T? result, cancellationToken) ? result : default;
    }

    /// <summary>
    /// Finds an optimal solution using Z3, reporting satisfiability separately from the solution.
    /// </summary>
    /// <typeparam name="T">Theorem environment type.</typeparam>
    /// <typeparam name="TResult">The Theorem Result.</typeparam>
    /// <param name="direction">The optimization goal, i.e. whether to minimize or maximize the solution.</param>
    /// <param name="lambda">Expression representing the value to minimize or maximize.</param>
    /// <param name="result">The optimal solution, when the theorem could be satisfied.</param>
    /// <param name="cancellationToken">A token that interrupts the optimisation.</param>
    /// <returns><see langword="true"/> if the theorem was satisfiable; otherwise <see langword="false"/>.</returns>
    /// <exception cref="TheoremUndecidedException">Z3 stopped without deciding - a limit on the <see cref="Z3Context"/> was reached, or it gave up.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    protected bool TryOptimize<T, TResult>(
        Optimization direction,
        Expression<Func<T, TResult>> lambda,
        [MaybeNullWhen(false)] out T result,
        CancellationToken cancellationToken)
    {
        using Context ctx = this.context.CreateContext();
        var environment = GetEnvironment(ctx, typeof(T));

        Optimize optimizer = ctx.MkOptimize();

        AssertConstraints<T>(ctx, optimizer, environment);

        var expression = ExpressionVisitor.Translate(ctx, environment, lambda.Body, lambda.Parameters[0]);

        switch (direction)
        {
            case Optimization.Maximize:
                optimizer.MkMaximize(expression);
                break;
            case Optimization.Minimize:
                optimizer.MkMinimize(expression);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }

        Status status = this.Check(ctx, optimizer, cancellationToken);

        if (status != Status.SATISFIABLE)
        {
            result = default;
            return false;
        }

        result = GetSolution<T>(ctx, optimizer.Model, environment, this.template);
        return true;
    }

    /// <summary>
    /// Runs the check on a solver or optimizer under the limits set on the <see cref="Z3Context"/>
    /// and the caller's token, and turns an undecided outcome into an exception.
    /// </summary>
    /// <param name="ctx">The native context the check runs in.</param>
    /// <param name="approach">The <see cref="Solver"/> or <see cref="Optimize"/> to check.</param>
    /// <param name="cancellationToken">A token that interrupts the check.</param>
    /// <returns><see cref="Status.SATISFIABLE"/> or <see cref="Status.UNSATISFIABLE"/> - never <see cref="Status.UNKNOWN"/>.</returns>
    /// <remarks>
    /// <para>
    /// A cancelled token interrupts Z3 through <see cref="Context.Interrupt"/>. An interrupt that
    /// arrives before the check has started is lost - measured, not assumed - so the token is
    /// also inspected before the check, which leaves only the moment between that inspection and
    /// Z3 starting work.
    /// </para>
    /// <para>
    /// Z3 says why it stopped as a string, and the strings are not consistent: the solver says
    /// <c>timeout</c> or <c>interrupted</c>, the optimizer says <c>canceled</c> for either, and an
    /// exhausted resource limit is <c>canceled</c> on both. So cancellation is recognised from the
    /// token rather than the string, and every other <see cref="Status.UNKNOWN"/> is a
    /// <see cref="TheoremUndecidedException"/> carrying the string. Before #85 an
    /// <see cref="Status.UNKNOWN"/> was reported as unsatisfiable, which was defensible only
    /// because nothing could cause one.
    /// </para>
    /// </remarks>
    private Status Check(Context ctx, Z3Object approach, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Params? limits = this.context.CreateLimits(ctx);

        switch (approach)
        {
            case Solver solver when limits is not null:
                solver.Parameters = limits;
                break;
            case Optimize optimizer when limits is not null:
                optimizer.Parameters = limits;
                break;
        }

        Status status;

        using (cancellationToken.Register(ctx.Interrupt))
        {
            cancellationToken.ThrowIfCancellationRequested();

            status = approach switch
            {
                Solver solver => solver.Check(),
                Optimize optimizer => optimizer.Check(),
                _ => throw new ArgumentException("Expected a Solver or an Optimize.", nameof(approach)),
            };
        }

        if (status == Status.UNKNOWN)
        {
            cancellationToken.ThrowIfCancellationRequested();

            throw new TheoremUndecidedException(approach switch
            {
                Solver solver => solver.ReasonUnknown,
                Optimize optimizer => optimizer.ReasonUnknown,
                _ => "unknown",
            });
        }

        return status;
    }

    /// <summary>
    /// Asserts the theorem constraints on the Z3 context.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="approach"></param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <typeparam name="T">Theorem environment type.</typeparam>
    private void AssertConstraints<T>(Context context, Z3Object approach, Environment environment)
    {
        var constraintsToAssert = this.constraints;

        // Global rewriter registered?
        var rewriterAttr = typeof(T).GetCustomAttributes<TheoremGlobalRewriterAttribute>(false).SingleOrDefault();

        if (rewriterAttr != null)
        {
            // Make sure the specified rewriter type implements the ITheoremGlobalRewriter.
            var rewriterType = rewriterAttr.RewriterType;

            if (!typeof(ITheoremGlobalRewriter).IsAssignableFrom(rewriterType))
            {
                throw new InvalidOperationException("Invalid global rewriter type definition. Did you implement ITheoremGlobalRewriter?");
            }

            // Assume a parameterless public constructor to new up the rewriter.
            var rewriter = (ITheoremGlobalRewriter)Activator.CreateInstance(rewriterType)!;

            // Do the rewrite.
            constraintsToAssert = rewriter.Rewrite(constraintsToAssert);
        }

        // Visit, assert and log.
        foreach (var constraint in constraintsToAssert)
        {
            BoolExpr expression = (BoolExpr)ExpressionVisitor.Translate(context, environment, constraint.Body, constraint.Parameters[0]);

            switch (approach)
            {
                case Solver solver:
                    solver.Assert(expression);
                    break;
                case Optimize optimize:
                    optimize.Assert(expression);
                    break;
            }

            this.context.LogWriteLine(expression.ToString());
        }

        AssertBounds(context, approach, environment);
    }

    /// <summary>
    /// Asserts, for every scalar symbol whose CLR type is a bounded integer, that the symbol lies
    /// within the range of that type.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="approach">The <see cref="Solver"/> or <see cref="Optimize"/> to assert into.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <remarks>
    /// <para>
    /// A <c>short</c>, <c>int</c>, <c>long</c> or <see cref="DateTime"/> symbol is an unbounded
    /// Z3 integer, so without this a constraint no value of the type can satisfy - a
    /// <c>short</c> equal to 40000, a <see cref="DateTime"/> after <see cref="DateTime.MaxValue"/>
    /// - still had a model, and the read back then failed. With it the theorem is unsatisfiable,
    /// which is the true answer, and an optimisation with no other bound on the symbol returns
    /// the extreme of the type rather than whatever Z3 happened to pick. See #87.
    /// </para>
    /// <para>
    /// Only scalars are bounded. A collection is an array from <c>Int</c> to the element sort,
    /// its length is not known here - it comes from the instance when the solution is read - and
    /// bounding every element would take a quantifier, which can cost Z3 its completeness. So an
    /// element is read with a checked conversion instead, and a value outside its type is loud
    /// rather than wrong.
    /// </para>
    /// <para>
    /// The bounds are not logged: the log shows the constraints the caller wrote.
    /// </para>
    /// </remarks>
    private static void AssertBounds(Context context, Z3Object approach, Environment environment)
    {
        foreach ((MemberInfo member, Environment child) in environment.Properties)
        {
            if (child.Expr is IntExpr symbol && GetBounds(Type.GetTypeCode(SymbolType(member))) is (long low, long high))
            {
                BoolExpr bounds = context.MkAnd(
                    context.MkGe(symbol, context.MkInt(low)),
                    context.MkLe(symbol, context.MkInt(high)));

                switch (approach)
                {
                    case Solver solver:
                        solver.Assert(bounds);
                        break;
                    case Optimize optimize:
                        optimize.Assert(bounds);
                        break;
                }
            }

            AssertBounds(context, approach, child);
        }
    }

    /// <summary>
    /// The range of values a CLR type can hold, for the types that travel through Z3 as an
    /// integer, or <see langword="null"/> for a type with no such range.
    /// </summary>
    /// <param name="typeCode">The type code of the CLR type.</param>
    /// <returns>The inclusive range, or <see langword="null"/>.</returns>
    /// <remarks>
    /// A <see cref="DateTime"/> is its ticks (#83), so its range is
    /// <see cref="DateTime.MinValue"/> to <see cref="DateTime.MaxValue"/> in ticks.
    /// </remarks>
    private static (long Low, long High)? GetBounds(TypeCode typeCode)
    {
        return typeCode switch
        {
            TypeCode.Int16 => (short.MinValue, short.MaxValue),
            TypeCode.Int32 => (int.MinValue, int.MaxValue),
            TypeCode.Int64 => (long.MinValue, long.MaxValue),
            TypeCode.DateTime => (DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks),
            _ => null,
        };
    }

    /// <summary>
    /// The CLR type a member is solved as: its declared type, or the type a
    /// <see cref="TheoremVariableTypeMappingAttribute"/> on that type maps it to.
    /// </summary>
    /// <param name="member">The property or field.</param>
    /// <returns>The type the symbol is declared and read as.</returns>
    private static Type SymbolType(MemberInfo member)
    {
        Type type = member switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new NotSupportedException(),
        };

        TheoremVariableTypeMappingAttribute? mapping = type.GetCustomAttributes<TheoremVariableTypeMappingAttribute>(false).SingleOrDefault();

        return mapping?.RegularType ?? type;
    }

    /// <summary>
    /// Gets the Z3 sort a symbol of the given CLR type is declared with, or <see langword="null"/>
    /// if the type is not one the library maps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the only mapping from CLR type to sort. A scalar symbol is a constant of this sort,
    /// and a collection symbol is a Z3 array from <c>Int</c> to it, so the two cannot disagree -
    /// there is nothing else to consult. Collections used to carry a mapping of their own, and
    /// only its <c>int</c> row agreed with this one; every other element type declared a domain or
    /// range that contradicted how its elements were constrained and read back. See #64.
    /// </para>
    /// <para>
    /// <see cref="ExpressionVisitor"/> asks the same question when a constraint converts between
    /// CLR types: whether the conversion is a no-op, an integer-to-real, or a real-to-integer
    /// depends only on the sorts the two types map to here. See #76.
    /// </para>
    /// </remarks>
    internal static Sort? TryGetSymbolSort(Context context, TypeCode typeCode)
    {
        return typeCode switch
        {
            TypeCode.String => context.StringSort,
            TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.DateTime => context.IntSort,
            TypeCode.Boolean => context.BoolSort,
            TypeCode.Single or TypeCode.Decimal or TypeCode.Double => context.RealSort,
            TypeCode.UInt32 or TypeCode.UInt64 => context.MkBitVecSort(BitVectorWidth(typeCode)!.Value),
            _ => null,
        };
    }

    /// <summary>
    /// The width, in bits, of the Z3 bit-vector a fixed-width unsigned CLR type maps to, or
    /// <see langword="null"/> for a type that is not a bit-vector.
    /// </summary>
    /// <param name="typeCode">The type code of the CLR type.</param>
    /// <returns>The bit width, or <see langword="null"/>.</returns>
    /// <remarks>
    /// <c>uint</c> and <c>ulong</c> travel through Z3 as bit-vectors of their width - so they
    /// carry wrapping arithmetic, bitwise operators and shifts, which a mathematical integer has
    /// no counterpart for. <c>short</c>, <c>int</c> and <c>long</c> stay unbounded integers.
    /// <c>byte</c> and <c>ushort</c> are deliberately not mapped: C# promotes them to <c>int</c>
    /// in every expression, so a <c>byte</c> symbol could never keep its bit-vector sort through
    /// a constraint, and mapping it would only produce a confusing conversion error rather than a
    /// usable symbol.
    /// </remarks>
    internal static uint? BitVectorWidth(TypeCode typeCode)
    {
        return typeCode switch
        {
            TypeCode.UInt32 => 32,
            TypeCode.UInt64 => 64,
            _ => null,
        };
    }

    private Environment GetEnvironment(Context context, Type targetType)
    {
        return GetEnvironment(context, targetType, targetType.Name);
    }

    private Environment GetEnvironment(Context context, Type targetType, string prefix)
    {
        var toReturn = new Environment();

        if (IsCollection(targetType))
        {
            Type? elType;

            if (targetType.IsArray)
            {
                elType = targetType.GetElementType();
            }
            else
            {
                elType = targetType.GetGenericArguments()[0];
            }

            TypeCode elTypeCode = Type.GetTypeCode(elType);

            if (elTypeCode == TypeCode.Object)
            {
                toReturn.IsArray = true;

                foreach (PropertyInfo parameter in elType!.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var newPrefix = parameter.Name;

                    if (!string.IsNullOrEmpty(prefix))
                    {
                        newPrefix = $"{prefix}_{newPrefix}";
                    }

                    toReturn.Properties[parameter] = GetEnvironment(context, parameter, newPrefix, true);
                }

                return toReturn;
            }

            // Elements are always read back with an integer index - ConvertZ3Expression selects
            // with MkInt(i) - so the domain is Int whatever the element type. The range is the
            // sort a scalar of that type would get, from the one mapping both share. See #64.
            Sort elementSort = TryGetSymbolSort(context, elTypeCode)
                ?? throw new NotSupportedException($"Unsupported member type {targetType.FullName}");

            toReturn.Expr = context.MkArrayConst(prefix, context.IntSort, elementSort);
        }
        else
        {
            foreach (var parameter in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var newPrefix = parameter.Name;
                if (!string.IsNullOrEmpty(prefix))
                {
                    newPrefix = $"{prefix}_{newPrefix}";
                }

                toReturn.Properties[parameter] = GetEnvironment(context, parameter, newPrefix, false);
            }

            foreach (var parameter in targetType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var newPrefix = parameter.Name;
                if (!string.IsNullOrEmpty(prefix))
                {
                    newPrefix = $"{prefix}_{newPrefix}";
                }

                toReturn.Properties[parameter] = GetEnvironment(context, parameter, newPrefix, false);
            }
        }

        return toReturn;
    }

    private Environment GetEnvironment(Context context, MemberInfo parameter, string prefix, bool isArray)
    {
        var toReturn = new Environment();

        var parameterType = parameter switch
        {
            PropertyInfo parameterProperty => parameterProperty.PropertyType,
            FieldInfo parameterField => parameterField.FieldType,
            _ => throw new NotSupportedException(),
        };

        TheoremVariableTypeMappingAttribute? parameterTypeMapping = parameterType.GetCustomAttributes<TheoremVariableTypeMappingAttribute>(false).SingleOrDefault();

        if (parameterTypeMapping != null)
        { 
            parameterType = parameterTypeMapping.RegularType; 
        }

        // Map the environment onto Z3-compatible types.
        Expr constrExp;
        if (!isArray)
        {
            // To deal correctly with nested properties, we can't just use the property name.
            // That breaks with ValueTuples with arity of 8 or higher because those have
            // both x.Item1 and x.Rest.Item1, and if we call both of those "Item1" they become
            // indistinguishable. Using the prefix means those become ValueTuple`8_Item1 and
            // ValueTuple`8_Rest_Item1.
            string name = prefix;
            TypeCode typeCode = Type.GetTypeCode(parameterType);

            if (typeCode == TypeCode.Object)
            {
                return GetEnvironment(context, parameterType, prefix);
            }

            Sort sort = TryGetSymbolSort(context, typeCode)
                ?? throw new NotSupportedException("Unsupported parameter type for " + name + ".");

            constrExp = context.MkConst(name, sort);
        }
        else
        {
            // One Z3 array per property of the element type, indexed by position. Domain and
            // range are chosen exactly as for a collection of the property type. See #64.
            Sort elementSort = TryGetSymbolSort(context, Type.GetTypeCode(parameterType))
                ?? throw new NotSupportedException($"Only one level of object collections is currently supported, 2 levels detected with prefix {prefix}");

            constrExp = context.MkArrayConst(prefix, context.IntSort, elementSort);
        }

        toReturn.Expr = constrExp;

        return toReturn;
    }

    private static object ConvertZ3Expression(object destinationObject, Context context, Model model, Environment subEnv, MemberInfo parameter, object? templateValue)
    {
        // Normalize types when facing Z3. Theorem variable type mappings allow for strong
        // typing within the theorem, while underlying variable representations are Z3-
        // friendly types.
        var parameterType = parameter switch
        {
            PropertyInfo parameterProperty => parameterProperty.PropertyType,
            FieldInfo parameterField => parameterField.FieldType,
            _ => throw new NotSupportedException(),
        };

        TheoremVariableTypeMappingAttribute? parameterTypeMapping = parameterType.GetCustomAttributes<TheoremVariableTypeMappingAttribute>(false).SingleOrDefault();

        if (parameterTypeMapping != null)
        {
            parameterType = parameterTypeMapping.RegularType;
        }

        object value;
        TypeCode typeCode = Type.GetTypeCode(parameterType);
        if (typeCode == TypeCode.Object)
        {
            if (IsCollection(parameterType))
            {
                Type eltType = parameterType.IsArray ? parameterType.GetElementType()! : parameterType.GetGenericArguments()[0];

                if (eltType == null)
                {
                    throw new NotSupportedException("Unsupported untyped array parameter type for " + parameter.Name + ".");
                }

                var arrVal = (ArrayExpr)(subEnv.Expr ?? throw new ArgumentException(
                    $"nameof(ConvertZ3Expression) requires {nameof(subEnv)}.{nameof(subEnv.Expr)} to be non-null",
                    nameof(subEnv)));

                var results = new ArrayList();

                // A solution never changes the length of a collection, so the length has to come
                // from somewhere: the collection on the template passed to NewTheorem, or failing
                // that the one already on the instance - its initialiser. A value tuple has nowhere
                // to put an initialiser and an anonymous instance is created uninitialised, so for
                // those the template is the only source. With neither, say so by name rather than
                // fail on the null. See #53 and #78.
                if ((templateValue ?? GetMemberValue(parameter, destinationObject)) is not ICollection existing)
                {
                    throw new NotSupportedException(
                        $"Collection symbol {parameter.Name} has no length. A collection must be pre-sized: initialise it on the environment type, or pass an instance with it initialised to NewTheorem.");
                }

                int existingLength = existing.Count;

                for (int i = 0; i < existingLength; i++)
                {
                    var numValExpr = EvaluateWithCompletion(model, context.MkSelect(arrVal, context.MkInt(i)));

                    object numVal;

                    switch (Type.GetTypeCode(eltType))
                    {
                        case TypeCode.String:
                            numVal = numValExpr.String;
                            break;
                        case TypeCode.Int16:
                            // Checked: an element is not bounded to the range of its type - #87
                            // bounds scalars only - so Z3 can pick a value no short can hold, and
                            // an unchecked cast would wrap it into a plausible wrong answer. See #63.
                            numVal = checked((short)((IntNum)numValExpr).Int);
                            break;
                        case TypeCode.Int32:
                            numVal = ((IntNum)numValExpr).Int;
                            break;
                        case TypeCode.Int64:
                            numVal = ((IntNum)numValExpr).Int64;
                            break;
                        case TypeCode.UInt32:
                            numVal = (uint)((BitVecNum)numValExpr).UInt64;
                            break;
                        case TypeCode.UInt64:
                            numVal = ((BitVecNum)numValExpr).UInt64;
                            break;
                        case TypeCode.DateTime:
                            // Ticks on the UTC timeline, for the reason given on the scalar arm below.
                            numVal = ToDateTime(((IntNum)numValExpr).Int64, parameter.Name);
                            break;
                        case TypeCode.Boolean:
                            numVal = numValExpr.IsTrue;
                            break;
                        case TypeCode.Single:
                            numVal = float.Parse(((RatNum)numValExpr).ToDecimalString(32), CultureInfo.InvariantCulture);
                            break;
                        case TypeCode.Decimal:
                            // Read the element the loop selected, not the array constant it was
                            // selected from - every other arm here uses numValExpr. See #55.
                            string numValue = ((RatNum)numValExpr).ToDecimalString(128);

                            ReadOnlySpan<char> numValueSpan = numValue.AsSpan();
                            if (numValue.EndsWith('?'))
                            {
                                numValueSpan = numValueSpan[..^1];
                            }

                            numVal = decimal.Parse(numValueSpan, NumberStyles.Number, CultureInfo.InvariantCulture);
                            break;
                        case TypeCode.Double:
                            numVal = double.Parse(((RatNum)numValExpr).ToDecimalString(64), CultureInfo.InvariantCulture);
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported array parameter type for {parameter.Name} and array element type {eltType.Name}.");
                    }

                    results.Add(numVal);
                }

                value = parameterType.IsArray ? results.ToArray(eltType) : Activator.CreateInstance(parameterType, results.ToArray(eltType))!;
            }
            else
            {
                value = GetSolution(parameterType, context, model, subEnv, templateValue);
            }
        }
        else
        {
            Expr subEnvExpr = subEnv.Expr ?? throw new ArgumentException(
                $"nameof(ConvertZ3Expression) requires {nameof(subEnv)}.{nameof(subEnv.Expr)} to be non-null",
                nameof(subEnv));

            Expr val = EvaluateWithCompletion(model, subEnvExpr);

            switch (typeCode)
            {
                case TypeCode.String:
                    value = val.String;
                    break;
                case TypeCode.Int16:
                    // Int16 cannot share the Int32 arm: the model value is an int, and reflection
                    // refuses to write an int to a short member. The cast is checked as a defence:
                    // since #87 a scalar short is bounded to its range when the constraints are
                    // asserted, so the check cannot fire here, but it costs nothing and the element
                    // arm above still depends on it. See #63.
                    value = checked((short)((IntNum)val).Int);
                    break;
                case TypeCode.Int32:
                    value = ((IntNum)val).Int;
                    break;
                case TypeCode.Int64:
                    value = ((IntNum)val).Int64;
                    break;
                case TypeCode.UInt32:
                    value = (uint)((BitVecNum)val).UInt64;
                    break;
                case TypeCode.UInt64:
                    value = ((BitVecNum)val).UInt64;
                    break;
                case TypeCode.DateTime:
                    // The write path encodes the instant as its ticks on the UTC timeline
                    // (ExpressionVisitor.ToUtcTicks), so the value is read back as UTC from the
                    // same ticks. It used to be a Windows file time, read with FromFileTimeUtc, which
                    // could express nothing before 1601. See #56 and #83.
                    value = ToDateTime(((IntNum)val).Int64, parameter.Name);
                    break;
                case TypeCode.Boolean:
                    value = val.IsTrue;
                    break;
                case TypeCode.Single:
                    value = float.Parse(((RatNum)val).ToDecimalString(32), CultureInfo.InvariantCulture);
                    break;
                case TypeCode.Decimal:

                    string decValue = ((RatNum)val).ToDecimalString(128);

                    ReadOnlySpan<char> decValueSpan = decValue.AsSpan();
                    if (decValue.EndsWith('?'))
                    {
                        decValueSpan = decValueSpan[..^1];
                    }

                    value = decimal.Parse(decValueSpan, NumberStyles.Number, CultureInfo.InvariantCulture);
                    break;
                case TypeCode.Double:
                    value = double.Parse(((RatNum)val).ToDecimalString(64), CultureInfo.InvariantCulture);
                    break;

                default:
                    throw new NotSupportedException("Unsupported parameter type for " + parameter.Name + ".");
            }
        }

        // If there was a type mapping, we need to convert back to the original type.
        // In that case we expect a constructor with the mapped type to be available.
        if (parameterTypeMapping != null)
        {
            if (parameter is PropertyInfo propertyInfo)
            {
                var ctor = propertyInfo.PropertyType.GetConstructor(new Type[] { parameterType });

                if (ctor == null)
                {
                    throw new InvalidOperationException("Could not construct an instance of the mapped type " + propertyInfo.PropertyType.Name + ". No public constructor with parameter type " + parameterType + " found.");
                }

                value = ctor.Invoke(new object[] { value! });
            }
            if (parameter is FieldInfo fieldInfo)
            {
                var ctor = fieldInfo.FieldType.GetConstructor(new Type[] { parameterType });

                if (ctor == null)
                {
                    throw new InvalidOperationException("Could not construct an instance of the mapped type " + fieldInfo.FieldType.Name + ". No public constructor with parameter type " + parameterType + " found.");
                }

                value = ctor.Invoke(new object[] { value! });
            }
        }

        return value!;
    }

    /// <summary>
    /// Reads a <see cref="DateTime"/> symbol back from the ticks Z3 holds it as.
    /// </summary>
    /// <remarks>
    /// A scalar <see cref="DateTime"/> symbol is bounded to the range of the type when the
    /// constraints are asserted (#87), so for a scalar this guard cannot fire. A collection
    /// element is not bounded - its length is not known when the constraints are asserted - so
    /// an element constrained beyond the range still has a model and still reaches here, and this
    /// throws naming the symbol and the range rather than letting the <see cref="DateTime"/>
    /// constructor complain about a parameter. The same trade-off as the checked read of a
    /// <c>short</c> element: loud rather than wrong.
    /// </remarks>
    private static DateTime ToDateTime(long ticks, string name)
    {
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
        {
            throw new OverflowException(
                $"The value Z3 chose for the DateTime symbol {name} is outside the range a DateTime can hold, 0001-01-01 to 9999-12-31. See https://github.com/endjin/Z3.Linq/issues/87.");
        }

        return new DateTime(ticks, DateTimeKind.Utc);
    }

    /// <summary>
    /// Reads <paramref name="member"/> off <paramref name="instance"/>, or returns
    /// <see langword="null"/> if there is no instance to read it from.
    /// </summary>
    private static object? GetMemberValue(MemberInfo member, object? instance)
    {
        return instance is null ? null : member switch
        {
            PropertyInfo property => property.GetValue(instance),
            FieldInfo field => field.GetValue(instance),
            _ => null,
        };
    }

    /// <summary>
    /// Whether <paramref name="type"/> is one the library treats as a collection symbol: an
    /// array, or a generic type implementing <see cref="IEnumerable"/>.
    /// </summary>
    private static bool IsCollection(Type type)
    {
        return type.IsArray || (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type.GetGenericTypeDefinition()));
    }

    /// <summary>
    /// Gets the solution object for the solved theorem.
    /// </summary>
    /// <typeparam name="T">Environment type to create an instance of.</typeparam>
    /// <param name="context">Z3 context.</param>
    /// <param name="model">Z3 model to evaluate theorem parameters under.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <param name="template">An instance of <typeparamref name="T"/> whose collections give the solution's their length, or null.</param>
    /// <returns>Instance of the environment type with theorem-satisfying values.</returns>
    private static T GetSolution<T>(Context context, Model model, Environment environment, object? template)
    {
        Type t = typeof(T);
        return (T) GetSolution(t, context, model, environment, template);
    }

    /// <summary>
    /// Gets the solution object for the solved theorem.
    /// </summary>
    /// <param name="t">Environment type to create an instance of.</param>
    /// <param name="context">Z3 context.</param>
    /// <param name="model">Z3 model to evaluate theorem parameters under.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <param name="template">An instance of <paramref name="t"/> whose collections give the solution's their length, or null.</param>
    /// <returns>Instance of the environment type with theorem-satisfying values.</returns>
    private static object GetSolution(Type t, Context context, Model model, Environment environment, object? template)
    {
        // Determine whether T is a compiler-generated type, indicating an anonymous type.
        // This check might not be reliable enough but works for now.
        if (t.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Any())
        {
            // Anonymous types have a constructor that takes in values for all its properties.
            // However, we don't know the order and it's hard to correlate back the parameters
            // to the underlying properties. So, we want to bypass that constructor altogether
            // by using the FormatterServices to create an uninitialized (all-zero) instance.
            object result = RuntimeHelpers.GetUninitializedObject(t);

            // Here we take advantage of undesirable knowledge on how anonymous types are
            // implemented by the C# compiler. This is risky but we can live with it for
            // now in this POC. Because the properties are get-only, we need to perform
            // nominal matching with the corresponding backing fields.
            var fields = t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var parameter in environment.Properties.Keys.Cast<PropertyInfo>())
            {
                // Mapping from property to field.
                var field = fields.SingleOrDefault(f => f.Name.StartsWith($"<{parameter.Name}>"));

                if (field == null) 
                {
                    continue;
                }

                var subEnv = environment.Properties[parameter];

                // The same marshaller a named environment uses, so an anonymous one supports the
                // same types - including a nested object, which is materialised by the recursion
                // inside ConvertZ3Expression rather than evaluated here. This branch used to carry
                // a marshaller of its own that handled bool and int, and evaluated the handle
                // before checking the type - so a nested object, whose handle is null, reached Z3
                // and surfaced as a NullReferenceException. See #75.
                //
                // The instance here is uninitialised, so a collection on it has no length of its own;
                // the one on the template - the instance passed to NewTheorem - supplies it. See #78.
                field.SetValue(result, ConvertZ3Expression(result, context, model, subEnv, parameter, GetMemberValue(parameter, template)));
            }

            return result;
        }
        else
        {
            // Straightforward case of having an "onymous type" at hand.
            object result = Activator.CreateInstance(t)!;

            foreach (var parameter in environment.Properties.Keys)
            {
                if (parameter is PropertyInfo)
                {
                    var prop = parameter as PropertyInfo;

                    if (prop == null) 
                    {
                        continue;
                    }

                    // Evaluation of the values though the handle in the environment bindings.
                    object value;

                    var subEnv = environment.Properties[prop];

                    value = ConvertZ3Expression(result, context, model, subEnv, prop, GetMemberValue(prop, template));

                    prop.SetValue(result, value, null);
                }

                if (parameter is FieldInfo)
                {
                    var prop = parameter as FieldInfo;

                    if (prop == null)
                    {
                        continue;
                    }

                    // Evaluation of the values though the handle in the environment bindings.
                    object value;

                    var subEnv = environment.Properties[prop];

                    value = ConvertZ3Expression(result, context, model, subEnv, prop, GetMemberValue(prop, template));

                    prop.SetValue(result, value);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Evaluates an expression under a model, supplying a value for any term the model has no
    /// interpretation for.
    /// </summary>
    /// <param name="model">Z3 model to evaluate under.</param>
    /// <param name="expr">Term to evaluate. Nullable by necessity - see the remarks.</param>
    /// <returns>The model value of <paramref name="expr"/>.</returns>
    /// <remarks>
    /// <para>
    /// A Z3 model is partial: it assigns values only to the constants the solver actually
    /// needed. Evaluating one it does not interpret hands back the term itself - an
    /// <c>IntExpr</c> rather than an <c>IntNum</c> - and the casts in the marshalling switches
    /// above then fail. The condition is not "no constraint mentions it" but "the model does
    /// not interpret it": a constraint the solver simplifies away, such as x == x, leaves its
    /// symbol uninterpreted just the same.
    /// </para>
    /// <para>
    /// Such a theorem is still satisfiable and its free symbols may take any value, so
    /// completion is enabled and Z3 supplies one. Completion only fills gaps - it never
    /// overrides a value the solver chose - so symbols the model does interpret are
    /// unaffected. See https://github.com/endjin/Z3.Linq/issues/51.
    /// </para>
    /// </remarks>
    private static Expr EvaluateWithCompletion(Model model, Expr expr)
        => model.Eval(expr, completion: true);
}