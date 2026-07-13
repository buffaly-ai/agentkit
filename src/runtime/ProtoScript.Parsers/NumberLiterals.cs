using BasicUtilities;

namespace ProtoScript.Parsers
{
	public class NumberLiterals
	{
		static public ProtoScript.NumberLiteral ? Parse(Tokenizer tok)
		{
			string strTok = tok.getNextToken();
			if (tok.peekNextChar() == '.')
			{
				strTok += tok.getNextChar();
				strTok += tok.getNextToken();
			}

			if (strTok.EndsWith("M", StringComparison.InvariantCultureIgnoreCase))
			{
				if (!StringUtil.IsNumber(strTok.Substring(0, strTok.Length - 1)))
					return null;

				return new DecimalLiteral(strTok);
			}

			if (strTok.EndsWith("UL", StringComparison.InvariantCultureIgnoreCase))
			{
				if (!StringUtil.IsNumber(strTok.Substring(0, strTok.Length - 2)))
					return null;

				return new UnsignedLongLiteral(strTok);
			}

			if (strTok.EndsWith("L", StringComparison.InvariantCultureIgnoreCase))
			{
				if (!StringUtil.IsNumber(strTok.Substring(0, strTok.Length - 1)))
					return null;

				return new LongLiteral(strTok);
			}

			if (strTok.EndsWith("F", StringComparison.InvariantCultureIgnoreCase))
			{
				if (!StringUtil.IsNumber(strTok.Substring(0, strTok.Length - 1)))
					return null;

				return new FloatLiteral(strTok);
			}

			if (!StringUtil.IsNumber(strTok))
				return null;

			if (strTok.Contains("."))
				return new DoubleLiteral(strTok);

			return new IntegerLiteral(strTok);
		}

	}
}
