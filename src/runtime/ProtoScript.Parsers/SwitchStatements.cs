namespace ProtoScript.Parsers
{
	public class SwitchStatements
	{
		static public ProtoScript.CaseStatement ParseCaseStatement(Tokenizer tok)
		{
			ProtoScript.CaseStatement result = new ProtoScript.CaseStatement();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("case");

			result.Value = Parsers.Expressions.Parse(tok);

			tok.MustBeNext(":");

			result.Info.StopStatement(tok.getCursor());

			return result;
		}

		static public ProtoScript.DefaultStatement ParseDefaultStatement(Tokenizer tok)
		{
			ProtoScript.DefaultStatement result = new ProtoScript.DefaultStatement();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("default");
			tok.MustBeNext(":");

			result.Info.StopStatement(tok.getCursor());

			return result;
		}

		static public ProtoScript.SwitchStatement Parse(Tokenizer tok)
		{
			ProtoScript.SwitchStatement result = new ProtoScript.SwitchStatement();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("switch");
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
