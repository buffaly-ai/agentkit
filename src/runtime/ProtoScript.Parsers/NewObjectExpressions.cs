namespace ProtoScript.Parsers
{
	public class NewObjectExpressions
	{
		static public ProtoScript.NewObjectExpression Parse(string strExpression)
		{
			Tokenizer tok = new Tokenizer(strExpression);
			return Parse(tok);
		}


		static public ProtoScript.NewObjectExpression Parse(Tokenizer tok)
		{
			ProtoScript.NewObjectExpression result = new ProtoScript.NewObjectExpression();
			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("new");

			if ("{" == tok.peekNextToken())     //anonymous type 
			{
				result.Type = new ProtoScript.Type() { TypeName = "(anonymous)" };
			}
			else
			{
				//Normal case	new Object
				result.Type = ProtoScript.Parsers.Types.Parse(tok);
			}

			if (result.Type.IsArray && tok.peekNextToken() == "{")
			{
				result.ArrayInitializer = ProtoScript.Parsers.ArrayLiterals.Parse(tok);
			}

			else if (tok.peekNextToken() == "(")
			{
				result.Parameters.AddRange(ProtoScript.Parsers.Expressions.ParseExpressionList(tok).Expressions);
			}

			//Object initializer
			//https://docs.microsoft.com/en-us/dotnet/ProtoScript/programming-guide/classes-and-structs/object-and-collection-initializers

			if (tok.peekNextToken() == "{")
			{
				result.Initializers = ParseObjectInitializer2(tok).Expressions;
			}
			
			result.Info.StopStatement(tok.getCursor());

			return result;
		}

		private static ExpressionList ParseObjectInitializer2(Tokenizer tok)
		{
			ExpressionList lstInitializers = new ExpressionList();
			if (tok.MustBeNext("{"))
			{
				bool bFirst = true;

				while (tok.hasMoreTokens() && tok.peekNextToken() != "}")
				{
					if (bFirst)
						bFirst = false;
					else if (tok.peekNextToken() == ";")
						throw new ProtoScriptParsingException("", tok.getCursor(), "comma or end of statement", "Object initializer should not contain ;");
					else
						tok.MustBeNext(",");

					string strTok = tok.peekNextToken();

					if ("{" == strTok)
					{
						ExpressionList lstSub = ParseObjectInitializer2(tok);
						lstInitializers.Expressions.Add(lstSub);
					}

					else if ("new" == strTok)
					{
						lstInitializers.Expressions.Add(NewObjectExpressions.Parse(tok));
					}

					else
					{
						int iCursor = tok.getCursor();
						bool parsedIdentifier = false;
						try
						{
							strTok = Identifiers.ParseMultiple(tok);
							parsedIdentifier = true;
						}
						catch
						{
							tok.setCursor(iCursor);
						}

						if (parsedIdentifier && tok.CouldBeNext("="))
						{
							NewObjectExpression.ObjectInitializer initializer = new NewObjectExpression.ObjectInitializer();
							initializer.Info.StartStatement(iCursor);
							initializer.Info.File = Files.CurrentFile;

							initializer.Name = strTok;
							initializer.Value = Expressions.Parse(tok);
							initializer.Info.StopStatement(tok.getCursor());

							lstInitializers.Expressions.Add(initializer);
						}
						else
						{
							tok.setCursor(iCursor);
							lstInitializers.Expressions.Add(Expressions.Parse(tok));
						}
					}

				}

				tok.MustBeNext("}");
			}

			return lstInitializers;
		}


		private static NewObjectExpression.ObjectInitializer ParseObjectInitializer(Expression expression, Tokenizer tok)
		{
			NewObjectExpression.ObjectInitializer initializer = new NewObjectExpression.ObjectInitializer();

			if (expression.Terms.Count < 3)
				throw new ProtoScriptParsingException(tok.getString(), tok.getCursor(), "Name = Value");

			if (!(expression.Terms[0] is Identifier))
				throw new ProtoScriptParsingException(tok.getString(), tok.getCursor(), "Identifier");

			initializer.Name = (expression.Terms[0] as Identifier).Value;
			expression.Terms.RemoveAt(0);

			if (!(expression.Terms[0] is BinaryOperator) || (expression.Terms[0] as BinaryOperator).Value != "=")
				throw new ProtoScriptParsingException(tok.getString(), tok.getCursor(), "=");

			expression.Terms.RemoveAt(0);
			initializer.Value = expression;

			return initializer;
		}
	}
}

