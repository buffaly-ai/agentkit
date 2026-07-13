namespace ProtoScript
{
	public class CaseStatement : Statement
	{
		public Expression Value;
	}

	public class DefaultStatement : Statement
	{

	}

	public class SwitchStatement : Statement
	{
		public Expression Expression = new Expression();
		public CodeBlock Statements = new CodeBlock();
	}
}
