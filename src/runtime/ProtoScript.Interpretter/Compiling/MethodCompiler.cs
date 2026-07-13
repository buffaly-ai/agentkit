using BasicUtilities;
using Ontology;
using ProtoScript.Diagnostics;
using ProtoScript.Interpretter.Compiled;
using ProtoScript.Interpretter.RuntimeInfo;
using ProtoScript.Interpretter.Symbols;
using System.Threading.Tasks;

namespace ProtoScript.Interpretter.Compiling
{
	public class MethodCompiler
	{
		public static Compiled.Expression CompileMethodEvaluationInternal(MethodEvaluation methodEval, Compiled.Expression expression, Compiler compiler)
		{
			List<Compiled.Expression> lstParameters = new List<Compiled.Expression>();
			List<System.Type> lstParameterTypes = new List<System.Type>();

			object obj = expression.InferredType;
			string strMethod = methodEval.MethodName;

			//N20231031-01 - Removed to support this.From<CSharp.Code.Hidden.2D68F5D82DBB2F6AE40A3B22EFBC399E>()
			//if (strMethod.Contains("."))
			//	strMethod = StringUtil.RightOfLast(strMethod, ".");

			for (int i = 0; i < methodEval.Parameters.Count; i++)
			{
				Compiled.Expression exp = compiler.Compile(methodEval.Parameters[i]);
				if (null == exp)
				{
					compiler.AddDiagnostic(new Diagnostic("Unknown Parameter"), null, methodEval.Parameters[i]);
					return null;
				}

				exp.Info = methodEval.Parameters[i].Info;

				lstParameters.Add(exp);

				if (exp is LambdaOperator)
					lstParameterTypes.Add(typeof(Predicate<Prototype>));
				else if (exp is Compiled.Literal && (exp as Compiled.Literal).Value == null)
					lstParameterTypes.Add(null);
				else
					lstParameterTypes.Add(exp.InferredType.Type);
			}


			if (obj is PrototypeTypeInfo)
			{
				PrototypeTypeInfo typeInfo = obj as PrototypeTypeInfo;
				FunctionRuntimeInfo functionRuntimeInfo = ResolveMethod2(typeInfo.Prototype, strMethod, compiler.Symbols);

				if (null != functionRuntimeInfo)
				{
					FunctionEvaluation functionEvaluation = new FunctionEvaluation();
					functionEvaluation.Info = methodEval.Info;
					functionEvaluation.Parameters = lstParameters;

					//TODO: allow for function overloading here
					if (functionEvaluation.Parameters.Count != functionRuntimeInfo.Parameters.Count)
					{
						compiler.AddDiagnostic(new Diagnostic("Incorrect number of parameters"), null, methodEval);
						return null;
					}
					for (int i = 0; i < functionRuntimeInfo.Parameters.Count; i++)
					{
						ParameterRuntimeInfo destParam = functionRuntimeInfo.Parameters[i];
						Compiled.Expression exp = functionEvaluation.Parameters[i];

						//We should be able to pass null to a method
						//if (exp.InferredType == null)
						//{
						//	compiler.AddDiagnostic($"Parameter {destParam.ParameterName} is null", null, methodEval);
						//	return null;
						//}
						//else 
						if (!SimpleInterpretter.IsAssignableFrom(exp.InferredType, destParam.Type))
						{
							compiler.AddDiagnostic(new CannotConvert(exp.InferredType.ToString(), destParam.Type.ToString()), null, methodEval);
							return null;
						}

					}

					functionEvaluation.Function = functionRuntimeInfo;
					functionEvaluation.InferredType = functionRuntimeInfo.ReturnType;
					functionEvaluation.Object = expression;

					return functionEvaluation;
				}

				if (null != typeInfo.Generic && strMethod.Contains("<"))
				{
					functionRuntimeInfo = typeInfo.Scope.GetSymbol(strMethod) as FunctionRuntimeInfo;

					//Create an instance of the method from the generic 
					if (null == functionRuntimeInfo)
					{
						string strGenericMethod = StringUtil.LeftOfFirst(strMethod, "<") + "<>";
						string strGenericParameter = StringUtil.Between(strMethod, "<", ">");
						TypeInfo typeGenericParameter = compiler.Symbols.GetTypeInfo(strGenericParameter);

						FunctionRuntimeInfo genericInfo = typeInfo.Generic.Scope.GetSymbol(strGenericMethod) as FunctionRuntimeInfo;

						if (null != genericInfo)
						{
							functionRuntimeInfo = new FunctionRuntimeInfo();
							functionRuntimeInfo.ReturnType = genericInfo.ReturnType;
							functionRuntimeInfo.Scope = genericInfo.Scope;

							//Use the original name, so it can override later
							functionRuntimeInfo.FunctionName = strMethod;
							if (functionRuntimeInfo.ReturnType is GenericTypeInfo)
								functionRuntimeInfo.ReturnType = typeGenericParameter;

							typeInfo.Scope.Symbols[strMethod] = functionRuntimeInfo;
						}
					}

					if (null == functionRuntimeInfo)
					{
						compiler.AddDiagnostic(new Diagnostic("Could not find method: " + strMethod), null, methodEval);
						return null;
					}

					FunctionEvaluation functionEvaluation = new FunctionEvaluation();
					functionEvaluation.Info = methodEval.Info;
					functionEvaluation.Parameters = lstParameters;

					//TODO: allow for function overloading here
					if (functionEvaluation.Parameters.Count != functionRuntimeInfo.Parameters.Count)
					{
						compiler.AddDiagnostic(new Diagnostic("Incorrect number of parameters"), null, methodEval);
						return null;
					}
					for (int i = 0; i < functionRuntimeInfo.Parameters.Count; i++)
					{
						ParameterRuntimeInfo destParam = functionRuntimeInfo.Parameters[i];
						Compiled.Expression exp = functionEvaluation.Parameters[i];

						if (exp.InferredType == null)
						{
							compiler.AddDiagnostic($"Parameter {destParam.ParameterName} is null", null, methodEval);
							return null;
						}
						else if (!SimpleInterpretter.IsAssignableFrom(exp.InferredType, destParam.Type))
						{
							compiler.AddDiagnostic(new CannotConvert(exp.InferredType.ToString(), destParam.Type.ToString()), null, methodEval);
							return null;
						}

					}

					functionEvaluation.Function = functionRuntimeInfo;
					functionEvaluation.InferredType = functionRuntimeInfo.ReturnType;
					functionEvaluation.Object = expression;

					return functionEvaluation;
				}
			}

			{
				List<System.Type> receiverTypes = new List<System.Type>();
				if (obj is TypeInfo infoType)
				{
					IReadOnlyList<System.Type> candidateTypes = ValueConversions.GetDotNetReceiverTypes(infoType);
					for (int i = 0; i < candidateTypes.Count; i++)
					{
						System.Type candidateType = candidateTypes[i];
						if (!receiverTypes.Contains(candidateType))
							receiverTypes.Add(candidateType);
					}
				}
				else
				{
					receiverTypes.Add(obj.GetType());
				}

				System.Reflection.MethodInfo method = null;
				for (int i = 0; i < receiverTypes.Count; i++)
				{
					System.Type receiverType = receiverTypes[i];
					method = ReflectionUtil.GetMethod(receiverType, strMethod, lstParameterTypes);
					if (method == null)
						method = TryResolveRuntimeCompatibleMethod(receiverType, strMethod, lstParameterTypes);
					if (method != null)
						break;
				}

				if (null != method)
				{



					DotNetMethodEvaluation dotNetMethodEval = new DotNetMethodEvaluation();
					dotNetMethodEval.Info = methodEval.Info;
					dotNetMethodEval.Method = method;
					dotNetMethodEval.Parameters = lstParameters;
					dotNetMethodEval.Object = expression;
					dotNetMethodEval.InferredType = new TypeInfo(GetInferredReturnType(method.ReturnType));
					dotNetMethodEval.IsNullConditional = methodEval.IsNullConditional;

					return dotNetMethodEval;
				}

				for (int i = 0; i < receiverTypes.Count; i++)
				{
					System.Type receiverType = receiverTypes[i];
					method = receiverType.GetMethod(strMethod);
					if (null != method)
					{
						compiler.AddDiagnostic(
							new Diagnostic($"Method {strMethod} exists but parameters don't match"),
							null,
							GetDiagnosticExpression(methodEval, expression));
						return null;
					}
				}

			}

			compiler.AddDiagnostic(
				new Diagnostic($"Cannot find compatible method {strMethod} on object"),
				null,
				GetDiagnosticExpression(methodEval, expression));
			return null;
		}

		private static global::ProtoScript.Expression GetDiagnosticExpression(MethodEvaluation methodEval, Compiled.Expression expression)
		{
			if (methodEval.Info != null && !string.IsNullOrWhiteSpace(methodEval.Info.File))
			{
				return methodEval;
			}

			if (expression?.Info != null && !string.IsNullOrWhiteSpace(expression.Info.File))
			{
				Identifier fallbackExpression = new Identifier("_");
				fallbackExpression.Info = expression.Info;
				return fallbackExpression;
			}

			return methodEval;
		}

		private static System.Reflection.MethodInfo? TryResolveRuntimeCompatibleMethod(System.Type receiverType, string methodName, List<System.Type> argumentTypes)
		{
			System.Reflection.MethodInfo[] candidates = receiverType
				.GetMethods()
				.Where(m => m.Name == methodName)
				.ToArray();

			System.Reflection.MethodInfo? bestMethod = null;
			int bestScore = int.MinValue;

			for (int i = 0; i < candidates.Length; i++)
			{
				System.Reflection.MethodInfo candidate = candidates[i];
				System.Reflection.ParameterInfo[] parameters = candidate.GetParameters();
				bool hasParamArray = parameters.Length > 0 &&
					parameters[parameters.Length - 1].IsDefined(typeof(ParamArrayAttribute), inherit: false);
				int requiredCount = GetRequiredParameterCount(parameters, hasParamArray);
				if (argumentTypes.Count < requiredCount)
					continue;
				if (!hasParamArray && argumentTypes.Count > parameters.Length)
					continue;

				int score = 0;
				bool compatible = true;
				for (int j = 0; j < argumentTypes.Count; j++)
				{
					System.Type parameterType;
					bool isExpandedParamArrayElement = false;
					if (hasParamArray && j >= parameters.Length - 1)
					{
						System.Type? elementType = parameters[parameters.Length - 1].ParameterType.GetElementType();
						if (elementType == null)
						{
							compatible = false;
							break;
						}

						parameterType = elementType;
						isExpandedParamArrayElement = true;
					}
					else
					{
						parameterType = parameters[j].ParameterType;
					}

					System.Type? argumentType = argumentTypes[j];

					if (argumentType == null)
					{
						if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
						{
							compatible = false;
							break;
						}

						score += 1;
						if (isExpandedParamArrayElement)
							score += 1;
						continue;
					}

					if (parameterType.IsAssignableFrom(argumentType))
					{
						score += 3;
						if (isExpandedParamArrayElement)
							score += 1;
						continue;
					}

					System.Type? nullableUnderlying = Nullable.GetUnderlyingType(parameterType);
					if (nullableUnderlying != null &&
						(argumentType == nullableUnderlying || nullableUnderlying.IsAssignableFrom(argumentType)))
					{
						score += 2;
						if (isExpandedParamArrayElement)
							score += 1;
						continue;
					}

					// Allow runtime bridge from Prototype-typed expressions to CLR parameters.
					if (typeof(Prototype).IsAssignableFrom(argumentType))
					{
						score += 1;
						if (isExpandedParamArrayElement)
							score += 1;
						continue;
					}

					compatible = false;
					break;
				}

				if (compatible && argumentTypes.Count < parameters.Length)
				{
					for (int j = argumentTypes.Count; j < parameters.Length; j++)
					{
						if (hasParamArray && j == parameters.Length - 1)
							continue;

						if (!parameters[j].IsOptional)
						{
							compatible = false;
							break;
						}

						score += 1;
					}
				}

				if (!compatible)
					continue;

				if (score > bestScore)
				{
					bestScore = score;
					bestMethod = candidate;
				}
			}

			return bestMethod;
		}

		private static int GetRequiredParameterCount(System.Reflection.ParameterInfo[] parameters, bool hasParamArray)
		{
			int limit = hasParamArray ? parameters.Length - 1 : parameters.Length;
			int required = 0;
			for (int i = 0; i < limit; i++)
			{
				if (!parameters[i].IsOptional)
					required++;
			}

			return required;
		}

		private static System.Type GetInferredReturnType(System.Type methodReturnType)
		{
			if (!typeof(Task).IsAssignableFrom(methodReturnType))
			{
				return methodReturnType;
			}

			if (methodReturnType.IsGenericType && methodReturnType.GetGenericTypeDefinition() == typeof(Task<>))
			{
				return methodReturnType.GetGenericArguments()[0];
			}

			return typeof(void);
		}

		public static FunctionRuntimeInfo ResolveMethod2(Prototype prototype, string strSubObj, SymbolTable symbols)
		{
			PrototypeTypeInfo prototypeTypeInfo = symbols.GetTypeInfo(prototype.PrototypeName) as PrototypeTypeInfo;
			if (null != prototypeTypeInfo && null != prototypeTypeInfo.Scope)
			{
				FunctionRuntimeInfo infoFunc = prototypeTypeInfo.Scope.GetSymbol(strSubObj) as FunctionRuntimeInfo;
				if (null != infoFunc)
					return infoFunc;
			}
			foreach (int protoTypeOf in prototype.GetTypeOfs())
			{
				var tuple = ResolveMethod2(Prototypes.GetPrototype(protoTypeOf), strSubObj, symbols);

				if (null != tuple)
				{
					return tuple;
				}
			}

			return null;
		}
	}
}
