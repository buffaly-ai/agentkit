namespace ProtoScript.Parsers
{
	public class NamespaceDefinitions
	{
		static public ProtoScript.NamespaceDefinition Parse(string strCode)
		{
			Tokenizer tok = new Tokenizer(strCode);
			return Parse(tok);
		}
		static public ProtoScript.NamespaceDefinition Parse(Tokenizer tok)
		{
			ProtoScript.NamespaceDefinition result = new ProtoScript.NamespaceDefinition();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("namespace");

			result.Namespaces.Add(ProtoScript.Parsers.Identifiers.Parse(tok));

			while (tok.CouldBeNext("."))
			{
				result.Namespaces.Add(ProtoScript.Parsers.Identifiers.Parse(tok));
			}

			tok.MustBeNext("{");

			List<AnnotationExpression> lstAnnotations = new List<AnnotationExpression>();

			while (tok.hasMoreTokens() && tok.peekNextToken() != "}")
			{
				string strToken = tok.peekNextToken();

				switch (strToken)
				{
					case "partial":
					case "prototype":
						{
							PrototypeDefinition protoDef = ProtoScript.Parsers.PrototypeDefinitions.Parse(tok);
							if (lstAnnotations.Count > 0)
							{
								protoDef.Annotations = lstAnnotations;
								lstAnnotations = new List<AnnotationExpression>();
							}
							result.PrototypeDefinitions.Add(protoDef);

							break;
						}
					case "extern":
						{
							if (IsPrototypeDefinitionAhead(tok))
							{
								PrototypeDefinition protoDef = ProtoScript.Parsers.PrototypeDefinitions.Parse(tok);
								if (lstAnnotations.Count > 0)
								{
									protoDef.Annotations = lstAnnotations;
									lstAnnotations = new List<AnnotationExpression>();
								}
								result.PrototypeDefinitions.Add(protoDef);
								break;
							}

							Statement statement = Statements.Parse(tok);
							result.Statements.Add(statement);
							break;
						}

					case "[":
						{
							lstAnnotations.Add(ProtoScript.Parsers.AnnotationExpressions.Parse(tok));
							break;
						}
					case "":
						{
							//Comment as the last piece of a file
							tok.getNextToken();
							break;
						}
					default:
						{
							Statement statement = Statements.Parse(tok);
							//if (statement is FunctionDefinition && lstAnnotations.Count > 0)
							//{
							//	FunctionDefinition functionDefinition = statement as FunctionDefinition;
							//	functionDefinition.Annotations = lstAnnotations;
							//	lstAnnotations = new List<AnnotationExpression>();
							//}

							//if (null == statement)
							//{

							//}
							result.Statements.Add(statement);
							break;
						}
				}
			}
			tok.MustBeNext("}");
			
			result.Info.StopStatement(tok.getCursor());

			return result;
		}

		private static bool IsPrototypeDefinitionAhead(Tokenizer tok)
		{
			int saveCursor = tok.getCursor();
			try
			{
				ProtoScript.Parsers.Modifiers.Parse(tok);
				return tok.peekNextToken() == "prototype";
			}
			finally
			{
				tok.setCursor(saveCursor);
			}
		}

	}
}
