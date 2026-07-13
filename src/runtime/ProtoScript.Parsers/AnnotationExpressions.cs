namespace ProtoScript.Parsers
{
	public class AnnotationExpressions
	{
		static public ProtoScript.AnnotationExpression Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		static public ProtoScript.AnnotationExpression Parse(Tokenizer tok)
		{
			ProtoScript.AnnotationExpression result = new ProtoScript.AnnotationExpression();

			tok.movePastWhitespace();
			result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			try
			{
				tok.MustBeNext("[");

				
				result.Terms = ProtoScript.Parsers.Expressions.Parse(tok).Terms;

				tok.MustBeNext("]");

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
