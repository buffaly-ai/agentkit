//added
using ProtoScript.Interpretter.Symbols;

namespace ProtoScript.Interpretter.Compiling
{
	public class DoCompiler
	{
		public static Compiled.Statement Compile(DoStatement statement, Compiler compiler)
		{
			Compiled.DoStatement compiled = new Compiled.DoStatement();
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
