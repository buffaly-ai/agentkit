namespace ProtoScript.Parsers
{
	public class MethodEvaluations
	{
		static public ProtoScript.MethodEvaluation Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}
		static public ProtoScript.MethodEvaluation Parse(Tokenizer tok)
		{
			ProtoScript.MethodEvaluation result = new MethodEvaluation();
			result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			//N20220428-01
			result.MethodName = Identifiers.ParseWithType(tok).ToString();

			//if (tok.CouldBeNext("<"))
			//{
			//	result.MethodName += "<";
			//	result.MethodName += Identifiers.Parse(tok);
			//	result.MethodName += ">";

			//	tok.MustBeNext(">");
			//}

			if (tok.peekNextToken() != "(")
				return null;

			if (result.MethodName == "typeof" || result.MethodName == "nameof")
			{
				tok.MustBeNext("(");
				Expression expression = new Expression();
				expression.Terms.Add(ProtoScript.Parsers.Types.Parse(tok));
				result.Parameters.Add(expression);
				tok.MustBeNext(")");
			}
			else
			{
				ExpressionList parameters = Expressions.ParseExpressionList(tok);
				result.Parameters.AddRange(parameters.Expressions);
			}

			result.Info.StopStatement(tok.getCursor());

			return result;
		}
	}
}
