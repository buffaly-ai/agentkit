namespace ProtoScript.Interpretter.Compiled
{
	public class ConditionalOperator : Expression
	{
		public Expression Condition;
		public Expression TrueExpression;
		public Expression FalseExpression;
	}
}
