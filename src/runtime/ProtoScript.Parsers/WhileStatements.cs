namespace ProtoScript.Parsers
{
	public class WhileStatements
	{
		static public ProtoScript.WhileStatement Parse(Tokenizer tok)
		{
			ProtoScript.WhileStatement result = new ProtoScript.WhileStatement();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("while");
			tok.MustBeNext("(");

			result.Expression = ProtoScript.Parsers.Expressions.Parse(tok);

			tok.MustBeNext(")");

			//SImple implementation without any dedicated elements for cases
			result.Statements = ProtoScript.Parsers.CodeBlocks.Parse(tok);

			result.Info.StopStatement(tok.getCursor());

			return result;
		}

	}
}
