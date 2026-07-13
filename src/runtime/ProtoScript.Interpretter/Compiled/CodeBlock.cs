//added
using ProtoScript.Interpretter.Symbols;

namespace ProtoScript.Interpretter.Compiled
{
	public class CodeBlock : List<Statement>
	{
		public Scope Scope = new Scope(Scope.ScopeTypes.Block);
	}

	public class CodeBlockStatement : Statement
	{
		public CodeBlock Statements;

		public CodeBlockStatement()
		{

		}

		public CodeBlockStatement(CodeBlock codeBlock)
		{
			this.Statements = codeBlock;
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
		}
	}

}
