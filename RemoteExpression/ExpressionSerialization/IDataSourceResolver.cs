using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace ExpressionSerialization
{
    public interface IDataSourceResolver
    {
        object ResolveValue(XElement xml);
        IQueryable ResolveDataSource(Type itemType);
    }
}
