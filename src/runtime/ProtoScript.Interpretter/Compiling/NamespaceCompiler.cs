using ProtoScript.Interpretter.Compiled;
using ProtoScript.Interpretter.RuntimeInfo;
using ProtoScript.Interpretter.Symbols;

namespace ProtoScript.Interpretter.Compiling
{
	public class NamespaceCompiler
	{
		public static List<Compiled.Statement> DeclarePrototypes(NamespaceDefinition statement, Compiler compiler)
		{
			Scope ns = GetOrInsertNamespaceChain(statement.Namespaces, compiler.Symbols);
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();
			try
			{
				compiler.Symbols.EnterScope(ns);

				foreach (PrototypeDefinition prototypeDefinition in statement.PrototypeDefinitions)
				{
					string strOriginalName = prototypeDefinition.PrototypeName.TypeName;
					prototypeDefinition.PrototypeName.TypeName = string.Join(".", statement.Namespaces) + "." + strOriginalName;
					PrototypeDeclaration declaration = PrototypeCompiler.DeclarePrototype(prototypeDefinition, compiler);
					lstStatements.Add(declaration);

					ns.InsertSymbol(strOriginalName, declaration.TypeInfo);
				}

			}
			finally
			{
				compiler.Symbols.LeaveScope();
			}

			return lstStatements;
		}

		public static void Declare(NamespaceDefinition statement, Compiler compiler)
		{
			Scope ns = GetOrInsertNamespaceChain(statement.Namespaces, compiler.Symbols);

			try
			{
				compiler.Symbols.EnterScope(ns);

				foreach (PrototypeDefinition prototypeDefinition in statement.PrototypeDefinitions)
				{
					PrototypeCompiler.DeclarePrototypeTypeOfs(prototypeDefinition, compiler);
				}

				foreach (PrototypeDefinition prototypeDefinition in statement.PrototypeDefinitions)
				{
					PrototypeCompiler.DefinePrototypeFields(prototypeDefinition, compiler);
				}

				foreach (PrototypeDefinition prototypeDefinition in statement.PrototypeDefinitions)
				{
					PrototypeCompiler.DeclarePrototypeFunctions(prototypeDefinition, compiler);
				}

			}
			finally
			{
				compiler.Symbols.LeaveScope();
			}
		}

		public static List<Compiled.Statement> Define(NamespaceDefinition statement, Compiler compiler)
		{
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			Scope ns = GetOrInsertNamespaceChain(statement.Namespaces, compiler.Symbols);

			try
			{
				compiler.Symbols.EnterScope(ns);

				foreach (PrototypeDefinition prototypeDefinition in statement.PrototypeDefinitions)
				{
					lstStatements.AddRange(PrototypeCompiler.DefinePrototype(prototypeDefinition, compiler));
				}
			}
			finally
			{
				compiler.Symbols.LeaveScope();
			}

			return lstStatements;
		}

		public static Scope GetOrInsertNamespaceChain(List<string> lstNamespaces, SymbolTable symbols)
		{
			Scope scopeCurrent = symbols.GetGlobalScope();
			Namespace nsCurrent = null;
			for (int i = 0; i < lstNamespaces.Count; i++)
			{
				string strNamespace = lstNamespaces[i];

				object obj = scopeCurrent.GetSymbol(strNamespace);

				if (null == obj)
				{
					Namespace ns = new Namespace();
					ns.NamespaceName = strNamespace;

					//N20231010-01 - Adding a namespace short form format
					if (nsCurrent == null && i > 0)
						throw new InvalidOperationException($"Cannot create namespace segment '{strNamespace}' after entering a prototype scope.");

					ns.ParentNamespace = nsCurrent;
					ns.Scope.Name = ns.NamespaceName;
					ns.Index = symbols.GlobalStack.Add(ns);

					scopeCurrent.InsertSymbol(strNamespace, ns);
					scopeCurrent = ns.Scope;

					nsCurrent = ns;
				}

				else if (obj is PrototypeTypeInfo)
				{
					PrototypeTypeInfo prototypeTypeInfo = obj as PrototypeTypeInfo;
					scopeCurrent = prototypeTypeInfo.Scope;
					nsCurrent = null;
				}

				else if (!(obj is Namespace))
					throw new InvalidOperationException($"Expected namespace '{strNamespace}', but found symbol type '{obj.GetType().Name}'.");

				else
				{
					Namespace ns = obj as Namespace;
					scopeCurrent = ns.Scope;

					nsCurrent = ns;
				}
			}

			return scopeCurrent;
		}
	}
}
