void Main()
{
	var objs = new List<Test>(){
			 new Test(){Id=1,Son = new Test(){Id=9}}
			,new Test(){Id=2}
			,new Test(){Id=3}
	}.AsQueryable();
	var ans = SearchString(objs,new List<string>(){"9","2"});
	ans.Dump();
}

ExpressionMethodExtensions eme = new ExpressionMethodExtensions();
public IQueryable<T> SearchString<T>(IQueryable<T> source, List<string> searchStringInfo, List<string> noSearchProperty = null,int recursiveMax = 1) where T : class, new()
{
    if (searchStringInfo != null)
    {
		var searchs = searchStringInfo;
        var expression = GetSelectExpression(typeof(T), searchs,noSearchProperty,recursiveMax);
		if(expression != null)
		{	
			source = source.Where(expression as Expression<Func<T,bool>>);
		}
	}
	return source;
}

Expression GetSelectExpression(Type entityType, List<string> searchs,List<string> noSearchProperty = null,int recursiveMax = 1,int recursiveN = 0) 
{
	var needFindField = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance).AsEnumerable();
	if (noSearchProperty != null) 
	{
		needFindField = needFindField.Where(o => !noSearchProperty.Contains(o.Name));
	}
	List<Expression> AllExpressionCalls = new List<Expression>();
	var entity = Expression.Parameter(entityType,$"o{recursiveN}");
	foreach(var property in needFindField)
	{
		Expression expression = null;
		var member = Expression.Property(entity,property);
		var propertyType = property.PropertyType;
		var genericType = propertyType.GenericTypeArguments;
		if(propertyType.IsValueType || propertyType == typeof(string)) //數值字串型
		{
			expression = SelectAndConvert(member,propertyType,searchs) ;
		}
		else if(genericType.Count() == 0 && !propertyType.IsArray)//class
		{
			if(recursiveMax > recursiveN)
			{
				List<Expression> classExpression = new List<Expression>();
				foreach (var proprty in propertyType.GetProperties())
	            {
	                var classMember = Expression.Property(member, proprty);
					var classType = proprty.PropertyType;
					if(classType.IsValueType || classType == typeof(string))
					{
						var classex = SelectAndConvert(classMember,classType,searchs) ;
						classExpression.Add(classex);
					}
	            }
	            if (classExpression.Count > 0) 
	            {
	                expression =OrElseExpression(classExpression);
	            }
			}
		}
		else if(recursiveMax > recursiveN)//enum
		{
			
			Expression lambda = null;			
			var enumOfType = genericType.FirstOrDefault() ?? propertyType.GetElementType();
			//列表型
			if(enumOfType.IsValueType || enumOfType == typeof(string))
			{
				var theEntity = Expression.Parameter(enumOfType,$"o{recursiveN + 1}");
				var ans = SelectAndConvert(theEntity,enumOfType,searchs);
				lambda = Expression.Lambda(ans , theEntity);
			}
			else//class型 做遞迴
			{
				lambda = GetSelectExpression(enumOfType, searchs, noSearchProperty, recursiveMax, recursiveN + 1);
			}	
			expression = ElemetAny(member,enumOfType,(LambdaExpression)lambda);
		}

		if (expression != null)
		{
			if(!(propertyType.IsValueType && genericType.Length == 0)) //非純數值
			{
				expression = AddIsNotNull(member,expression);
			}
			
			AllExpressionCalls.Add(expression);
		}
	}
	if(AllExpressionCalls.Count > 0) 
	{
		var ans = OrElseExpression(AllExpressionCalls);
		return Expression.Lambda(ans, entity);
	}
	return null;
}
private Expression SelectAndConvert(Expression member,Type type,List<string> searchs)
{
	if(type != typeof(string))
	{
		member = ConvertString(member);
	}
	var ans = OrElseExpression(ExpressionContains(member,searchs));
	return ans;
}


private Expression ElemetAny(MemberExpression thisMember,Type thisType,LambdaExpression lambda)
{
	var method = eme.Any.MakeGenericMethod(thisType);
	var result = Expression.Call(method,thisMember,lambda);
	return result;
}

private List<Expression> ExpressionContains(Expression member,List<string> searchs) 
{
	var eme = new ExpressionMethodExtensions();
    var ans =searchs.Select(qt =>
	            Expression.Call(
                   Expression.Call(member, eme.ToLower),
                    eme.Contains,
                    Expression.Constant(qt, typeof(string))
    	        )
    		).ToList<Expression>(); 
	return ans;
}

//加不為Null
private Expression AddIsNotNull(MemberExpression member,Expression addExpression)
{
	if(addExpression ==null)
	{
		return addExpression;
	}
	var notEqual = Expression.NotEqual(member, Expression.Constant(null));
	var result = Expression.AndAlso(notEqual, addExpression);
	return result;
}
//轉成String
Expression ConvertString(Expression member)
{
	if(member == null)
	{
		return null;
	}
	var ans = Expression.Convert(Expression.Call(member, eme.ToString),typeof(string));
    return ans;
}
//將Expression集合 用||合併
private Expression OrElseExpression(List<Expression> exs)
{
	if(exs.Count == 0)
	{
	   return null;
	}
    var expression = exs[0];
    for (var i = 1; i < exs.Count; i++)
    {
        expression = Expression.OrElse(expression, exs[i]);
    }
    return expression;
}
// Define other methods, classes and namespaces here
public class Test
{
	public Test Son {get;set;}
	public int Id {get;set;}
	public List<Test2> sons2{get;set;}
}

public class Test2
{
	public string Name {get;set;}
}

    /// <summary>
    /// 取得Lambda 內的Method
    /// </summary>
    public class ExpressionMethodExtensions
    {
        private MethodInfo _ContainsMethod;
        public MethodInfo Contains
        {
            get
            {
                if (_ContainsMethod is null)
                {
                    _ContainsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                }
                return _ContainsMethod;
            }
        }
        private MethodInfo _AnyMethod;
        public MethodInfo Any
        {
            get
            {
                if (_AnyMethod is null)
                {
                    _AnyMethod = typeof(Enumerable).GetMethods().Single(m => m.Name == "Any" && m.GetParameters().Length == 2);
                }
                return _AnyMethod;
            }
        }

        private MethodInfo _ToLowerMethod;
        public MethodInfo ToLower
        {
            get
            {
                if (_ToLowerMethod is null)
                {
                    _ToLowerMethod = typeof(string).GetMethod("ToLower", new Type[] { });
                }
                return _ToLowerMethod;
            }
        }

        private MethodInfo _ToStringMethod;
        public new MethodInfo ToString
        {
            get
            {
                if (_ToStringMethod is null)
                {
                    _ToStringMethod = typeof(object).GetMethod("ToString");
                }
                return _ToStringMethod;
            }
        }

    }
