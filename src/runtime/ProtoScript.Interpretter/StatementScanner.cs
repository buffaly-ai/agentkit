namespace ProtoScript.Interpretter
{
	public class StatementScanner
	{
		static public bool Any(Statement statement, Func<Statement, bool> f)
		{
			if (f(statement))
				return true;

			foreach (Statement child in statement.GetChildrenStatements())
			{
				if (Any(child, f))
					return true;
			}

			return false;
		}
	}
}

