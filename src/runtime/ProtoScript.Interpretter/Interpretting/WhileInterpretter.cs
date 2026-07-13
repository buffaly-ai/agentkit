namespace ProtoScript.Interpretter.Interpretting
{
	public class WhileInterpretter
	{
		static public bool Evaluate(Compiled.WhileStatement statement, NativeInterpretter interpretter)
		{
			interpretter.Symbols.EnterScope(statement.Scope.Clone());

			try
			{
				for (int i = 0; i < 100; i++)
				{
					if (!interpretter.EvaluateAsBool(statement.Expression))
						break;					
					
					if (interpretter.Evaluate(statement.Statements))
						return true;
				}
			}
			finally
			{
				interpretter.Symbols.LeaveScope();
			}

			return false;
		}
	}
}
