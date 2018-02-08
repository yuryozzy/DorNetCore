using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml.Linq;
using System.Web;
using System.Text;
using System.ComponentModel;

namespace ExpressionSerialization
{
    public class ExpressionSerializer
    {
        private static readonly Type[] attributeTypes = new[] { typeof(Single), typeof(string), typeof(char), typeof(int), typeof(bool), typeof(Guid), typeof(ExpressionType)/*, typeof(TypeIndex) */};
        private Dictionary<string, ParameterExpression> parameters = new Dictionary<string, ParameterExpression>();
        private ExpressionSerializationTypeResolver resolver;
        //public List<CustomExpressionXmlConverter> Converters { get; private set; }

        IDataSourceResolver _customResolver;

        /*public ExpressionSerializer(ExpressionSerializationTypeResolver resolver)
        {
            this.resolver = resolver;
            Converters = new List<CustomExpressionXmlConverter>();
        }*/

        public ExpressionSerializer()
        {
            this.resolver = new ExpressionSerializationTypeResolver();
            //Converters = new List<CustomExpressionXmlConverter>();
        }



        /*
         * SERIALIZATION 
         */
        public static string ConvertStringToHex(String input, System.Text.Encoding encoding)
        {
            Byte[] stringBytes = encoding.GetBytes(input);
            StringBuilder sbBytes = new StringBuilder(stringBytes.Length * 2);
            foreach (byte b in stringBytes)
            {
                sbBytes.AppendFormat("{0:X2}", b);
            }
            return "h" + sbBytes.ToString();
        }

        public static string ConvertHexToString(String hexInput, System.Text.Encoding encoding)
        {
            int numberChars = hexInput.Length - 1;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hexInput.Substring(i + 1, 2), 16);
            }
            return encoding.GetString(bytes);
        }

        public XElement Serialize(IQueryable q)
        {
            return Serialize(q.Expression);
        }

        public XElement Serialize(Expression e)
        {
            return GenerateXmlFromExpressionCore(e);
            //return GenerateXmlFromExpressionCore(e);
        }

        private XElement GenerateXmlFromExpressionCore(Expression e)
        {
            if (e == null)
                return null;

            //XElement replace = ApplyCustomConverters(e);

            //if (replace != null)
            //{
            //    /*replace.Add(from prop in e.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            //                select GenerateXmlFromProperty(prop.PropertyType, prop.Name, prop.GetValue(e, null)));*/
            //    return replace;
            //}

            return new XElement("ExpressionElement",
                        from prop in e.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        select GenerateXmlFromProperty(prop.PropertyType, prop.Name, prop.GetValue(e, null)),
                        new XAttribute("ExpressionName", GetNameOfExpression(e)));
        }

        /*private XElement ApplyCustomConverters(Expression e)
        {
            foreach (var converter in Converters)
            {
                if (converter == null) continue;
                XElement result = converter.Serialize(e);
                if (result != null)
                    return result;
            }
            return null;
        }
        */
        private string GetNameOfExpression(Expression e)
        {
            if (e is LambdaExpression)
                return "LambdaExpression";
            return e.GetType().Name;
        }

        private object GenerateXmlFromObjectCollection(object value)
        {
            var collection = value as ICollection;
            throw new NotImplementedException();
        }

        private object GenerateXmlFromProperty(Type propType, string propName, object value)
        {
            //System.Console.Out.WriteLine("GenerateXmlFromProperty {0} - {1}", propType, propName);
            if (attributeTypes.Contains(propType))
                return GenerateXmlFromPrimitive(propName, value);
            if (propType.Equals(typeof(object)))
                return GenerateXmlFromObject(propName, value);
            if (propType.Name == "MdFieldInfo")
                return GenerateXmlFromRtFieldInfo(propName, value);
            if (propType.Name == "RtFieldInfo")
                    return GenerateXmlFromRtFieldInfo(propName, value);
            if (typeof(Expression).IsAssignableFrom(propType))
                return GenerateXmlFromExpression(propName, value as Expression);
            if (value is MethodInfo || propType.Equals(typeof(MethodInfo)))
                return GenerateXmlFromMethodInfo(propName, value as MethodInfo);
            if (value is PropertyInfo || propType.Equals(typeof(PropertyInfo)))
                return GenerateXmlFromPropertyInfo(propName, value as PropertyInfo);
            if (value is FieldInfo || propType.Equals(typeof(FieldInfo)))
                return GenerateXmlFromFieldInfo(propName, value as FieldInfo);
            if (value is ConstructorInfo || propType.Equals(typeof(ConstructorInfo)))
                return GenerateXmlFromConstructorInfo(propName, value as ConstructorInfo);
            if (propType.Equals(typeof(Type)))
                return GenerateXmlFromType(propName, value as Type);
            if (IsIEnumerableOf<Expression>(propType))
                return GenerateXmlFromExpressionList(propName, AsIEnumerableOf<Expression>(value));
            if (IsIEnumerableOf<MemberInfo>(propType))
                return GenerateXmlFromMemberInfoList(propName, AsIEnumerableOf<MemberInfo>(value));
            if (IsIEnumerableOf<ElementInit>(propType))
                return GenerateXmlFromElementInitList(propName, AsIEnumerableOf<ElementInit>(value));
            if (IsIEnumerableOf<MemberBinding>(propType))
                return GenerateXmlFromBindingList(propName, AsIEnumerableOf<MemberBinding>(value));
            if (propType.GetInterfaces().Any(iface => iface == typeof(ICollection)))
                return GenerateXmlFromObjectCollection(value);
            return GenerateXmlFromObject(propName, value);
        }

        private object GenerateXmlFromObject(string propName, object value)
        {
            object result = null;
            if(value == null)
            {
                return new XElement(propName, value);
            }
            else if (value is Type)
                result = GenerateXmlFromTypeCore((Type)value);
            else if (value is Expression)
                result = GenerateXmlFromExpressionCore((Expression)value);
            //if value is expression.... convert as expression
            if (result == null)
            {
                //result = value.ToString();
                var type = value.GetType();
                if (type.IsEnum)
                {
                    var strVal = TypeDescriptor.GetConverter(type).ConvertToInvariantString(value);
                    return new XElement(propName, strVal);
                }
                if (attributeTypes.Contains(type))
                {
                    return new XElement(propName, value.ToString());
                }
                var members = value.GetType().GetMembers().OfType<FieldInfo>().ToArray();

                //result = GenerateXmlFromMemberInfoList(propName, members);
                var subXml = from member in members select new XElement("FieldInfo", new object[] { new XAttribute("Name", member.Name), GenerateXmlFromProperty(member.GetType(), "Value", member.GetValue(value)) });

                result = new XElement(propName, subXml);
                return result;
            }

            return new XElement(propName,
                result);
        }

        private bool IsIEnumerableOf<T>(Type propType)
        {
            if (!propType.IsGenericType)
                return false;
            Type[] typeArgs = propType.GetGenericArguments();
            if (typeArgs.Length != 1)
                return false;
            if (!typeof(T).IsAssignableFrom(typeArgs[0]))
                return false;
            if (!typeof(IEnumerable<>).MakeGenericType(typeArgs).IsAssignableFrom(propType))
                return false;
            return true;
        }

        private IEnumerable<T> AsIEnumerableOf<T>(object value)
        {
            if (value == null)
                return null;
            return (value as IEnumerable).Cast<T>();
        }

        private object GenerateXmlFromElementInitList(string propName, IEnumerable<ElementInit> initializers)
        {
            if (initializers == null)
                initializers = new ElementInit[] { };
            return new XElement(propName,
                from elementInit in initializers
                select GenerateXmlFromElementInitializer(elementInit));
        }

        private object GenerateXmlFromElementInitializer(ElementInit elementInit)
        {
            return new XElement("ElementInit",
                GenerateXmlFromMethodInfo("AddMethod", elementInit.AddMethod),
                GenerateXmlFromExpressionList("Arguments", elementInit.Arguments));
        }

        private object GenerateXmlFromExpressionList(string propName, IEnumerable<Expression> expressions)
        {
            return new XElement(propName,
                    from expression in expressions
                    select GenerateXmlFromExpressionCore(expression));
        }

        private object GenerateXmlFromMemberInfoList(string propName, IEnumerable<MemberInfo> members)
        {
            if (members == null)
                members = new MemberInfo[] { };
            return new XElement(propName,
                   from member in members
                   select GenerateXmlFromProperty(member.GetType(), "Info", member));
        }

        private object GenerateXmlFromBindingList(string propName, IEnumerable<MemberBinding> bindings)
        {
            if (bindings == null)
                bindings = new MemberBinding[] { };
            return new XElement(propName,
                from binding in bindings
                select GenerateXmlFromBinding(binding));
        }

        private object GenerateXmlFromBinding(MemberBinding binding)
        {
            switch (binding.BindingType)
            {
                case MemberBindingType.Assignment:
                    return GenerateXmlFromAssignment(binding as MemberAssignment);
                case MemberBindingType.ListBinding:
                    return GenerateXmlFromListBinding(binding as MemberListBinding);
                case MemberBindingType.MemberBinding:
                    return GenerateXmlFromMemberBinding(binding as MemberMemberBinding);
                default:
                    throw new NotSupportedException(string.Format("Binding type {0} not supported.", binding.BindingType));
            }
        }

        private object GenerateXmlFromMemberBinding(MemberMemberBinding memberMemberBinding)
        {
            return new XElement("MemberMemberBinding",
                GenerateXmlFromProperty(memberMemberBinding.Member.GetType(), "Member", memberMemberBinding.Member),
                GenerateXmlFromBindingList("Bindings", memberMemberBinding.Bindings));
        }


        private object GenerateXmlFromListBinding(MemberListBinding memberListBinding)
        {
            return new XElement("MemberListBinding",
                GenerateXmlFromProperty(memberListBinding.Member.GetType(), "Member", memberListBinding.Member),
                GenerateXmlFromProperty(memberListBinding.Initializers.GetType(), "Initializers", memberListBinding.Initializers));
        }

        private object GenerateXmlFromAssignment(MemberAssignment memberAssignment)
        {
            return new XElement("MemberAssignment",
                GenerateXmlFromProperty(memberAssignment.Member.GetType(), "Member", memberAssignment.Member),
                GenerateXmlFromProperty(memberAssignment.Expression.GetType(), "Expression", memberAssignment.Expression));
        }

        private XElement GenerateXmlFromExpression(string propName, Expression e)
        {
            return new XElement(propName, GenerateXmlFromExpressionCore(e));
        }

        private object GenerateXmlFromType(string propName, Type type)
        {
            return new XElement(propName, GenerateXmlFromTypeCore(type));
        }

        private XElement GenerateXmlFromTypeCore(Type type)
        {
            //vsadov: add detection of VB anon types
            if (type.Name.StartsWith("<>f__") || type.Name.StartsWith("VB$AnonymousType"))
                return new XElement("AnonymousType",
                    new XAttribute("Name", type.FullName),
                    from property in type.GetProperties()
                    select new XElement("Property",
                        new XAttribute("Name", property.Name),
                        GenerateXmlFromTypeCore(property.PropertyType)),
                    new XElement("Constructor",
                            from parameter in type.GetConstructors().First().GetParameters()
                            select new XElement("Parameter",
                                new XAttribute("Name", parameter.Name),
                                GenerateXmlFromTypeCore(parameter.ParameterType))
                    ));

            else
            {
                //vsadov: GetGenericArguments returns args for nongeneric types 
                //like arrays no need to save them.
                if (type.IsGenericType)
                {
                    return new XElement("Type",
                                            new XAttribute("Name", type.GetGenericTypeDefinition().FullName),
                                            from genArgType in type.GetGenericArguments()
                                            select GenerateXmlFromTypeCore(genArgType));
                }
                else
                {
                    return new XElement("Type", new XAttribute("Name", type.FullName));
                }

            }
        }

        private object GenerateXmlFromPrimitive(string propName, object value)
        {
            return new XAttribute(propName, value ?? "null");
        }

        private object GenerateXmlFromMethodInfo(string propName, MethodInfo methodInfo)
        {
            if (methodInfo == null)
                return new XElement(propName);
            return new XElement(propName,
                        new XAttribute("MemberType", methodInfo.MemberType),
                        new XAttribute("MethodName", methodInfo.Name),
                        GenerateXmlFromType("DeclaringType", methodInfo.DeclaringType),
                        new XElement("Parameters",
                            from param in methodInfo.GetParameters()
                            select GenerateXmlFromType("Type", param.ParameterType)),
                        new XElement("GenericArgTypes",
                            from argType in methodInfo.GetGenericArguments()
                            select GenerateXmlFromType("Type", argType)));
        }

        private object GenerateXmlFromPropertyInfo(string propName, PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
                return new XElement(propName);
            return new XElement(propName,
                        new XAttribute("MemberType", propertyInfo.MemberType),
                        new XAttribute("PropertyName", propertyInfo.Name),
                        GenerateXmlFromType("DeclaringType", propertyInfo.DeclaringType),
                        new XElement("IndexParameters",
                            from param in propertyInfo.GetIndexParameters()
                            select GenerateXmlFromType("Type", param.ParameterType)));
        }

        private object GenerateXmlFromFieldInfo(string propName, FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
                return new XElement(propName);
            return new XElement(propName,
                        new XAttribute("MemberType", fieldInfo.MemberType),
                        new XAttribute("FieldName", fieldInfo.Name),
                        GenerateXmlFromType("DeclaringType", fieldInfo.DeclaringType));
        }

        private object GenerateXmlFromRtFieldInfo(string propName, object fieldInfo)
        {
            if (fieldInfo == null)
                return new XElement(propName);

            var type = fieldInfo.GetType();
            if (attributeTypes.Contains(type))
            {
                return new XElement(propName, fieldInfo.ToString());
            }

            var members = fieldInfo.GetType().GetProperties().Where(m => m.GetType().Name == "RuntimePropertyInfo").ToArray();

            var r = new XElement(propName, 
                        from member in members
                        select GenerateXmlFromProperty((member.GetValue(fieldInfo, null) ?? "").GetType(), member.Name, member.GetValue(fieldInfo, null) ?? ""));
            return r;
        }

        private object GenerateXmlFromConstructorInfo(string propName, ConstructorInfo constructorInfo)
        {
            if (constructorInfo == null)
                return new XElement(propName);
            return new XElement(propName,
                        new XAttribute("MemberType", constructorInfo.MemberType),
                        new XAttribute("MethodName", constructorInfo.Name),
                        GenerateXmlFromType("DeclaringType", constructorInfo.DeclaringType),
                        new XElement("Parameters",
                            from param in constructorInfo.GetParameters()
                            select new XElement("Parameter",
                                new XAttribute("Name", param.Name),
                                GenerateXmlFromType("Type", param.ParameterType))));
        }


        /*
         * DESERIALIZATION 
         */

        Type _itemSourceType;
        object _dataSource;

        public Expression Deserialize(XElement xml, object dataSource)
        {
            if (typeof(IDataSourceResolver).IsAssignableFrom(dataSource.GetType()))
            {
                _customResolver = (IDataSourceResolver)dataSource;
            }
            else
            {
                _dataSource = dataSource;

                _itemSourceType = dataSource.GetType();
                if (_itemSourceType.IsGenericType)
                {
                    _itemSourceType = _itemSourceType.GetGenericArguments()[0];
                }
            }

            return Deserialize(xml);
        }


        public Expression Deserialize(XElement xml)
        {
            parameters.Clear();
            return ParseExpressionFromXmlNonNull(xml);
        }

        public Expression<TDelegate> Deserialize<TDelegate>(XElement xml)
        {
            Expression e = Deserialize(xml);
            if (e is Expression<TDelegate>)
                return e as Expression<TDelegate>;
            throw new Exception("xml must represent an Expression<TDelegate>");
        }

        private Expression ParseExpressionFromXml(XElement xml)
        {
            if (xml == null || xml.IsEmpty)
                return null;

            return ParseExpressionFromXmlNonNull(xml.Elements().First());
        }

        private Expression ParseExpressionFromXmlNonNull(XElement xml)
        {
            //Expression expression = ApplyCustomDeserializers(xml);
            //if (expression != null)
            //    return expression;

            var attr = xml.Attributes("ExpressionName").FirstOrDefault();
            var name = (attr == null) ? string.Empty : attr.Value;

            switch (name)
            {
                case "InstanceMethodCallExpressionN":
                    return ParseMethodCallExpressionFromXml(xml);
                case "SimpleBinaryExpression":
                    return ParseBinaryExpresssionFromXml(xml);
                case "MethodBinaryExpression":
                case "BinaryExpression":
                    return ParseBinaryExpresssionFromXml(xml);
                case "TypedConstantExpression":
                    return ParseTypedConstatExpressionFromXml(xml);
                case "ConstantExpression":
                    return ParseConstatExpressionFromXml(xml);
                case "TypedParameterExpression":
                case "ParameterExpression":
                    return ParseParameterExpressionFromXml(xml);
                case "LambdaExpression":
                    return ParseLambdaExpressionFromXml(xml);
                case "MethodCallExpression":
                case "MethodCallExpressionN":
                case "MethodCallExpression2":
                    return ParseMethodCallExpressionFromXml(xml);
                case "UnaryExpression":
                    return ParseUnaryExpressionFromXml(xml);
                case "PropertyExpression":
                case "MemberExpression":
                    return ParseMemberExpressionFromXml(xml);
                case "NewExpression":
                    return ParseNewExpressionFromXml(xml);
                case "ListInitExpression":
                    return ParseListInitExpressionFromXml(xml);
                case "MemberInitExpression":
                    return ParseMemberInitExpressionFromXml(xml);
                case "ConditionalExpression":
                    return ParseConditionalExpressionFromXml(xml);
                case "NewArrayExpression":
                    return ParseNewArrayExpressionFromXml(xml);
                case "TypeBinaryExpression":
                    return ParseTypeBinaryExpressionFromXml(xml);
                case "InvocationExpression":
                    return ParseInvocationExpressionFromXml(xml);
                case "LogicalBinaryExpression":
                    return ParseBinaryExpresssionFromXml(xml);
                case "FieldExpression":
                    return ParseMemberExpressionFromXml(xml);

                default:
                    {
                        if (name.StartsWith("PrimitiveParameterExpression"))
                        {
                            return ParseParameterExpressionFromXml(xml);
                        }
                        else
                            throw new NotSupportedException(name);
                    }
            }
        }

/*        private Expression ApplyCustomDeserializers(XElement xml)
        {
            foreach (var converter in Converters)
            {
                if (converter == null) continue;
                Expression result = converter.Deserialize(xml);
                if (result != null)
                    return result;
            }
            return null;
        }
        */
        private Expression ParseInvocationExpressionFromXml(XElement xml)
        {
            Expression expression = ParseExpressionFromXml(xml.Element("Expression"));
            var arguments = ParseExpressionListFromXml<Expression>(xml, "Arguments");
            return Expression.Invoke(expression, arguments);
        }

        private Expression ParseTypeBinaryExpressionFromXml(XElement xml)
        {
            Expression expression = ParseExpressionFromXml(xml.Element("Expression"));
            Type typeOperand = ParseTypeFromXml(xml.Element("TypeOperand"));
            return Expression.TypeIs(expression, typeOperand);
        }

        private Expression ParseNewArrayExpressionFromXml(XElement xml)
        {
            Type type = ParseTypeFromXml(xml.Element("Type"));
            if (!type.IsArray)
                throw new Exception("Expected array type");
            Type elemType = type.GetElementType();
            var expressions = ParseExpressionListFromXml<Expression>(xml, "Expressions");
            switch (xml.Attribute("NodeType").Value)
            {
                case "NewArrayInit":
                    return Expression.NewArrayInit(elemType, expressions);
                case "NewArrayBounds":
                    return Expression.NewArrayBounds(elemType, expressions);
                default:
                    throw new Exception("Expected NewArrayInit or NewArrayBounds");
            }
        }

        private Expression ParseConditionalExpressionFromXml(XElement xml)
        {
            Expression test = ParseExpressionFromXml(xml.Element("Test"));
            Expression ifTrue = ParseExpressionFromXml(xml.Element("IfTrue"));
            Expression ifFalse = ParseExpressionFromXml(xml.Element("IfFalse"));
            return Expression.Condition(test, ifTrue, ifFalse);
        }

        private Expression ParseMemberInitExpressionFromXml(XElement xml)
        {
            NewExpression newExpression = ParseNewExpressionFromXml(xml.Element("NewExpression").Element("NewExpression")) as NewExpression;
            var bindings = ParseBindingListFromXml(xml, "Bindings").ToArray();
            return Expression.MemberInit(newExpression, bindings);
        }



        private Expression ParseListInitExpressionFromXml(XElement xml)
        {
            NewExpression newExpression = ParseExpressionFromXml(xml.Element("NewExpression")) as NewExpression;
            if (newExpression == null) throw new Exception("Expceted a NewExpression");
            var initializers = ParseElementInitListFromXml(xml, "Initializers").ToArray();
            return Expression.ListInit(newExpression, initializers);
        }

        private Expression ParseNewExpressionFromXml(XElement xml)
        {
            ConstructorInfo constructor = ParseConstructorInfoFromXml(xml.Element("Constructor"));
            var arguments = ParseExpressionListFromXml<Expression>(xml, "Arguments").ToArray();
            var members = ParseMemberInfoListFromXml<MemberInfo>(xml, "Members").ToArray();
            if (members.Length == 0)
                return Expression.New(constructor, arguments);
            return Expression.New(constructor, arguments, members);
        }

        private Expression ParseMemberExpressionFromXml(XElement xml)
        {
            Expression expression = ParseExpressionFromXml(xml.Element("Expression"));
            MemberInfo member = ParseMemberInfoFromXml(xml.Element("Member"));
            return Expression.MakeMemberAccess(expression, member);
        }

        private MemberInfo ParseMemberInfoFromXml(XElement xml)
        {
            MemberTypes memberType = (MemberTypes)ParseConstantFromAttribute<MemberTypes>(xml, "MemberType");
            switch (memberType)
            {
                case MemberTypes.Field:
                    return ParseFieldInfoFromXml(xml);
                case MemberTypes.Property:
                    return ParsePropertyInfoFromXml(xml);
                case MemberTypes.Method:
                    return ParseMethodInfoFromXml(xml);
                case MemberTypes.Constructor:
                    return ParseConstructorInfoFromXml(xml);
                case MemberTypes.Custom:
                case MemberTypes.Event:
                case MemberTypes.NestedType:
                case MemberTypes.TypeInfo:
                default:
                    throw new NotSupportedException(string.Format("MEmberType {0} not supported", memberType));
            }

        }

        private MemberInfo ParseFieldInfoFromXml(XElement xml)
        {
            string fieldName = (string)ParseConstantFromAttribute<string>(xml, "FieldName");
            Type declaringType = ParseTypeFromXml(xml.Element("DeclaringType"));
            return declaringType.GetField(fieldName);
        }

        private MemberInfo ParsePropertyInfoFromXml(XElement xml)
        {
            string propertyName = (string)ParseConstantFromAttribute<string>(xml, "PropertyName");
            Type declaringType = ParseTypeFromXml(xml.Element("DeclaringType"));
            var ps = from paramXml in xml.Element("IndexParameters").Elements()
                     select ParseTypeFromXml(paramXml);
            return declaringType.GetProperty(propertyName, ps.ToArray());
        }

        private Expression ParseUnaryExpressionFromXml(XElement xml)
        {
            Expression operand = ParseExpressionFromXml(xml.Element("Operand"));
            MethodInfo method = ParseMethodInfoFromXml(xml.Element("Method"));
            var isLifted = (bool)ParseConstantFromAttribute<bool>(xml, "IsLifted");
            var isLiftedToNull = (bool)ParseConstantFromAttribute<bool>(xml, "IsLiftedToNull");
            var expressionType = (ExpressionType)ParseConstantFromAttribute<ExpressionType>(xml, "NodeType");
            var type = ParseTypeFromXml(xml.Element("Type"));
            // TODO: Why can't we use IsLifted and IsLiftedToNull here?  
            // May need to special case a nodeType if it needs them.
            return Expression.MakeUnary(expressionType, operand, type, method);
        }

        private Expression ParseMethodCallExpressionFromXml(XElement xml)
        {
            Expression instance = ParseExpressionFromXml(xml.Element("Object"));
            MethodInfo method = ParseMethodInfoFromXml(xml.Element("Method"));
            var arguments = ParseExpressionListFromXml<Expression>(xml, "Arguments").ToArray();
            return Expression.Call(instance, method, arguments);
        }

        private Expression ParseLambdaExpressionFromXml(XElement xml)
        {
            var body = ParseExpressionFromXml(xml.Element("Body"));
            if (body == null)
            {
                if (_customResolver == null)
                    return null; // Expression.Constant(null, typeof(object));
                return 
                    (Expression) _customResolver.ResolveValue(xml);
            }
            var parameters = ParseExpressionListFromXml<ParameterExpression>(xml, "Parameters");
            var type = ParseTypeFromXml(xml.Element("Type"));
            // We may need to 
            //var lambdaExpressionReturnType = type.GetMethod("Invoke").ReturnType;
            //if (lambdaExpressionReturnType.IsArray)
            //{

            //    type = typeof(IEnumerable<>).MakeGenericType(type.GetElementType());
            //}
            return Expression.Lambda(type, body, parameters);
        }

        private IEnumerable<T> ParseExpressionListFromXml<T>(XElement xml, string elemName) where T : Expression
        {
            var xmlElem = xml.Element(elemName);
            return xmlElem == null? null : from tXml in xmlElem.Elements()
                   select (T)ParseExpressionFromXmlNonNull(tXml);
        }

        private IEnumerable<T> ParseMemberInfoListFromXml<T>(XElement xml, string elemName) where T : MemberInfo
        {
            return from tXml in xml.Element(elemName).Elements()
                   select (T)ParseMemberInfoFromXml(tXml);
        }

        private IEnumerable<ElementInit> ParseElementInitListFromXml(XElement xml, string elemName)
        {
            return from tXml in xml.Element(elemName).Elements()
                   select ParseElementInitFromXml(tXml);
        }

        private ElementInit ParseElementInitFromXml(XElement xml)
        {
            MethodInfo addMethod = ParseMethodInfoFromXml(xml.Element("AddMethod"));
            var arguments = ParseExpressionListFromXml<Expression>(xml, "Arguments");
            return Expression.ElementInit(addMethod, arguments);

        }

        private IEnumerable<MemberBinding> ParseBindingListFromXml(XElement xml, string elemName)
        {
            return from tXml in xml.Element(elemName).Elements()
                   select ParseBindingFromXml(tXml);
        }

        private MemberBinding ParseBindingFromXml(XElement tXml)
        {
            MemberInfo member = ParseMemberInfoFromXml(tXml.Element("Member"));
            switch (tXml.Name.LocalName)
            {
                case "MemberAssignment":
                    Expression expression = ParseExpressionFromXml(tXml.Element("Expression"));
                    return Expression.Bind(member, expression);
                case "MemberMemberBinding":
                    var bindings = ParseBindingListFromXml(tXml, "Bindings");
                    return Expression.MemberBind(member, bindings);
                case "MemberListBinding":
                    var initializers = ParseElementInitListFromXml(tXml, "Initializers");
                    return Expression.ListBind(member, initializers);
            }
            throw new NotImplementedException();
        }


        private Expression ParseParameterExpressionFromXml(XElement xml)
        {
            Type type = ParseTypeFromXml(xml.Element("Type"));
            string name = (string)ParseConstantFromAttribute<string>(xml, "Name");
            //vs: hack
            string id = name + type.FullName;
            if (!parameters.ContainsKey(id))
                parameters.Add(id, Expression.Parameter(type, name));
            return parameters[id];
        }

        private Expression ParseTypedConstatExpressionFromXml(XElement xml)
        {
            Type type = ParseTypeFromXml(xml.Element("Type"));
            var constant = ParseTypeFromXml(xml.Element("Value"));
            return Expression.Constant(constant, type);
        }

        private Expression ParseConstatExpressionFromXml(XElement xml)
        {
            Type type = ParseTypeFromXml(xml.Element("Type"));
            var constant = ParseConstantFromElement(xml, "Value", type);
            return Expression.Constant(constant, type);
        }

        private Type ParseTypeFromXml(XElement xml)
        {
            if (xml == null) return null;
            Debug.Assert(xml.Elements().Count() == 1);
            return ParseTypeFromXmlCore(xml.Elements().First());
        }

        private Type ParseTypeFromXmlCore(XElement xml)
        {
            switch (xml.Name.ToString())
            {
                case "Type":
                    return ParseNormalTypeFromXmlCore(xml);
                case "AnonymousType":
                    return ParseAnonymousTypeFromXmlCore(xml);
                default:
                    throw new ArgumentException("Expected 'Type' or 'AnonymousType'");
            }

        }

        private Type ParseNormalTypeFromXmlCore(XElement xml)
        {
            if (!xml.HasElements)
                return resolver.GetType(xml.Attribute("Name").Value);

            var genericArgumentTypes = from genArgXml in xml.Elements()
                                       select ParseTypeFromXmlCore(genArgXml);
            return resolver.GetType(xml.Attribute("Name").Value, genericArgumentTypes);
        }

        private Type ParseAnonymousTypeFromXmlCore(XElement xElement)
        {
            string name = xElement.Attribute("Name").Value;
            var properties = from propXml in xElement.Elements("Property")
                             select new ExpressionSerializationTypeResolver.NameTypePair
                             {
                                 Name = propXml.Attribute("Name").Value,
                                 Type = ParseTypeFromXml(propXml)
                             };
            var ctr_params = from propXml in xElement.Elements("Constructor").Elements("Parameter")
                             select new ExpressionSerializationTypeResolver.NameTypePair
                             {
                                 Name = propXml.Attribute("Name").Value,
                                 Type = ParseTypeFromXml(propXml)
                             };

            return resolver.GetOrCreateAnonymousTypeFor(name, properties.ToArray(), ctr_params.ToArray());
        }

        private Expression ParseBinaryExpresssionFromXml(XElement xml)
        {
            var expressionType = (ExpressionType)ParseConstantFromAttribute<ExpressionType>(xml, "NodeType"); ;
            var left = ParseExpressionFromXml(xml.Element("Left"));
            var right = ParseExpressionFromXml(xml.Element("Right"));
            var isLifted = (bool)ParseConstantFromAttribute<bool>(xml, "IsLifted");
            var isLiftedToNull = (bool)ParseConstantFromAttribute<bool>(xml, "IsLiftedToNull");
            var type = ParseTypeFromXml(xml.Element("Type"));
            var method = ParseMethodInfoFromXml(xml.Element("Method"));
            LambdaExpression conversion = ParseExpressionFromXml(xml.Element("Conversion")) as LambdaExpression;
            if (expressionType == ExpressionType.Coalesce)
                return Expression.Coalesce(left, right, conversion);
            return Expression.MakeBinary(expressionType, left, right, isLiftedToNull, method);
        }

        private MethodInfo ParseMethodInfoFromXml(XElement xml)
        {
            if (xml.IsEmpty)
                return null;
            string name = (string)ParseConstantFromAttribute<string>(xml, "MethodName");
            Type declaringType = ParseTypeFromXml(xml.Element("DeclaringType"));
            var ps = from paramXml in xml.Element("Parameters").Elements()
                     select ParseTypeFromXml(paramXml);
            var genArgs = from argXml in xml.Element("GenericArgTypes").Elements()
                          select ParseTypeFromXml(argXml);
            return resolver.GetMethod(declaringType, name, ps.ToArray(), genArgs.ToArray());
        }

        private ConstructorInfo ParseConstructorInfoFromXml(XElement xml)
        {
            if (xml.IsEmpty)
                return null;
            Type declaringType = ParseTypeFromXml(xml.Element("DeclaringType"));
            var ps = from paramXml in xml.Element("Parameters").Elements()
                     select ParseParameterFromXml(paramXml);
            ConstructorInfo ci = declaringType.GetConstructor(ps.ToArray());
            return ci;
        }

        private Type ParseParameterFromXml(XElement xml)
        {
            string name = (string)ParseConstantFromAttribute<string>(xml, "Name");
            Type type = ParseTypeFromXml(xml.Element("Type"));
            return type;

        }

        private object ParseConstantFromAttribute<T>(XElement xml, string attrName)
        {
            string objectStringValue = xml.Attribute(attrName).Value;
            if (typeof(Type).IsAssignableFrom(typeof(T)))
                throw new Exception("We should never be encoding Types in attributes now.");
            if (typeof(Enum).IsAssignableFrom(typeof(T)))
                return Enum.Parse(typeof(T), objectStringValue);
            return Convert.ChangeType(objectStringValue, typeof(T));
        }

        private object ParseConstantFromAttribute(XElement xml, string attrName, Type type)
        {
            string objectStringValue = xml.Attribute(attrName).Value;
            if (typeof(Type).IsAssignableFrom(type))
                throw new Exception("We should never be encoding Types in attributes now.");
            if (typeof(Enum).IsAssignableFrom(type))
                return Enum.Parse(type, objectStringValue);
            return Convert.ChangeType(objectStringValue, type);
        }

        private object ParseFieldInfo(XElement xml, Type type, object instance)
        {
            var fieldName = xml.Attribute("Name").Value.ToString();
            var prop = type.GetField(fieldName);

            if (attributeTypes.Contains(prop.FieldType))
            {
                prop.SetValue(instance, ParseConstantFromElement(xml, "Value", prop.FieldType));
            }
            else
            {
                var fieldValue = ParseLambdaExpressionFromXml(xml.Element("Value"));
                if(fieldValue != null)
                     prop.SetValue(instance, fieldValue);
            }
            return instance;
        }

        private object ParseConstantFromElement(XElement xml, string elemName, Type type)
        {
            if (xml.Element(elemName).Elements().Where(e => e.Name == "FieldInfo").Any())
            {
                var instance = Activator.CreateInstance(type);
                var elements = xml.Element(elemName).Elements().Where(e => e.Name == "FieldInfo").ToArray();
                foreach (var element in elements)
                {
                    ParseFieldInfo(element, type, instance);
                }
                //return from element in l select ParseFieldInfo(element);
                return instance;

            }

            var valueElement = xml.Element(elemName);

            if (valueElement.IsEmpty)
            {
                var elementType = type;
                if (elementType.IsGenericType)
                {
                    elementType = elementType.GetGenericArguments()[0];
                }

                var dataSource = _customResolver.ResolveDataSource(elementType);

                if (dataSource != null)
                    return dataSource;

                if (_dataSource != null && elementType.IsAssignableFrom(_itemSourceType))
                {
                    return _dataSource;
                }
            }

            string objectStringValue = xml.Element(elemName).Value;

            if (objectStringValue == string.Empty && type != typeof(string))
            {
                var xmlValue = valueElement.Elements().First();
                return ParseExpressionFromXmlNonNull(xmlValue);
            }

            if (typeof(Type).IsAssignableFrom(type))
                return ParseTypeFromXml(xml.Element("Value"));
            if (typeof(Enum).IsAssignableFrom(type))
                return Enum.Parse(type, objectStringValue);

            return TypeDescriptor.GetConverter(type).ConvertFromInvariantString(objectStringValue);
        }
    }

    //public interface IProxyMarker
    //{
    //}

    //public interface IProxyQueriable<T>:IQueryable<T>, IProxyMarker
    //{

    //}

    //class ProxyProvider:IQueryProvider
    //{
    //    IQueryProvider _pxy;

    //    public ProxyProvider(IQueryProvider pxy)
    //    {
    //        _pxy = pxy;
    //    }

    //    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    //    {
    //        return _pxy.CreateQuery<TElement>(expression);
    //    }

    //    public IQueryable CreateQuery(Expression expression)
    //    {
    //        return _pxy.CreateQuery(expression);
    //    }

    //    public TResult Execute<TResult>(Expression expression)
    //    {
    //        return _pxy.Execute<TResult>(expression);
    //    }

    //    public object Execute(Expression expression)
    //    {
    //        return _pxy.Execute(expression);
    //    }
    //}

    //class ProxyMarker<T>:IProxyQueriable<T>
    //{
    //    IQueryable<T> _pxy;
    //    IQueryProvider _provider;

    //    public ProxyMarker()
    //    {
    //        _pxy = Enumerable.Empty<T>().AsQueryable();
    //        _provider = new ProxyProvider(_pxy.Provider);
    //    }

    //    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    //    {
    //        return _pxy.GetEnumerator();
    //    }

    //    IEnumerator IEnumerable.GetEnumerator()
    //    {
    //        return _pxy.GetEnumerator();
    //    }

    //    Type IQueryable.ElementType
    //    {
    //        get { return _pxy.ElementType; }
    //    }

    //    Expression IQueryable.Expression
    //    {
    //        get { return _pxy.Expression; }
    //    }

    //    IQueryProvider IQueryable.Provider
    //    {
    //        get { return _provider; }
    //    }
    //}


}
