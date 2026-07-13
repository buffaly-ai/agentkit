namespace ProtoScript.Parsers
{
	public class FieldDefinitions
	{
		static public ProtoScript.FieldDefinition Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		static public ProtoScript.FieldDefinition ? Parse(Tokenizer tok)
		{
			ProtoScript.FieldDefinition result = new ProtoScript.FieldDefinition();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			ModifiersState(tok, result);

			result.Type = ProtoScript.Parsers.Types.Parse(tok);

			if (tok.peekNextToken() == "=") // initializer short form 
				return null;

			result.FieldName = ProtoScript.Parsers.Identifiers.Parse(tok);

			if (tok.CouldBeNext("="))
			{
				result.Initializer = VariableDeclarations.ParseInitializer(tok);
			}

			tok.MustBeNext(";");

			result.Info.StopStatement(tok.getCursor());

			return result;

		}

		//static readonly String s = "test";
		//static public readonly String s2 = "test";
		//public readonly String s3 = "test";
		//readonly public static String s4 = "test";
		//public const String s5 = "test";
		//readonly static public String s6 = "test";


		static void ModifiersState(Tokenizer tok, ProtoScript.FieldDefinition result)
		{
			ProtoScript.Modifiers modifiers = ProtoScript.Parsers.Modifiers.Parse(tok);
			result.IsStatic = modifiers.IsStatic;
			result.Visibility = modifiers.Visibility;
			result.IsReadonly = modifiers.IsReadonly;
			result.IsConst = modifiers.IsConst;
			result.IsNew = modifiers.IsNew;
			result.IsOverride = modifiers.IsOverride;
		}

	}
}
