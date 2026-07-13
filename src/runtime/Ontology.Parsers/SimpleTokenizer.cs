namespace Ontology.Parsers
{
	public class SimpleTokenizer
	{
	public static Prototype Tokenize(string strInput)
		{
			return Tokenize(strInput, true);
		}


		public static Prototype Tokenize(string strInput, bool bAddNewLexemes)
		{
			Prototype parent = Ontology.Collection.Prototype.Clone();

			foreach (string strToken in Lexemes.Split(strInput))
			{
				Prototype ? rowLexeme = null;
				if (bAddNewLexemes)
					rowLexeme = Lexemes.GetOrInsertLexeme(strToken);
				else
					rowLexeme = Lexemes.GetLexemeByLexeme(strToken);

				if (null == rowLexeme)
					throw new Exception("Unknown Lexeme: " + strToken); 

				parent.Children.Add(Prototypes.GetPrototype(rowLexeme.PrototypeID));
			}

			return parent;
		}

	}
}

