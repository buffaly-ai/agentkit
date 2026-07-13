using System.Text;

namespace ProtoScript
{
	public class ForEachStatement : Statement
	{
		public ProtoScript.Type Type;
		public string IteratorName;
		public Expression Expression;
		public CodeBlock Statements;

		public override IEnumerable<Statement> GetChildrenStatements()
		{
			yield return new CodeBlockStatement(Statements);
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder("ProtoScript.ForEachStatement[");
			sb.Append("foreach (");
			sb.Append(Type.ToString());
			sb.Append(" ");
			sb.Append(IteratorName);
			sb.Append(" in ");
			sb.Append(Expression.ToString());
			sb.Append(")");
			sb.Append("]");
			return sb.ToString();
		}
	}
}
