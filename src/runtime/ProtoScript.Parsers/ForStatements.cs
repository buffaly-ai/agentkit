namespace ProtoScript.Parsers
{
	public class ForStatements
	{
		static public ProtoScript.ForStatement Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		static public ProtoScript.ForStatement Parse(Tokenizer tok)
		{
			ProtoScript.ForStatement result = new ProtoScript.ForStatement();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("for");

			try
			{
				tok.MustBeNext("(");

				if (tok.peekNextToken() != ";")
				{
					//Commas are treated as boolean operators so it should be able to parse something like "int i = 0, int j = 0" (TODO)
					result.Start = ProtoScript.Parsers.Statements.Parse(tok);
				}

				//The Statements.Parse consumes the ; if it isn't empty
				else
					tok.MustBeNext(";");

				if (tok.peekNextToken() != ";")
				{
					result.Condition = ProtoScript.Parsers.Expressions.Parse(tok);
				}

				tok.MustBeNext(";");

				if (tok.peekNextToken() != ")")
				{
					result.Iteration = new ExpressionList();
					result.Iteration.Expressions.Add(ProtoScript.Parsers.Expressions.Parse(tok));
					while (tok.CouldBeNext(","))
					{
						result.Iteration.Expressions.Add(ProtoScript.Parsers.Expressions.Parse(tok));
					}
				}

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
