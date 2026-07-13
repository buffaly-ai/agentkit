using BasicUtilities;

namespace ProtoScript.Parsers
{
	public class Types
	{
		static public ProtoScript.Type Parse(string str)
		{
			return Parse(new Tokenizer(str));
		}

		static public ProtoScript.Type Parse(Tokenizer tok)
		{
			ProtoScript.Type result = new ProtoScript.Type();
			result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			//Allow Path.To.Type
			do
			{
				if (!StringUtil.IsEmpty(result.TypeName))
				{
					tok.MustBeNext(".");
					result.TypeName += "." + ProtoScript.Parsers.Identifiers.Parse(tok);
				}
				else
				{
					result.TypeName = ProtoScript.Parsers.Identifiers.Parse(tok);
				}

				if (tok.peekNextToken() == "::")
					result.TypeName += tok.getNextToken() + ProtoScript.Parsers.Identifiers.Parse(tok);
			}
			while (tok.peekNextToken() == ".");

			NullableState(tok, result);
			GenericState(tok, result);
			ArrayLiteralState(tok, result);

			result.Info.StopStatement(tok.getCursor());

			return result;
		}

		static void NullableState(Tokenizer tok, ProtoScript.Type result)
		{
			string strTok = tok.peekNextToken();
			if (strTok == "?")
			{
				result.IsNullable = true;
				tok.getNextToken();
			}
			else
				result.IsNullable = false;
		}

		static void GenericState(Tokenizer tok, ProtoScript.Type result)
		{
			string strTok = tok.peekNextToken();

			if (strTok == "<")
			{
				tok.getNextToken();

				do
				{
					ProtoScript.Type type = Parse(tok);
					result.ElementTypes.Add(type);

					strTok = tok.peekNextToken();
					if (strTok == ",")
					{
						tok.getNextToken();
						continue;
					}

					if (strTok == ">>")
					{
						strTok = ">";
						tok.movePast(">");
					}

					else
						tok.MustBeNext(">");

					//if (strTok == ">")
					//	tok.getNextToken();


					return;
				}
				while (true);
			}
		}

		static void ArrayLiteralState(Tokenizer tok, ProtoScript.Type result)
		{
			if (tok.CouldBeNext("["))
			{
				result.IsArray = true;
				result.ArraySize = new Expression();

				do
				{
					while (tok.CouldBeNext(","))
					{
					
						result.ArraySize.Terms.Add(null);
					}

					if ("]" != tok.peekNextToken())
					{
						do
						{
							result.ArraySize.Terms.AddRange(Expressions.Parse(tok).Terms);
						}
						while (tok.CouldBeNext(","));
					}
					else
					{
						//N-20181228-02
						result.ArraySize.Terms.Add(null);
					}

					tok.MustBeNext("]");
				}
				while (tok.CouldBeNext("["));
			}
		}

	}
}
