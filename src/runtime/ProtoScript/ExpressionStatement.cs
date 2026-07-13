namespace ProtoScript
{
	public class ExpressionStatement : Statement
	{
		public Expression Expression;

		public override string ToString()
		{
			return Expression.ToString() + ";";
		}
	}

}
