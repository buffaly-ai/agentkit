namespace ProtoScript
{
	public class Operator : Expression
	{
		public string Value;
		public Operator(string strValue)
		{
			Value = strValue;
			this.Terms = null;
		}

		public Operator()
		{
			this.Terms = null;
		}

		public override string ToString()
		{
			return Value;
		}

		public override Expression Clone()
		{
			Operator op = new Operator();
			op.Value = this.Value;
			op.IsParenthesized = this.IsParenthesized;
			op.Info = this.Info;
			return op;
		}
	}

	public class IndexOperator : BinaryOperator
	{
		public IndexOperator()
		{
			this.Terms = null;
			this.Value = "[]";
		}

		public override string ToString()
		{
			return "[" + Right?.ToString() + "]";

		}
	}

	public class CastingOperator : Operator
	{
		public ProtoScript.Type Type;
		public CastingOperator() : base(null)
		{

		}
	}

	public class UnaryOperator : Operator
	{
		public UnaryOperator(string strValue) : base(strValue)
		{

		}

		public UnaryOperator()
		{
			this.Terms = null;
		}

		public Expression Right = null;
	}

	public class BinaryOperator : Operator
	{
		public BinaryOperator(string strValue) : base(strValue)
		{

		}

		public BinaryOperator()
		{
			this.Terms = null;
		}

		public Expression Left = null;
		public Expression Right = null;

		public override IEnumerable<Expression> GetChildrenExpressions()
		{
			if (null != Left)
			{
				yield return Left;

				foreach (Expression term in Left.GetChildrenExpressions())
				{
					yield return term;
				}
			}

			if (null != Right)
			{
				yield return Right;

				foreach (Expression term in Right.GetChildrenExpressions())
				{
					yield return term;
				}
			}

			yield break;
		}

		public override Expression Clone()
		{
			BinaryOperator op = new BinaryOperator();
			op.Value = this.Value;
			op.IsParenthesized = this.IsParenthesized;
			op.Info = this.Info;
			op.Left = this.Left?.Clone();
			op.Right = this.Right?.Clone();
			return op;
		}

		public override string ToString()
		{
			return Left?.ToString() + " " + Value + " " + Right?.ToString();
		}
	}

	public class IsInitializedOperator : UnaryOperator
	{
	}
}
