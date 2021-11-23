namespace Z3.Linq;
 
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

using MiaPlaza.ExpressionUtils;
using MiaPlaza.ExpressionUtils.Evaluating;

using Microsoft.Z3;

public static class ExpressionVisitor
{
    /// <summary>
    /// Main visitor method to translate the LINQ expression tree into a Z3 expression handle.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <param name="expression">LINQ expression tree node to be translated.</param>
    /// <param name="param">Parameter used to express the constraint on.</param>
    /// <returns>Z3 expression handle.</returns>
    public static Expr Visit(Context context, Environment environment, Expression expression, ParameterExpression param)
    {
        // Largely table-driven mechanism, providing constructor lambdas to generic Visit* methods, classified by type and arity.
        switch (expression.NodeType)
        {
            case ExpressionType.And:
            case ExpressionType.AndAlso:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkAnd((BoolExpr)a, (BoolExpr)b));

            case ExpressionType.Or:
            case ExpressionType.OrElse:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkOr((BoolExpr)a, (BoolExpr)b));

            case ExpressionType.ExclusiveOr:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkXor((BoolExpr)a, (BoolExpr)b));

            case ExpressionType.Not:
                return VisitUnary(context, environment, (UnaryExpression)expression, param, (ctx, a) => ctx.MkNot((BoolExpr)a));

            case ExpressionType.Negate:
            case ExpressionType.NegateChecked:
                return VisitUnary(context, environment, (UnaryExpression)expression, param, (ctx, a) => ctx.MkUnaryMinus((ArithExpr)a));

            case ExpressionType.Add:
            case ExpressionType.AddChecked:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkAdd((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.Subtract:
            case ExpressionType.SubtractChecked:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkSub((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.Multiply:
            case ExpressionType.MultiplyChecked:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkMul((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.Divide:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkDiv((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.Modulo:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkRem((IntExpr)a, (IntExpr)b));

            case ExpressionType.LessThan:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkLt((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.LessThanOrEqual:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkLe((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.GreaterThan:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkGt((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.GreaterThanOrEqual:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkGe((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.Equal:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkEq(a, b));

            case ExpressionType.NotEqual:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkNot(ctx.MkEq(a, b)));

            case ExpressionType.MemberAccess:
                return VisitMember(context, environment, (MemberExpression)expression, param);

            case ExpressionType.Constant:
                return VisitConstant(context, (ConstantExpression)expression);

            case ExpressionType.Call:
                return VisitCall(context, environment, (MethodCallExpression)expression, param);

/*               case ExpressionType.Parameter:
                return VisitParameter(context, environment, (ParameterExpression)expression, param);
            */
            case ExpressionType.ArrayIndex:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkSelect((ArrayExpr)a, b));
                
            case ExpressionType.Index:
                return VisitIndex(context, environment, (IndexExpression)expression, param, (ctx, a, b) => ctx.MkSelect((ArrayExpr)a, b));

            case ExpressionType.Convert:
                return VisitConvert(context, environment, (UnaryExpression)expression, param);

            case ExpressionType.Power:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkPower((ArithExpr)a, (ArithExpr)b));

            default:
                throw new NotSupportedException("Unsupported expression node type encountered: " + expression.NodeType);
        }
    }

    private static Expr VisitConvert(Context context, Environment environment, UnaryExpression expression, ParameterExpression param)
    {
        if (expression.Type == expression.Operand.Type)
        {
            return Visit(context, environment, expression.Operand, param);
        }

        var inner = Visit(context, environment, expression.Operand, param);

        switch (Type.GetTypeCode(expression.Operand.Type))
        {
            case TypeCode.Int16:
            case TypeCode.Int32:
                break;
        }

        switch (Type.GetTypeCode(expression.Type))
        {
            case TypeCode.Double:
                return context.MkInt2Real((IntExpr)inner);
            case TypeCode.Int32:
                return context.MkReal2Int((RealExpr)inner);
            case TypeCode.Char:
                if (inner.IsInt)
                {
                    return inner;// context.MkInt(1);// ((IntExpr)inner).int);
                }
                break;
        }

        throw new NotImplementedException($"Cast '{expression.Operand} ({expression.Operand.Type})' to {expression.Type}");
    }

    /// <summary>
    /// Visitor method to translate a binary expression.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <param name="expression">Binary expression.</param>
    /// <param name="ctor">Constructor to combine recursive visitor results.</param>
    /// <param name="param">Parameter used to express the constraint on.</param>
    /// <returns>Z3 expression handle.</returns>
    private static Expr VisitBinary(Context context, Environment environment, BinaryExpression expression, ParameterExpression param, Func<Context, Expr, Expr, Expr> ctor)
    {
        return ctor(context, Visit(context, environment, expression.Left, param), Visit(context, environment, expression.Right, param));
    }

    /// <summary>
    /// Visitor method to translate a method call expression.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <param name="call">Method call expression.</param>
    /// <param name="param">Parameter used to express the constraint on.</param>
    /// <returns>Z3 expression handle.</returns>
    private static Expr VisitCall(Context context, Environment environment, MethodCallExpression call, ParameterExpression param)
    {
        var method = call.Method;

        // Does the method have a rewriter attribute applied?
        var rewriterAttr = method.GetCustomAttributes<TheoremPredicateRewriterAttribute>(false).SingleOrDefault();

        if (rewriterAttr != null)
        {
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
            return Visit(context, environment, result, param);
        }

        // Filter for known Z3 operators.
        if (method.IsGenericMethod && method.GetGenericMethodDefinition() == typeof(Z3Methods).GetMethod("Distinct"))
        {
            // We know the signature of the Distinct method call. Its argument is a params
            // array, hence we expect a NewArrayExpression.
            IEnumerable? distinctExps = null;

            var itemsExpression = call.Arguments[0];
            if (itemsExpression is MethodCallExpression mExp)
            {
                if (mExp.Method.IsGenericMethod && mExp.Method.GetGenericMethodDefinition() == typeof(Enumerable)
                    .GetMethods().First(m => m.Name == nameof(Enumerable.ToArray)))
                {
                    var callerToArrayExp = mExp.Arguments[0];
                    if (callerToArrayExp is MethodCallExpression callerToArrayMethodExp)
                    {
                        if (callerToArrayMethodExp.Method.IsGenericMethod && callerToArrayMethodExp.Method.GetGenericMethodDefinition() == typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.Select) && m.GetParameters().Length == 2))
                        {
                            var caller = (ICollection)ExpressionInterpreter.Instance.Interpret(callerToArrayMethodExp.Arguments[0]);
                            var arg = callerToArrayMethodExp.Arguments[1] as LambdaExpression;
                            var subExps = new List<Expression>(caller.Count);
                                
                            foreach (var item in caller)
                            {
                                var substitutedExpression = ParameterSubstituter.SubstituteParameter(arg, Expression.Constant(item));
                                var newlyFlattened = PartialEvaluator.PartialEval(substitutedExpression, ExpressionInterpreter.Instance);
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
                throw new NotSupportedException("unsuported method call:" + method.ToString() + "with sub expression " + call.Arguments[0].ToString());
            }

            IEnumerable<Expr> args = from Expression arg in distinctExps 
                                        select Visit(context, environment, arg, param);

            return context.MkDistinct(args.ToArray());
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

                return Visit(context, environment, indexer, param);
            }
        }

        throw new NotSupportedException("Unknown method call:" + method.ToString());
    }

    /// <summary>
    /// Visitor method to translate a constant expression.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="constant">Constant expression.</param>
    /// <returns>Z3 expression handle.</returns>
    private static Expr VisitConstant(Context context, ConstantExpression constant)
    {
        return VisitConstantValue(context, constant.Value!);
    }

    private static Expr VisitConstantValue(Context context, object val)
    {
        switch (Type.GetTypeCode(val.GetType()))
        {
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
                return context.MkInt(Convert.ToInt64(val));
            case TypeCode.Boolean:
                return context.MkBool((bool)val);
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return context.MkReal(val.ToString());
            case TypeCode.DateTime:
                return context.MkInt(((DateTime)val).ToFileTimeUtc());
            case TypeCode.String:
                return context.MkString(val.ToString());
            default:
                throw new NotSupportedException($"Unsupported constant {val}");
        }
    }

    private static Expr VisitIndex(Context context, Environment environment, IndexExpression expression, ParameterExpression param, Func<Context, Expr, Expr[], Expr> ctor)
    {
        var args = expression.Arguments.Select(argExp => Visit(context, environment, argExp, param)).ToArray();
        return ctor(context, Visit(context, environment, expression.Object!, param), args);
    }

    /// <summary>
    /// Visitor method to translate a member expression.
    /// </summary>
    /// <param name="context">the Z3 context to manipulate</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <param name="member">Member expression.</param>
    /// <param name="param">Parameter used to express the constraint on.</param>
    /// <returns>Z3 expression handle.</returns>
    private static Expr VisitMember(Context context, Environment environment, MemberExpression member, ParameterExpression param)
    {
        // E.g. Symbols l = ...;
        //      theorem.Where(s => l.X1)
        //                         ^^
        var hierarchy = new List<MemberExpression>();
        var mExp = member;
        hierarchy.Add(mExp);

        while (mExp.Expression is MemberExpression parent)
        {
            mExp = parent;
            hierarchy.Add(parent);
        }

        hierarchy.Reverse();

        var topMember = hierarchy.First();

        if (topMember.Expression != param)
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
                    return VisitConstantValue(context, val);
                }

                throw new NotSupportedException($"Could not reduce expression {topMember.Expression}");
            }
            else
            {
                //Debugger.Break(); 
            }
        }

        // Only members we allow currently are direct accesses to the theorem's variables
        // in the environment type. So we just try to find the mapping from the environment
        // bindings table.
        Environment subEnv = environment;

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

/*      
    private static Expr VisitParameter(Context context, Environment environment, ParameterExpression expression, ParameterExpression param)
    {
        Expr value;

        if (!environment.Properties.TryGetValue(expression., out value))
        {
            throw new NotSupportedException("Unknown parameter encountered: " + expression.Name + ".");
        }

        return value;
    }
*/

    /// <summary>
    /// Visitor method to translate a unary expression.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <param name="expression">Unary expression.</param>
    /// <param name="ctor">Constructor to combine recursive visitor results.</param>
    /// <param name="param">Parameter used to express the constraint on.</param>
    /// <returns>Z3 expression handle.</returns>
    private static Expr VisitUnary(Context context, Environment environment, UnaryExpression expression, ParameterExpression param, Func<Context, Expr, Expr> ctor)
    {
        return ctor(context, Visit(context, environment, expression.Operand, param));
    }
}