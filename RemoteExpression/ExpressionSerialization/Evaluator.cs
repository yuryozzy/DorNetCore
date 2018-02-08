using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ExpressionSerialization
{
    public static class Evaluator
    {
        public class ParameterSubstitutor : ExpressionVisitor
        {
            ParameterExpression _original;
            Expression _substitute;

            public ParameterSubstitutor(ParameterExpression originalExpression, Expression newExpression)
            {
                _original = originalExpression;
                _substitute = newExpression;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == _original)
                {
                    return _substitute;
                }
                return base.VisitParameter(node);
            }
        }

        public class EvaluationContext
        {
            public LambdaExpression OriginalExpression { get; set; }
            public Expression Expression { get; set; }
            public int[] NodeNums { get; set; }

            class ParameterEvaluator : ExpressionVisitor
            {
                int nodeNum;
                int[] _nodeNums;
                int currentNode = 0;
                public object[] _parameters;

                public ParameterEvaluator(int[] nodeNums, Expression expr)
                {
                    _nodeNums = nodeNums;
                    _parameters = new object[_nodeNums.Length];
                    Visit(expr);
                }

                public override Expression Visit(Expression node)
                {
                    nodeNum++;

                    if (currentNode >= _nodeNums.Length) return node;

                    if (nodeNum == _nodeNums[currentNode])
                    {
                        var lam = Expression.Lambda(node);
                        var fun = lam.Compile();
                        var param = fun.DynamicInvoke();
                        _parameters[currentNode++] = param;
                        return node;
                    }

                    return base.Visit(node);
                }
            }

            public object[] this[LambdaExpression expression]
            {
                get
                {
                    return OriginalExpression.Parameters.Select(item => default(object))
                        .Concat(new ParameterEvaluator(NodeNums, expression)._parameters).ToArray();
                }
            }
        }

        /// <summary>
        /// Performs evaluation & replacement of independent sub-trees
        /// </summary>
        /// <param name="expression">The root of the expression tree.</param>
        /// <param name="fnCanBeEvaluated">A function that decides whether a given expression node can be part of the local function.</param>
        /// <returns>A new tree with sub-trees evaluated and replaced.</returns>
        public static Expression PartialEval(Expression expression, Func<Expression, bool> fnCanBeEvaluated)
        {
            var evaluator = new SubtreeEvaluator(new Nominator(fnCanBeEvaluated).Nominate(expression));
            var newExpression = evaluator.Eval(expression);
            return newExpression;
        }

        public static EvaluationContext GetEvaluationContext(LambdaExpression expression)
        {
            var evaluator = new SubtreeEvaluator(new Nominator(Evaluator.CanBeEvaluatedLocally).Nominate(expression));
            var newExpression = evaluator.Eval(expression);
            var nodeNums = evaluator._substituteList.Select(item => item.Item2).ToArray();
            return new EvaluationContext { 
                OriginalExpression = expression, 
                Expression = newExpression, 
                NodeNums = nodeNums };
        }

        /// <summary>
        /// Performs evaluation & replacement of independent sub-trees
        /// </summary>
        /// <param name="expression">The root of the expression tree.</param>
        /// <returns>A new tree with sub-trees evaluated and replaced.</returns>
        public static Expression PartialEval(Expression expression)
        {
            return PartialEval(expression, Evaluator.CanBeEvaluatedLocally);
        }

        private static bool CanBeEvaluatedLocally(Expression expression)
        {
            return expression.NodeType != ExpressionType.Parameter /*&& expression.NodeType != ExpressionType.Quote*/;
        }

        /// <summary>
        /// Evaluates & replaces sub-trees when first candidate is reached (top-down)
        /// </summary>
        class SubtreeEvaluator : ExpressionVisitor
        {
            HashSet<Expression> candidates;
            public List<Tuple<ParameterExpression, int>> _substituteList = new List<Tuple<ParameterExpression, int>>();
            int depth = 0;
            int nodeNum = 0;
            LambdaExpression _original;

            //public object[] Parameters //mutable part, may be used for passing arguments remotely
            //{
            //    get
            //    {
            //        return _original.Parameters.Select(item => (object)null).Concat(_substituteList.Select(item => item.Item2.DynamicInvoke())).ToArray();
            //    }
            //}

            internal SubtreeEvaluator(HashSet<Expression> candidates)
            {
                this.candidates = candidates;
            }

            internal Expression Eval(Expression exp)
            {
                return this.Visit(exp);
            }

            public override Expression Visit(Expression exp)
            {
                nodeNum++;
                if (exp == null)
                {
                    return null;
                }

                if (depth == 0)
                {
                    _original = (LambdaExpression)exp;
                }

                depth++;
                if (this.candidates.Contains(exp))
                {
                    depth--;
                    return this.Evaluate(exp);
                }
                var ret = base.Visit(exp);
                depth--;

                if (depth == 0 && _substituteList.Count > 0)
                {
                    var newLambda = (LambdaExpression)ret;
                    ret = Expression.Lambda(newLambda.Body, _original.Parameters.Concat(_substituteList.Select(elem => elem.Item1)));
                }

                return ret;
            }

            private Expression Evaluate(Expression e)
            {
                if (e.NodeType == ExpressionType.Constant || e.NodeType == ExpressionType.Call)
                {
                    return e;
                }

                //LambdaExpression lambda = Expression.Lambda(e);
                //Delegate fn = lambda.Compile();

                //var parameter = Expression.Parameter(e.Type);
                var parameter = Expression.Parameter(e.Type, string.Format("magic{0}", _substituteList.Count));

                _substituteList.Add(new Tuple<ParameterExpression,int>(parameter, nodeNum));

                return parameter;

                /*                LambdaExpression lambda = Expression.Lambda(e);
                                Delegate fn = lambda.Compile();
                                return Expression.Constant(fn.DynamicInvoke(null), e.Type);*/
            }
        }

        /// <summary>
        /// Performs bottom-up analysis to determine which nodes can possibly
        /// be part of an evaluated sub-tree.
        /// </summary>
        class Nominator : ExpressionVisitor
        {
            Func<Expression, bool> fnCanBeEvaluated;
            HashSet<Expression> candidates;
            bool cannotBeEvaluated;

            internal Nominator(Func<Expression, bool> fnCanBeEvaluated)
            {
                this.fnCanBeEvaluated = fnCanBeEvaluated;
            }

            internal HashSet<Expression> Nominate(Expression expression)
            {
                this.candidates = new HashSet<Expression>();
                this.Visit(expression);
                return this.candidates;
            }

            public override Expression Visit(Expression expression)
            {
                if (expression != null)
                {
                    bool saveCannotBeEvaluated = this.cannotBeEvaluated;
                    this.cannotBeEvaluated = false;
                    base.Visit(expression);
                    if (!this.cannotBeEvaluated)
                    {
                        if (this.fnCanBeEvaluated(expression))
                        {
                            this.candidates.Add(expression);
                        }
                        else
                        {
                            this.cannotBeEvaluated = true;
                        }
                    }
                    this.cannotBeEvaluated |= saveCannotBeEvaluated;
                }
                return expression;
            }
        }
    }
}
