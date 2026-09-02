namespace Z3.Linq;

using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

using MiaPlaza.ExpressionUtils;
using MiaPlaza.ExpressionUtils.Evaluating;

using Microsoft.Z3;

/// <summary>
/// Translates the LINQ expression tree of one theorem constraint into a Z3 expression.
/// </summary>
/// <remarks>
/// One instance translates one constraint: the Z3 context, the environment its symbols are bound
/// in, and the lambda's parameter are fixed for the whole walk, so they are held as fields and set
/// once by <see cref="Translate"/> rather than threaded through every method. Driven by
/// <c>Theorem</c> during <c>Solve</c> and <c>Optimize</c>, which build the <see cref="Environment"/>
/// it translates against.
/// </remarks>
internal sealed class ExpressionVisitor
{
    /// <summary>The generic definition of <see cref="Z3Methods.Distinct{T}"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo DistinctMethod = typeof(Z3Methods).GetMethod(nameof(Z3Methods.Distinct))!;

    /// <summary>The generic definition of <see cref="Enumerable.ToArray{TSource}"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo EnumerableToArrayMethod =
        typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.ToArray));

    /// <summary>The generic definition of the two-argument <see cref="Enumerable.Select{TSource, TResult}(IEnumerable{TSource}, Func{TSource, TResult})"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo EnumerableSelectMethod =
        typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.Select) && m.GetParameters().Length == 2);

    private readonly Context context;
    private readonly Environment environment;
    private readonly ParameterExpression param;

    private ExpressionVisitor(Context context, Environment environment, ParameterExpression param)
    {
        this.context = context;
        this.environment = environment;
        this.param = param;
    }

    /// <summary>
    /// Translates a constraint's expression tree into a Z3 expression handle.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <param name="expression">LINQ expression tree to be translated.</param>
    /// <param name="param">The constraint lambda's parameter, i.e. the theorem's environment variable.</param>
    /// <returns>Z3 expression handle.</returns>
    internal static Expr Translate(Context context, Environment environment, Expression expression, ParameterExpression param)
    {
        return new ExpressionVisitor(context, environment, param).Visit(expression);
    }

    private Expr Visit(Expression expression)
    {
        // Largely table-driven mechanism, providing constructor lambdas to Visit* methods,
        // classified by node type and arity. The lambdas take the context so they stay
        // non-capturing and are cached by the compiler rather than allocated per node.
        switch (expression.NodeType)
        {
            case ExpressionType.And:
            case ExpressionType.AndAlso:
                return VisitBitwise((BinaryExpression)expression, "&", static (ctx, a, b) => ctx.MkAnd(a, b), static (ctx, a, b) => ctx.MkBVAND(a, b));

            case ExpressionType.Or:
            case ExpressionType.OrElse:
                return VisitBitwise((BinaryExpression)expression, "|", static (ctx, a, b) => ctx.MkOr(a, b), static (ctx, a, b) => ctx.MkBVOR(a, b));

            case ExpressionType.ExclusiveOr:
                return VisitBitwise((BinaryExpression)expression, "^", static (ctx, a, b) => ctx.MkXor(a, b), static (ctx, a, b) => ctx.MkBVXOR(a, b));

            // C# spells both boolean '!' and bitwise '~' with the Not node (OnesComplement is the
            // alternative spelling some providers emit for '~'); the operand's sort decides.
            case ExpressionType.Not:
            case ExpressionType.OnesComplement:
                return VisitUnary((UnaryExpression)expression, static (ctx, a) => a switch
                {
                    BoolExpr boolExpr => ctx.MkNot(boolExpr),
                    BitVecExpr bvExpr => ctx.MkBVNot(bvExpr),
                    _ => throw new NotSupportedException("The '~' operator is supported only on bit-vector (uint/ulong) symbols; '!' only on Boolean operands."),
                });

            case ExpressionType.Negate:
            case ExpressionType.NegateChecked:
                return VisitUnary((UnaryExpression)expression, static (ctx, a) => ctx.MkUnaryMinus((ArithExpr)a));

            case ExpressionType.Add:
            case ExpressionType.AddChecked:
                return VisitArithmetic((BinaryExpression)expression, static (ctx, a, b) => ctx.MkAdd(a, b), static (ctx, a, b) => ctx.MkBVAdd(a, b));

            case ExpressionType.Subtract:
            case ExpressionType.SubtractChecked:
                return VisitArithmetic((BinaryExpression)expression, static (ctx, a, b) => ctx.MkSub(a, b), static (ctx, a, b) => ctx.MkBVSub(a, b));

            case ExpressionType.Multiply:
            case ExpressionType.MultiplyChecked:
                return VisitArithmetic((BinaryExpression)expression, static (ctx, a, b) => ctx.MkMul(a, b), static (ctx, a, b) => ctx.MkBVMul(a, b));

            case ExpressionType.Divide:
                return VisitArithmetic((BinaryExpression)expression, static (ctx, a, b) => ctx.MkDiv(a, b), static (ctx, a, b) => ctx.MkBVUDiv(a, b));

            case ExpressionType.Modulo:
                return VisitBinary((BinaryExpression)expression, static (ctx, a, b) => (a, b) switch
                {
                    (BitVecExpr ba, BitVecExpr bb) => ctx.MkBVURem(ba, bb),
                    (IntExpr ia, IntExpr ib) => ctx.MkRem(ia, ib),
                    _ => throw new NotSupportedException("The modulo operator is supported only on integer or bit-vector operands; Z3 has no remainder on real-sorted values."),
                });

            case ExpressionType.LeftShift:
                return VisitShift((BinaryExpression)expression, "<<", static (ctx, a, b) => ctx.MkBVSHL(a, b));

            case ExpressionType.RightShift:
                return VisitShift((BinaryExpression)expression, ">>", static (ctx, a, b) => ctx.MkBVLSHR(a, b));

            case ExpressionType.LessThan:
                return VisitComparison((BinaryExpression)expression, static (ctx, a, b) => ctx.MkLt(a, b), static (ctx, a, b) => ctx.MkBVULT(a, b));

            case ExpressionType.LessThanOrEqual:
                return VisitComparison((BinaryExpression)expression, static (ctx, a, b) => ctx.MkLe(a, b), static (ctx, a, b) => ctx.MkBVULE(a, b));

            case ExpressionType.GreaterThan:
                return VisitComparison((BinaryExpression)expression, static (ctx, a, b) => ctx.MkGt(a, b), static (ctx, a, b) => ctx.MkBVUGT(a, b));

            case ExpressionType.GreaterThanOrEqual:
                return VisitComparison((BinaryExpression)expression, static (ctx, a, b) => ctx.MkGe(a, b), static (ctx, a, b) => ctx.MkBVUGE(a, b));

            case ExpressionType.Equal:
                return VisitBinary((BinaryExpression)expression, static (ctx, a, b) => ctx.MkEq(a, b));

            case ExpressionType.NotEqual:
                return VisitBinary((BinaryExpression)expression, static (ctx, a, b) => ctx.MkNot(ctx.MkEq(a, b)));

            case ExpressionType.MemberAccess:
                return VisitMember((MemberExpression)expression);

            case ExpressionType.Constant:
                return VisitConstantValue(((ConstantExpression)expression).Value!);

            case ExpressionType.Call:
                return VisitCall((MethodCallExpression)expression);

            case ExpressionType.Conditional:
                return VisitConditional((ConditionalExpression)expression);

            case ExpressionType.ArrayIndex:
                return VisitBinary((BinaryExpression)expression, static (ctx, a, b) => ctx.MkSelect((ArrayExpr)a, b));

            case ExpressionType.Index:
                return VisitIndex((IndexExpression)expression, static (ctx, a, b) => ctx.MkSelect((ArrayExpr)a, b));

            case ExpressionType.Convert:
                return VisitConvert((UnaryExpression)expression);

            case ExpressionType.Power:
                return VisitBinary((BinaryExpression)expression, static (ctx, a, b) => ctx.MkPower((ArithExpr)a, (ArithExpr)b));

            default:
                throw new NotSupportedException("Unsupported expression node type encountered: " + expression.NodeType);
        }
    }

    private Expr VisitConvert(UnaryExpression expression)
    {
        if (expression.Type == expression.Operand.Type)
        {
            return Visit(expression.Operand);
        }

        Expr inner = Visit(expression.Operand);

        // A numeric conversion the compiler inserted, or the caller wrote, means one of three
        // things in Z3, and which one depends on the sorts involved rather than on the CLR
        // types: nothing at all when both types map to the same sort (short to int, int to
        // long, float to double, a cast that only narrows); integer-to-real when the operand is
        // an integer and the target a real (int to double, int to decimal); real-to-integer the
        // other way round. The target sort comes from the same mapping the symbols are declared
        // with, so a conversion can never disagree with a symbol about what a type is.
        //
        // This used to be a switch on the target type alone, which assumed the operand sort from
        // it - int-to-real for every conversion to double, real-to-int for every conversion to
        // int - and had no arm at all for long, float or decimal. See #63 and #76.
        Sort? targetSort = expression.Type == typeof(char)
            ? this.context.IntSort
            : Theorem.TryGetSymbolSort(this.context, Type.GetTypeCode(expression.Type));

        if (targetSort is not null)
        {
            if (inner.Sort.Equals(targetSort))
            {
                return inner;
            }

            if (inner.IsInt && targetSort is RealSort)
            {
                return this.context.MkInt2Real((IntExpr)inner);
            }

            if (inner.IsReal && targetSort is IntSort)
            {
                return this.context.MkReal2Int((RealExpr)inner);
            }
        }

        throw new NotImplementedException($"Cast '{expression.Operand} ({expression.Operand.Type})' to {expression.Type}");
    }

    /// <summary>
    /// Translates a binary expression, combining the translated operands with <paramref name="ctor"/>.
    /// </summary>
    /// <param name="expression">Binary expression.</param>
    /// <param name="ctor">Combines the context and the two recursively translated operands.</param>
    /// <returns>Z3 expression handle.</returns>
    private Expr VisitBinary(BinaryExpression expression, Func<Context, Expr, Expr, Expr> ctor)
    {
        return ctor(this.context, Visit(expression.Left), Visit(expression.Right));
    }

    /// <summary>
    /// Translates an arithmetic operator, choosing the integer/real form or the bit-vector form
    /// from the operands' sort.
    /// </summary>
    /// <param name="expression">Binary expression.</param>
    /// <param name="arith">Builds the term for integer- or real-sorted operands.</param>
    /// <param name="bv">Builds the term for bit-vector operands.</param>
    /// <returns>Z3 expression handle.</returns>
    /// <remarks>
    /// A bit-vector symbol (an unsigned CLR type) carries wrapping arithmetic; an <c>int</c>,
    /// <c>long</c> or real symbol carries mathematical arithmetic. Both operands share a sort,
    /// since C# would not compile a mixed expression without a conversion, which
    /// <see cref="VisitConvert"/> handles first.
    /// </remarks>
    private Expr VisitArithmetic(BinaryExpression expression, Func<Context, ArithExpr, ArithExpr, ArithExpr> arith, Func<Context, BitVecExpr, BitVecExpr, BitVecExpr> bv)
    {
        Expr left = Visit(expression.Left);
        Expr right = Visit(expression.Right);

        return left is BitVecExpr bvLeft
            ? bv(this.context, bvLeft, (BitVecExpr)right)
            : arith(this.context, (ArithExpr)left, (ArithExpr)right);
    }

    /// <summary>
    /// Translates a relational operator, choosing the ordered-arithmetic form or the
    /// <em>unsigned</em> bit-vector form from the operands' sort.
    /// </summary>
    /// <param name="expression">Binary expression.</param>
    /// <param name="arith">Builds the comparison for integer- or real-sorted operands.</param>
    /// <param name="bv">Builds the unsigned comparison for bit-vector operands.</param>
    /// <returns>Z3 expression handle.</returns>
    /// <remarks>
    /// Bit-vectors map from the unsigned CLR types, so the comparison is unsigned - <c>uint</c>
    /// order, not two's-complement signed order.
    /// </remarks>
    private Expr VisitComparison(BinaryExpression expression, Func<Context, ArithExpr, ArithExpr, BoolExpr> arith, Func<Context, BitVecExpr, BitVecExpr, BoolExpr> bv)
    {
        Expr left = Visit(expression.Left);
        Expr right = Visit(expression.Right);

        return left is BitVecExpr bvLeft
            ? bv(this.context, bvLeft, (BitVecExpr)right)
            : arith(this.context, (ArithExpr)left, (ArithExpr)right);
    }

    /// <summary>
    /// Translates <c>&amp;</c>, <c>|</c> and <c>^</c>, which C# uses for Boolean logic, integer
    /// bitwise arithmetic, and bit-vector bitwise arithmetic alike.
    /// </summary>
    /// <param name="expression">Binary expression.</param>
    /// <param name="op">The C# operator, for the diagnostic when the operands fit neither form.</param>
    /// <param name="boolOp">Builds the logical term for Boolean operands.</param>
    /// <param name="bvOp">Builds the bitwise term for bit-vector operands.</param>
    /// <returns>Z3 expression handle.</returns>
    /// <remarks>
    /// The operator is chosen from the operands' Z3 sort, not the expression node, which is the
    /// same for <c>bool &amp; bool</c>, <c>int &amp; int</c> and <c>uint &amp; uint</c>. Boolean
    /// operands give the logical operator and bit-vector operands the bitwise one. A plain
    /// integer symbol has neither - Z3's integer sort has no bitwise operations - so it is
    /// rejected with a message pointing at the unsigned types, rather than an
    /// <see cref="InvalidCastException"/> from inside the cast.
    /// </remarks>
    private Expr VisitBitwise(BinaryExpression expression, string op, Func<Context, BoolExpr, BoolExpr, BoolExpr> boolOp, Func<Context, BitVecExpr, BitVecExpr, BitVecExpr> bvOp)
    {
        Expr left = Visit(expression.Left);
        Expr right = Visit(expression.Right);

        return (left, right) switch
        {
            (BoolExpr boolLeft, BoolExpr boolRight) => boolOp(this.context, boolLeft, boolRight),
            (BitVecExpr bvLeft, BitVecExpr bvRight) => bvOp(this.context, bvLeft, bvRight),
            _ => throw new NotSupportedException(
                $"The '{op}' operator is supported on Boolean operands and on bit-vector (uint/ulong) symbols. It is not supported on plain integer symbols, whose Z3 sort has no bitwise operations."),
        };
    }

    /// <summary>
    /// Translates a shift, <c>&lt;&lt;</c> or <c>&gt;&gt;</c>, on a bit-vector.
    /// </summary>
    /// <param name="expression">Binary expression.</param>
    /// <param name="op">The C# operator, for the diagnostic.</param>
    /// <param name="bvOp">Builds the shift term from the value and the shift amount.</param>
    /// <returns>Z3 expression handle.</returns>
    /// <remarks>
    /// C# types the shift amount as <c>int</c>, so the right operand translates to an integer and
    /// is converted to a bit-vector of the value's width before the shift. Only a bit-vector value
    /// can be shifted; Z3's integer sort has no shift.
    /// </remarks>
    private Expr VisitShift(BinaryExpression expression, string op, Func<Context, BitVecExpr, BitVecExpr, BitVecExpr> bvOp)
    {
        Expr value = Visit(expression.Left);
        Expr amount = Visit(expression.Right);

        if (value is not BitVecExpr bvValue)
        {
            throw new NotSupportedException($"The '{op}' shift operator is supported only on bit-vector (uint/ulong) symbols.");
        }

        BitVecExpr bvAmount = amount as BitVecExpr ?? this.context.MkInt2BV(bvValue.SortSize, (IntExpr)amount);

        return bvOp(this.context, bvValue, bvAmount);
    }

    /// <summary>
    /// Translates a conditional (ternary <c>?:</c>) expression.
    /// </summary>
    /// <param name="expression">Conditional expression.</param>
    /// <returns>Z3 expression handle.</returns>
    /// <remarks>
    /// Maps onto Z3's if-then-else term. The two branches must share a sort, which they do for
    /// any ternary the C# compiler accepts, since both arms are converted to a common type.
    /// </remarks>
    private Expr VisitConditional(ConditionalExpression expression)
    {
        Expr test = Visit(expression.Test);
        Expr ifTrue = Visit(expression.IfTrue);
        Expr ifFalse = Visit(expression.IfFalse);

        return this.context.MkITE((BoolExpr)test, ifTrue, ifFalse);
    }

    /// <summary>
    /// Translates a method call expression.
    /// </summary>
    /// <param name="call">Method call expression.</param>
    /// <returns>Z3 expression handle.</returns>
    private Expr VisitCall(MethodCallExpression call)
    {
        var method = call.Method;

        // Does the method have a rewriter attribute applied? IsDefined is checked first so a call
        // to an ordinary method - the common case, hit once per call node in every constraint -
        // does not allocate an attribute array; the attribute is read only when one is present.
        if (method.IsDefined(typeof(TheoremPredicateRewriterAttribute), false))
        {
            var rewriterAttr = method.GetCustomAttributes<TheoremPredicateRewriterAttribute>(false).Single();

            // Make sure the specified rewriter type implements the ITheoremPredicateRewriter.
            var rewriterType = rewriterAttr.RewriterType;

            if (!typeof(ITheoremPredicateRewriter).IsAssignableFrom(rewriterType))
            {
                throw new InvalidOperationException("Invalid predicate rewriter type definition. Did you implement ITheoremPredicateRewriter?");
            }

            // Assume a parameterless public constructor to new up the rewriter.
            var rewriter = (ITheoremPredicateRewriter)Activator.CreateInstance(rewriterType)!;

            // Make sure we don't get stuck when the rewriter just returned its input. Valid
            // rewriters should satisfy progress guarantees.
            var result = rewriter.Rewrite(call);

            if (result == call)
            {
                throw new InvalidOperationException("The expression tree rewriter of type " + rewriterType.Name + " did not perform any rewrite. Aborting compilation to avoid infinite looping.");
            }

            // Visit the rewritten expression.
            return Visit(result);
        }

        // Filter for known Z3 operators.
        if (method.IsGenericMethod && method.GetGenericMethodDefinition() == DistinctMethod)
        {
            // We know the signature of the Distinct method call. Its argument is a params
            // array, hence we expect a NewArrayExpression.
            IEnumerable? distinctExps = null;

            var itemsExpression = call.Arguments[0];
            if (itemsExpression is MethodCallExpression mExp)
            {
                if (mExp.Method.IsGenericMethod && mExp.Method.GetGenericMethodDefinition() == EnumerableToArrayMethod)
                {
                    var callerToArrayExp = mExp.Arguments[0];
                    if (callerToArrayExp is MethodCallExpression callerToArrayMethodExp)
                    {
                        if (callerToArrayMethodExp.Method.IsGenericMethod && callerToArrayMethodExp.Method.GetGenericMethodDefinition() == EnumerableSelectMethod)
                        {
                            var caller = (ICollection)ExpressionInterpreter.Instance.Interpret(callerToArrayMethodExp.Arguments[0]);
                            var arg = callerToArrayMethodExp.Arguments[1] as LambdaExpression;
                            var subExps = new List<Expression>(caller.Count);

                            foreach (var item in caller)
                            {
                                var substitutedExpression = ParameterSubstituter.SubstituteParameter(arg, Expression.Constant(item));

                                // SubstituteParameter yields the selector's body, which is not a
                                // lambda, but from 1.3.0 PartialEval only accepts a LambdaExpression.
                                // Wrapping the body in a lambda and taking the partially evaluated
                                // Body back off is the supported equivalent of the overload that
                                // used to take a bare Expression. The body still references the
                                // theorem's own parameter, which stays free in the wrapper - the
                                // evaluator leaves parameter-dependent subtrees alone and folds
                                // only the closed ones, exactly as before.
                                var wrappedExpression = Expression.Lambda(substitutedExpression);
                                var newlyFlattened = PartialEvaluator.PartialEval(wrappedExpression, ExpressionInterpreter.Instance).Body;
                                subExps.Add(newlyFlattened);
                            }

                            distinctExps = subExps;
                        }
                    }
                }
            }
            else
            {
                if (itemsExpression is NewArrayExpression arrExp)
                {
                    distinctExps = arrExp.Expressions;
                }
            }

            if (distinctExps == null)
            {
                throw new NotSupportedException("Unsupported method call: " + method.ToString() + " with sub expression " + call.Arguments[0].ToString());
            }

            IEnumerable<Expr> args = from Expression arg in distinctExps
                                     select Visit(arg);

            return this.context.MkDistinct(args.ToArray());
        }

        if (method.Name.StartsWith("get_"))
        {
            // Assuming it's an indexed property
            string prop = method.Name[4..];
            var propinfo = method.DeclaringType?.GetProperty(prop);
            var target = call.Object;

            if (target != null)
            {
                var args = call.Arguments;
                var indexer = Expression.MakeIndex(target, propinfo, args);

                return Visit(indexer);
            }
        }

        throw new NotSupportedException("Unknown method call:" + method.ToString());
    }

    /// <summary>
    /// Translates a constant value into a Z3 term.
    /// </summary>
    /// <param name="val">The constant value.</param>
    /// <returns>Z3 expression handle.</returns>
    private Expr VisitConstantValue(object val)
    {
        switch (Type.GetTypeCode(val.GetType()))
        {
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.Int64:
                return this.context.MkInt(Convert.ToInt64(val));
            case TypeCode.UInt32:
            case TypeCode.UInt64:
                // uint and ulong are bit-vectors of their width. See #99's bit-vector follow-up.
                return this.context.MkBV(Convert.ToUInt64(val), Theorem.BitVectorWidth(Type.GetTypeCode(val.GetType()))!.Value);
            case TypeCode.Boolean:
                return this.context.MkBool((bool)val);
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                // Invariant, not current: Z3's parser accepts only '.' as the decimal separator,
                // and about half of all cultures render 1.5 as something else. See #52.
                return this.context.MkReal(((IFormattable)val).ToString(null, CultureInfo.InvariantCulture));
            case TypeCode.DateTime:
                // A DateTime is encoded as its ticks - 100ns intervals from 0001-01-01 - on the UTC
                // timeline, which covers the whole DateTime range. A Windows file time counted from
                // 1601 instead, so nothing earlier could be written or read. See #83.
                //
                // A Kind of Local is converted to UTC first; Unspecified is taken to be UTC already,
                // which is the convention ToFileTimeUtc had and the read path inverts. See #56.
                return this.context.MkInt(ToUtcTicks((DateTime)val));
            case TypeCode.String:
                return this.context.MkString((string)val);
            default:
                throw new NotSupportedException($"Unsupported constant {val}");
        }
    }

    private Expr VisitIndex(IndexExpression expression, Func<Context, Expr, Expr[], Expr> ctor)
    {
        var args = expression.Arguments.Select(Visit).ToArray();
        return ctor(this.context, Visit(expression.Object!), args);
    }

    /// <summary>
    /// Translates a member expression - a symbol access on the environment, or a captured constant.
    /// </summary>
    /// <param name="member">Member expression.</param>
    /// <returns>Z3 expression handle.</returns>
    private Expr VisitMember(MemberExpression member)
    {
        // E.g. Symbols l = ...;
        //      theorem.Where(s => l.X1)
        //                         ^^
        List<MemberExpression> hierarchy = [];
        var mExp = member;
        hierarchy.Add(mExp);

        while (mExp.Expression is MemberExpression parent)
        {
            mExp = parent;
            hierarchy.Add(parent);
        }

        hierarchy.Reverse();

        var topMember = hierarchy.First();

        if (topMember.Expression != this.param)
        {
            if ((topMember.Expression is ConstantExpression expression))
            {
                // We only ever get here if SimplifyLambda is set to false, otherwise partial evaluation does it earlier
                var target = expression.Value;
                var hierarchyIdx = 0;
                object? val = target;

                while (hierarchyIdx < hierarchy.Count)
                {
                    var currentMember = hierarchy[hierarchyIdx].Member;

                    switch (currentMember.MemberType)
                    {
                        case MemberTypes.Property:
                            var property = (PropertyInfo)currentMember;
                            val = property.GetValue(target);
                            break;
                        case MemberTypes.Field:
                            var field = (FieldInfo)currentMember;
                            val = field.GetValue(target);
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported constant {target} .");
                    }

                    hierarchyIdx++;
                }

                if (val != null)
                {
                    return VisitConstantValue(val);
                }

                throw new NotSupportedException($"Could not reduce expression {topMember.Expression}");
            }
        }

        // Only members we allow currently are direct accesses to the theorem's variables
        // in the environment type. So we just try to find the mapping from the environment
        // bindings table.
        Environment subEnv = this.environment;

        foreach (var memberExpression in hierarchy)
        {
            // Nullability rules require us to give TryGetValue a nullable holder because it
            // might not succeed. However, C#'s flow analysis is able to determine that if we
            // make it past this if statement, the result definitely wasn't null, so it is
            // happy for us to assign it into the never-null subEnv.
            Environment? nextSubEnv;
            if (!((memberExpression.Member is PropertyInfo property && subEnv.Properties.TryGetValue(property, out nextSubEnv)) ||
                    (memberExpression.Member is FieldInfo field && subEnv.Properties.TryGetValue(field, out nextSubEnv))))
            {
                throw new NotSupportedException("Unknown parameter encountered: " + member.Member.Name + ".");
            }
            subEnv = nextSubEnv;
        }

        return subEnv.Expr!;
    }

    /// <summary>
    /// Translates a unary expression, transforming the translated operand with <paramref name="ctor"/>.
    /// </summary>
    /// <param name="expression">Unary expression.</param>
    /// <param name="ctor">Combines the context and the recursively translated operand.</param>
    /// <returns>Z3 expression handle.</returns>
    private Expr VisitUnary(UnaryExpression expression, Func<Context, Expr, Expr> ctor)
    {
        return ctor(this.context, Visit(expression.Operand));
    }

    /// <summary>
    /// The ticks of <paramref name="value"/> on the UTC timeline, which is how a
    /// <see cref="DateTime"/> is encoded for Z3.
    /// </summary>
    /// <remarks>
    /// A <see cref="DateTimeKind.Local"/> value is converted to UTC; an
    /// <see cref="DateTimeKind.Unspecified"/> one is taken to be UTC already, as
    /// <see cref="DateTime.ToFileTimeUtc"/> - the previous encoding - did. The read path in
    /// <c>Theorem</c> produces a <see cref="DateTimeKind.Utc"/> value from the same ticks.
    /// See #56 and #83.
    /// </remarks>
    internal static long ToUtcTicks(DateTime value)
    {
        return value.Kind == DateTimeKind.Local ? value.ToUniversalTime().Ticks : value.Ticks;
    }
}
