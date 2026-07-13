namespace ProtoScript
{
	public class CategorizationOperator : Operator
	{
		public CategorizationOperator()
		{
			this.Terms = null;
		}

		public Expression Left = null;
		public Expression Middle = null;
		public ScopedExpressionList Right = null;
	}
}
