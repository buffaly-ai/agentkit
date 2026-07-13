namespace ProtoScript.Parsers
{
	public class VariableDeclarations
	{
		static public ProtoScript.VariableDeclaration Parse(string strStatement, bool bNaked = false)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok, bNaked);
		}

		static public ProtoScript.VariableDeclaration Parse(Tokenizer tok, bool bNaked = false)
		{
			ProtoScript.VariableDeclaration result = new ProtoScript.VariableDeclaration();

			try
			{

				tok.movePastWhitespace();
				result.Info.StartStatement(tok.getCursor());
				result.Info.File = Files.CurrentFile;

				while (true)
				{
					if (tok.CouldBeNext("extern"))
					{
						result.IsExternal = true;
						continue;
					}

					if (tok.CouldBeNext("const"))
					{
						result.IsConst = true;
						continue;
					}

					break;
				}

				result.Type = ProtoScript.Parsers.Types.Parse(tok);

				result.VariableName = ProtoScript.Parsers.Identifiers.Parse(tok);

				if (tok.CouldBeNext("="))
				{
					result.Initializer = ParseInitializer(tok);
				}

				while (tok.CouldBeNext(","))
				{
					VariableDeclaration declaration = new VariableDeclaration();
					declaration.Type = result.Type; 
					declaration.VariableName = ProtoScript.Parsers.Identifiers.Parse(tok);
					declaration.IsConst = result.IsConst;
					declaration.IsExternal = result.IsExternal;

					if (tok.CouldBeNext("="))
						declaration.Initializer = ParseInitializer(tok);


					if (null == result.ChainedDeclarations)
						result.ChainedDeclarations = new List<VariableDeclaration>();

					result.ChainedDeclarations.Add(declaration);
				}

				if (!bNaked)
					tok.MustBeNext(";");
			}
			catch (Exception err)
			{
				if (!Settings.BestCaseExpressions)
throw;

				result.Info.IsIncomplete = true;
			}

			result.Info.StopStatement(tok.getCursor());

			return result;

		}

		static public ProtoScript.VariableDeclaration ParseWithoutExceptions(Tokenizer tok, bool bNaked = false)
		{
			ProtoScript.VariableDeclaration result = new ProtoScript.VariableDeclaration();

			try
			{

				tok.movePastWhitespace();
				result.Info.StartStatement(tok.getCursor());
				result.Info.File = Files.CurrentFile;

				while (true)
				{
					if (tok.CouldBeNext("extern"))
					{
						result.IsExternal = true;
						continue;
					}

					if (tok.CouldBeNext("const"))
					{
						result.IsConst = true;
						continue;
					}

					break;
				}

				if (tok.IsOperator(tok.peekNextToken()))
					return null;

				result.Type = ProtoScript.Parsers.Types.Parse(tok);

				if (tok.IsOperator(tok.peekNextToken()))
					return null;

				result.VariableName = ProtoScript.Parsers.Identifiers.Parse(tok);

				if (tok.CouldBeNext("="))
				{
					result.Initializer = ParseInitializer(tok);
				}

				while (tok.CouldBeNext(","))
				{
					VariableDeclaration declaration = new VariableDeclaration();
					declaration.Type = result.Type;
					declaration.VariableName = ProtoScript.Parsers.Identifiers.Parse(tok);
					declaration.IsConst = result.IsConst;
					declaration.IsExternal = result.IsExternal;

					if (tok.CouldBeNext("="))
						declaration.Initializer = ParseInitializer(tok);


					if (null == result.ChainedDeclarations)
						result.ChainedDeclarations = new List<VariableDeclaration>();

					result.ChainedDeclarations.Add(declaration);
				}

				if (!bNaked)
					tok.MustBeNext(";");
			}
			catch (Exception err)
			{
				if (!Settings.BestCaseExpressions)
throw;

				result.Info.IsIncomplete = true;
			}

			result.Info.StopStatement(tok.getCursor());

			return result;

		}

		static internal Expression ParseInitializer(Tokenizer tok)
		{
			Expression exp = null; 
			//ArrayLiteral initializer
			if (tok.peekNextToken() == "{")
			{
				exp = new Expression();
				exp.Terms.Add(ProtoScript.Parsers.ArrayLiterals.Parse(tok));
			}
			else
			{
				exp = ProtoScript.Parsers.Expressions.Parse(tok);
			}
			return exp;
		}

	}
}
