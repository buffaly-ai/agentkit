using System.Text;

namespace ProtoScript
{
	public class CodeBlock : List<Statement>
	{
		private StatementParsingInfo ? _info = null;
		public StatementParsingInfo Info
		{
			get
			{
				return _info ??= new StatementParsingInfo();
			}
			set
			{
				_info = value;
			}
		}

		public override string ToString()
		{
			if (this.Count == 0)
			{
				return "{}";
			}
			StringBuilder sb = new StringBuilder();
			sb.Append("{\n");
			foreach (Statement statement in this)
			{
				sb.Append(statement.ToString());
				sb.Append("\n");
			}
			sb.Append("}");
			return sb.ToString();
		}
	}

	public class CodeBlockStatement : Statement
	{
		public CodeBlock Statements;

		public override IEnumerable<Statement> GetChildrenStatements()
		{
			foreach (Statement statement in Statements)
			{
				yield return statement;
			}
		}

		public CodeBlockStatement()
		{

		}

		public CodeBlockStatement(CodeBlock codeBlock)
		{
			this.Statements = codeBlock;
			this.Info = codeBlock.Info;
		}

		public override string ToString()
		{
			return Statements?.ToString() ?? "{}";
		}
	}

	public class CodeBlockExpression : Expression
	{
		public CodeBlock Statements;


		public CodeBlockExpression()
		{

		}

		public CodeBlockExpression(CodeBlock codeBlock)
		{
			this.Statements = codeBlock;
			this.Info = codeBlock.Info;
		}
	}

}
