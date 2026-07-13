//added
using ProtoScript.Interpretter.Symbols;

namespace ProtoScript.Interpretter.Compiled
{
	public class ScopedExpressionList : Compiled.Expression
	{
		public Scope Scope;
		public List<Compiled.Expression> Expressions = new List<Expression>();
	}
}
