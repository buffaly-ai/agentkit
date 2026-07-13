namespace Ontology
{
	public class Lexeme
	{
		public const string PrototypeName = nameof(Ontology) + "." + nameof(Lexeme);

		public static int PrototypeID
		{
			get
			{
				return Prototype.PrototypeID;
			}
		}

		private static ResettablePrototypeAsyncLocal m_Prototype = new ResettablePrototypeAsyncLocal();
		public static Prototype Prototype
		{
			get
			{
				if (null == m_Prototype.Value)
					m_Prototype.Value = Prototypes.GetOrInsertPrototype(PrototypeName);

				return m_Prototype.Value;
			}
		}

		static public string GetStringValue(Prototype prototype)
		{
			TemporaryLexeme? lexeme = prototype as TemporaryLexeme;

			if (null == lexeme)
				throw new ArgumentException("Prototype is not a Lexeme: " + prototype.PrototypeName, nameof(prototype));

			return lexeme.Lexeme;			
		}
	}
}
