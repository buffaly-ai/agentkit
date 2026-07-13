namespace ProtoScript.Parsers
{
	public class ScopedExpressionLists
	{
		static public ProtoScript.ScopedExpressionList Parse(string strExpression)
		{
			Tokenizer tok = new Tokenizer(strExpression);
			return Parse(tok);
		}

		static public ProtoScript.ScopedExpressionList Parse(Tokenizer tok)
		{
			ProtoScript.ScopedExpressionList result = new ProtoScript.ScopedExpressionList();

			tok.MustBeNext("{");

			try
			{
				if ("}" != tok.peekNextToken())
					result.Expressions.Add(ProtoScript.Parsers.Expressions.Parse(tok));

				while (tok.hasMoreTokens() && tok.peekNextToken() != "}")
				{
					tok.MustBeNext(",");

					result.Expressions.Add(ProtoScript.Parsers.Expressions.Parse(tok));
				}

				tok.MustBeNext("}");
			}
			catch (Exception err)
			{
				if (!Settings.BestCaseExpressions)
throw;

				result.Info.IsIncomplete = true;
			}

			return result;
		}
	}
}
