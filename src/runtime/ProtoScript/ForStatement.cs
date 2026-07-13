namespace ProtoScript
{
	public class ForStatement : Statement
	{
		public Statement Start;
		public Expression Condition;
		public ExpressionList Iteration;
		public CodeBlock Statements;

		public override IEnumerable<Statement> GetChildrenStatements()
		{
			yield return new CodeBlockStatement(Statements);
		}
	}
}
