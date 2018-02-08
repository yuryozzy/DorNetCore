using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Xml.Linq;

namespace ExpressionSerialization
{
    public abstract class CustomExpressionXmlConverter
    {
        public abstract Expression Deserialize(XElement expressionXml);
        public abstract XElement Serialize(Expression expression);
        public abstract IQueryable QueryKind { get; }
    }
}
