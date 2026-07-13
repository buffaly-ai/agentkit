namespace ProtoScript.Interpretter.Compiled
{
	public class IfStatement : Statement
	{
		public Expression Condition;
		public CodeBlock TrueBody;

		public List<Expression> ElseIfConditions = new List<Expression>();
		public List<CodeBlock> ElseIfBodies = new List<CodeBlock>();

		public CodeBlock ElseBody;

	}


}
