using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ExpressionSerialization
{
    public static class ExpressionSignatureExt
    {
        class ExpressionSignature : ExpressionVisitor
        {
            List<object> _signatureSources = new List<object>();
            int _depth = 0;
            Dictionary<string, int> _names = new Dictionary<string, int>();


            public ExpressionSignature(Expression expression)
            {
                GetSignature(expression);
            }

            public Guid Hash
            {
                get
                {
                    using (var md5 = new MD5CryptoServiceProvider())
                    {
                        return new Guid(md5.ComputeHash(_signatureSources.Aggregate((s1, s2) => s1.ToString() + s2.ToString()).ToString().ToCharArray().Select(chr => (byte)chr).ToArray()));
                    }
                }
            }

            public override Expression Visit(Expression node)
            {
                _depth++;

                if (node != null)
                {
                    _signatureSources.Add(_depth);
                    _signatureSources.Add(node.NodeType);

                }

                var ret = base.Visit(node);
                _depth--;
                return ret;
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                _signatureSources.Add(node.Member.MemberType);
                _signatureSources.Add(node.Member.DeclaringType.FullName);
                _signatureSources.Add(node.Member.Name);
                return base.VisitMember(node);
            }

            protected override Expression VisitConstant(ConstantExpression node)
            {
                _signatureSources.Add(node.Value.GetType().FullName);
                _signatureSources.Add(node.Value.ToString());
                return base.VisitConstant(node);
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                _signatureSources.Add(node.Type.FullName);
                if (!_names.ContainsKey(node.Name))
                {
                    _names.Add(node.Name, _names.Count);
                }
                _signatureSources.Add(_names[node.Name]);
                return base.VisitParameter(node);
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                _signatureSources.Add(node.Method.ReturnType.FullName);
                _signatureSources.Add(node.Method.DeclaringType.FullName);
                _signatureSources.Add(node.Method.Name);
                _signatureSources.AddRange(node.Method.GetGenericArguments().Select(arg => arg.FullName));
                _signatureSources.AddRange(node.Arguments.Select(arg => arg.Type.FullName));
                return base.VisitMethodCall(node);
            }

            protected object[] GetSignature(Expression expression)
            {
                Visit(expression);
                return _signatureSources.ToArray();
            }
        }

        public static Guid Signature(this Expression expression)
        {
            return new ExpressionSignature(expression).Hash;
        }
    }
}
