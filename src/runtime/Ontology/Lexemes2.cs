using System.Text;
using BasicUtilities;

namespace Ontology
{		
    public partial class Lexemes
    {


		public static TemporaryLexeme ? GetLexemeByLexeme(string Lexeme)
		{
			return TemporaryLexemes.GetLexemeByLexeme(Lexeme);
		}

		static public Prototype GetLexemeByPrototypeID(int iPrototypeID)
		{
			Prototype protoLexeme = TemporaryPrototypes.GetTemporaryPrototype(iPrototypeID); 
			if (null == protoLexeme || !protoLexeme.TypeOf(Lexeme.Prototype))
			{
				throw new Exception("Lexeme not found for PrototypeID: " + iPrototypeID);
			}

			return protoLexeme;
		}

		static public List<Prototype> GetAsMultiple(Prototype protoLexeme, Prototype protoParent)
		{
			List<Prototype> lstResults = new List<Prototype>();

			if (Prototypes.TypeOf(protoLexeme, Lexeme.Prototype))
			{
				TemporaryLexeme? lexeme = protoLexeme as TemporaryLexeme;

				if (null == lexeme)
					throw new Exception("Lexeme not found for PrototypeID: " + protoLexeme.PrototypeID);

				foreach (var rowLexemePrototype in lexeme.LexemePrototypes)
				{
					//Use this method so it can retrieve temporary if necessary 
					Prototype protoRelated = rowLexemePrototype.Key;					
					if (protoRelated.TypeOf(protoParent))
						lstResults.Add(protoRelated);
				}
			}

			return lstResults;
		}
	

		public static Prototype GetOrInsertLexeme(string strLexeme)
		{
			return TemporaryLexemes.GetOrInsertLexeme(strLexeme);
		}

		const string SYMBOLS = "\\.:,?!/-\"\'";

		public static List<string> Split(string strLexeme)
		{
			Tokenizer tokens = new Tokenizer(strLexeme);
			tokens.clearSymbols();
			foreach (char c in SYMBOLS)
				tokens.insertSymbol(c);

			return tokens.split();
		}

		public static string ToPrototypeName(string strLexeme)
		{
			if (StringUtil.IsEmpty(strLexeme))
				throw new Exception("Cannot create empty Lexeme");

			//>if any character is not a number or letter, replace it with _
			StringBuilder sbResult = new StringBuilder();

			bool capitalizeNext = true;               // upper-case first usable char and every char after space/period

			foreach (char c in strLexeme)
			{
				if (char.IsWhiteSpace(c) || c == '.')
				{
					sbResult.Append("_");
					capitalizeNext = true;            // the next α-numeric should be capitalised
				}
				else if (char.IsLetterOrDigit(c))
				{
					sbResult.Append(capitalizeNext && char.IsLetter(c) ? char.ToUpperInvariant(c) : c);
					capitalizeNext = false;
				}
				else
				{
					sbResult.Append(Convert.ToInt32(c));
					capitalizeNext = false;
				}
			}

			//N20250420-01 - To avoid collisions on single characters, e.g. "_" -> _45 
			if (strLexeme.Length == 1 && !char.IsLetterOrDigit(strLexeme, 0))
				sbResult.Append("_Char");

			strLexeme = sbResult.ToString();

			if (!char.IsLetter(strLexeme[0]))
				strLexeme = "_" + strLexeme;

			else
				strLexeme = StringUtil.UppercaseFirstLetter(strLexeme);

			return strLexeme;
		}

		public static string TokenToMultiToken(string strToken)
		{
			//Use this method to turn a single token like FileNameHere into a multi-token like File Name Here
			strToken = string.Join(" ", StringUtil.SplitUppercaseWords(strToken));
			StringBuilder cleanedFileNameBuilder = new StringBuilder(strToken);

			for (int i = 0; i < cleanedFileNameBuilder.Length; i++)
			{
				if (!char.IsLetterOrDigit(cleanedFileNameBuilder[i]))
					cleanedFileNameBuilder[i] = ' ';
			}

			strToken = cleanedFileNameBuilder.ToString();

			return strToken;
		}
	}
}    

