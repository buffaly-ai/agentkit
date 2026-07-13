//added
namespace ProtoScript.Interpretter.Compiled
{
	public class CategorizationOperator : Compiled.Expression
	{
		public Expression Left;
		public Expression Middle;
		public ScopedExpressionList Right;
	}
}
