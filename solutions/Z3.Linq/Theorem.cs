namespace Z3.Linq;
 
using Microsoft.Z3;

using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Z3 context under which the theorem is solved.
    /// </summary>
    private readonly Z3Context context;

    /// <summary>
    /// Creates a new theorem for the given Z3 context.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    protected Theorem(Z3Context context)
        : this(context, new List<LambdaExpression>())
    {
    }

    /// <summary>
    /// Creates a new pre-constrained theorem for the given Z3 context.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="constraints">Constraints to apply to the created theorem.</param>
    protected Theorem(Z3Context context, IEnumerable<LambdaExpression> constraints)
    {
        this.context = context;
        this.constraints = constraints;
    }

    /// <summary>
    /// Gets the constraints of the theorem.
    /// </summary>
    protected IEnumerable<LambdaExpression> Constraints => constraints;

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
    /// <returns>Result of solving the theorem; default(T) if the theorem cannot be satisfied.</returns>
    protected T? Solve<T>()
    {
        using Context ctx = this.context.CreateContext();
        var environment = GetEnvironment(ctx, typeof(T));

        // Solver solver = context.MkSimpleSolver();
        Solver solver = ctx.MkSolver();

        AssertConstraints<T>(ctx, solver, environment);

        Status status = solver.Check();

        if (status != Status.SATISFIABLE)
        {
            return default;
        }

        return GetSolution<T>(ctx, solver.Model, environment);
    }

    /// <summary>
    /// Solves the theorem using Z3.
    /// </summary>
    /// <typeparam name="T">Theorem environment type.</typeparam>
    /// <typeparam name="TResult">The Theorem Result.</typeparam>
    /// <returns>Result of solving the theorem; default(T) if the theorem cannot be satisfied.</returns>
    protected T Optimize<T, TResult>(Optimization direction, Expression<Func<T, TResult>> lambda)
    {
        using Context ctx = this.context.CreateContext();
        var environment = GetEnvironment(ctx, typeof(T));

        Optimize optimizer = ctx.MkOptimize();

        AssertConstraints<T>(ctx, optimizer, environment);

        var expression = ExpressionVisitor.Visit(ctx, environment, lambda.Body, lambda.Parameters[0]);

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

        Status status = optimizer.Check();

        if (status != Status.SATISFIABLE)
        {
            return default!;
        }

        return GetSolution<T>(ctx, optimizer.Model, environment);
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
            BoolExpr expression = (BoolExpr)ExpressionVisitor.Visit(context, environment, constraint.Body, constraint.Parameters[0]);

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
    }

    private Environment GetEnvironment(Context context, Type targetType)
    {
        return GetEnvironment(context, targetType, targetType.Name);
    }

    private Environment GetEnvironment(Context context, Type targetType, string prefix)
    {
        var toReturn = new Environment();

        if (targetType.IsArray || (targetType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(targetType.GetGenericTypeDefinition())))
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

            Sort arrDomain;
            Sort arrRange;
                
            switch (Type.GetTypeCode(elType))
            {
                case TypeCode.String:
                    arrDomain = context.StringSort;
                    arrRange = context.MkBitVecSort(16);
                    break;
                case TypeCode.Int16:
                    arrDomain = context.IntSort;
                    arrRange = context.MkBitVecSort(16);
                    break;
                case TypeCode.Int32:
                    arrDomain = context.IntSort;
                    arrRange = context.IntSort;
                    break;
                case TypeCode.Int64:
                case TypeCode.DateTime:
                    arrDomain = context.IntSort;
                    arrRange = context.MkBitVecSort(64);
                    break;
                case TypeCode.Boolean:
                    arrDomain = context.BoolSort;
                    arrRange = context.BoolSort;
                    break;
                case TypeCode.Single:
                    arrDomain = context.RealSort;
                    arrRange = context.MkFPSortSingle();
                    break;
                case TypeCode.Decimal:
                    arrDomain = context.RealSort;
                    arrRange = context.MkFPSortSingle();
                    break;
                case TypeCode.Double:
                    arrDomain = context.RealSort;
                    arrRange = context.MkFPSortDouble();
                    break;
                case TypeCode.Object:
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
                default:
                    throw new NotSupportedException($"Unsupported member type {targetType.FullName}");
            }

            toReturn.Expr = context.MkArrayConst(prefix, arrDomain, arrRange);
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
            switch (Type.GetTypeCode(parameterType))
            {
                case TypeCode.String:
                    constrExp = context.MkConst(name, context.StringSort);
                    break;
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.DateTime:
                    constrExp = context.MkIntConst(name);
                    break;
                case TypeCode.Boolean:
                    constrExp = context.MkBoolConst(name);
                    break;
                case TypeCode.Single:
                case TypeCode.Decimal:
                case TypeCode.Double:
                    constrExp = context.MkRealConst(name);
                    break;
                case TypeCode.Object:
                    return GetEnvironment(context, parameterType, prefix);
                default:
                    throw new NotSupportedException("Unsupported parameter type for " + name + ".");
            }
        }
        else
        {
            Sort arrDomain;
            Sort arrRange;
            switch (Type.GetTypeCode(parameterType))
            {
                case TypeCode.String:
                    arrDomain = context.StringSort;
                    arrRange = context.MkBitVecSort(16);
                    break;
                case TypeCode.Int16:
                    arrDomain = context.IntSort;
                    arrRange = context.MkBitVecSort(16);
                    break;
                case TypeCode.Int32:
                    arrDomain = context.IntSort;
                    arrRange = context.IntSort;
                    break;
                case TypeCode.Int64:
                case TypeCode.DateTime:
                    arrDomain = context.IntSort;
                    arrRange = context.MkBitVecSort(64);
                    break;
                case TypeCode.Boolean:
                    arrDomain = context.BoolSort;
                    arrRange = context.BoolSort;
                    break;
                case TypeCode.Single:
                    arrDomain = context.RealSort;
                    arrRange = context.MkFPSortSingle();
                    break;
                case TypeCode.Decimal:
                    arrDomain = context.RealSort;
                    arrRange = context.MkFPSortSingle();
                    break;
                case TypeCode.Double:
                    arrDomain = context.RealSort;
                    arrRange = context.MkFPSortDouble();
                    break;
                default:
                    throw new NotSupportedException($"Only one level of object collections is currently supported, 2 levels detected with prefix {prefix}");

            }
            constrExp = context.MkArrayConst(prefix, arrDomain, arrRange);
        }

        toReturn.Expr = constrExp;

        return toReturn;
    }

    private static object ConvertZ3Expression(object destinationObject, Context context, Model model, Environment subEnv, MemberInfo parameter)
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
            if (parameterType.IsArray || (parameterType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(parameterType.GetGenericTypeDefinition())))
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

                //todo: deal with length in a more robust way

                int existingLength = parameter switch
                {
                    // Both branches read the value the member holds on the instance. The field
                    // branch used to cast the FieldInfo itself, which no environment could
                    // satisfy - see #53.
                    PropertyInfo property => ((ICollection)property.GetValue(destinationObject, null)!).Count,
                    FieldInfo field => ((ICollection)field.GetValue(destinationObject)!).Count,
                    _ => 0
                };

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
                        case TypeCode.Int32:
                            numVal = ((IntNum)numValExpr).Int;
                            break;
                        case TypeCode.Int64:
                            numVal = ((IntNum)numValExpr).Int64;
                            break;
                        case TypeCode.DateTime:
                            // FromFileTimeUtc, for the reason given on the scalar arm below. See #56.
                            numVal = DateTime.FromFileTimeUtc(((IntNum)numValExpr).Int64);
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
                value = GetSolution(parameterType, context, model, subEnv);
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
                case TypeCode.Int32:
                    value = ((IntNum)val).Int;
                    break;
                case TypeCode.Int64:
                    value = ((IntNum)val).Int64;
                    break;
                case TypeCode.DateTime:
                    // The write path encodes the instant with ToFileTimeUtc (ExpressionVisitor.cs:311),
                    // whose inverse is FromFileTimeUtc. FromFileTime converts to local time, which
                    // shifts the value by the machine's UTC offset and makes the same theorem answer
                    // differently on different machines. See #56.
                    value = DateTime.FromFileTimeUtc(((IntNum)val).Int64);
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
    /// Gets the solution object for the solved theorem.
    /// </summary>
    /// <typeparam name="T">Environment type to create an instance of.</typeparam>
    /// <param name="context">Z3 context.</param>
    /// <param name="model">Z3 model to evaluate theorem parameters under.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <returns>Instance of the environment type with theorem-satisfying values.</returns>
    private static T GetSolution<T>(Context context, Model model, Environment environment)
    {
        Type t = typeof(T);
        return (T) GetSolution(t, context, model, environment);
    }

    /// <summary>
    /// Gets the solution object for the solved theorem.
    /// </summary>
    /// <param name="t">Environment type to create an instance of.</param>
    /// <param name="context">Z3 context.</param>
    /// <param name="model">Z3 model to evaluate theorem parameters under.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <returns>Instance of the environment type with theorem-satisfying values.</returns>
    private static object GetSolution(Type t, Context context, Model model, Environment environment)
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

                // Evaluation of the values though the handle in the environment bindings.
                var subEnv = environment.Properties[parameter];

                Expr val = EvaluateWithCompletion(model, subEnv.Expr);
                if (parameter.PropertyType == typeof(bool))
                {
                    field.SetValue(result, val.IsTrue);
                }
                else if (parameter.PropertyType == typeof(int))
                {
                    field.SetValue(result, ((IntNum)val).Int);
                }
                else
                { 
                    throw new NotSupportedException("Unsupported parameter type for " + parameter.Name + "."); 
                }
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

                    value = ConvertZ3Expression(result, context, model, subEnv, prop);

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

                    value = ConvertZ3Expression(result, context, model, subEnv, prop);

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
    /// <para>
    /// <paramref name="expr"/> is nullable because <c>Environment.Expr</c> is, and the
    /// anonymous-type branch of <c>GetSolution</c> passes it without the guard the three sites
    /// in <c>ConvertZ3Expression</c> use. A null reaches Microsoft.Z3 and surfaces as a
    /// <c>NullReferenceException</c> - reachable today with an anonymous environment holding a
    /// nested object, and tracked by https://github.com/endjin/Z3.Linq/issues/75. Declaring the
    /// parameter nullable states that honestly; a non-nullable one would need a suppression or
    /// a new guard here, and either would change behaviour that this change deliberately leaves
    /// alone. Issue 75 proposes the better fix - checking the property type before evaluating,
    /// so the existing <c>NotSupportedException</c> fires and names the property.
    /// </para>
    /// </remarks>
    private static Expr EvaluateWithCompletion(Model model, Expr? expr)
        => model.Eval(expr, completion: true);
}