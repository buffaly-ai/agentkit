namespace ProtoScript.Parsers
{
	public class ReturnStatements
	{
		static public ProtoScript.ReturnStatement Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		static public ProtoScript.ReturnStatement Parse(Tokenizer tok)
		{
			ProtoScript.ReturnStatement result = new ProtoScript.ReturnStatement();

			tok.movePastWhitespace();
			result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			try
			{
				tok.MustBeNext("return");

				if (tok.peekNextToken() != ";")
					result.Expression = ProtoScript.Parsers.Expressions.Parse(tok);

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
