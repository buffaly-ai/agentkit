namespace Ontology.Parsers
{
	public class LexemeUtil
	{
		static public Prototype CharacterTokenizer(Prototype prototype)
		{
			TemporaryLexeme? lexeme = prototype as TemporaryLexeme;
			if (lexeme == null)
			{
				throw new ArgumentException("Prototype must be a TemporaryLexeme", nameof(prototype));
			}

			Prototype protoResult = Ontology.Collection.Prototype.Clone();
			CharacterTokenizer tok = new CharacterTokenizer(lexeme.Lexeme);

			while (tok.hasMoreTokens())
			{
				string strTok = tok.getNextToken();

				//N20181216-01
				Prototype rowLexemeToken = Lexemes.GetOrInsertLexeme(strTok);
				protoResult.Children.Add(rowLexemeToken);
			}

			return protoResult;
		}

		static public Prototype UppercaseTokenizer(Prototype prototype)
		{
			TemporaryLexeme? lexeme = prototype as TemporaryLexeme;
			if (lexeme == null)
			{
				throw new ArgumentException("Prototype must be a TemporaryLexeme", nameof(prototype));
			}

			Prototype protoResult = Ontology.Collection.Prototype.Clone();

			UppercaseTokenizer tok = new UppercaseTokenizer(lexeme.Lexeme);
			while (tok.hasMoreTokens())
			{
				string strTok = tok.getNextToken();

				Prototype rowLexemeToken = Lexemes.GetOrInsertLexeme(strTok);
				protoResult.Children.Add(rowLexemeToken);
			}

			return protoResult;
		}

	}

}

