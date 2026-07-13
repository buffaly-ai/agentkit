using ProtoScript.Interpretter.RuntimeInfo;
using System.Text;

namespace ProtoScript.Interpretter.Compiled
{
	public class FunctionEvaluation : Expression
	{
		public List<Expression> Parameters = new List<Expression>();
		public FunctionRuntimeInfo Function = null;
		public Expression Object;

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			if (Object != null)
			{
				sb.Append(Object.ToString()).Append(".");
			}
			sb.Append(Function.FunctionName).Append("(");
			for (var i = 0; i < this.Parameters.Count; i++)
			{
				if (i != 0)
					sb.Append(", ");
				sb.Append(this.Parameters[i].ToString());
			}
			sb.Append(")");
			return sb.ToString();
		}
	}
}
