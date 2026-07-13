namespace ProtoScript.Parsers
{
	public class Statements
	{
		static public ProtoScript.Statement ParseBestCase(string strStatement)
		{
			ProtoScript.Parsers.Settings.BestCaseExpressions = true;
			Statement statement = Parse(strStatement);
			ProtoScript.Parsers.Settings.BestCaseExpressions = false;
			return statement;
		}

		static public ProtoScript.Statement Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		static public ProtoScript.Statement Parse(Tokenizer tok)
		{
			string strTok = tok.peekNextToken();
			int iCursor = tok.getCursor();

			try
			{

				switch (strTok)
				{
					case "return":
						return ProtoScript.Parsers.ReturnStatements.Parse(tok);

					case "switch":
						return ProtoScript.Parsers.SwitchStatements.Parse(tok);

					case "case":
						return ProtoScript.Parsers.SwitchStatements.ParseCaseStatement(tok);

					case "default":
						return ProtoScript.Parsers.SwitchStatements.ParseDefaultStatement(tok);

					case "continue":
						ContinueStatement contStatement = new ContinueStatement();
						contStatement.Info.StartStatement(iCursor);
						contStatement.Info.File = Files.CurrentFile;

						tok.getNextToken();

						contStatement.Info.StopStatement(tok.getCursor());

						tok.MustBeNext(";");
						return contStatement;

					case "break":
						BreakStatement breakStatement = new BreakStatement();
						breakStatement.Info.StartStatement(iCursor);
						breakStatement.Info.File = Files.CurrentFile;

						tok.getNextToken();
						breakStatement.Info.StopStatement(tok.getCursor());
						tok.MustBeNext(";");

						return breakStatement;

					case "throw":
						return ProtoScript.Parsers.ThrowStatements.Parse(tok);


					case "try":
						return ProtoScript.Parsers.TryStatements.Parse(tok);

					case "while":
						return ProtoScript.Parsers.WhileStatements.Parse(tok);
					case "do":
						return ProtoScript.Parsers.DoStatements.Parse(tok);

					case "foreach":
						return ProtoScript.Parsers.ForEachStatements.Parse(tok);

					case "if":
						return ProtoScript.Parsers.IfStatements.Parse(tok);

					case "for":
						return ProtoScript.Parsers.ForStatements.Parse(tok);


					case "{":
						CodeBlockStatement statement = new CodeBlockStatement(ProtoScript.Parsers.CodeBlocks.Parse(tok));
						return statement;

					case ";":
						tok.getNextToken();
						throw new ProtoScriptParsingException(tok.getString(), tok.getCursor(), "not empty statement");

					case "yield":
						return ProtoScript.Parsers.YieldStatements.Parse(tok);

					case "function":
						return ProtoScript.Parsers.FunctionDefinitions.Parse(tok);

					default:
						return ParseDeclarationOrExpression(tok, false);
				}
			}
			catch (Exception err)
			{
				if (Settings.FailOnParsingErrors)
					throw Logs.LogError(err);
				else
				{
					tok.setCursor(iCursor); //go back to the start of the line, then move past the end of the line, in case we consumed the end of the line already
					tok.movePast("\n");       //N20200505-02 - Try to recover on the next line
					Logs.DebugLog.WriteErrorPretty(err);
				}
			}

			return null;

		}

		public static ProtoScript.Statement ParseDeclarationOrExpression(Tokenizer tok, bool bNaked)
		{
			int iCursor = tok.getCursor();
			ProtoScript.Statement statement = null;
			ProtoScriptTokenizingException savedException = null;
			try
			{
				statement = ProtoScript.Parsers.VariableDeclarations.ParseWithoutExceptions(tok, bNaked);

				if (null != statement && (statement as VariableDeclaration).Type == null)
					statement = null;
			}
			catch (ProtoScriptTokenizingException err)
			{
				savedException = err;
				statement = null; 
			}

			if (null == statement)
			{
				try
				{
					tok.setCursor(iCursor);
					statement = ProtoScript.Parsers.ExpressionStatements.Parse(tok, bNaked);
				}
				catch (ProtoScriptTokenizingException err2)
				{
					tok.setCursor(iCursor);

					err2.Explanation = BuildCombinedParseExplanation(savedException, err2, tok.getString(), iCursor);

					throw err2;
				}
			}

			return statement;
		}

		private static string BuildCombinedParseExplanation(
			ProtoScriptTokenizingException? declarationError,
			ProtoScriptTokenizingException expressionError,
			string source,
			int cursor)
		{
			string left = declarationError?.Explanation?.Trim() ?? string.Empty;
			string right = expressionError.Explanation?.Trim() ?? string.Empty;

			if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
				return left + ", " + right;
			if (!string.IsNullOrWhiteSpace(left))
				return left;
			if (!string.IsNullOrWhiteSpace(right))
				return right;

			if (LooksLikeMissingPrototypeDeclaration(source, cursor))
			{
				return "Invalid top-level declaration. If this is a project/entity instance, use `prototype Name : BaseType { ... }`.";
			}

			string expected = expressionError?.Expected?.Trim();
			if (!string.IsNullOrWhiteSpace(expected))
				return "Could not parse statement. Expected: " + expected;

			return "Could not parse statement.";
		}

		private static bool LooksLikeMissingPrototypeDeclaration(string source, int cursor)
		{
			if (string.IsNullOrEmpty(source) || cursor < 0 || cursor >= source.Length)
				return false;

			List<string> nearbyLines = GetNearbyNonEmptyLines(source, cursor, 4);
			foreach (string line in nearbyLines)
			{
				if (line.StartsWith("prototype ", StringComparison.Ordinal))
					continue;

				int hashIndex = line.IndexOf('#');
				if (hashIndex <= 0 || hashIndex >= line.Length - 1)
					continue;

				bool allAllowed = true;
				for (int i = 0; i < line.Length; i++)
				{
					char c = line[i];
					if (!(char.IsLetterOrDigit(c) || c == '_' || c == '#'))
					{
						allAllowed = false;
						break;
					}
				}

				if (allAllowed)
					return true;
			}

			return false;
		}

		private static List<string> GetNearbyNonEmptyLines(string source, int cursor, int maxLines)
		{
			List<string> lines = new List<string>();
			if (maxLines <= 0)
				return lines;

			int pos = Math.Min(cursor, source.Length - 1);
			while (pos >= 0 && lines.Count < maxLines)
			{
				int lineStart = source.LastIndexOf('\n', pos);
				lineStart = lineStart < 0 ? 0 : lineStart + 1;
				int lineEnd = source.IndexOf('\n', lineStart);
				if (lineEnd < 0)
					lineEnd = source.Length;

				string line = source.Substring(lineStart, Math.Max(0, lineEnd - lineStart)).Trim();
				if (!string.IsNullOrWhiteSpace(line))
					lines.Add(line);

				pos = lineStart - 2;
			}

			return lines;
		}
	}

}
