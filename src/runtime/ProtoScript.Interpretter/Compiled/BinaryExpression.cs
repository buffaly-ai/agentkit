namespace ProtoScript.Interpretter.Compiled
{
	public class BinaryExpression : Expression
	{
		public Expression Left;
		public Expression Right;
	}

	public class AddOperator : BinaryExpression
	{
	}
}
