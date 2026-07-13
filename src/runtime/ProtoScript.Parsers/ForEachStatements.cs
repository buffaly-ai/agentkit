namespace ProtoScript.Parsers
{
	public class ForEachStatements
	{
		static public ProtoScript.ForEachStatement Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		static public ProtoScript.ForEachStatement Parse(Tokenizer tok)
		{
			ProtoScript.ForEachStatement result = new ProtoScript.ForEachStatement();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("foreach");

			try
			{
				tok.MustBeNext("(");

				result.Type = ProtoScript.Parsers.Types.Parse(tok);
				result.IteratorName = ProtoScript.Parsers.Identifiers.Parse(tok);

				tok.MustBeNext("in");

				result.Expression = ProtoScript.Parsers.Expressions.Parse(tok);

				tok.MustBeNext(")");

				result.Statements = ProtoScript.Parsers.CodeBlocks.Parse(tok);
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
