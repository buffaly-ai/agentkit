namespace ProtoScript.Parsers
{
	public class FunctionDefinitions
	{
		static public ProtoScript.FunctionDefinition Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		static public ProtoScript.FunctionDefinition Parse(Tokenizer tok)
		{
			ProtoScript.FunctionDefinition result = new ProtoScript.FunctionDefinition();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("function");

			ModifiersState(tok, result);

			//Interfaces definitions can have multiple: IEnumerable.GetEnumerator
			result.FunctionName = ProtoScript.Parsers.Identifiers.ParseMultiple(tok);

			tok.MustBeNext("(");

			result.Parameters = ProtoScript.Parsers.FunctionDefinitions.ParseParameterList(tok);

			tok.MustBeNext(")");

			tok.MustBeNext(":");

			result.ReturnType = ProtoScript.Parsers.Types.Parse(tok);

			if (tok.CouldBeNext(";"))
				result.IsAbstract = true;
			else
			{
				result.Statements = ProtoScript.Parsers.CodeBlocks.Parse(tok);
			}

			result.Info.StopStatement(tok.getCursor());

			return result;
		}

		static public void ModifiersState(Tokenizer tok, ProtoScript.FunctionDefinition result)
		{
			ProtoScript.Modifiers modifiers = ProtoScript.Parsers.Modifiers.Parse(tok);
			result.SetModifiers(modifiers);
		}

		static public List<ParameterDeclaration> ParseParameterList(Tokenizer tok)
		{
			List<ParameterDeclaration> result = new List<ParameterDeclaration>();
			if (tok.peekNextToken() != ")")
				result.Add(ProtoScript.Parsers.ParameterDeclarations.Parse(tok));

			while (tok.hasMoreTokens() && tok.peekNextToken() != ")")
			{
				tok.MustBeNext(",");

				result.Add(ProtoScript.Parsers.ParameterDeclarations.Parse(tok));
			}

			return result;
		}

	}
}
