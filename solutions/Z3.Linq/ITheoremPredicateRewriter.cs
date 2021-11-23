namespace Z3.Linq;
 
using System.Linq.Expressions;

public interface ITheoremPredicateRewriter
{
    MethodCallExpression Rewrite(MethodCallExpression call);
}