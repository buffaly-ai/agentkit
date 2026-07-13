namespace ProtoScript.Parsers
{
	public class PropertyDefinitions
	{
		static public ProtoScript.PropertyDefinition Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		static public ProtoScript.PropertyDefinition Parse(Tokenizer tok)
		{
			ProtoScript.PropertyDefinition result = new ProtoScript.PropertyDefinition();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			ModifiersState(tok, result);

			result.Type = ProtoScript.Parsers.Types.Parse(tok);

			result.PropertyName = ProtoScript.Parsers.Identifiers.ParseMultiple(tok);

			if (tok.CouldBeNext("["))
			{
				result.Indexer = ProtoScript.Parsers.ParameterDeclarations.Parse(tok);
				tok.MustBeNext("]");
			}

			tok.MustBeNext("{");

			Visibilities.Parse(tok);			///discard these for now  { private get; set; }

			if (tok.CouldBeNext("get"))
			{
				GetterState(tok, result);

				Visibilities.Parse(tok);

				if (tok.CouldBeNext("set"))
					SetterState(tok, result);
			}

			else if (tok.CouldBeNext("set"))
			{
				SetterState(tok, result);

				Visibilities.Parse(tok);

				if (tok.CouldBeNext("get"))
					GetterState(tok, result);
			}

			tok.MustBeNext("}");

			result.Info.StopStatement(tok.getCursor());

			return result;
		}

		static void ModifiersState(Tokenizer tok, ProtoScript.PropertyDefinition result)
		{
			ProtoScript.Modifiers modifiers = ProtoScript.Parsers.Modifiers.Parse(tok);
			result.SetModifiers(modifiers);
		}

		static void GetterState(Tokenizer tok, ProtoScript.PropertyDefinition result)
		{
			//automatic
			if (tok.CouldBeNext(";"))
				result.Getter = new CodeBlock();
			else
				result.Getter = ProtoScript.Parsers.CodeBlocks.Parse(tok);
		}

		static void SetterState(Tokenizer tok, ProtoScript.PropertyDefinition result)
		{
			//automatic
			if (tok.CouldBeNext(";"))
				result.Setter = new CodeBlock();
			else
				result.Setter = ProtoScript.Parsers.CodeBlocks.Parse(tok);
		}
	}
}
