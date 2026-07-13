namespace ProtoScript.Parsers
{
	public class ArrayLiterals
	{
		static public ProtoScript.ArrayLiteral Parse(Tokenizer tok)
		{
			ProtoScript.ArrayLiteral result = new ProtoScript.ArrayLiteral();

			tok.MustBeNext("[");

			while (tok.hasMoreTokens() && tok.peekNextToken() != "]")
			{
				result.Values.Add(ProtoScript.Parsers.Expressions.Parse(tok));

				//Note: won't be needed as Expression contains the ","
				if (tok.peekNextToken() == ",")
					tok.getNextToken();
			}

			tok.MustBeNext("]");

			return result;
		}

	}
}
