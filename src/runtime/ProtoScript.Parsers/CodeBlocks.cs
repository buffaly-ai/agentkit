namespace ProtoScript.Parsers
{
	public class CodeBlocks
	{
		public static ProtoScript.CodeBlock Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		public static ProtoScript.CodeBlock Parse(Tokenizer tok)
		{
			ProtoScript.CodeBlock result = new ProtoScript.CodeBlock();

			tok.movePastWhitespace();
			result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			if (tok.peekNextToken() == "{")
			{
				tok.getNextToken();

				while (tok.peekNextToken() != "}" && tok.hasMoreTokens())
				{
					Statement statement = ProtoScript.Parsers.Statements.Parse(tok);
					if (null != statement)
						result.Add(statement);
				}

				tok.MustBeNext("}");
			}
			else
			{
				Statement statement = ProtoScript.Parsers.Statements.Parse(tok);
				if (null != statement)
					result.Add(statement);
			}

			result.Info.StopStatement(tok.getCursor());

			return result;
		}
	}
}
