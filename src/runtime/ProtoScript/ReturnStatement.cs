using System.Text;

namespace ProtoScript
{
	public class ReturnStatement : Statement
	{
		public Expression Expression;

		public ReturnStatement()
		{
			Expression = null;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder("return ");
			if (null != Expression)
				sb.Append(Expression.ToString());
			return sb.ToString();
		}
	}
}
