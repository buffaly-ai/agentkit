namespace ProtoScript.Parsers
{
	public class YieldStatements
	{
		static public ProtoScript.YieldStatement Parse(Tokenizer tok)
		{
			ProtoScript.YieldStatement result = new ProtoScript.YieldStatement();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("yield");

			if (tok.CouldBeNext("return"))
				result.Expression = ProtoScript.Parsers.Expressions.Parse(tok);

			else
				tok.MustBeNext("break"); 

			tok.MustBeNext(";");

			result.Info.StopStatement(tok.getCursor());

			return result;
		}

	}
}
