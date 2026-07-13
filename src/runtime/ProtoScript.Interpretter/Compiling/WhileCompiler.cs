//added
using ProtoScript.Interpretter.Symbols;

namespace ProtoScript.Interpretter.Compiling
{
	public class WhileCompiler
	{
		public static Compiled.Statement Compile(WhileStatement statement, Compiler compiler)
		{
			Compiled.WhileStatement compiled = new Compiled.WhileStatement();
			compiled.Info = statement.Info;
			compiled.Scope = new Scope();

			compiler.Symbols.EnterScope(compiled.Scope);

			try
			{
				compiled.Expression = compiler.Compile(statement.Expression);
				compiled.Statements = compiler.Compile(statement.Statements);
			}
			finally
			{
				compiler.Symbols.LeaveScope();
			}

			return compiled;
		}
	}
}
