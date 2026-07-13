using BasicUtilities;
using System.Text;

namespace ProtoScript.Parsers
{
	public class Identifiers
	{
		static public string Parse(Tokenizer tok)
		{
			string strTok = tok.getNextToken();

			//abstracted to allow for checking. These are just examples
			if (StringUtil.IsEmpty(strTok))
				throw new ProtoScriptParsingException(tok.getString(), tok.getCursor(), "A non-null identifier");

			if (tok.IsOperator(strTok))
				throw new ProtoScriptParsingException(tok.getString(), tok.getCursor(), "A string identifier");

			if (strTok.Length == 1 && tok.isSymbol(strTok.First()))
				throw new ProtoScriptParsingException(tok.getString(), tok.getCursor(), "A string identifier");

			return strTok;
		}

		static public Identifier ParseAsIdentifier(Tokenizer tok)
		{
			Identifier identifier = new Identifier();
			identifier.Info.File = Files.CurrentFile;
			identifier.Info.StartStatement(tok.getCursor());
			identifier.Value = ParseWithType(tok).ToString();
			identifier.Info.StopStatement(tok.getCursor());
			return identifier;
		}

		static public StringBuilder ParseWithType(Tokenizer tok)
		{
			int iCursor = tok.getCursor();
			StringBuilder strTok = new StringBuilder(Parse(tok));
			if (tok.peekNextToken() == "<")
			{
				strTok.Append(tok.getNextToken());

				try
				{
					tok.setCursor(iCursor);
					ProtoScript.Type type = ProtoScript.Parsers.Types.Parse(tok);
					strTok = new StringBuilder(SimpleGenerator.Generate(type));
				}
				catch
				{
					tok.setCursor(iCursor);
					return new StringBuilder(Parse(tok));
				}
			}

			return strTok;
			
		}

		//static public string ParseWithType(Tokenizer tok)
		//{
		//	int iCursor = tok.getCursor();
		//	string strTok = Parse(tok);
		//	if (tok.peekNextToken() == "<")
		//	{
		//		strTok += tok.getNextToken();
		//		strTok += ParseWithType(tok);
		//		while (tok.CouldBeNext(","))
		//		{
		//			strTok += ", " + ParseWithType(tok);
		//		}

		//		if (tok.peekNextToken() == ">>")
		//		{
		//			strTok += ">";
		//			tok.movePast(">");
		//		}

		//		else if (tok.peekNextToken() == ">")
		//		{
		//			strTok += tok.getNextToken();
		//		}

		//		else
		//		{
		//			tok.setCursor(iCursor);
		//			return Parse(tok);
		//		}
		//	}

		//	return strTok;

		//}

		static public string ParseMultiple(Tokenizer tok)
		{
			StringBuilder strTok = ParseWithType(tok);

			try
			{
				if (strTok.ToString() == "global")
				{
					tok.MustBeNext("::");
					strTok.Append("::");
					strTok.Append(ParseWithType(tok));
				}

				while (tok.CouldBeNext("."))
				{
					strTok.Append(".").Append(ParseWithType(tok));
				}
			}
			catch (Exception err)
			{
				if (!Settings.BestCaseExpressions)
throw;
			}
				
			return strTok.ToString();
		}
	}
}
