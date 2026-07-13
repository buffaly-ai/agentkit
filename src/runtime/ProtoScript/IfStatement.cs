namespace ProtoScript
{
	public class IfStatement : Statement
	{
		public Expression Condition;
		public CodeBlock TrueBody;

		public List<Expression> ElseIfConditions = new List<Expression>();
		public List<CodeBlock> ElseIfBodies = new List<CodeBlock>();
	
		public CodeBlock ElseBody;

		public override IEnumerable<Statement> GetChildrenStatements()
		{
			yield return new CodeBlockStatement(TrueBody);

			if (null != ElseIfBodies)
			{
				foreach (CodeBlock codeBlock in ElseIfBodies)
				{
					yield return new CodeBlockStatement(codeBlock);
				}
			}

			if (null != ElseBody)
				yield return new CodeBlockStatement(ElseBody);
		}
	}
}
