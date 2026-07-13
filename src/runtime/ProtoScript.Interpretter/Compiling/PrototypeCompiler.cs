//added
using BasicUtilities;
using Ontology;
using ProtoScript.Diagnostics;
using ProtoScript.Interpretter.RuntimeInfo;
using ProtoScript.Interpretter.Compiled;
using ProtoScript.Interpretter.Symbols;
using ProtoScript.Parsers;

namespace ProtoScript.Interpretter.Compiling
{
	public class PrototypeCompiler
	{
		static public bool RequireFieldInitializers = false;
		static public bool EnableDatabase = true;

		static public PrototypeTypeInfo InsertTemporaryPrototypeAsSymbol(Prototype prototype, SymbolTable symbols)
		{
			string strPrototypeName = prototype.PrototypeName;

			PrototypeTypeInfo info = new PrototypeTypeInfo();
			info.Prototype = prototype;
			info.Index = symbols.GlobalStack.Add(info);
			info.Scope = new Scope(Scope.ScopeTypes.Class);
			info.Scope.InsertSymbol("that", info);


			//N20231010-01 - Short cut format for creating nested prototypes
			if (strPrototypeName.Contains(".") && !StringUtil.InString(strPrototypeName, ".Field."))
			{
				string[] parts = StringUtil.Split(strPrototypeName, ".");
				string strName = parts[^1];                             // last segment

				List<string> lstNamespaces = new List<string>(parts.Length - 1);
				for (int i = 0; i < parts.Length - 1; i++)
					lstNamespaces.Add(parts[i]);

				Scope ns = NamespaceCompiler.GetOrInsertNamespaceChain(lstNamespaces, symbols);
				ns.InsertSymbol(strName, info);
			}

			symbols.GetGlobalScope().InsertSymbol(strPrototypeName, info);
			prototype.Data["TypeInfo"] = info;

			return info;
		}

		static public PrototypeDeclaration ? DeclarePrototype(PrototypeDefinition protoDef, Compiler compiler)
		{
			Prototype ? prototype = null;

			if (compiler.Symbols.GetGlobalScope().TryGetSymbol(protoDef.PrototypeName.TypeName, out Object obj))
			{
				PrototypeTypeInfo ? infoExisting = obj as PrototypeTypeInfo;

				if (null == infoExisting)
				{
					compiler.AddDiagnostic(new Diagnostic("Prototype already exists as another type: " + protoDef.PrototypeName.TypeName), protoDef, null);
					return null;
				}

				prototype = infoExisting.Prototype;
				protoDef.ResolvedPrototype = prototype;

				if (protoDef.IsPartial)
					prototype.Data["IsPartial"] = true;

				if (!protoDef.IsPartial && prototype.Data.TryGetValue("IsPartial", out object ? b) && b as bool? != true)
				{
					compiler.AddDiagnostic(new Diagnostic("Prototype already exists: " + protoDef.PrototypeName), protoDef, null);
					return null;
				}

				return new PrototypeDeclaration() { PrototypeName = protoDef.PrototypeName.TypeName, TypeInfo = (obj as PrototypeTypeInfo), Info = protoDef.Info };
			}


			if (protoDef.IsExternal && !EnableDatabase)
			{
				compiler.AddDiagnostic(new Diagnostic("External prototypes are not supported when the database is disabled"), protoDef, null);
				return null;
			}

			prototype = Prototypes.GetOrInsertPrototype(protoDef.PrototypeName.TypeName);
			if (protoDef.IsPartial)
				prototype.Data["IsPartial"] = true;
			protoDef.ResolvedPrototype = prototype;

			PrototypeTypeInfo info = InsertTemporaryPrototypeAsSymbol(prototype, compiler.Symbols);

			if (protoDef.IsExternal)
				info.Type = typeof(Prototype);

			if (protoDef.PrototypeName.ElementTypes.Count > 0)
			{
				//Note: This is so we can support syntax like new T() during compilation
				foreach (Type type in protoDef.PrototypeName.ElementTypes)
				{
					TypeInfo typeInfo = new GenericTypeInfo();
					typeInfo.Index = info.Scope.Stack.Add(typeInfo);
					info.Scope.InsertSymbol(type.TypeName, typeInfo);
				}

				info.IsGeneric = true;
			}
			
			if (protoDef.PrototypeDefinitions.Count > 0)
				DeclareNestedPrototypes(protoDef, compiler);

			return new PrototypeDeclaration() { PrototypeName = protoDef.PrototypeName.TypeName, TypeInfo = info, Info = protoDef.Info };
		}
	

		//uses Scope instead of ResolvePrototype
		static public void DeclarePrototypeTypeOfs(PrototypeDefinition protoDef, Compiler compiler)
		{
			Prototype prototype = GetResolvedPrototypeOrThrow(protoDef);
			PrototypeTypeInfo info = GetPrototypeTypeInfoOrThrow(prototype, protoDef, compiler);

			foreach (ProtoScript.Type typeOf in protoDef.Inherits)
			{				
				PrototypeTypeInfo ? typeInfo = compiler.Symbols.GetTypeInfo(typeOf) as PrototypeTypeInfo;
				Prototype ? protoTypeOf = typeInfo?.Prototype;

				if (null == protoTypeOf)
				{
					compiler.AddDiagnostic(BuildMissingTypeOfDiagnostic(typeOf.TypeName, compiler), protoDef, null);
					return;
				}

				// Re-compiling the same project in-process can hit this path with existing
				// inheritance already established on persistent ontology prototypes.
				// Re-inserting an existing direct type-of edge can trigger deep cache
				// invalidation in Ontology internals, so skip direct no-op links.
				// Keep this check cheap (direct edge only) for large compile batches.
				// PERF NOTE: this guard was added to avoid reload recursion crashes. A prior
				// version used Prototypes.TypeOf(...) and was flagged as a hot-path slowdown
				// risk at scale; we intentionally keep this as direct-edge Contains(...) only.
				// Revisit if compile throughput regresses under very large prototype counts.
				if (prototype.GetTypeOfs().Contains(protoTypeOf.PrototypeID))
				{
					if (null == info.PrimaryParent)
						info.PrimaryParent = protoTypeOf;
					continue;
				}

				if (protoTypeOf.GetTypeOfs().Contains(prototype.PrototypeID))
				{
					compiler.AddDiagnostic("Cicular inheritance detected: " + typeOf.TypeName, protoDef, null);
					return;
				}

				prototype.InsertTypeOf(protoTypeOf);

				//N20220602-02
				if (null == info.PrimaryParent)
					info.PrimaryParent = protoTypeOf;
			}

			foreach (PrototypeDefinition prototypeDefinition in protoDef.PrototypeDefinitions)
			{
				PrototypeCompiler.DeclarePrototypeTypeOfs(prototypeDefinition, compiler);
			}
		}

		private static string BuildMissingTypeOfDiagnostic(string typeName, Compiler compiler)
		{
			TypeInfo? resolvedTypeInfo = compiler.Symbols.GetTypeInfo(typeName);
			if (resolvedTypeInfo != null && resolvedTypeInfo is not PrototypeTypeInfo)
			{
				string resolvedName = resolvedTypeInfo.Type?.FullName ?? resolvedTypeInfo.GetType().Name;
				return "Type Of is not found: " + typeName
					+ ". '" + typeName + "' resolves to non-prototype type '" + resolvedName + "'. "
					+ "Prototype inheritance only supports prototype base types. "
					+ "Use a field/property for value types instead.";
			}

			if (IsLikelyNativeValueTypeName(typeName))
			{
				return "Type Of is not found: " + typeName
					+ ". '" + typeName + "' appears to be a value/native type. "
					+ "Prototype inheritance only supports prototype base types. "
					+ "Use a field/property for value types instead.";
			}

			return "Type Of is not found: " + typeName;
		}

		private static bool IsLikelyNativeValueTypeName(string typeName)
		{
			return StringUtil.EqualNoCase(typeName, "string")
				|| StringUtil.EqualNoCase(typeName, "String")
				|| StringUtil.EqualNoCase(typeName, "bool")
				|| StringUtil.EqualNoCase(typeName, "Boolean")
				|| StringUtil.EqualNoCase(typeName, "int")
				|| StringUtil.EqualNoCase(typeName, "Int32")
				|| StringUtil.EqualNoCase(typeName, "long")
				|| StringUtil.EqualNoCase(typeName, "Int64")
				|| StringUtil.EqualNoCase(typeName, "double")
				|| StringUtil.EqualNoCase(typeName, "float")
				|| StringUtil.EqualNoCase(typeName, "decimal")
				|| StringUtil.EqualNoCase(typeName, "DateTime")
				|| StringUtil.EqualNoCase(typeName, "Guid");
		}



		static public List<Compiled.Statement> DeclarePrototypeFunctions(PrototypeDefinition protoDef, Compiler compiler)
		{
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			Prototype prototype = GetResolvedPrototypeOrThrow(protoDef);
			PrototypeTypeInfo info = GetPrototypeTypeInfoOrThrow(prototype, protoDef, compiler);

			compiler.Symbols.EnterScope(info.Scope);

			try
			{
				foreach (FunctionDefinition functionDefinition in protoDef.Methods)
				{
					FunctionRuntimeInfo infoFunc = compiler.DeclareMethod(functionDefinition, prototype);
				}
			}
			finally
			{
				compiler.Symbols.LeaveScope();
			}

			return lstStatements;
		}

		static public List<Compiled.Statement> DefinePrototypes(ProtoScript.File file, Compiler compiler)
		{
			compiler.Source = file.RawCode;

			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			foreach (PrototypeDefinition protoDef in file.PrototypeDefinitions)
			{
				try
				{
					List<Compiled.Statement>? prototypeStatements = DefinePrototype(protoDef, compiler);
					if (prototypeStatements != null)
						lstStatements.AddRange(prototypeStatements);
				}
				catch (ProtoScriptCompilerException)
				{
					throw;
				}
				catch (Exception ex)
				{
					StatementParsingInfo info = protoDef.Info ?? new StatementParsingInfo { StartingOffset = 0, Length = 1 };
					string stackFrame = ex.StackTrace?
						.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
						.FirstOrDefault()?.Trim() ?? string.Empty;
					string detail = string.IsNullOrWhiteSpace(stackFrame) ? ex.Message : ex.Message + " @ " + stackFrame;
					throw new ProtoScriptCompilerException(
						info,
						$"Failed to define prototype '{protoDef.PrototypeName?.TypeName ?? "(unknown)"}': {ex.GetType().Name}: {detail}");
				}
			}

			return lstStatements;
		}

		static public List<Compiled.Statement> DefinePrototype(PrototypeDefinition protoDef, Compiler compiler)
		{
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			//Ignore the ElementTypes here. If we have 
			//prototype Converts<T>;
			//then ignore the T until the Declaration. 
			Prototype prototype = GetResolvedPrototypeOrThrow(protoDef);
			PrototypeTypeInfo info = GetPrototypeTypeInfoOrThrow(prototype, protoDef, compiler);

			List<Compiled.Statement>? prototypeFunctions = DefinePrototypeFunctions(protoDef, info, compiler);
			if (prototypeFunctions != null)
				lstStatements.AddRange(prototypeFunctions);

			if (protoDef.PrototypeDefinitions.Count > 0)
			{
				List<Compiled.Statement>? nestedStatements = DefineNestedPrototypes(protoDef, compiler);
				if (nestedStatements != null)
					lstStatements.AddRange(nestedStatements);
			}

			//N20220726-01 - Annotate the prototype before any methods within it
			List<Compiled.Statement>? prototypeAnnotations = AnnotatePrototype(protoDef, compiler);
			if (prototypeAnnotations != null)
				lstStatements.AddRange(prototypeAnnotations);
			List<Compiled.Statement>? internalAnnotations = DefinePrototypeInternalAnnotations(protoDef, info, compiler);
			if (internalAnnotations != null)
				lstStatements.AddRange(internalAnnotations);

			compiler.Symbols.EnterScope(info.Scope);

			try
			{
				List<Compiled.Statement> lstInitializers = new List<Compiled.Statement>();
				foreach (PrototypeInitializer initializer in protoDef.Initializers)
				{
					List<Compiled.Statement>? compiledInitializer = PrototypeInitializerCompiler.Compile(initializer, info, compiler);
					if (compiledInitializer != null)
						lstInitializers.AddRange(compiledInitializer);
				}

				//Initializers need to run before any annotations
				lstStatements.InsertRange(0, lstInitializers);
			}
			finally
			{
				compiler.Symbols.LeaveScope();
			}

			return lstStatements;
		}

		static public List<Compiled.Statement> DefinePrototypeInternalAnnotations(PrototypeDefinition protoDef, PrototypeTypeInfo infoThis, Compiler compiler)
		{
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			compiler.Symbols.EnterScope(infoThis.Scope);

			try
			{
				foreach (FunctionDefinition functionDefinition in protoDef.Methods)
				{
					if (functionDefinition.Annotations.Count == 0)
						continue;

					List<Compiled.Statement>? methodAnnotations = CompileMethodAnnotations(functionDefinition, infoThis, compiler);
					if (methodAnnotations != null)
						lstStatements.AddRange(methodAnnotations);
				}

				foreach (FieldDefinition fieldDefinition in protoDef.Fields)
				{
					if (fieldDefinition.Annotations.Count == 0)
						continue;

					List<Compiled.Statement> annotations = CompileFieldAnnotations(fieldDefinition, infoThis, compiler);
					if (null != annotations)
						lstStatements.AddRange(annotations);
				}
			}
			finally
			{
				compiler.Symbols.LeaveScope();
			}

			return lstStatements;
		}

		static public List<Compiled.Statement> AnnotatePrototype(PrototypeDefinition protoDef, Compiler compiler)
		{
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			foreach (AnnotationExpression annotation in protoDef.Annotations)
			{
				MethodEvaluation? method = annotation.GetAnnotationMethodEvaluation();
				if (method == null)
				{
					compiler.AddDiagnostic(
						new Diagnostic("Annotation must be a method call, for example [MyAnnotation(...)]"),
						protoDef,
						annotation);
					continue;
				}
				if (!annotation.IsExpanded)
				{
					method.Parameters.Insert(0, new Identifier(protoDef.PrototypeName.TypeName));
					annotation.IsExpanded = true;
				}
			
				if (!TryCompileAnnotationFunction(annotation, compiler, out Compiled.FunctionEvaluation? functionEvaluation))
					continue;

				lstStatements.Add(new Compiled.PrototypeAnnotation { AnnotationFunction = functionEvaluation, Info = annotation.Info });
			}

			return lstStatements;
		}
		static public List<Compiled.Statement> CompileMethodAnnotations(FunctionDefinition funcDef, PrototypeTypeInfo infoThis, Compiler compiler)
		{
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			FunctionRuntimeInfo? funcInfo = infoThis.Scope.GetSymbol(funcDef.FunctionName) as FunctionRuntimeInfo;

			if (null == funcInfo)
			{
				compiler.AddDiagnostic(new Diagnostic("Could not find method: " + funcDef.FunctionName), funcDef, null);
				return null;
			}


			foreach (AnnotationExpression annotation in funcDef.Annotations)
			{
				MethodEvaluation? method = compiler.GetAnnotationMethodEvaluation(annotation);
				if (method == null)
				{
					compiler.AddDiagnostic(
						new Diagnostic("Annotation must be a method call, for example [MyAnnotation(...)]"),
						funcDef,
						annotation);
					continue;
				}
				method.Parameters.Insert(0, new Identifier(infoThis.Prototype.PrototypeName + "." + funcDef.FunctionName));

				if (!TryCompileAnnotationFunction(annotation, compiler, out Compiled.FunctionEvaluation? functionEvaluation))
					continue;

				lstStatements.Add(new Compiled.PrototypeAnnotation { AnnotationFunction = functionEvaluation, Info = annotation.Info });
			}


			return lstStatements;
		}



		static public List<Compiled.Statement> CompileFieldAnnotations(FieldDefinition fieldDef, PrototypeTypeInfo infoThis, Compiler compiler)
		{
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			FieldTypeInfo ? info = infoThis.Scope.GetSymbol(fieldDef.FieldName) as FieldTypeInfo;

			if (null == info)
			{
				compiler.AddDiagnostic(new Diagnostic("Could not find method: " + fieldDef.FieldName), fieldDef, null);
				return null;
			}


			foreach (AnnotationExpression annotation in fieldDef.Annotations)
			{
				string strFieldType;
				if (info.FieldInfo is PrototypeTypeInfo prototypeTypeInfo)
					strFieldType = prototypeTypeInfo.Prototype.PrototypeName;
				else
					strFieldType = info.FieldInfo.Type.Name;
					

				MethodEvaluation? method = annotation.GetAnnotationMethodEvaluation();
				if (method == null)
				{
					compiler.AddDiagnostic(
						new Diagnostic("Annotation must be a method call, for example [MyAnnotation(...)]"),
						fieldDef,
						annotation);
					continue;
				}
				if (!annotation.IsExpanded)
				{
					method.Parameters.Insert(0, new Identifier(infoThis.Prototype.PrototypeName));
					method.Parameters.Insert(1, new Identifier(info.Prototype.PrototypeName));
					method.Parameters.Insert(2, new Identifier(strFieldType));

					annotation.IsExpanded = true;
				}

				if (!TryCompileAnnotationFunction(annotation, compiler, out Compiled.FunctionEvaluation? functionEvaluation))
					continue;

				//If the type and the field name are the same it will use the field name for both values. Manually 
				//get the field's type. 
				GetGlobalStack ? op = functionEvaluation.Parameters[2] as GetGlobalStack;
				if (null == op)
				{
					compiler.AddDiagnostic(new Diagnostic("Annotation field type is not a GetGlobalStack"), fieldDef, null);
					return null;
				}

				TypeInfo infoFieldType = compiler.Symbols.GetGlobalScope().GetSymbol(strFieldType) as TypeInfo;
				if (infoFieldType == null)
				{
					compiler.AddDiagnostic(
						new Diagnostic("Could not resolve annotation field type symbol: " + strFieldType),
						fieldDef,
						null);
					continue;
				}
				op.Index = infoFieldType.Index;
				op.InferredType = infoFieldType;

				lstStatements.Add(new Compiled.PrototypeAnnotation { AnnotationFunction = functionEvaluation, Info = annotation.Info });
			}


			return lstStatements;
		}

		private static bool TryCompileAnnotationFunction(AnnotationExpression annotation, Compiler compiler, out Compiled.FunctionEvaluation? functionEvaluation)
		{
			functionEvaluation = null;

			Compiled.Expression? expression = compiler.Compile(annotation);
			if (expression is Compiled.FunctionEvaluation evaluation)
			{
				functionEvaluation = evaluation;
				return true;
			}

			compiler.AddDiagnostic(
				new Diagnostic("Annotation must be a method call, for example [MyAnnotation(...)]"),
				null,
				annotation);
			return false;
		}

		static public List<Compiled.Statement> DefinePrototypeFunctions(PrototypeDefinition protoDef, PrototypeTypeInfo infoThis, Compiler compiler)
		{
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			compiler.Symbols.EnterScope(infoThis.Scope);

			try
			{
				foreach (FunctionDefinition functionDefinition in protoDef.Methods)
				{
					compiler.Compile(functionDefinition);
				}
			}
			finally
			{
				compiler.Symbols.LeaveScope();
			}

			return lstStatements;
		}


		static public List<Compiled.Statement> DefinePrototypeFields(PrototypeDefinition protoDef, Compiler compiler)
		{
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			Prototype prototype = GetResolvedPrototypeOrThrow(protoDef);
			PrototypeTypeInfo info = GetPrototypeTypeInfoOrThrow(prototype, protoDef, compiler);

			compiler.Symbols.EnterScope(info.Scope);
			Scope scopeGlobal = compiler.Symbols.GetGlobalScope();

			try
			{
				if (protoDef.IsExternal)
				{
					foreach (var pair in prototype.NormalProperties)
					{
						Prototype protoFieldName = Prototypes.GetPrototype(pair.Key);
						TypeInfo infoType = new TypeInfo(typeof(Prototype));

						//For interpreter 
						FieldTypeInfo infoField = new FieldTypeInfo();
						infoField.Prototype = protoFieldName;
						infoField.Index = compiler.Symbols.GlobalStack.Add(infoField);
						infoField.Initializer = null;
						infoField.FieldInfo = infoType;

						scopeGlobal.InsertSymbol(infoField.Prototype.PrototypeName, infoField);

						info.Scope.InsertSymbol(StringUtil.RightOfLast(protoFieldName.PrototypeName, "."), infoField);
					}
				}

				foreach (FieldDefinition fieldDefinition in protoDef.Fields)
				{
					TypeInfo infoType = compiler.Symbols.GetTypeInfo(fieldDefinition.Type);

					if (infoType == null)
					{
						compiler.AddDiagnostic(new UnknownPrototype(fieldDefinition.Type.TypeName), fieldDefinition, fieldDefinition.Type);
						continue;
					}

					if (protoDef.IsExternal)
					{
						Prototype rowField = Prototypes.GetPrototypeByPrototypeName(prototype.PrototypeName + ".Field." + fieldDefinition.FieldName);
						if (null != rowField)
						{
							//For interpreter 
							FieldTypeInfo infoField = new FieldTypeInfo();
							infoField.Prototype = rowField;
							infoField.Index = compiler.Symbols.GlobalStack.Add(infoField);
							infoField.Initializer = null;
							infoField.FieldInfo = infoType;

							scopeGlobal.InsertSymbol(infoField.Prototype.PrototypeName, infoField);
							infoField.Prototype.Data["TypeInfo"] = infoField;

							info.Scope.InsertSymbol(fieldDefinition.FieldName, infoField);
							continue;
						}
					}

					//Standard Prototype field
					if (infoType is PrototypeTypeInfo)
					{
						PrototypeTypeInfo prototypeTypeInfo = infoType as PrototypeTypeInfo;

						//For interpreter 
						FieldTypeInfo infoField = new FieldTypeInfo();
						infoField.Prototype = SimpleInterpretter.NewPrototypeField(info.Prototype, fieldDefinition.FieldName, prototypeTypeInfo.Prototype);
						infoField.Index = compiler.Symbols.GlobalStack.Add(infoField);
						if (RequireFieldInitializers && null == fieldDefinition.Initializer)
						{
							compiler.AddDiagnostic(new Diagnostic("Prototype fields should have an initializer"), fieldDefinition, null);
							return null;
						}
						
						if (null != fieldDefinition.Initializer)
							infoField.Initializer = compiler.Compile(fieldDefinition.Initializer);

						infoField.FieldInfo = prototypeTypeInfo;

						scopeGlobal.InsertSymbol(infoField.Prototype.PrototypeName, infoField);
						infoField.Prototype.Data["TypeInfo"] = infoField;


						info.Scope.InsertSymbol(fieldDefinition.FieldName, infoField);
					}

					//C# property 
					else if (infoType is DotNetTypeInfo)
					{

						if (!ReflectionUtil.HasBaseType(infoType.Type, typeof(Prototype)))
						{
							compiler.AddDiagnostic(new Diagnostic("Prototype property types must inherit from Prototype"), fieldDefinition, fieldDefinition.Type);
							return null;
						}

						//For interpreter 
						FieldTypeInfo infoField = new FieldTypeInfo();
						Prototype protoField = TemporaryPrototypes.GetOrCreateTemporaryPrototype(infoType.Type.FullName);
						
						//We use this for resolution of the property. We could use scope instead but it's
						//more complex with the multiple inheritence
						infoField.Prototype = SimpleInterpretter.NewPrototypeField(info.Prototype, fieldDefinition.FieldName, protoField);
						infoField.Index = compiler.Symbols.GlobalStack.Add(infoField);
						infoField.Initializer = fieldDefinition.Initializer == null ? null : compiler.Compile(fieldDefinition.Initializer);
						infoField.FieldInfo = infoType;

						scopeGlobal.InsertSymbol(infoField.Prototype.PrototypeName, infoField);
						infoField.Prototype.Data["TypeInfo"] = infoField;

						info.Scope.InsertSymbol(fieldDefinition.FieldName, infoField);
					}
					else
					{
						compiler.AddDiagnostic(
							new Diagnostic(BuildUnsupportedFieldTypeDiagnostic(infoType, fieldDefinition)),
							fieldDefinition,
							fieldDefinition.Type);
						continue;
					}


				}

			}
			finally
			{
				compiler.Symbols.LeaveScope();
			}

			return lstStatements;
		}

		private static string BuildUnsupportedFieldTypeDiagnostic(TypeInfo infoType, FieldDefinition fieldDefinition)
		{
			string declaredTypeName = fieldDefinition.Type?.TypeName ?? infoType.Type?.Name ?? "unknown";
			string message = "Fields of this type are not yet supported: " + declaredTypeName;

			string normalized = declaredTypeName.Trim().ToLowerInvariant();
			if (normalized == "string")
			{
				return message + ". For prototype fields, use 'String' instead of 'string'.";
			}

			if (normalized == "int")
			{
				return message + ". For prototype fields, use 'Int' or 'Integer' instead of 'int'.";
			}

			if (normalized == "bool" || normalized == "boolean")
			{
				return message + ". For prototype fields, use 'Bool' or 'Boolean' instead of '" + declaredTypeName + "'.";
			}

			if (normalized == "double")
			{
				return message + ". For prototype fields, use 'Double' instead of 'double'.";
			}

			return message;
		}


		//Nested Prototypes
		public static void DeclareNestedPrototypes(PrototypeDefinition protoDef, Compiler compiler)
		{
			Prototype prototype = GetResolvedPrototypeOrThrow(protoDef);
			PrototypeTypeInfo info = GetPrototypeTypeInfoOrThrow(prototype, protoDef, compiler);

			try
			{
				compiler.Symbols.EnterScope(info.Scope);

				foreach (PrototypeDefinition prototypeDefinition in protoDef.PrototypeDefinitions)
				{
					string strOriginalName = prototypeDefinition.PrototypeName.TypeName;
					prototypeDefinition.PrototypeName.TypeName = protoDef.PrototypeName.TypeName + "." + strOriginalName;
					PrototypeDeclaration? declaration = PrototypeCompiler.DeclarePrototype(prototypeDefinition, compiler);
					if (declaration?.TypeInfo == null)
					{
						compiler.AddDiagnostic(
							new Diagnostic("Could not declare nested prototype: " + prototypeDefinition.PrototypeName.TypeName),
							prototypeDefinition,
							null);
						continue;
					}
					info.Scope.InsertSymbol(strOriginalName, declaration.TypeInfo);
				}

				foreach (PrototypeDefinition prototypeDefinition in protoDef.PrototypeDefinitions)
				{
					PrototypeCompiler.DeclarePrototypeFunctions(prototypeDefinition, compiler);
				}

			}
			finally
			{
				compiler.Symbols.LeaveScope();
			}
		}

		public static List<Compiled.Statement> DefineNestedPrototypes(PrototypeDefinition protoDef, Compiler compiler)
		{
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			Prototype prototype = GetResolvedPrototypeOrThrow(protoDef);
			PrototypeTypeInfo info = GetPrototypeTypeInfoOrThrow(prototype, protoDef, compiler);

			try
			{
				compiler.Symbols.EnterScope(info.Scope);

				foreach (PrototypeDefinition prototypeDefinition in protoDef.PrototypeDefinitions)
				{
					PrototypeCompiler.DefinePrototype(prototypeDefinition, compiler);
				}

				//N20240530-02 - Moved this here from DeclareNestedPrototypes because the types aren't always known 
				//when the prototypes are declared. May need to separate this into a two stage process
				foreach (PrototypeDefinition prototypeDefinition in protoDef.PrototypeDefinitions)
				{
					PrototypeCompiler.DefinePrototypeFields(prototypeDefinition, compiler);
				}

				foreach (PrototypeDefinition prototypeDefinition in protoDef.PrototypeDefinitions)
				{
					List<Compiled.Statement>? annotations = PrototypeCompiler.AnnotatePrototype(prototypeDefinition, compiler);
					if (annotations != null)
						lstStatements.AddRange(annotations);
				}
			}
			finally
			{
				compiler.Symbols.LeaveScope();
			}

			return lstStatements;
		}

		private static Prototype GetResolvedPrototypeOrThrow(PrototypeDefinition protoDef)
		{
			return protoDef.ResolvedPrototype
				?? throw new InvalidOperationException($"Prototype '{protoDef.PrototypeName?.TypeName ?? "(unknown)"}' was not resolved during declaration.");
		}

		private static PrototypeTypeInfo GetPrototypeTypeInfoOrThrow(Prototype prototype, PrototypeDefinition protoDef, Compiler compiler)
		{
			return compiler.Symbols.GetTypeInfo(prototype.PrototypeName) as PrototypeTypeInfo
				?? throw new InvalidOperationException($"Prototype '{protoDef.PrototypeName?.TypeName ?? prototype.PrototypeName}' is missing TypeInfo metadata in compiler symbols.");
		}
	}
}
