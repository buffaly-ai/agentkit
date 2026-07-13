using ProtoScript.Interpretter.RuntimeInfo;
using ProtoScript.Interpretter.Symbols;
using System;

namespace ProtoScript.Interpretter.Compiling
{
	//>+ create  class TryCompiler to compiler the TryStatement
	public class TryCompiler
	{
		public static Compiled.Statement Compile(TryStatement statement, Compiler compiler)
		{
			Compiled.TryStatement compiled = new Compiled.TryStatement();
			compiled.Info = statement.Info;

			try
			{
				compiled.TryBlock = compiler.Compile(statement.TryBlock);

				foreach (var catchBlock in statement.CatchBlocks)
				{
					Compiled.TryStatement.CatchBlock compiledCatchBlock = new Compiled.TryStatement.CatchBlock();
					compiledCatchBlock.Type = catchBlock.Type;
					compiledCatchBlock.ExceptionName = catchBlock.ExceptionName;

					TypeInfo infoType;
					if (catchBlock.Type == null)
					{
						infoType = new TypeInfo(typeof(Exception));
					}
					else
					{
						infoType = compiler.Symbols.GetTypeInfo(catchBlock.Type) as TypeInfo;
					}

					if (infoType == null)
					{
						compiler.AddDiagnostic("Could not resolve catch type", statement, null);
						continue;
					}

					Scope scope = new Scope(Scope.ScopeTypes.Block);
					if (!string.IsNullOrWhiteSpace(compiledCatchBlock.ExceptionName))
					{
						VariableRuntimeInfo variableRuntimeInfo = new VariableRuntimeInfo();
						variableRuntimeInfo.Type = infoType;
						variableRuntimeInfo.Index = scope.Stack.Add(variableRuntimeInfo);
						compiledCatchBlock.ExceptionValue = variableRuntimeInfo;
						scope.InsertSymbol(compiledCatchBlock.ExceptionName, variableRuntimeInfo);
					}
					compiler.Symbols.EnterScope(scope);
					
					try
					{
						compiledCatchBlock.Statements = compiler.Compile(catchBlock.Statements);
						compiledCatchBlock.Statements.Scope = scope;
					}
					finally
					{
						compiler.Symbols.LeaveScope();
					}
		
					compiled.CatchBlocks.Add(compiledCatchBlock);
				}

				if (statement.FinallyBlock != null)
				{
					compiled.FinallyBlock = compiler.Compile(statement.FinallyBlock);
				}
			}
			finally
			{
				
			}

			return compiled;
		}

		//> create a method to compile the throw Statement
		public static Compiled.Statement Compile(ThrowStatement statement, Compiler compiler)
		{
			Compiled.ThrowStatement compiled = new Compiled.ThrowStatement();
			compiled.Info = statement.Info;
			compiled.Expression = compiler.Compile(statement.Expression);
			return compiled;
		}
	}
}
