namespace ProtoScript.Parsers
{
	public class ImportStatements
	{
		static public ProtoScript.ImportStatement Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		public static ProtoScript.ImportStatement Parse(Tokenizer tok)
		{
			ProtoScript.ImportStatement result = new ProtoScript.ImportStatement();

			tok.movePastWhitespace();
			result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("import");

			result.Reference = ParseImportIdentifier(
				tok,
				"assembly alias",
				"Import statements use: import <ReferenceAlias> <TypeName> <ImportAlias>;.");
			result.Type = ParseImportIdentifier(
				tok,
				"type name",
				"Import statements use: import <ReferenceAlias> <TypeName> <ImportAlias>;.");

			// Shorthand import with implicit alias:
			// import Ontology.Simulation Ontology.Simulation.StringWrapper;
			if (tok.peekNextToken() == ";")
			{
				result.Import = InferAlias(result.Type);
			}
			else
			{
				result.Import = ParseImportIdentifier(
					tok,
					"import alias",
					"Import statements require an import alias. Example: import Ontology.Simulation Ontology.Simulation.StringWrapper String;");
			}

			try
			{
				tok.MustBeNext(";");
			}
			catch (ProtoScriptTokenizingException)
			{
				throw new ProtoScriptParsingException(
					tok.getString(),
					tok.getCursor(),
					"';'",
					"Import statements must end with ';'. Example: import Ontology.Simulation Ontology.Simulation.StringWrapper String;");
			}

			result.Info.StopStatement(tok.getCursor());
			return result;
		}

		private static string ParseImportIdentifier(Tokenizer tok, string expectedPart, string explanation)
		{
			try
			{
				return ProtoScript.Parsers.Identifiers.ParseMultiple(tok);
			}
			catch (ProtoScriptTokenizingException)
			{
				throw new ProtoScriptParsingException(tok.getString(), tok.getCursor(), expectedPart, explanation);
			}
		}

		private static string InferAlias(string typeName)
		{
			if (string.IsNullOrWhiteSpace(typeName))
				return typeName;

			int lastDot = typeName.LastIndexOf('.');
			if (lastDot >= 0 && lastDot < typeName.Length - 1)
				return typeName.Substring(lastDot + 1);

			return typeName;
		}
	}
}
