namespace ProtoScript.Interpretter.Interpretting
{
	public class DoInterpretter
	{
		static public bool Evaluate(Compiled.DoStatement statement, NativeInterpretter interpretter)
		{
			interpretter.Symbols.EnterScope(statement.Scope.Clone());

			try
			{
				for (int i = 0; i < 100; i++)
				{
					if (interpretter.Evaluate(statement.Statements))
						return true;

					if (!interpretter.EvaluateAsBool(statement.Expression))
						break;

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
