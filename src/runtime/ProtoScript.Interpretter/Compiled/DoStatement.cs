//added
using ProtoScript.Interpretter.Symbols;

namespace ProtoScript.Interpretter.Compiled
{
	public class DoStatement : Statement
	{
		public Expression Expression;
		public CodeBlock Statements;
		public Scope Scope;

	}
}
