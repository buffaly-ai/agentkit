namespace ProtoScript.Parsers
{
	public class ThrowStatements
	{
		static public ProtoScript.ThrowStatement Parse(Tokenizer tok)
		{
			ProtoScript.ThrowStatement result = new ProtoScript.ThrowStatement();

			tok.movePastWhitespace();
			result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			try
			{
				tok.MustBeNext("throw");

				if (tok.peekNextToken() != ";")
				{
					result.Expression = ProtoScript.Parsers.Expressions.Parse(tok);
				}

				tok.MustBeNext(";");

			}
			catch (Exception err)
			{
				if (!Settings.BestCaseExpressions)
throw;
			}


			result.Info.StopStatement(tok.getCursor());

			return result;
		}

	}
}
