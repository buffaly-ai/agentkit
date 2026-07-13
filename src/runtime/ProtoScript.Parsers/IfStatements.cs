namespace ProtoScript.Parsers
{
	public class IfStatements
	{
		static public ProtoScript.IfStatement Parse(Tokenizer tok)
		{
			ProtoScript.IfStatement result = new ProtoScript.IfStatement();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			try
			{

				tok.MustBeNext("if");
				tok.MustBeNext("(");

				result.Condition = ProtoScript.Parsers.Expressions.Parse(tok);

				tok.MustBeNext(")");

				result.TrueBody = ProtoScript.Parsers.CodeBlocks.Parse(tok);

				while (tok.CouldBeNext("else"))
				{
					if (tok.CouldBeNext("if"))
					{
						tok.MustBeNext("(");

						result.ElseIfConditions.Add(ProtoScript.Parsers.Expressions.Parse(tok));

						tok.MustBeNext(")");

						result.ElseIfBodies.Add(ProtoScript.Parsers.CodeBlocks.Parse(tok));
					}
					else
					{
						result.ElseBody = ProtoScript.Parsers.CodeBlocks.Parse(tok);
					}
				}
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
