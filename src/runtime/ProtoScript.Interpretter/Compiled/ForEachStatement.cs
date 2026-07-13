//added
using ProtoScript.Interpretter.RuntimeInfo;
using ProtoScript.Interpretter.Symbols;

namespace ProtoScript.Interpretter.Compiled
{
	public class ForEachStatement : Statement
	{
		public VariableRuntimeInfo Iterator;
		public Expression Expression;
		public CodeBlock Statements;
		public Scope Scope;

	}
}
