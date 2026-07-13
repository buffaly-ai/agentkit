//added
using BasicUtilities;
using Ontology;
using Ontology.BaseTypes;
using Ontology.Simulation;
using ProtoScript.Interpretter.Compiled;
using ProtoScript.Interpretter.Interpretting;
using ProtoScript.Interpretter.RuntimeInfo;
using ProtoScript.Interpretter.Symbols;
using System;
using System.Threading;

namespace ProtoScript.Interpretter
{


	[Serializable]
	public class RuntimeException : Exception
	{
		public StatementParsingInfo Info = null;
		public RuntimeException() { }
		public RuntimeException(string message, StatementParsingInfo info) : base(message)
		{
			Info = info;
		}
		public RuntimeException(string message, StatementParsingInfo info, Exception inner) : base(message, inner)
		{
			Info = info;
		}
		public RuntimeException(string message, Exception inner) : base(message, inner) { }

		protected RuntimeException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}

	public class IncompatiblePrototypeParameter : RuntimeException
	{
		public IncompatiblePrototypeParameter(StatementParsingInfo info, string strSource, string strTarget) : base($"Incompatible Prototype Parameter {strSource} -> {strTarget}", info)
		{
		}

	}

	public class NativeInterpretter
	{
		private class UninitializedReturn { }
		private int m_isRunningEntryPoint;
		private sealed class DotNetIndexSetterInfo
		{
			public object Target;
			public System.Reflection.PropertyInfo Indexer;
			public object ConvertedKey;
		}


		public Compiler Compiler;
		public SymbolTable Symbols;
		public string Source = string.Empty;
		public bool LogMethodCalls = false;
		public bool AllowLazyPropertyInitialization = true;

		public NativeInterpretter(Compiler compiler)
		{
			Symbols = compiler.Symbols;
			Source = compiler.Source;
			Compiler = compiler;
		}


		public Prototype NewInstance(Prototype prototype)
		{
			return NewInstance(prototype, new List<Compiled.Expression>());
		}

		public Prototype NewInstance(string strPrototypeName, List<Compiled.Expression> lstParameters)
		{
			//Allow for passing in types to be interpretted at runtime, like BoundCharacter<CCharacter>
			PrototypeTypeInfo infoGeneric = (PrototypeTypeInfo)this.Symbols.GetTypeInfo(Parsers.Types.Parse(strPrototypeName));
			Prototype protoInstance = this.NewInstance(infoGeneric.Prototype, lstParameters);

			return protoInstance;
		}
		public Prototype NewInstance(Prototype prototype, List<Compiled.Expression> lstParameters)
		{
			//I need constructors to be called if they exist, but for constructorless objects I want to use
			//a clone instead of a #instance. 
			Prototype protoThis = null;

			FunctionRuntimeInfo functionRuntimeInfo = GetConstructor(prototype);

			//If we happen to pass in an instance, make sure to get the base type. 
			//N20241220-01 - Check for a constructor on the instance before going to the base
			if (null == functionRuntimeInfo && prototype.IsInstance())
			{
				//N20250130-02 - We no longer get the base type (still getting constructor)
				//Keep an eye on this code, but we need to move towards always keeping the instance 
				//type
				//prototype = SimpleInterpretter.GetBaseTypeOf(prototype);
				functionRuntimeInfo = GetConstructor(prototype);
			}

			if (null != functionRuntimeInfo)
			{
				protoThis = SimpleInterpretter.NewInstance(prototype);
				RunConstructor(functionRuntimeInfo, lstParameters, protoThis);
			}

			if (null == protoThis)
				protoThis = prototype.Clone();

			return protoThis;
		}

		private PrototypeTypeInfo? GetPrototypeTypeInfo(Prototype prototype)
		{
			PrototypeTypeInfo? prototypeTypeInfo = Symbols.GetTypeInfo(prototype.PrototypeName) as PrototypeTypeInfo;
			if (prototypeTypeInfo != null)
				return prototypeTypeInfo;

			// Legacy fallback for partially initialized contexts.
			return prototype.Data["TypeInfo"] as PrototypeTypeInfo;
		}

		private FunctionRuntimeInfo GetConstructor(Prototype prototype)
		{
			PrototypeTypeInfo? prototypeTypeInfo = GetPrototypeTypeInfo(prototype);
			if (null != prototypeTypeInfo)
			{
				FunctionRuntimeInfo? functionRuntimeInfo = prototypeTypeInfo.Scope.Symbols.FirstOrDefault(x => (x.Value is FunctionRuntimeInfo && (x.Value as FunctionRuntimeInfo).IsConstructor)).Value as FunctionRuntimeInfo;
				return functionRuntimeInfo;
			}

			return null;
		}

		public Prototype NewInstanceWithoutClone(Prototype prototype)
		{
			//Always create a new instance, never use clone
			Prototype protoThis = prototype.CreateInstance();
			PrototypeTypeInfo? prototypeTypeInfo = GetPrototypeTypeInfo(prototype);
			if (null != prototypeTypeInfo)
			{
				FunctionRuntimeInfo? functionRuntimeInfo = prototypeTypeInfo.Scope.Symbols.FirstOrDefault(x => (x.Value is FunctionRuntimeInfo && (x.Value as FunctionRuntimeInfo).IsConstructor)).Value as FunctionRuntimeInfo;
				if (null != functionRuntimeInfo)
				{
					RunConstructor(functionRuntimeInfo, new List<Compiled.Expression>(), protoThis);
				}
			}

			return protoThis;
		}
		public Prototype GetPrimaryParent(Prototype prototype)
		{
			if (prototype.IsInstance())
				prototype = prototype.GetBaseType();

			PrototypeTypeInfo prototypeTypeInfo = Symbols.GetTypeInfo(prototype.PrototypeName) as PrototypeTypeInfo;
			return prototypeTypeInfo.PrimaryParent;
		}


		public void InsertGlobalObject(string strName, object obj)
		{
			Scope globalScope = Compiler.Symbols.GetGlobalScope();
			if (globalScope.TryGetSymbol(strName, out object existing) && existing is ValueRuntimeInfo existingInfo)
			{
				TypeInfo sourceType = new TypeInfo(obj.GetType());
				if (!SimpleInterpretter.IsAssignableFrom(sourceType, existingInfo.Type))
				{
					throw new InvalidOperationException($"Cannot inject object of type '{obj.GetType().FullName}' into '{strName}' declared as '{existingInfo.Type}'.");
				}

				existingInfo.Value = obj;
				if (existingInfo.Index >= 0 && existingInfo.Index < Compiler.Symbols.GlobalStack.Count)
				{
					object existingStackEntry = Compiler.Symbols.GlobalStack[existingInfo.Index];
					if (existingStackEntry is ValueRuntimeInfo existingStackInfo)
					{
						existingStackInfo.Value = obj;
					}
					else
					{
						Compiler.Symbols.GlobalStack[existingInfo.Index] = existingInfo;
					}
				}

				globalScope.InsertSymbol(strName, existingInfo);
				return;
			}

			DotNetTypeInfo info = new DotNetTypeInfo(obj.GetType());
			info.Index = Compiler.Symbols.GlobalStack.Add(info);

			ValueRuntimeInfo valueRuntimeInfo = new ValueRuntimeInfo();
			valueRuntimeInfo.Type = info;
			valueRuntimeInfo.Value = obj;
			valueRuntimeInfo.Index = Compiler.Symbols.GlobalStack.Add(valueRuntimeInfo);

			globalScope.InsertSymbol(strName, valueRuntimeInfo);
		}

		public bool TryInsertDeclaredGlobalObject(string strName, object obj, out string error)
		{
			error = string.Empty;

			if (string.IsNullOrWhiteSpace(strName))
			{
				error = "Global object name is required.";
				return false;
			}

			if (obj == null)
			{
				error = $"Global object '{strName}' cannot be null.";
				return false;
			}

			Scope globalScope = Compiler.Symbols.GetGlobalScope();
			if (!globalScope.TryGetSymbol(strName, out object existing) || existing is not ValueRuntimeInfo existingInfo)
			{
				error = $"Global symbol '{strName}' is not predeclared.";
				return false;
			}

			TypeInfo sourceType = new TypeInfo(obj.GetType());
			if (!SimpleInterpretter.IsAssignableFrom(sourceType, existingInfo.Type))
			{
				error = $"Cannot inject object of type {obj.GetType().FullName} into {strName} declared as {existingInfo.Type}";
				return false;
			}

			existingInfo.Value = obj;
			if (existingInfo.Index >= 0 && existingInfo.Index < Compiler.Symbols.GlobalStack.Count)
			{
				object existingStackEntry = Compiler.Symbols.GlobalStack[existingInfo.Index];
				if (existingStackEntry is ValueRuntimeInfo existingStackInfo)
				{
					existingStackInfo.Value = obj;
				}
				else
				{
					Compiler.Symbols.GlobalStack[existingInfo.Index] = existingInfo;
				}
			}

			globalScope.InsertSymbol(strName, existingInfo);
			return true;
		}

		public bool TryResolvePrototypeReference(object? rawValue, TypeInfo? expectedType, out Prototype? prototype, out string error)
		{
			prototype = null;
			error = string.Empty;

			if (rawValue == null)
			{
				error = "Prototype reference cannot be null.";
				return false;
			}

			Prototype? resolved = SimpleInterpretter.GetAsPrototype(rawValue);
			string sourceText = string.Empty;

			if (resolved == null)
			{
				if (rawValue is not string textValue)
				{
					error = $"Prototype reference must be a prototype or prototype name string, but was {rawValue.GetType().Name}.";
					return false;
				}

				string prototypeName = textValue.Trim();
				sourceText = prototypeName;
				if (string.IsNullOrWhiteSpace(prototypeName))
				{
					error = "Prototype reference name cannot be empty.";
					return false;
				}

				object? symbol = Symbols.GetGlobalScope().GetSymbol(prototypeName);
				if (symbol is PrototypeTypeInfo prototypeTypeInfoFromScope && prototypeTypeInfoFromScope.Prototype != null)
				{
					resolved = prototypeTypeInfoFromScope.Prototype;
				}
				else
				{
					try
					{
						resolved = Prototypes.GetPrototypeByPrototypeName(prototypeName);
					}
					catch
					{
						resolved = null;
					}
				}

				if (resolved == null)
				{
					error = $"Prototype not found: {prototypeName}";
					return false;
				}
			}
			else
			{
				sourceText = resolved.PrototypeName ?? string.Empty;
			}

			if (IsPrototypeReferenceParameterType(expectedType) && expectedType is PrototypeTypeInfo expectedPrototypeTypeInfo)
			{
				Prototype? expectedPrototype = expectedPrototypeTypeInfo.Prototype;
				if (expectedPrototype != null && !Prototypes.TypeOf(resolved, expectedPrototype))
				{
					error = $"Expected prototype type '{expectedPrototype.PrototypeName}' but got '{resolved.PrototypeName}' from '{sourceText}'.";
					return false;
				}
			}

			prototype = resolved;
			return true;
		}

		public bool TryBindMethodCall(
			Prototype? protoInstance,
			string methodName,
			IDictionary<string, object?> rawParameters,
			out FunctionRuntimeInfo? method,
			out Dictionary<string, object> boundParameters,
			out string error)
		{
			method = null;
			boundParameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			error = string.Empty;

			if (string.IsNullOrWhiteSpace(methodName))
			{
				error = "Method name is required.";
				return false;
			}

			if (rawParameters == null)
			{
				error = "Raw parameter dictionary is required.";
				return false;
			}

			if (protoInstance == null)
			{
				method = this.Symbols.GetSymbol(methodName) as FunctionRuntimeInfo;
				if (method == null)
				{
					error = "Could not find global method: " + methodName;
					return false;
				}
			}
			else
			{
				method = this.FindOverriddenMethod(protoInstance, methodName);
				if (method == null)
				{
					error = "Could not find method: " + methodName + ", on prototype: " + protoInstance.PrototypeName;
					return false;
				}
			}

			foreach (ParameterRuntimeInfo parameterInfo in method.Parameters)
			{
				string parameterName = parameterInfo.ParameterName;
				if (!rawParameters.TryGetValue(parameterName, out object? rawValue))
				{
					error = "Missing parameter: " + parameterName;
					return false;
				}

				TypeInfo? parameterType = parameterInfo.OriginalType ?? parameterInfo.Type;
				if (IsPrototypeReferenceParameterType(parameterType))
				{
					if (!TryResolvePrototypeReference(rawValue, parameterType, out Prototype? resolvedPrototype, out string resolveError))
					{
						error = $"Invalid prototype parameter '{parameterName}': {resolveError}";
						return false;
					}

					boundParameters[parameterName] = resolvedPrototype!;
				}
				else
				{
					if (rawValue == null)
					{
						error = "Parameter cannot be null: " + parameterName;
						return false;
					}

					object? convertedValue = null;
					if (parameterType != null && !ValueConversions.TryMakeAssignable(rawValue, parameterType, out convertedValue))
					{
						string targetTypeName = parameterType?.Type?.Name ?? parameterType?.ToString() ?? "unknown";
						error = parameterName + " wrong type: " + targetTypeName;
						return false;
					}

					boundParameters[parameterName] = convertedValue ?? rawValue;
				}
			}

			return true;
		}

		private static bool IsPrototypeReferenceParameterType(TypeInfo? parameterType)
		{
			if (parameterType is PrototypeTypeInfo prototypeTypeInfo)
			{
				Prototype? prototype = prototypeTypeInfo.Prototype;
				if (prototype == null)
					return true;

				try
				{
					return !NativeValuePrototypes.IsBaseType(prototype.PrototypeID);
				}
				catch
				{
					return true;
				}
			}

			System.Type? dotNetType = parameterType?.Type;
			if (dotNetType != null && typeof(Prototype).IsAssignableFrom(dotNetType))
				return true;

			return false;
		}

		public void InsertLocalPrototype(string strName, Prototype prototype)
		{
			PrototypeTypeInfo? typeInfo = GetPrototypeTypeInfo(prototype);

			if (null == typeInfo)
			{
				typeInfo = new PrototypeTypeInfo() { Prototype = prototype };
			}

			VariableRuntimeInfo info = new VariableRuntimeInfo();

			info.Type = typeInfo;
			info.Index = this.Symbols.LocalStack.Add(info);
			info.OriginalType = typeInfo.Clone();
			info.Value = prototype;

			this.Symbols.InsertSymbol(strName, info);
		}

		public Prototype GetLocalPrototype(string strName)
		{
			return this.GetAsPrototype(this.Symbols.GetSymbol(strName));
		}

		public void InterpretCodeBlock(string strCode)
		{
			CodeBlock codeBlock = Parsers.CodeBlocks.Parse("{" + strCode + "}");
			Interpretter.Compiled.CodeBlock compiled = this.Compiler.Compile(codeBlock);
			this.Evaluate(compiled);
		}

		public void InterpretStatement(Statement statement)
		{
			if (statement is PrototypeDefinition pd)
			{
				List<Compiled.Statement> lstStatements = this.Compiler.Compile(pd);
				foreach (Compiled.Statement compiledStatement in lstStatements)
				{
					this.Evaluate(compiledStatement);
				}
			}
			else
			{
				Interpretter.Compiled.Statement compiled = this.Compiler.Compile(statement);
				this.Evaluate(compiled);
			}
		}

		public bool Evaluate(Compiled.File file)
		{

			//N20240929-01 - We aren't using file scope now, so this call causes test cases to fail
			if (null != file.Scope)
				Symbols.EnterScope(file.Scope);

			try
			{
				foreach (ProtoScript.Interpretter.Compiled.Statement statement in file.Statements)
				{
					try
					{
						Evaluate(statement);
					}
					catch (RuntimeException)
					{
						throw;
					}
					catch (Exception err)
					{
						if (null != statement.Info)
						{
							string strStatement = Source.Substring(statement.Info.StartingOffset, statement.Info.Length);
							throw new RuntimeException(strStatement, statement.Info, err);
						}
					}
				}
			}
			finally
			{
				if (null != file.Scope)
					Symbols.LeaveScope();
			}

			return false;
		}

		public void InterpretProject(string strProjectFile)
		{
			List<Compiled.Statement> statements = Compiler.CompileProject(strProjectFile);
			InterpretStatements(statements);
		}

		public void InterpretStatements(List<Compiled.Statement> statements)
		{
			Source = Compiler.Source;

			foreach (Compiled.Statement statement in statements)
			{
				if (statement == null)
					continue;

				try
				{
					Evaluate(statement);
				}
				catch (RuntimeException)
				{
					throw;
				}
				catch (Exception err)
				{
					if (statement.Info != null)
					{
						File? file = Compiler.Files.FirstOrDefault(x => x.Info?.FullName == statement.Info?.File);
						if (file != null)
						{
							string strStatement = file.RawCode.Substring(statement.Info.StartingOffset, statement.Info.Length);
							throw new RuntimeException(strStatement, statement.Info, err);
						}
					}

					throw new RuntimeException("Interpretter Error", err);
				}
			}
		}

		virtual public bool Evaluate(Compiled.Statement statement)
		{
			if (statement == null)
				return false;

			try
			{
				if (statement is Compiled.VariableDeclaration)
					return Evaluate(statement as Compiled.VariableDeclaration);

				else if (statement is Compiled.ExpressionStatement)
					return Evaluate(statement as Compiled.ExpressionStatement);

				else if (statement is Compiled.ReturnStatement)
					return Evaluate(statement as Compiled.ReturnStatement);

				//These can be ignored 
				else if (statement is Compiled.PrototypeDeclaration)
				{
					return false;
				}
				else if (statement is FunctionRuntimeInfo)
				{
					return false;
				}

				else if (statement is Compiled.PrototypeAnnotation)
				{
					return Evaluate(statement as Compiled.PrototypeAnnotation);
				}

				else if (statement is Compiled.FunctionAnnotation)
				{
					return Evaluate(statement as Compiled.FunctionAnnotation);
				}

				else if (statement is Compiled.InsertTypeOf)
					return false;

				else if (statement is Compiled.IfStatement)
					return Evaluate(statement as Compiled.IfStatement);

				else if (statement is Compiled.ForEachStatement)
					return Evaluate(statement as Compiled.ForEachStatement);

				else if (statement is Compiled.CodeBlockStatement)
					return Evaluate((statement as Compiled.CodeBlockStatement).Statements as Compiled.CodeBlock);

				else if (statement is Compiled.DoStatement)
					return DoInterpretter.Evaluate(statement as Compiled.DoStatement, this);

				else if (statement is Compiled.WhileStatement)
					return WhileInterpretter.Evaluate(statement as Compiled.WhileStatement, this);

				else if (statement is Compiled.TryStatement)
					return TryInterpretter.Evaluate(statement as Compiled.TryStatement, this);

				else if (statement is Compiled.ThrowStatement)
					return TryInterpretter.Evaluate(statement as Compiled.ThrowStatement, this);

				throw new NotImplementedException($"Unhandled compiled statement type: {statement.GetType().FullName}");
			}
			catch (NotImplementedException)
			{
				throw;
			}
			catch (IncompatiblePrototypeParameter)
			{
				throw;
			}
			catch (RuntimeException)
			{
				throw;
			}
			catch (Exception err)
			{
				if (null != err.InnerException && err.InnerException is RuntimeException)
					throw err.InnerException;

				if (null != statement.Info)
				{
					File? file = this.Compiler.Files.FirstOrDefault(x => x.Info?.FullName == statement.Info?.File);
					if (null != file)
					{
						string strStatement = file.RawCode.Substring(statement.Info.StartingOffset, statement.Info.Length);
						string strMessage = err.Message;
						throw new RuntimeException(err.Message + "\r\n: " + strStatement, statement.Info, err);
					}
				}

				throw;
			}
		}

		public bool Evaluate(Compiled.PrototypeAnnotation annotation)
		{
			Evaluate(annotation.AnnotationFunction);
			return false;
		}

		public bool Evaluate(Compiled.FunctionAnnotation annotation)
		{
			Evaluate(annotation.AnnotationFunction);
			return false;
		}

		public bool Evaluate(Compiled.VariableDeclaration statement)
		{
			object obj = Symbols.LocalStack[statement.RuntimeInfo.Index];
			if (!(obj is VariableRuntimeInfo))
			{
				throw new RuntimeException("Unsupported type in variable declaration", statement.Info);
			}

			VariableRuntimeInfo infoVar = (VariableRuntimeInfo)obj;
			infoVar.Type = infoVar.OriginalType;

			object oVal = null;
			if (null != statement.Initializer)
			{
				oVal = Evaluate(statement.Initializer);
				oVal = MakeAssignable(oVal, infoVar.Type, statement.Initializer.Info);
				infoVar.Value = oVal;
			}

			return false;

		}

		public bool Evaluate(Compiled.ExpressionStatement statement)
		{
			Evaluate(statement.Expression);
			return false;
		}

		public bool Evaluate(Compiled.ReturnStatement statement)
		{
			object oRes = statement.Expression == null ? null : Evaluate(statement.Expression);

			Symbols.MethodScope.Stack[0] = oRes;
			return true;
		}


		public bool Evaluate(Compiled.ForEachStatement statement)
		{
			object oRes = Evaluate(statement.Expression);
			Symbols.EnterScope(statement.Scope.Clone());
			VariableRuntimeInfo varIterator = (VariableRuntimeInfo)Symbols.LocalStack[0];

			try
			{

				Prototype protoValue = GetAsPrototype(oRes);
				if (null == protoValue)
				{
					if (oRes is ValueRuntimeInfo variableRuntimeInfo &&
							variableRuntimeInfo.Value is System.Collections.IEnumerable)
					{
						foreach (object obj in variableRuntimeInfo.Value as System.Collections.IEnumerable)
						{
							varIterator.Value = obj;
							if (Evaluate(statement.Statements))
								return true;
						}

						return false;
					}
					else
					{
						string actualType = oRes?.GetType().FullName ?? "null";
						throw new RuntimeException($"For-each expected a prototype with children or an IEnumerable value, but got '{actualType}'.", statement.Info);
					}
				}

				for (int i = 0; i < protoValue.Children.Count; i++)
				{
					varIterator.Value = protoValue.Children[i];

					if (Evaluate(statement.Statements))
						return true;
				}
			}
			finally
			{
				Symbols.LeaveScope();
			}

			return false;
		}


		public bool Evaluate(Compiled.IfStatement statement)
		{
			bool bRes = EvaluateAsBool(statement.Condition);

			if (bRes)
			{
				return Evaluate(statement.TrueBody);
			}

			for (int i = 0; i < statement.ElseIfConditions.Count; i++)
			{
				bRes = EvaluateAsBool(statement.ElseIfConditions[i]);
				if (bRes)
				{
					return Evaluate(statement.ElseIfBodies[i]);
				}
			}

			if (statement.ElseBody != null && statement.ElseBody.Count > 0)
			{
				return Evaluate(statement.ElseBody);
			}

			return false;
		}

		public bool Evaluate(Compiled.CodeBlock statements)
		{
			Symbols.EnterScope(statements.Scope.Clone());

			try
			{
				foreach (Compiled.Statement statement in statements)
				{
					if (Evaluate(statement))
						return true;
				}
			}
			finally
			{
				Symbols.LeaveScope();
			}

			return false;
		}

		public bool EvaluateAsBool(Compiled.Expression exp)
		{
			object oRes = Evaluate(exp);
			object? oResAsBool = ValueConversions.GetAs(oRes, typeof(bool));

			if (!(oResAsBool is bool))
				throw new RuntimeException("Boolean value expected", exp.Info);

			return (bool)oResAsBool;
		}


		public object Evaluate(Compiled.Expression exp)
		{
			if (exp is null)
				throw new ArgumentNullException(nameof(exp), "Expression is null");

			switch (exp)
			{
				case GetLocalStack gLocal:
					return Evaluate(gLocal);

				case GetGlobalStack gGlobal:
					return Evaluate(gGlobal);

				case GetStack gStack:
					return Evaluate(gStack);

				case NewInstance newInst:
					return Evaluate(newInst);

				case PrototypeFieldReference protoField:
					return Evaluate(protoField);

				case BinaryExpression binary:
					return Evaluate(binary);

				case UnaryExpression unary:
					return Evaluate(unary);

				case FunctionEvaluation funcEval:
					return Evaluate(funcEval);

				case DotNetMethodEvaluation dotMethod:
					return Evaluate(dotMethod);

				case DotNetNewInstance dotNew:
					return Evaluate(dotNew);

				case Compiled.ArrayLiteral arrayLit:
					return Evaluate(arrayLit);

				case Compiled.Literal lit:
					return lit.Value;

				case Compiled.CategorizationOperator catOp:
					return Evaluate(catOp);

				case Compiled.ConditionalOperator conditionalOp:
					return Evaluate(conditionalOp);

				case Compiled.DotNetFieldReference dotField:
					return Evaluate(dotField);

				case Compiled.DotNetPropertyReference dotProp:
					return Evaluate(dotProp);

				default:
					throw new NotImplementedException(
						$"Unhandled expression type: {exp.GetType().Name}");
			}
		}


		public object Evaluate(GetLocalStack exp)
		{
			//Local stack, should be function stack, it should be first open function type
			if (Symbols.ActiveScope().ScopeType == Scope.ScopeTypes.Method || Symbols.ActiveScope().ScopeType == Scope.ScopeTypes.File)
			{
				return Symbols.ActiveScope().Stack[exp.Index];
			}

			Scope ? scope = Symbols.ActiveScopes.LastOrDefault(x => x.ScopeType == Scope.ScopeTypes.Method || x.ScopeType == Scope.ScopeTypes.File);
			if (null == scope)
				throw new RuntimeException("Could not find function scope", exp.Info);

			return scope.Stack[exp.Index];
		}
		public object Evaluate(GetStack exp)
		{
			int iID = exp.Scope.ID;
			if (Symbols.ActiveScope().ID == iID)
			{
				return Symbols.ActiveScope().Stack[exp.Index];
			}

			for (int i = Symbols.ActiveScopes.Count - 1; i >= 0; i--)
			{
				if (Symbols.ActiveScopes[i].ID == iID)
					return Symbols.ActiveScopes[i].Stack[exp.Index];
			}

			if (IsScopeOwnedByCurrentRuntime(exp.Scope))
			{
				return exp.Scope.Stack[exp.Index];
			}

			throw new RuntimeException(
				$"GetStack scope {iID} is not owned by current runtime. Active scope IDs: {string.Join(",", Symbols.ActiveScopes.Select(x => x.ID))}",
				exp.Info);
		}

		private bool IsScopeOwnedByCurrentRuntime(Scope scope)
		{
			if (scope == null)
				return false;

			if (Symbols.GetGlobalScope().ID == scope.ID)
				return true;

			if (Symbols.ActiveScopes.Any(x => x.ID == scope.ID))
				return true;

			foreach (object? value in Symbols.GetGlobalScope().Symbols.Values)
			{
				if (TryGetOwnedScope(value, out Scope? ownedScope) && ownedScope != null && ownedScope.ID == scope.ID)
					return true;
			}

			foreach (object? value in Symbols.GlobalStack)
			{
				if (TryGetOwnedScope(value, out Scope? ownedScope) && ownedScope != null && ownedScope.ID == scope.ID)
					return true;
			}

			return false;
		}

		private static bool TryGetOwnedScope(object? value, out Scope? scope)
		{
			switch (value)
			{
				case Scope s:
					scope = s;
					return true;
				case FunctionRuntimeInfo functionRuntimeInfo:
					scope = functionRuntimeInfo.Scope;
					return scope != null;
				case PrototypeTypeInfo prototypeTypeInfo:
					scope = prototypeTypeInfo.Scope;
					return scope != null;
				default:
					scope = null;
					return false;
			}
		}

		public object Evaluate(GetGlobalStack exp)
		{
			return Symbols.GetGlobalScope().Stack[exp.Index];
		}

		public object Evaluate(Compiled.ArrayLiteral literal)
		{
			Collection collection = new Collection();

			foreach (Compiled.Expression exp in literal.Values)
			{
				object value = Evaluate(exp);
				Prototype protoValue = GetOrConvertToPrototype(value);
				collection.Children.Add(protoValue);
			}

			return collection;
		}

		public object Evaluate(NewInstance exp)
		{
			PrototypeTypeInfo infoType = exp.InferredType as PrototypeTypeInfo;
			Prototype protoThis = SimpleInterpretter.NewInstance(infoType.Prototype);
			if (null != exp.Constructor)
			{
				RunConstructor(exp.Constructor, exp.Parameters, protoThis);
			}

			foreach (NewInstance.ObjectInitializer initializer in exp.Initializers)
			{
				object oValue = Evaluate(initializer.Value);
				Prototype protoValue = GetOrConvertToPrototype(oValue);

				protoThis.Properties[initializer.Property.PrototypeID] = protoValue;
			}

			return protoThis;
		}



		private void RunConstructor(FunctionRuntimeInfo infoConstructor, List<Compiled.Expression> lstParameters, Prototype protoThis)
		{
			Scope scope = GetFunctionEvaluationScope(infoConstructor, lstParameters);
			Symbols.EnterScope(scope);
			try
			{
				scope.Stack[1] = protoThis;

				foreach (Compiled.Statement statement in infoConstructor.Statements)
				{
					if (Evaluate(statement))
						break;
				}
			}
			finally
			{
				Symbols.LeaveScope();
			}
		}

		public object Evaluate(BinaryExpression exp)
		{
			if (exp is null)
				throw new ArgumentNullException(nameof(exp));

			switch (exp)
			{
				case AssignmentOperator assign:
					return Evaluate(assign);

				case TypeOfOperator typeOfOp:
					return Evaluate(typeOfOp);

				case Compiled.IndexOperator indexOp:
					return Evaluate(indexOp);

				case Compiled.EqualsOperator equalsOp:
					return Evaluate(equalsOp);

				case Compiled.CastingOperator castOp:
					return Evaluate(castOp);

				case Compiled.OrOperator orOp:
					return Evaluate(orOp);

				case Compiled.AndOperator andOp:
					return Evaluate(andOp);

				case Compiled.NullCoalescingOperator nullCoalesceOp:
					return Evaluate(nullCoalesceOp);

				case Compiled.AddOperator addOp:
					return Evaluate(addOp);

				case Compiled.ComparisonOperator cmpOp:
					return Evaluate(cmpOp);

				default:
					throw new NotImplementedException(
						$"Unhandled binary expression: {exp.GetType().Name}");
			}
		}


		public object Evaluate(Compiled.AddOperator exp)
		{
			object left = Evaluate(exp.Left);
			object right = Evaluate(exp.Right);

			object? strLeftObj = ValueConversions.GetAs(left, typeof(string));
			object? strRightObj = ValueConversions.GetAs(right, typeof(string));

			if (!(strLeftObj is string strLeft))
				throw new RuntimeException("Left side of '+' is not convertible to string", exp.Info);

			if (!(strRightObj is string strRight))
				throw new RuntimeException("Right side of '+' is not convertible to string", exp.Info);

			return strLeft + strRight;
		}

		public object Evaluate(Compiled.ComparisonOperator exp)
		{
			// Evaluate both sides once, coerce to int
			object? leftObj = ValueConversions.GetAs(Evaluate(exp.Left), typeof(int));
			object? rightObj = ValueConversions.GetAs(Evaluate(exp.Right), typeof(int));

			if (!(leftObj is int left))
				throw new RuntimeException("Left side of comparison is not convertible to int", exp.Info);

			if (!(rightObj is int right))
				throw new RuntimeException("Right side of comparison is not convertible to int", exp.Info);

			switch (exp.Operator)
			{
				case ">":
					{
						return left > right;
					}

				case "<":
					{
						return left < right;
					}

				case ">=":
					{
						return left >= right;
					}

				case "<=":
					{
						return left <= right;
					}

				case "==":
					{
						return left == right;
					}

				// two spellings for “not equal”
				case "!=":
				case "<>":
					{
						return left != right;
					}

				default:
					throw new RuntimeException("Unsupported comparison operator", exp.Info);
			}
		}





		public object Evaluate(Compiled.CastingOperator exp)
		{
			object left = Evaluate(exp.Left);
			object right = Evaluate(exp.Right);

			Prototype protoLeft = GetAsPrototype(left);
			Prototype protoRight = GetAsPrototype(right);

			if (!Prototypes.TypeOf(protoLeft, protoRight))
			{
				if (right is DotNetTypeInfo)
				{
					DotNetTypeInfo typeInfo = right as DotNetTypeInfo;
					if (null != left && left.GetType() == typeInfo.Type)
						return left;

					return MakeAssignable(left, typeInfo, exp.Info);
				}

				return null;
			}

			return protoLeft;
		}

		public object Evaluate(Compiled.CategorizationOperator exp)
		{
			object left = Evaluate(exp.Left);

			Prototype protoLeft = GetAsPrototype(left);
			Prototype protoMiddle = GetAsPrototype(Evaluate(exp.Middle));

			if (!Prototypes.TypeOf(protoLeft, protoMiddle))
			{
				return false;
			}

			//Don't required intialized(property) around every field 
			bool bSavedAllowLazy = AllowLazyPropertyInitialization;
			AllowLazyPropertyInitialization = false;

			Symbols.EnterScope(exp.Right.Scope);

			try
			{
				if (protoLeft.PrototypeID == Possibilities.PrototypeID)
				{
					foreach (Prototype protoPossible in protoLeft.Children)
					{
						exp.Right.Scope.Stack[0] = protoPossible;

						bool bFailed = false;
						foreach (Compiled.Expression expr in exp.Right.Expressions)
						{
							bool bRes = EvaluateAsBool(expr);
							if (!bRes)
							{
								bFailed = true;
								break;
							}
						}

						if (!bFailed)
							return true;
					}

					return false;
				}
				else
				{
					exp.Right.Scope.Stack[0] = left;

					foreach (Compiled.Expression expr in exp.Right.Expressions)
					{
						bool bRes = EvaluateAsBool(expr);
						if (!bRes)
							return false;
					}
				}
			}
			finally
			{
				AllowLazyPropertyInitialization = bSavedAllowLazy;
				Symbols.LeaveScope();
			}

			return true;
		}

		public object Evaluate(Compiled.EqualsOperator exp)
		{
			object left = Evaluate(exp.Left);
			object right = Evaluate(exp.Right);

			if (left is ValueRuntimeInfo leftValueRuntimeInfo)
				left = leftValueRuntimeInfo.Value;
			if (right is ValueRuntimeInfo rightValueRuntimeInfo)
				right = rightValueRuntimeInfo.Value;

			//TODO: This needs to be abstracted and made flexible. Also
			//compilation should check that the types can be compared
			if (left?.GetType() == typeof(int) && right?.GetType() == typeof(int))
			{
				return ((int)left == (int)right);
			}

			if (left?.GetType() == typeof(bool) && right?.GetType() == typeof(bool))
			{
				return ((bool)left == (bool)right);
			}

			if (left?.GetType() == typeof(string) && right?.GetType() == typeof(string))
			{
				return ((string)left == (string)right);
			}

			Prototype protoLeft = GetAsPrototype(left);
			Prototype protoRight = GetAsPrototype(right);

			if (protoLeft == null || protoRight == null)
			{
				if (protoLeft != null || protoRight != null)
					return false;

				return Equals(left, right);
			}

			//Note: Define separate operators for other equivalence
			//=== could be for AreEquivalentCircular
			//but it's probably best to use functions 
			return protoRight.ShallowEqual(protoLeft);

			//return SimpleInterpretter.AreEquivalentCircular(protoLeft, protoRight);
		}

		public object Evaluate(Compiled.ConditionalOperator exp)
		{
			if (EvaluateAsBool(exp.Condition))
				return Evaluate(exp.TrueExpression);

			return Evaluate(exp.FalseExpression);
		}

		public object Evaluate(Compiled.NullCoalescingOperator exp)
		{
			object? left = Evaluate(exp.Left);
			left = UnwrapNullCoalescingOperand(left);
			if (left != null)
				return left;

			object? right = Evaluate(exp.Right);
			return UnwrapNullCoalescingOperand(right);
		}

		private static object? UnwrapNullCoalescingOperand(object? value)
		{
			if (value is ValueRuntimeInfo valueRuntimeInfo)
				return valueRuntimeInfo.Value;

			if (value is PrototypeTypeInfo prototypeTypeInfo)
				return prototypeTypeInfo.Prototype;

			return value;
		}

		public object Evaluate(Compiled.IndexOperator exp)
		{
			object left = Evaluate(exp.Left);
			object right = Evaluate(exp.Right);

			if (left is PrototypePropertiesCollection)
			{
				PrototypePropertiesCollection properties = left as PrototypePropertiesCollection;
				int iPrototypeID;
				if (right is int)
				{
					iPrototypeID = (int)right;
				}
				else if (right is string str)
				{
					Prototype protoRight = TemporaryPrototypes.GetOrCreateTemporaryPrototype(str);
					iPrototypeID = protoRight.PrototypeID;
				}
				else
				{
					Prototype protoRight = GetAsPrototype(right);

					if (protoRight.TypeOf(System_String.Prototype))
					{
						string strValue = StringWrapper.ToString(protoRight);
						protoRight = TemporaryPrototypes.GetOrCreateTemporaryPrototype(strValue);
					}

					iPrototypeID = protoRight.PrototypeID;
				}

				return properties[iPrototypeID];
			}

			if (left is List<Prototype>)
			{
				if (!(right is int))
				{
					throw new RuntimeException("Index must of type integer on a prototype collection", exp.Info);
				}

				int iRight = Convert.ToInt32(right);

				List<Prototype> lstPrototypes = left as List<Prototype>;
				if (iRight < 0 || iRight >= lstPrototypes.Count)
					throw new RuntimeException("Index is outside the bounds of the collection", exp.Info);

				return lstPrototypes[iRight];
			}

			Prototype prototype = GetAsPrototype(left);

			if (null == prototype)
				throw new RuntimeException("Collection is not of recognized type for index operator", exp.Info);
			else
			{
				//Note: this is somewhat inconsistent because we could use PrototypeID here
				//Shorthand: 
				// prototype[0] = prototype.Children[0]
				if (right is int)
				{
					int iRight = Convert.ToInt32(right);

					if (iRight < 0 || iRight >= prototype.Children.Count)
						throw new RuntimeException("Index is outside the bounds of the collection", exp.Info);

					return prototype.Children[iRight];
				}

				int iPrototypeID;
				if (right is string)
				{
					//Shorthand: 
					//prototype["Property"] = protototype.Properties[Property.PrototypeID]
					Prototype protoRight = TemporaryPrototypes.GetOrCreateTemporaryPrototype(right as string);
					iPrototypeID = protoRight.PrototypeID;
				}
				else
				{
					//Shorthand: 
					//prototype[protoProp] = protototype.Properties[protoProp.PrototypeID]
					Prototype protoRight = GetAsPrototype(right);
					if (protoRight.PrototypeID == System_String.Prototype.PrototypeID)
					{
						string strValue = StringWrapper.ToString(protoRight);
						protoRight = TemporaryPrototypes.GetOrCreateTemporaryPrototype(strValue);
					}
					iPrototypeID = protoRight.PrototypeID;
				}

				return prototype.Properties[iPrototypeID];
			}
		}
		public object Evaluate(UnaryExpression exp)
		{
			if (exp is NotOperator)
				return Evaluate(exp as NotOperator);

			if (exp is Compiled.IsInitializedOperator)
				return Evaluate(exp as Compiled.IsInitializedOperator);

			throw new NotImplementedException($"Unhandled unary expression type: {exp.GetType().FullName}");
		}

		public object Evaluate(Compiled.IsInitializedOperator exp)
		{
			object right = Evaluate(exp.Right);
			return right != null;
		}
		public object Evaluate(OrOperator exp)
		{
			return EvaluateAsBool(exp.Left) || EvaluateAsBool(exp.Right);
		}

		public object Evaluate(AndOperator exp)
		{
			return EvaluateAsBool(exp.Left) && EvaluateAsBool(exp.Right);
		}
		public object Evaluate(NotOperator exp)
		{
			return !EvaluateAsBool(exp.Right);
		}
		public object Evaluate(TypeOfOperator exp)
		{
			object left = Evaluate(exp.Left);
			object right = Evaluate(exp.Right);

			Prototype protoLeft = GetAsPrototype(left);
			Prototype protoRight = GetAsPrototype(right);

			return Prototypes.TypeOf(protoLeft, protoRight);
		}

		public Prototype GetAsPrototype(object obj)
		{
			return SimpleInterpretter.GetAsPrototype(obj);
		}

		public Prototype? GetOrConvertToPrototype(object? oValue)
		{
			if (oValue is ValueRuntimeInfo valueRuntimeInfo)
				oValue = valueRuntimeInfo.Value;

			if (oValue is System.Collections.IEnumerable enumerableValue && oValue is not string && oValue is not Prototype)
			{
				if (TryConvertEnumerableToPrototypeCollection(enumerableValue, out Collection? collection))
					return collection;
			}

			return ValueConversions.ToPrototype(oValue);
		}

		private bool TryConvertEnumerableToPrototypeCollection(System.Collections.IEnumerable enumerableValue, out Collection? collection)
		{
			List<object?> items = new List<object?>();
			foreach (object? item in enumerableValue)
			{
				items.Add(item);
				if (!IsPrototypeLikeElement(item))
				{
					collection = null;
					return false;
				}
			}

			collection = new Collection();

			foreach (object? childValue in items)
			{
				Prototype? childPrototype = GetOrConvertToPrototype(childValue);
				if (childPrototype != null)
				{
					collection.Children.Add(childPrototype);
				}
			}

			return true;
		}

		private static bool IsPrototypeLikeElement(object? value)
		{
			object? current = value;

			if (current is ValueRuntimeInfo valueRuntimeInfo)
				current = valueRuntimeInfo.Value;

			if (current is PrototypeTypeInfo prototypeTypeInfo)
				current = prototypeTypeInfo.Prototype;

			return current == null || current is Prototype;
		}


		public object Evaluate(AssignmentOperator exp)
		{
			object left = EvaluateAsSet(exp.Left);
			object right = Evaluate(exp.Right);

			if (left is FieldSetterInfo refField)
			{
				right = MakeAssignable(right, exp.Left.InferredType, exp.Info);
				if (null == right)
					refField.Prototype.Properties[refField.Property.PrototypeID] = null;
				else if (right is Prototype)
					refField.Prototype.Properties[refField.Property.PrototypeID] = right as Prototype;
				else
					throw new RuntimeException("Cannot assign value", exp.Info);


			}

			else if (left is ValueRuntimeInfo infoValue)
			{
				right = MakeAssignable(right, exp.Left.InferredType, exp.Info);

				infoValue.Value = right;

			}
			else if (left is DotNetFieldReference)
			{
				DotNetFieldReference dotNetFieldReference = (left as DotNetFieldReference);
				object obj = Evaluate(dotNetFieldReference.Object);
				if (obj is ValueRuntimeInfo)
					obj = (obj as ValueRuntimeInfo).Value;
				else if (obj is PrototypeTypeInfo)
					obj = (obj as PrototypeTypeInfo).Prototype;

				right = MakeAssignable(right, exp.Left.InferredType, exp.Info);

				ReflectionCache.GetSetter(dotNetFieldReference.Field)(obj, right);
			}

			else if (left is DotNetPropertyReference)
			{
				DotNetPropertyReference dotNetPropertyReference = (left as DotNetPropertyReference);
				object obj = Evaluate(dotNetPropertyReference.Object);
				if (obj is ValueRuntimeInfo)
					obj = (obj as ValueRuntimeInfo).Value;
				else if (obj is PrototypeTypeInfo)
					obj = (obj as PrototypeTypeInfo).Prototype;
				right = MakeAssignable(right, exp.Left.InferredType, exp.Info);
				ReflectionCache.GetSetter(dotNetPropertyReference.Property)(obj, right);
			}
			else if (left is DotNetIndexSetterInfo dotNetIndexSetterInfo)
			{
				right = MakeAssignable(right, new TypeInfo(dotNetIndexSetterInfo.Indexer.PropertyType), exp.Info);
				dotNetIndexSetterInfo.Indexer.SetValue(
					dotNetIndexSetterInfo.Target,
					right,
					new object[] { dotNetIndexSetterInfo.ConvertedKey });
			}


			else if (left is FunctionRuntimeInfo && right is FunctionRuntimeInfo)
			{
				throw new RuntimeException("Function-to-function assignment is not supported.", exp.Info);

			}

			else if (left is IndexSetterInfo)
			{
				IndexSetterInfo indexSetterInfo = left as IndexSetterInfo;
				Prototype protoRight = GetAsPrototype(right);
				if (null == protoRight)
					throw new RuntimeException("Right side must be of type Prototype", exp.Info);

				indexSetterInfo.Collection.Children[indexSetterInfo.Index] = protoRight;
			}
			else
				throw new RuntimeException($"Unsupported assignment target type: {left?.GetType().FullName ?? "null"}", exp.Info);


			return null;
		}

		private object MakeAssignable(object value, TypeInfo typeInfo, StatementParsingInfo statementParsingInfo)
		{
			object? converted;
			if (ValueConversions.TryMakeAssignable(value, typeInfo, out converted))
				return converted;

			throw new RuntimeException("Cannot assign value", statementParsingInfo);
		}

		public object Evaluate(PrototypeFieldReference exp)
		{
			object oLeft = Evaluate(exp.Left);
			if (exp.IsNullConditional)
			{
				if (oLeft is ValueRuntimeInfo val)
					oLeft = val.Value;
				if (oLeft == null)
					return null;
			}
			object oRight = Evaluate(exp.Right);

			Prototype prototype = GetAsPrototype(oLeft);

			if (null == prototype)
				throw new RuntimeException("Could not evaluate left side", exp.Left.Info);

			Prototype protoProp = null;
			if (oRight is PrototypeTypeInfo)
			{
				PrototypeTypeInfo infoProp = oRight as PrototypeTypeInfo;
				protoProp = infoProp.Prototype;
			}
			else
			{
				string rightType = oRight?.GetType().FullName ?? "null";
				throw new RuntimeException($"Expected a prototype field reference on the right side, but got '{rightType}'.", exp.Right.Info);
			}

			if (null == prototype)
			{
				throw new RuntimeException("Null reference", exp.Info);
			}

			if (!prototype.NormalProperties.Any(x => x.Key == protoProp.PrototypeID))
			{
				if (exp.AllowLazyInitializaton && AllowLazyPropertyInitialization)
				{
					FieldTypeInfo fieldTypeInfo = null;

					//N20250426-01 - Revise the way we search for initializers
					Prototype protoValue = null;
					if (oLeft is ValueRuntimeInfo)
					{
						ValueRuntimeInfo valueRuntimeInfo = oLeft as ValueRuntimeInfo;
						protoValue = valueRuntimeInfo.Value as Prototype;
					}
					else if (oLeft is Prototype)
					{
						protoValue = oLeft as Prototype;
					}
					else if (oLeft is PrototypeTypeInfo)
					{
						protoValue = (oLeft as PrototypeTypeInfo).Prototype;
					}

					if (null == protoValue)
						throw new RuntimeException("Null property", exp.Info);

					string strPropertyName = StringUtil.RightOfLast(protoProp.PrototypeName, ".");

					PrototypeTypeInfo? infoPrototype = GetPrototypeTypeInfo(protoValue);
					if (null != infoPrototype)
					{
						fieldTypeInfo = infoPrototype.Scope.GetSymbol(strPropertyName) as FieldTypeInfo;
					}

					if (null == fieldTypeInfo)
					{
						foreach (int protoParent in protoValue.GetAllParents())
						{
							//TODO: Prototypes should be indexed by ID in global scope
							infoPrototype = Symbols.GetGlobalScope().GetSymbol(Prototypes.GetPrototypeName(protoParent)) as PrototypeTypeInfo;
							if (null != infoPrototype)
							{
								fieldTypeInfo = infoPrototype.Scope.GetSymbol(strPropertyName) as FieldTypeInfo;
								if (null != fieldTypeInfo)
									break;
							}
						}
					}


					if (null == fieldTypeInfo)
						fieldTypeInfo = exp.FieldInfo;

					LazyPropertyInitializer(prototype, protoProp, fieldTypeInfo);
				}

				else
					return null;
			}

			return prototype.Properties[protoProp.PrototypeID];

		}

		private void LazyPropertyInitializer(Prototype prototype, Prototype protoProp, FieldTypeInfo fieldInfo)
		{
			if (null != fieldInfo.Initializer)
			{
				object oVal = Evaluate(fieldInfo.Initializer);
				prototype.Properties[protoProp.PrototypeID] = GetOrConvertToPrototype(oVal);
			}
		}


		public object EvaluateAsSet(Compiled.Expression exp)
		{
			if (exp == null)
				throw new ArgumentNullException(nameof(exp));

			switch (exp)
			{
				case PrototypeFieldReference protoField:
					return EvaluateAsSet(protoField);

				case GetLocalStack getLocal:
					return EvaluateAsSet(getLocal);

				case GetStack getStack:
					return Evaluate(getStack);          // semantics identical to original

				// .NET members are already a “settable” reference; no further work
				case DotNetFieldReference _:
				case DotNetPropertyReference _:
					return exp;

				case Compiled.IndexOperator indexOp:
					return EvaluateAsSet(indexOp);

				case GetGlobalStack getGlobal:
					return Evaluate(getGlobal);

				default:
					throw new RuntimeException("Not implemented", exp.Info);
			}
		}


		public object EvaluateAsSet(Compiled.IndexOperator exp)
		{
			object left = Evaluate(exp.Left);
			object right = Evaluate(exp.Right);

			if (left is PrototypePropertiesCollection properties)
			{
				if (right is ValueRuntimeInfo)
					right = (right as ValueRuntimeInfo).Value;

				if (right is PrototypeTypeInfo)
					right = (right as PrototypeTypeInfo).Prototype;

				if (right is not Prototype)
				{
					throw new RuntimeException("Cannot assign from non-prototype", exp.Info);
				}

				FieldSetterInfo fieldSetter = new FieldSetterInfo();
				fieldSetter.Prototype = properties.GetParent();
				fieldSetter.Property = right as Prototype;
				return fieldSetter;
			}

			object dotNetTarget = left;
			if (dotNetTarget is ValueRuntimeInfo valueRuntimeInfo)
				dotNetTarget = valueRuntimeInfo.Value;
			else if (dotNetTarget is PrototypeTypeInfo prototypeTypeInfo)
				dotNetTarget = prototypeTypeInfo.Prototype;

			if (dotNetTarget != null && dotNetTarget is not Prototype)
			{
				if (TryCreateDotNetIndexSetter(dotNetTarget, right, exp.Info, out DotNetIndexSetterInfo? dotNetIndexSetterInfo))
					return dotNetIndexSetterInfo;

				throw new RuntimeException(
					$"No applicable public indexer setter was found on type '{dotNetTarget.GetType().FullName}'.",
					exp.Info);
			}

			Prototype protoLeft = GetAsPrototype(left);

			if (null == protoLeft)
				throw new RuntimeException("Indexed set operation must be on a prototype", exp.Info);

			object? indexValue = ValueConversions.GetAs(right, typeof(int));
			if (indexValue is int index)
				right = index;

			if (!(right is int))
				throw new RuntimeException("Indexer must be of type int", exp.Info);

			IndexSetterInfo indexSetterInfo = new IndexSetterInfo() { Collection = protoLeft, Index = (int)right };

			if (indexSetterInfo.Index >= indexSetterInfo.Collection.Children.Count || indexSetterInfo.Index < 0)
				throw new RuntimeException("Index is outside the bounds of the collection", exp.Info);

			return indexSetterInfo;
		}

		private static bool TryCreateDotNetIndexSetter(object target, object keyValue, StatementParsingInfo info, out DotNetIndexSetterInfo? setter)
		{
			setter = null;

			object convertedKeyCandidate = keyValue;
			if (convertedKeyCandidate is ValueRuntimeInfo valueRuntimeInfo)
				convertedKeyCandidate = valueRuntimeInfo.Value;
			else if (convertedKeyCandidate is PrototypeTypeInfo prototypeTypeInfo)
				convertedKeyCandidate = prototypeTypeInfo.Prototype;

			System.Reflection.PropertyInfo[] properties = target.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
			foreach (System.Reflection.PropertyInfo property in properties)
			{
				if (property.SetMethod == null)
					continue;

				System.Reflection.ParameterInfo[] indexParameters = property.GetIndexParameters();
				if (indexParameters.Length != 1)
					continue;

				if (!ValueConversions.TryMakeAssignable(convertedKeyCandidate, new TypeInfo(indexParameters[0].ParameterType), out object convertedKey))
					continue;

				setter = new DotNetIndexSetterInfo
				{
					Target = target,
					Indexer = property,
					ConvertedKey = convertedKey
				};

				return true;
			}

			return false;
		}

		public object EvaluateAsSet(PrototypeFieldReference exp)
		{
			object oLeft = Evaluate(exp.Left);
			object oRight = Evaluate(exp.Right);

			FieldSetterInfo fieldSetter = new FieldSetterInfo();

			if (oLeft is ValueRuntimeInfo)
			{
				ValueRuntimeInfo valueInfo = oLeft as ValueRuntimeInfo;
				fieldSetter.Prototype = valueInfo.Value as Prototype;
			}

			else if (oLeft is Prototype)
			{
				fieldSetter.Prototype = oLeft as Prototype;
			}
			else if (oLeft is PrototypeTypeInfo)
			{
				fieldSetter.Prototype = (oLeft as PrototypeTypeInfo).Prototype;
			}
			else
				throw new RuntimeException("Cannot assign to null value", exp.Info);


			if (oRight is PrototypeTypeInfo)
			{
				PrototypeTypeInfo infoProp = oRight as PrototypeTypeInfo;
				fieldSetter.Property = infoProp.Prototype;
			}
			else
				throw new RuntimeException("Cannot assign from non-prototype", exp.Info);

			return fieldSetter;
		}

		public object EvaluateAsSet(GetLocalStack exp)
		{
			return Evaluate(exp);
		}

		virtual public object Evaluate(FunctionEvaluation exp)
		{
			FunctionRuntimeInfo infoFunc = exp.Function;
			object obj = null;

			if (null != exp.Object)
			{
				obj = Evaluate(exp.Object);

				Prototype protoSpecificBase = null;

				//N20220922-01 - Use the "cast" form to force a method
				if (exp.Object is Compiled.CastingOperator2)        //in this case we want a specific method
				{
					protoSpecificBase = (exp.Object.InferredType as PrototypeTypeInfo).Prototype;
				}

				//Try to locate an overriden method
				if (obj is ValueRuntimeInfo)
				{
					ValueRuntimeInfo infoVar = obj as ValueRuntimeInfo;

					//"this" will be null for static method calls
					if (infoVar.Value != null)
					{
						if (infoVar.Type is PrototypeTypeInfo)
						{
							PrototypeTypeInfo infoType = infoVar.Type as PrototypeTypeInfo;

							//N20220209-01 - Use the Value not the Type for overriding
							FunctionRuntimeInfo funcOverriden = null;
							if (null == protoSpecificBase)
								funcOverriden = FindOverriddenMethod(infoVar.Value as Prototype, infoFunc.FunctionName);
							else
								funcOverriden = FindOverriddenMethodSingular(protoSpecificBase, infoFunc.FunctionName);

							if (null != funcOverriden)
								infoFunc = funcOverriden;

							if (LogMethodCalls)
							{
								Logs.DebugLog.WriteEvent("Calling Method", infoType.Prototype.PrototypeName + "." + infoFunc.FunctionName);
							}

						}
					}
				}
				else if (obj is Prototype)
				{
					FunctionRuntimeInfo funcOverriden = null;
					if (null == protoSpecificBase)
						funcOverriden = FindOverriddenMethod(obj as Prototype, infoFunc.FunctionName);
					else
						funcOverriden = FindOverriddenMethodSingular(protoSpecificBase, infoFunc.FunctionName);

					if (null != funcOverriden)
						infoFunc = funcOverriden;

					if (LogMethodCalls)
					{
						Logs.DebugLog.WriteEvent("Calling Method", infoFunc.FunctionName);
					}
				}

			}

			Scope scope = GetFunctionEvaluationScope(infoFunc, exp.Parameters);

			Symbols.EnterScope(scope);

			object oReturn = null;

			try
			{

				if (null != obj)
					scope.Stack[1] = obj;

				//Add a check for at a return value
				if (infoFunc.ReturnType != null)
					scope.Stack[0] = new UninitializedReturn();

				foreach (Compiled.Statement statement in infoFunc.Statements)
				{
					if (Evaluate(statement))
						break;
				}

				if (infoFunc.ReturnType != null)
				{
					oReturn = scope.Stack[0];

					if (oReturn is ValueRuntimeInfo)
						oReturn = (oReturn as ValueRuntimeInfo).Value;

					if (oReturn != null
						&& (infoFunc.ReturnType.Type == typeof(StringReference)
							|| infoFunc.ReturnType.Type == typeof(string))
						&& oReturn.GetType() != infoFunc.ReturnType.Type)
					{
						object? converted;
						if (ValueConversions.TryMakeAssignable(oReturn, infoFunc.ReturnType, out converted))
							oReturn = converted;
					}
				}

				if (oReturn is UninitializedReturn)
					throw new RuntimeException("Method did not return a value", infoFunc.Info);
			}
			finally
			{
				Symbols.LeaveScope();
			}

			return oReturn;
		}

		private Scope GetFunctionEvaluationScope(FunctionRuntimeInfo infoFunc, List<Compiled.Expression> lstParameters)
		{

			List<object> lstParameterValues = new List<object>();

			if (lstParameters.Count != infoFunc.Parameters.Count)
				throw new RuntimeException("Number of parameters does not match: " + infoFunc.FunctionName, infoFunc.Info);

			for (int i = 0; i < lstParameters.Count; i++)
			{
				Compiled.Expression expParam = lstParameters[i];
				ParameterRuntimeInfo infoParam = (ParameterRuntimeInfo)infoFunc.Parameters[i].Clone();

				object oParamValue = Evaluate(expParam);
				lstParameterValues.Add(oParamValue);
			}

			try
			{
				return GetFunctionEvaluationScope2(infoFunc, lstParameterValues);
			}
			catch (RuntimeException err)
			{
				//This is a hack to allow RunMethod and Evaluate(FunctionEvalutation) 
				//to both use this and method, and to try to preserve the StatementParsingINfo 
				//for the parameter
				if (err.Message == "Parameter is null")
					err.Info = lstParameters[err.Info.StartingOffset].Info;

				throw;
			}
		}

		private Scope GetFunctionEvaluationScope2(FunctionRuntimeInfo infoFunc, List<object> lstParameters)
		{
			// Evaluate each parameter and bind it into a cloned function scope with detailed assignment diagnostics.
			Scope scope = infoFunc.Scope.Clone();

			for (int i = 0; i < lstParameters.Count; i++)
			{
				object oParamValue = lstParameters[i];
				ParameterRuntimeInfo infoParam = (ParameterRuntimeInfo)infoFunc.Parameters[i].Clone();

				try
				{
					infoParam.Value = MakeAssignable(oParamValue, infoParam.Type, infoFunc.Info);
				}
				catch (RuntimeException err) when (err.Message == "Cannot assign value")
				{
					string functionName = string.IsNullOrWhiteSpace(infoFunc.FunctionName)
						? "(unknown)"
						: infoFunc.FunctionName;
					string parameterName = string.IsNullOrWhiteSpace(infoParam.ParameterName)
						? $"arg{i}"
						: infoParam.ParameterName;
					string expectedType = infoParam.Type?.ToString() ?? "(unknown)";
					string actualType = oParamValue?.GetType().FullName ?? "null";
					string actualValue = DescribeValueForDiagnostic(oParamValue);
					string detail = $"Cannot assign value for function '{functionName}' parameter '{parameterName}' at index {i}. Expected {expectedType}, got {actualType} value={actualValue}.";
					throw new RuntimeException(detail, err.Info ?? infoFunc.Info, err);
				}

				scope.Stack[infoParam.Index] = infoParam;
			}

			//Scope.Clone takes care of the following now
			//for (int i = 0; i < infoFunc.Scope.Stack.Count; i++)
			//{
			//	object oVal = infoFunc.Scope.Stack[i];
			//	if (oVal is VariableRuntimeInfo)
			//	{
			//		scope.Stack[i] = ((VariableRuntimeInfo)oVal).Clone();
			//	}
			//}

			return scope;
		}

		private static string DescribeValueForDiagnostic(object? value)
		{
			if (value == null)
				return "null";

			if (value is string str)
			{
				if (str.Length > 80)
					return "\"" + str.Substring(0, 80) + "...\"";
				return "\"" + str + "\"";
			}

			if (value is PrototypeTypeInfo prototypeTypeInfo)
				return "PrototypeTypeInfo(" + (prototypeTypeInfo.Prototype?.PrototypeName ?? "null") + ")";

			if (value is FunctionRuntimeInfo functionRuntimeInfo)
				return "FunctionRuntimeInfo(" + functionRuntimeInfo.FunctionName + ")";

			if (value is Prototype prototype)
				return "Prototype(" + prototype.PrototypeName + ")";

			if (value is ValueRuntimeInfo valueRuntimeInfo)
				return "ValueRuntimeInfo(" + (valueRuntimeInfo.Type?.ToString() ?? "unknown") + ")";

			try
			{
				string text = value.ToString() ?? value.GetType().Name;
				if (text.Length > 80)
					text = text.Substring(0, 80) + "...";
				return text;
			}
			catch
			{
				return value.GetType().Name;
			}
		}

		public Prototype? RunMethodAsPrototype(Prototype? protoInstance, string strMethodName, List<object> lstParameters)
		{
			object? oRes = RunMethodAsObject(protoInstance, strMethodName, lstParameters);

			return GetOrConvertToPrototype(oRes);
		}
		public object? RunMethodAsObject(Prototype? protoInstance, string strMethodName, List<object> lstParameters)
		{

			FunctionRuntimeInfo? infoFunction = null;

			if (null == protoInstance)
			{
				infoFunction = this.Symbols.GetSymbol(strMethodName) as FunctionRuntimeInfo;
				if (null == infoFunction)
					throw new MissingMethodException($"Could not find global method '{strMethodName}' with {lstParameters.Count} argument(s).");
			}

			else
			{
				infoFunction = this.FindOverriddenMethod(protoInstance, strMethodName);

				if (null == infoFunction)
					throw new MissingMethodException($"Could not find method '{strMethodName}' on prototype '{protoInstance.PrototypeName}' with {lstParameters.Count} argument(s).");
			}

			return this.RunMethod(infoFunction, protoInstance, lstParameters);
		}

		/// <summary>
		/// Runs a ProtoScript method using named arguments.
		/// </summary>
		/// <example>
		/// IDictionary<string, object> args = new Dictionary<string, object>();
		/// args["child"] = interpreter.GetLocalPrototype("Bart");
		/// interpreter.RunMethodAsObject("Homer", "IsParentOf", args);
		/// </example>
		public object? RunMethodAsObject(Prototype? protoInstance, string strMethodName, IDictionary<string, object> dictParameters)
		{
			FunctionRuntimeInfo? infoFunction = null;

			if (null == protoInstance)
			{
				infoFunction = this.Symbols.GetSymbol(strMethodName) as FunctionRuntimeInfo;
				if (null == infoFunction)
					throw new MissingMethodException($"Could not find global method '{strMethodName}' for named-argument invocation.");
			}

			else
			{
				infoFunction = this.FindOverriddenMethod(protoInstance, strMethodName);

				if (null == infoFunction)
					throw new MissingMethodException($"Could not find method '{strMethodName}' on prototype '{protoInstance.PrototypeName}' for named-argument invocation.");
			}

			List<object> lstParameters = new List<object>();

			foreach (ParameterRuntimeInfo infoParam in infoFunction.Parameters)
			{
				object oValue;
				if (!dictParameters.TryGetValue(infoParam.ParameterName, out oValue))
				{
					string provided = dictParameters.Count == 0 ? "(none)" : string.Join(", ", dictParameters.Keys);
					throw new ArgumentException($"Missing parameter '{infoParam.ParameterName}' for method '{strMethodName}'. Provided keys: {provided}", nameof(dictParameters));
				}
				lstParameters.Add(oValue);
			}

			return this.RunMethod(infoFunction, protoInstance, lstParameters);
		}

		public Prototype? RunMethodAsPrototype(Prototype protoInstance, string strMethodName, object oParam1)
		{
			return RunMethodAsPrototype(protoInstance, strMethodName, new List<object> { oParam1 });
		}

		public Prototype? RunMethodAsPrototype(FunctionRuntimeInfo infoFunc, object objInstance, List<object> lstParameters)
		{
			object? oRes = this.RunMethod(infoFunc, objInstance, lstParameters);

			return GetOrConvertToPrototype(oRes);
		}

		public object? RunMethod(FunctionRuntimeInfo infoFunc, object objInstance, List<object> lstParameters)
		{
			if (Interlocked.Exchange(ref m_isRunningEntryPoint, 1) == 1)
				throw new InvalidOperationException("NativeInterpretter does not support concurrent entry-point execution. Create a separate interpreter instance per concurrent call.");

			try
			{
				return RunMethodCore(infoFunc, objInstance, lstParameters);
			}
			finally
			{
				Volatile.Write(ref m_isRunningEntryPoint, 0);
			}
		}

		private object? RunMethodCore(FunctionRuntimeInfo infoFunc, object objInstance, List<object> lstParameters)
		{
			if (LogMethodCalls)
			{
				Logs.DebugLog.WriteEvent("Calling Method", (infoFunc.ParentPrototype == null ? "" : infoFunc.ParentPrototype.PrototypeName + ".") + infoFunc.FunctionName);
			}

			if (infoFunc.Parameters.Count != lstParameters.Count)
				throw new RuntimeException($"Not enough parameters passed to function {infoFunc.FunctionName}. {lstParameters.Count} vs {infoFunc.Parameters.Count}", infoFunc.Info);

			Scope scope = GetFunctionEvaluationScope2(infoFunc, lstParameters);
			Symbols.EnterScope(scope);
			object? oReturn = null;

			try
			{
				if (null != objInstance)
					scope.Stack[1] = objInstance;

				if (infoFunc.ReturnType != null)
				{
					scope.Stack[0] = new UninitializedReturn();
				}

				foreach (Compiled.Statement statement in infoFunc.Statements)
				{
					if (Evaluate(statement))
						break;
				}

				if (infoFunc.ReturnType != null)
				{
					oReturn = scope.Stack[0];
					if (oReturn is ValueRuntimeInfo valueRuntimeInfo)
						oReturn = valueRuntimeInfo.Value;

					// Only coerce string boundary contracts on external invocation; keep legacy return semantics for other types.
					if (oReturn != null
						&& (infoFunc.ReturnType.Type == typeof(StringReference)
							|| infoFunc.ReturnType.Type == typeof(string))
						&& oReturn.GetType() != infoFunc.ReturnType.Type)
					{
						object? converted;
						if (ValueConversions.TryMakeAssignable(oReturn, infoFunc.ReturnType, out converted))
							oReturn = converted;
					}

					if (oReturn is UninitializedReturn)
						throw new RuntimeException("Method did not return a value", infoFunc.Info);
				}
			}
			finally
			{
				Symbols.LeaveScope();
			}

			return oReturn;
		}

		public FunctionRuntimeInfo FindOverriddenMethod(Prototype prototype, string strFunctionName)
		{
			FunctionRuntimeInfo funcOverriden = FindOverriddenMethodSingular(prototype, strFunctionName);
			if (null != funcOverriden)
				return funcOverriden;

			//Changed to GetAllParents to it doesn't traverse the same prototypes more than once
			//N20221115-02 - Need to get the most specific if the TypeOfs are not in the correct order
			Prototype? protoFoundTypeOf = null;
			foreach (int protoTypeOfID in prototype.GetAllParents())
			{
				Prototype protoTypeOf = Prototypes.GetPrototype(protoTypeOfID);

				if (null == protoFoundTypeOf || Prototypes.TypeOf(protoTypeOf, protoFoundTypeOf))
				{
					FunctionRuntimeInfo funcOverridenSingle = FindOverriddenMethodSingular(protoTypeOf, strFunctionName);

					if (null != funcOverridenSingle)
					{
						funcOverriden = funcOverridenSingle;
						protoFoundTypeOf = protoTypeOf;
					}
				}
			}

			return funcOverriden;
		}

		private FunctionRuntimeInfo FindOverriddenMethodSingular(Prototype prototype, string strFunctionName)
		{
			PrototypeTypeInfo? infoPrototype = GetPrototypeTypeInfo(prototype);
			FunctionRuntimeInfo funcOverriden = null;

			//We can be looking at an instance, that won't have a base type, but we still 
			//need to consider it's TypeOfs
			if (null != infoPrototype &&
				null != infoPrototype.Scope     //For hidden entities we may not have a scope if the field is added as a TypeOf
				)
			{
				Scope scope = infoPrototype.Scope;

				if (scope.TryGetSymbol(strFunctionName, out funcOverriden))
				{
					if (LogMethodCalls)
						Logs.DebugLog.WriteEvent("Found Method", prototype.PrototypeName + "." + strFunctionName);

					return funcOverriden;
				}

			}

			return null;
		}

		public object Evaluate(DotNetFieldReference exp)
		{
			object obj = Evaluate(exp.Object);
			if (exp.IsNullConditional)
			{
				if (obj is ValueRuntimeInfo val)
					obj = val.Value;
				if (obj == null)
					return null;
			}
			if (obj is ValueRuntimeInfo) obj = ((ValueRuntimeInfo)obj).Value;
			else if (obj is PrototypeTypeInfo) obj = ((PrototypeTypeInfo)obj).Prototype;

			var getter = ReflectionCache.GetGetter(exp.Field);   // cached delegate
			return getter(obj);                                  // no reflection
		}

		public object Evaluate(DotNetPropertyReference exp)
		{
			object obj = Evaluate(exp.Object);
			if (exp.IsNullConditional)
			{
				if (obj is ValueRuntimeInfo val)
					obj = val.Value;
				if (obj == null)
					return null;
			}
			if (obj is ValueRuntimeInfo) obj = ((ValueRuntimeInfo)obj).Value;
			else if (obj is PrototypeTypeInfo) obj = ((PrototypeTypeInfo)obj).Prototype;

			var getter = ReflectionCache.GetGetter(exp.Property); // cached delegate
			return getter(obj);
		}

		virtual public object Evaluate(DotNetMethodEvaluation exp)
		{
			int iParamCount = exp.Parameters.Count;
			List<object> lstParameters = new List<object>(iParamCount);

			System.Reflection.ParameterInfo[] infoParams = exp.Method.GetParameters();
			bool hasParamArray = infoParams.Length > 0 &&
				infoParams[infoParams.Length - 1].IsDefined(typeof(ParamArrayAttribute), inherit: false);

			bool bSaved = AllowLazyPropertyInitialization;
			object obj;
			try
			{
				AllowLazyPropertyInitialization = true;
				obj = Evaluate(exp.Object);
			}
			finally
			{
				AllowLazyPropertyInitialization = bSaved;
			}

			if (exp.IsNullConditional)
			{
				if (obj is ValueRuntimeInfo val)
					obj = val.Value;
				else if (obj is PrototypeTypeInfo pti)
					obj = pti.Prototype;
				if (obj == null && !exp.Method.IsStatic)
					return null;
			}

			for (int i = 0; i < iParamCount; i++)
			{
				Compiled.Expression expParam = exp.Parameters[i];

				if (expParam is LambdaOperator lambdaOperator)
				{
					lambdaOperator.Interpretter = this;
					Delegate del = Delegate.CreateDelegate(typeof(Predicate<Prototype>),
						expParam, "Predicate");

					lstParameters.Add(del);
				}
				else
				{
					object oParam = Evaluate(expParam);

					if (oParam is ValueRuntimeInfo valueRuntimeInfo)
					{
						oParam = valueRuntimeInfo.Value;
					}

					System.Type targetParameterType;
					if (hasParamArray && i >= infoParams.Length - 1)
					{
						System.Type? elementType = infoParams[infoParams.Length - 1].ParameterType.GetElementType();
						if (elementType == null)
							throw new RuntimeException("Invalid params method signature", exp.Info);

						targetParameterType = elementType;
					}
					else
					{
						if (i >= infoParams.Length)
							throw new RuntimeException("Incorrect number of parameters", exp.Info);

						targetParameterType = infoParams[i].ParameterType;
					}

					lstParameters.Add(MakeAssignable(oParam, new TypeInfo(targetParameterType), expParam.Info));

				}
			}

			object[] arrParametersForInvoke = PrepareDotNetMethodArguments(infoParams, lstParameters, hasParamArray);

			if (obj is ValueRuntimeInfo valueRuntimeInfo1)
				obj = valueRuntimeInfo1.Value;
			else if (obj is PrototypeTypeInfo prototypeTypeInfo)
				obj = prototypeTypeInfo.Prototype;

			if (obj != null && !exp.Method.IsStatic)
			{
				System.Type? receiverType = exp.Method.DeclaringType;
				if (receiverType != null && !receiverType.IsAssignableFrom(obj.GetType()))
				{
					obj = MakeAssignable(obj, new TypeInfo(receiverType), exp.Info);
				}
			}

			if (null == obj && !exp.Method.IsStatic)
			{
				throw new RuntimeException("The method " + exp.Method.Name + " requires an instance, but the the object is null", exp.Info);
			}

			try
			{
				System.Reflection.MethodInfo oMethod = exp.Method;
				var executor = ReflectionCache.GetExecutor(oMethod);

				//Note: if this fails it may be because a .NET object was serialized to JSON 
				//then turned back into a prototype on deserialization
				object? oReturn;

				if (typeof(Task).IsAssignableFrom(oMethod.ReturnType))
				{
					// It's an async method
					object taskObj = executor(obj, arrParametersForInvoke);
					if (taskObj == null)
						return null;

					System.Type returnType = oMethod.ReturnType;
					if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
					{
						// It's Task<T>
						// Get the type argument of Task<T>
						System.Type taskTypeArgument = returnType.GetGenericArguments()[0];

						// Get the awaited result using reflection
						oReturn = GetTaskResultSync(taskObj, taskTypeArgument);
					}
					else
					{
						((Task)taskObj).Wait(); // Wait for completion
						oReturn = null;
					}
				}
				else
				{
					// Synchronous method
					oReturn = executor(obj, arrParametersForInvoke);
				}

				return oReturn;
			}
			catch (System.Reflection.TargetInvocationException err)
			{
				throw new RuntimeException(err.InnerException.Message, exp.Info);
			}

		}

		private static object[] PrepareDotNetMethodArguments(System.Reflection.ParameterInfo[] infoParams, List<object> providedParameters, bool hasParamArray)
		{
			if (!hasParamArray)
			{
				List<object> finalParametersNoParams = new List<object>(infoParams.Length);
				for (int i = 0; i < infoParams.Length; i++)
				{
					if (i < providedParameters.Count)
					{
						finalParametersNoParams.Add(providedParameters[i]);
						continue;
					}

					if (infoParams[i].IsOptional)
					{
						finalParametersNoParams.Add(GetOptionalParameterValue(infoParams[i]));
						continue;
					}

					throw new ArgumentException($"Missing required parameter '{infoParams[i].Name ?? i.ToString()}' for method invocation.");
				}

				return finalParametersNoParams.ToArray();
			}

			int fixedCount = infoParams.Length - 1;
			System.Type paramArrayType = infoParams[fixedCount].ParameterType;
			System.Type? elementType = paramArrayType.GetElementType();
			if (elementType == null)
			{
				string parameterName = infoParams[fixedCount].Name ?? "(unknown)";
				throw new InvalidOperationException($"Invalid params signature. Parameter '{parameterName}' was expected to be an array but was '{paramArrayType.FullName}'.");
			}

			List<object> finalParameters = new List<object>(infoParams.Length);
			for (int i = 0; i < fixedCount; i++)
			{
				if (i < providedParameters.Count)
				{
					finalParameters.Add(providedParameters[i]);
				}
				else if (infoParams[i].IsOptional)
				{
					finalParameters.Add(GetOptionalParameterValue(infoParams[i]));
				}
				else
				{
					throw new ArgumentException($"Missing required parameter '{infoParams[i].Name ?? i.ToString()}' for method invocation.");
				}
			}

			int paramsValueCount = Math.Max(0, providedParameters.Count - fixedCount);
			if (paramsValueCount == 1)
			{
				object singleValue = providedParameters[fixedCount];
				if (singleValue != null && paramArrayType.IsAssignableFrom(singleValue.GetType()))
				{
					finalParameters.Add(singleValue);
					return finalParameters.ToArray();
				}
			}

			Array paramsArray = Array.CreateInstance(elementType, paramsValueCount);
			for (int i = 0; i < paramsValueCount; i++)
			{
				paramsArray.SetValue(providedParameters[fixedCount + i], i);
			}

			finalParameters.Add(paramsArray);
			return finalParameters.ToArray();
		}

		private static object? GetOptionalParameterValue(System.Reflection.ParameterInfo parameterInfo)
		{
			object? defaultValue = parameterInfo.DefaultValue;
			if (defaultValue == DBNull.Value)
				return null;

			if (defaultValue == System.Reflection.Missing.Value)
				return System.Type.Missing;

			return defaultValue;
		}


		// Helper method to synchronously get Task<T> results
		private static object? GetTaskResultSync(object taskObj, System.Type taskType)
		{
			System.Reflection.PropertyInfo resultProperty = taskObj.GetType().GetProperty("Result");
			return resultProperty?.GetValue(taskObj);
		}
		public object Evaluate(DotNetNewInstance exp)
		{
			// pre-size to avoid List growth
			List<object> lstParameters = new List<object>(exp.Parameters.Count);

			System.Reflection.ParameterInfo[] infoParams = exp.Constructor.GetParameters();

			for (int i = 0; i < exp.Parameters.Count; i++)
			{
				Compiled.Expression expParam = exp.Parameters[i];
				System.Reflection.ParameterInfo infoParam = infoParams[i];

				object oParam = Evaluate(expParam);

				if (oParam is ValueRuntimeInfo)
					oParam = (oParam as ValueRuntimeInfo).Value;

				lstParameters.Add(
					MakeAssignable(oParam,
								   new TypeInfo(infoParam.ParameterType),
								   expParam.Info));
			}

			// use cached delegate instead of ConstructorInfo.Invoke
			var activator = ReflectionCache.GetConstructor(exp.Constructor);   // Func<object?[],object?>
			object oReturn = activator(lstParameters.ToArray());

			foreach (DotNetNewInstance.MemberInitializer initializer in exp.MemberInitializers)
			{
				ApplyDotNetMemberInitializer(oReturn, initializer);
			}

			foreach (DotNetNewInstance.CollectionInitializer initializer in exp.CollectionInitializers)
			{
				ApplyDotNetCollectionInitializer(oReturn, initializer);
			}

			return oReturn;
		}

		private void ApplyDotNetMemberInitializer(object target, DotNetNewInstance.MemberInitializer initializer)
		{
			object value = Evaluate(initializer.Value);
			if (value is ValueRuntimeInfo valueRuntimeInfo)
				value = valueRuntimeInfo.Value;

			System.Type targetType = target.GetType();
			System.Reflection.PropertyInfo? property = targetType.GetProperty(initializer.Name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
			if (property != null && property.SetMethod != null)
			{
				object converted = ConvertDotNetInitializerValue(value, property.PropertyType, initializer.Info);
				property.SetValue(target, converted);
				return;
			}

			System.Reflection.FieldInfo? field = targetType.GetField(initializer.Name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
			if (field != null)
			{
				object converted = ConvertDotNetInitializerValue(value, field.FieldType, initializer.Info);
				field.SetValue(target, converted);
				return;
			}

			throw new RuntimeException($"Cannot apply object initializer member '{initializer.Name}' to type '{targetType.FullName}'.", initializer.Info);
		}

		private void ApplyDotNetCollectionInitializer(object target, DotNetNewInstance.CollectionInitializer initializer)
		{
			object value = Evaluate(initializer.Value);
			if (value is ValueRuntimeInfo valueRuntimeInfo)
				value = valueRuntimeInfo.Value;

			if (TryAddDotNetCollectionValue(target, value, initializer.Info))
				return;

			throw new RuntimeException($"Cannot apply collection initializer entry to type '{target.GetType().FullName}'.", initializer.Info);
		}

		private bool TryAddDotNetCollectionValue(object target, object value, StatementParsingInfo info)
		{
			System.Type type = target.GetType();
			foreach (System.Reflection.MethodInfo method in type.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
			{
				if (!string.Equals(method.Name, "Add", StringComparison.Ordinal))
					continue;

				System.Reflection.ParameterInfo[] parameters = method.GetParameters();
				if (parameters.Length != 1)
					continue;

				object convertedValue = ConvertDotNetInitializerValue(value, parameters[0].ParameterType, info);
				method.Invoke(target, new object[] { convertedValue });
				return true;
			}

			return false;
		}
		private static object ConvertDotNetInitializerValue(object value, System.Type targetType, StatementParsingInfo info)
		{
			if (targetType == typeof(JsonValue))
			{
				if (value is JsonValue jsonValue)
					return jsonValue;

				return new JsonValue(value);
			}

			if (ValueConversions.TryMakeAssignable(value, new TypeInfo(targetType), out object convertedValue))
				return convertedValue;

			throw new RuntimeException($"Cannot assign initializer value to target type '{targetType.FullName}'.", info);
		}


	}
}

