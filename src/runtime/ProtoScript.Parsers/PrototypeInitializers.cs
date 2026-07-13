namespace ProtoScript.Parsers
{
	public class PrototypeInitializers
	{
		static public ProtoScript.PrototypeInitializer Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		static public ProtoScript.PrototypeInitializer Parse(Tokenizer tok)
		{
			ProtoScript.PrototypeInitializer result = new ProtoScript.PrototypeInitializer();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("init");

			try
			{
				//Note: This allows init Statement as well
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
