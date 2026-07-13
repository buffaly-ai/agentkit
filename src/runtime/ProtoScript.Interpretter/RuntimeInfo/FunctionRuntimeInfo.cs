//added
using Ontology;
using ProtoScript.Interpretter.Symbols;

namespace ProtoScript.Interpretter.RuntimeInfo
{
	public class FunctionRuntimeInfo : Compiled.Statement //added so I can return these from the compiler
	{
		public string FunctionName;
		public Prototype ParentPrototype;

		public List<ParameterRuntimeInfo> Parameters = new List<ParameterRuntimeInfo>();
		public List<Compiled.Statement> Statements = new List<Compiled.Statement>();
		public int Index;
		public TypeInfo ReturnType;
		public Scope Scope;
		public bool IsConstructor = false;

		public override string ToString()
		{
			return $"FunctionRuntimeInfo: {ParentPrototype?.ToString() ?? "No Parent"}.{FunctionName}({Parameters.Count})";
		}
	}
}
