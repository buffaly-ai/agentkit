//added
using ProtoScript.Interpretter.Symbols;

namespace ProtoScript.Interpretter.Compiled
{
	public class WhileStatement : Statement
	{
		public Expression Expression;
		public CodeBlock Statements;
		public Scope Scope;

	}
}
