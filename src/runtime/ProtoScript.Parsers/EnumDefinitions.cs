namespace ProtoScript.Parsers
{
	public class EnumDefinitions
	{
		static public ProtoScript.EnumDefinition Parse(Tokenizer tok)
		{
			ProtoScript.EnumDefinition result = new ProtoScript.EnumDefinition();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			result.Visibility = Visibilities.Parse(tok);

			tok.MustBeNext("enum");

			result.EnumName = ProtoScript.Parsers.Identifiers.Parse(tok);

			tok.MustBeNext("{");

			if (tok.peekNextToken() != "}")
				result.EnumTypes.Add(ProtoScript.Parsers.Identifiers.Parse(tok));

			while (tok.hasMoreTokens() && tok.peekNextToken() != "}")
			{
				tok.MustBeNext(",");

				result.EnumTypes.Add(ProtoScript.Parsers.Identifiers.Parse(tok));
			}

			tok.MustBeNext("}");
			tok.CouldBeNext(";");

			result.Info.StopStatement(tok.getCursor());

			return result;
		}

	}
}
