namespace ProtoScript.Parsers
{
	public class ParameterDeclarations
	{
		static public ProtoScript.ParameterDeclaration Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		static public ProtoScript.ParameterDeclaration Parse(Tokenizer tok)
		{
			ProtoScript.ParameterDeclaration result = new ProtoScript.ParameterDeclaration();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			if (tok.CouldBeNext("this"))
				result.IsThis = true;

			if (tok.CouldBeNext("ref"))
				result.IsRef = true;

			else if (tok.CouldBeNext("out"))
				result.IsOut = true;

			result.Type = ProtoScript.Parsers.Types.Parse(tok);

			result.ParameterName = ProtoScript.Parsers.Identifiers.Parse(tok);

			if (tok.CouldBeNext("="))
				result.DefaultValue = ProtoScript.Parsers.Expressions.Parse(tok);

			result.Info.StopStatement(tok.getCursor());

			return result;
		}
	}
}
