namespace ProtoScript.Parsers
{
	public class ExpressionStatements
	{
		static public ProtoScript.ExpressionStatement Parse(Tokenizer tok, bool bNaked)
		{
			ProtoScript.ExpressionStatement result = new ExpressionStatement();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			try
			{
				result.Expression = Expressions.Parse(tok);

				if (!bNaked)
					tok.MustBeNext(";");
			}
			catch (Exception err)
			{
				if (!Settings.BestCaseExpressions)
throw;

				result.Info.IsIncomplete = true;
			}

			result.Info.StopStatement(tok.getCursor());

			return result; 
		}
	}
}
