using ProtoScript.Interpretter.RuntimeInfo;
using ProtoScript.Interpretter.Symbols;

namespace ProtoScript.Interpretter.Interpretting
{
	//>+ create TryInterpretter class to interpret the TryStatement
	public class TryInterpretter
	{
		static public bool Evaluate(Compiled.TryStatement statement, NativeInterpretter interpretter)
		{
			try
			{
				if (interpretter.Evaluate(statement.TryBlock))
					return true;
			}
			catch (Exception ex)
			{
				Exception exWorking = ex.InnerException ?? ex; 

				foreach (var catchBlock in statement.CatchBlocks)
				{
					TypeInfo? typeInfo = catchBlock.Type == null ? null : interpretter.Symbols.GetTypeInfo(catchBlock.Type);
					if (catchBlock.Type == null || (typeInfo?.Type != null && typeInfo.Type.IsAssignableFrom(exWorking.GetType())))
					{
						Scope scope = catchBlock.Statements.Scope;

						try
						{
							interpretter.Symbols.EnterScope(scope.Clone());
							if (catchBlock.ExceptionValue != null)
							{
								catchBlock.ExceptionValue.Value = exWorking;
							}

							if (interpretter.Evaluate(catchBlock.Statements))
							{
								return true;
							}
						}
						finally
						{
							interpretter.Symbols.LeaveScope();
						}
					}
				}
				
			}
			finally
			{
				//> evaluate the finally block if it exists
				if (statement.FinallyBlock != null)
				{
					interpretter.Evaluate(statement.FinallyBlock);
				}
			}

			return false;
		}

		//> create a method to Evaluate the throw Statement
		public static bool Evaluate(Compiled.ThrowStatement statement, NativeInterpretter interpretter)
		{
			object ex = interpretter.Evaluate(statement.Expression);
			if (ex is Exception)
			{
				throw ex as Exception;
			}
			else
			{
				Logs.DebugLog.WriteEvent("InvalidThrow", "Throw expression did not evaluate to an exception");
				return false;
			}
		}
	}
}
