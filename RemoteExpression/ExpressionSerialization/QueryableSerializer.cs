using ExpressionSerialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Xml.Linq;

namespace ExpressionSerialization
{
    public static class QueryableSerializer
    {
        //static ExpressionSerializer serializer = new ExpressionSerializer();

        public static XElement Serialize(this IQueryable q)
        {
            return new ExpressionSerializer().Serialize(q);
        }

        public static XElement Serialize(this Expression x)
        {
            return new ExpressionSerializer().Serialize(x);
        }

        public static IQueryable Deserialize(this IQueryable q, XElement xmlQuery, IDataSourceResolver customResolver)
        {
            var expr = new ExpressionSerializer().Deserialize(xmlQuery, q);
            return q.Provider.CreateQuery(expr);
        }

        public static IQueryable Deserialize(this IQueryable q, XElement xmlQuery)
        {
            return q.Deserialize(xmlQuery, null);
        }

        public static Expression Deserialize(XElement xmlQuery)
        {
            return new ExpressionSerializer().Deserialize(xmlQuery);
        }

        public static LambdaExpression Deserialize(this XElement xmlQuery, IDataSourceResolver customResolver)
        {            
            var expr = new ExpressionSerializer().Deserialize(xmlQuery, customResolver);
            return Expression.Lambda(expr);
            ///return expr;
            /*var type = expr.Type;
            var itemSrcType = type;

            if (type.IsGenericType)
            {
                itemSrcType = type.GetGenericArguments()[0];
            }

            return ProxyCollection<object>().Provider.CreateQuery(expr);*/
        }
    }
}
