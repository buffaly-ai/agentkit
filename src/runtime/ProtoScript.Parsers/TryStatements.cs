namespace ProtoScript.Parsers
{
	public class TryStatements
	{
		static public ProtoScript.TryStatement Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		static public ProtoScript.TryStatement Parse(Tokenizer tok)
		{
			ProtoScript.TryStatement result = new ProtoScript.TryStatement();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("try");

			result.TryBlock = ProtoScript.Parsers.CodeBlocks.Parse(tok);

			while (tok.CouldBeNext("catch"))
			{
				if (tok.CouldBeNext("("))
				{					
					ProtoScript.TryStatement.CatchBlock block = new ProtoScript.TryStatement.CatchBlock();
					block.Type = ProtoScript.Parsers.Types.Parse(tok);

					if (tok.peekNextToken() != ")")
						block.ExceptionName = ProtoScript.Parsers.Identifiers.Parse(tok);

					tok.MustBeNext(")");

					block.Statements = ProtoScript.Parsers.CodeBlocks.Parse(tok);

					result.CatchBlocks.Add(block);
				}

				//catch with no exception 
				else
				{
					ProtoScript.TryStatement.CatchBlock block = new ProtoScript.TryStatement.CatchBlock();
					block.Statements = ProtoScript.Parsers.CodeBlocks.Parse(tok);

					result.CatchBlocks.Add(block);
				}
			}

			if (tok.CouldBeNext("finally"))
			{
				result.FinallyBlock= ProtoScript.Parsers.CodeBlocks.Parse(tok);
			}

			result.Info.StopStatement(tok.getCursor());

			return result;
		}

	}
}
