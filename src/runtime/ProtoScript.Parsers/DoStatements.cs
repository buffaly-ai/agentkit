namespace ProtoScript.Parsers
{
	public class DoStatements
	{
		static public ProtoScript.DoStatement Parse(Tokenizer tok)
		{
			ProtoScript.DoStatement result = new ProtoScript.DoStatement();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("do");

			if (tok.peekNextToken() != "{")
				throw new ProtoScriptParsingException(tok.getString(), tok.getCursor(), "{");

			result.Statements = ProtoScript.Parsers.CodeBlocks.Parse(tok);

			tok.MustBeNext("while");
			tok.MustBeNext("(");

			result.Expression = ProtoScript.Parsers.Expressions.Parse(tok);

			tok.MustBeNext(")");
			tok.MustBeNext(";");

			result.Info.StopStatement(tok.getCursor());
			
			return result;
		}


	}
}
