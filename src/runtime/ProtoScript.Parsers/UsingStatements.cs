namespace ProtoScript.Parsers
{
	public class UsingStatements
	{
		static public ProtoScript.UsingStatement Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		public static ProtoScript.UsingStatement Parse(Tokenizer tok)
		{
			ProtoScript.UsingStatement result = new ProtoScript.UsingStatement();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("using");

			if (tok.CouldBeNext("static"))
				result.IsStatic = true;

			do
			{
				string strTok = ProtoScript.Parsers.Identifiers.Parse(tok);
				result.Namespaces.Add(strTok);
			}
			while (tok.CouldBeNext("."));

			tok.MustBeNext(";");

			result.Info.StopStatement(tok.getCursor());

			return result;
		}

		static public List<string> ParseNamespaces(Tokenizer tok)
		{
			List<string> lstResults = new List<string>();
			do
			{
				string strTok = ProtoScript.Parsers.Identifiers.Parse(tok);
				lstResults.Add(strTok);
			}
			while (tok.CouldBeNext("."));

			return lstResults;
		}

	}
}
