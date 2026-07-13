using System.Text;

namespace ProtoScript
{
public class MethodEvaluation : Expression
{
public string MethodName;
public List<Expression> Parameters = new List<Expression>();
public bool IsNullConditional;

		public MethodEvaluation()
		{
			this.Terms = null;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();

			sb.Append(MethodName).Append("(");

			for (var i = 0; i < this.Parameters.Count; i++)
			{
				if (i != 0)
					sb.Append(", ");

				sb.Append(this.Parameters[i].ToString());
			}

			sb.Append(")");

			return sb.ToString();
		}

		public override Expression Clone()
		{
			MethodEvaluation methodEvaluation = new MethodEvaluation();
			methodEvaluation.MethodName = this.MethodName;
			methodEvaluation.Parameters = new List<Expression>();
			foreach (Expression parameter in Parameters)
			{
				methodEvaluation.Parameters.Add(parameter.Clone());
			}
			methodEvaluation.IsParenthesized = this.IsParenthesized;
			methodEvaluation.Info = this.Info;
			return methodEvaluation;
		}
	}
}
