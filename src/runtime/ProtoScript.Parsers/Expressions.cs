using System.Text;

namespace ProtoScript.Parsers
{
	public class Expressions
	{
		static public ProtoScript.Expression ParseBestCase(string strExpression)
		{
			ProtoScript.Parsers.Settings.BestCaseExpressions = true;
			try
			{
				ProtoScript.Expression expression = Parse(strExpression);				
				return expression;
			}
			finally
			{
				ProtoScript.Parsers.Settings.BestCaseExpressions = false;
			}
		}
		static public ProtoScript.Expression Parse(string strExpression)
		{
			Tokenizer tok = new Tokenizer(strExpression);
			return Parse(tok);
		}

		static public ProtoScript.Expression Parse(Tokenizer tok)
		{
			ProtoScript.Expression result = new ProtoScript.Expression();
			tok.movePastWhitespace();
			result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			try
			{

				string strTok = tok.peekNextToken();
				bool bHandled = false;
				Expression term = null;
				int iStart = tok.getCursor();

				if (strTok == ";") // Do we allow for a blank statement? It may be legal
				{
					throw new ProtoScriptParsingException(tok.getString(), tok.getCursor(), ";");
				}

				else if (tok.IsUnaryOperator(strTok))
				{
					UnaryOperator op = new UnaryOperator(tok.getNextToken());
					op.Right = ProtoScript.Parsers.Expressions.ParseTerm(tok);
					term = op;
					bHandled = true;
				}

				else if (strTok == "(")
				{
					tok.getNextToken();
					term = Parse(tok);
					tok.MustBeNext(")");
					term.IsParenthesized = true;
					bHandled = true;
					//int iCursor = tok.getCursor();
					//try
					//{
					//	CastingOperator op = ParseCastingOperator(tok);
					//	if (null != op)
					//	{
					//		if (tok.peekNextToken() != ";" && !tok.IsBinaryOperator(tok.peekNextToken()) && tok.peekNextToken() != ")")
					//		{
					//			result.Terms.Add(op);
					//			result.Terms.AddRange(ProtoScript.Parsers.Expressions.Parse(tok).Terms);
					//			bHandled = true;
					//		}
					//	}
					//}
					//catch
					//{
					//}

					//if (!bHandled)
					//	tok.setCursor(iCursor);

				}

				if (!bHandled)
				{
					term = ProtoScript.Parsers.Expressions.ParseTerm(tok);
					term.Info.StartStatement(iStart);
					term.Info.File = Files.CurrentFile;
					term.Info.StopStatement(tok.getCursor());
				}

				strTok = tok.peekNextToken();

				while (strTok == "[") //postfix index operator
				{
					IndexOperator op = new IndexOperator();
					op.Info.StartStatement(tok.getCursor());
					op.Info.File = Files.CurrentFile;

					tok.getNextToken();

					//Only allow one parameter (no [1, 3] )
					op.Right = ProtoScript.Parsers.Expressions.Parse(tok);

					op.Left = term;

					term = op;

					tok.MustBeNext("]");
					op.Info.StopStatement(tok.getCursor());

					strTok = tok.peekNextToken();
				}

				if ("++" == strTok || "--" == strTok) //unary postfix operator
				{
					term = new UnaryOperator(tok.getNextToken());
					strTok = tok.peekNextToken();
				}

				if (strTok == ".")
				{
					term = ParseDotOperators(tok, term);
					term.Info.StartStatement(iStart);
					term.Info.File = Files.CurrentFile;
					term.Info.StopStatement(tok.getCursor());
					strTok = tok.peekNextToken();
				}

				bool bNot = false;
				if (strTok == "!")
				{
					bNot = true;
					tok.getNextToken();
					strTok = tok.peekNextToken();
				}



				if (tok.IsBinaryOperator(strTok))
				{
					BinaryOperator op = new BinaryOperator(tok.getNextToken());
					op.Info.StartStatement(iStart);
					op.Info.File = Files.CurrentFile;
					op.Left = term;

					if (bNot)
					{
						UnaryOperator opNot = new UnaryOperator("!");
						opNot.Right = op;

						term = opNot;
					}
					else
					{
						term = op;
					}


					if (strTok == "=>" && tok.peekNextToken() == "{")
					{
						CodeBlock lambdaBlock = CodeBlocks.Parse(tok);
						CodeBlockExpression expressionBlock = new CodeBlockExpression(lambdaBlock);
						op.Right = expressionBlock;
					}
					else
					{
						Expression exprRight = GetSingleExpressionTerm(ProtoScript.Parsers.Expressions.Parse(tok), tok, "expression");
						if (exprRight is BinaryOperator)
						{
							BinaryOperator opRight = (exprRight as BinaryOperator);
							int iRight = GetPrecedence(opRight.Value);
							int iLeft = GetPrecedence(strTok);

							if (iLeft < iRight
								//||	(opRight.Value == "." &&  strTok == ".")	//Dot operators have a reverse direction of evaluation
							)
							{
								//N20220428-01
								//Expression nestedLeft = opRight.Left;
								//BinaryOperator opRightOriginal = opRight;
								//while (nestedLeft is BinaryOperator && (nestedLeft as BinaryOperator).Value == ".")
								//{
								//	opRight = (nestedLeft as BinaryOperator);
								//	nestedLeft = opRight.Left;
								//}
								//op.Right = nestedLeft;
								//opRight.Left = op;
								//term = opRightOriginal;

								op.Right = opRight.Left;
								opRight.Left = op;
								term = opRight;
							}
							else
							{
								op.Right = opRight;
							}

						}
						else
							op.Right = exprRight;
					}

					op.Info.StopStatement(tok.getCursor());
				}

				else if (strTok == "->")
				{
					CategorizationOperator op = new CategorizationOperator();
					op.Info.StartStatement(iStart);
					op.Left = term;
					tok.getNextToken();
					op.Middle = ParseTerm(tok);
					op.Right = ScopedExpressionLists.Parse(tok);
					op.Info.StopStatement(tok.getCursor());
					term = op;
				}

				else if (strTok == "?") // Ternary
				{
					BinaryOperator opQuestion = new BinaryOperator(tok.getNextToken());
					opQuestion.Info.StartStatement(iStart);
					opQuestion.Info.File = Files.CurrentFile;
					opQuestion.Left = term;

					Expression expressionWhenTrue = GetSingleExpressionTerm(ProtoScript.Parsers.Expressions.Parse(tok), tok, "ternary true expression");

					tok.MustBeNext(":");

					Expression expressionWhenFalse = GetSingleExpressionTerm(ProtoScript.Parsers.Expressions.Parse(tok), tok, "ternary false expression");

					BinaryOperator opColon = new BinaryOperator(":");
					opColon.Info.StartStatement(iStart);
					opColon.Info.File = Files.CurrentFile;
					opColon.Left = expressionWhenTrue;
					opColon.Right = expressionWhenFalse;
					opColon.Info.StopStatement(tok.getCursor());

					opQuestion.Right = opColon;
					opQuestion.Info.StopStatement(tok.getCursor());

					term = opQuestion;
				}

				else if (strTok == "(")
				{
					throw new ProtoScriptParsingException(tok.getString(), tok.getCursor(), "not an open parens");

					//function evaluation 
					//throw new NotImplementedException();
					//result.Terms.Add(ProtoScript.Parsers.ExpressionLists.Parse(tok));
				}

				result.Terms.Add(term);
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

		private static int GetPrecedence(string strOp)
		{
			switch (strOp)
			{
				case "*":
				case "/":
				case "%":
					return 1;

				case "+":
				case "-":
					return 2;

				case "=":
				case "+=":
				case "-=":
				case "*=":
				case "/=":
				case "%=":
				case "&=":
				case "|=":
				case "^=":
				case "<<=":
				case ">>=":
					return 6;

				case ".":
				case "[]":
					return 0;		

				case ">":
				case ">=":
				case "<":
				case "<=":
					return 3;

				case "typeof":
				case "==":
				case "!=":
					return 4;

				case "&&":
				case "||":
				case "??":
					return 5;

				case "?":
				case ":":
					return 6;

				default:
					return 4;
			}

		}

		private static Expression GetSingleExpressionTerm(ProtoScript.Expression expression, Tokenizer tok, string strExpected)
		{
			if (expression == null || expression.Terms == null || expression.Terms.Count != 1 || expression.Terms[0] == null)
				throw new ProtoScriptParsingException(tok.getString(), tok.getCursor(), strExpected);

			return expression.Terms[0];
		}

		public static ProtoScript.Expression ParseTerm(Tokenizer tok)
		{
			var iStart = tok.getCursor();
			string strTok = tok.peekNextToken();

			switch (strTok)
			{
				case "null":
					tok.getNextToken();
					return new NullLiteral();

				case "new":					
					return ProtoScript.Parsers.NewObjectExpressions.Parse(tok);

					//*case "{":
					//return JsParser.ParseObjectLiteral(tok);

				case "[":
					return ProtoScript.Parsers.ArrayLiterals.Parse(tok);

				case "(":
					return ParseExpressionList(tok);


					//I don't think these are valid expression terms
				//case "continue":
				//	tok.getNextToken();
				//	return new ContinueStatement();


				//case "break":
				//	tok.getNextToken();
				//	return new BreakStatement();

				case "true":
					tok.getNextToken();
					return new BooleanLiteral(true);

				case "false":
					tok.getNextToken();
					return new BooleanLiteral(false);

				case "initialized":

					return ParseIsInitialized(tok);


				default:

					if (strTok.Length == 0)
						throw new ProtoScriptParsingException("Unable to parse term: unexpected end of input");

					if (strTok[0] == '\"')
					{
						return new StringLiteral(tok.getNextToken());
					}

					if (strTok[0] == '@')
					{
						return new AtPrefixedStringLiteral(tok.getNextToken());
					}

					if (strTok[0] == '$')
					{
						if (strTok.Length > 2 && strTok[1] == '$')
							return new PrototypeStringLiteral(tok.getNextToken());

						return ParseDollarPrefixedStringLiteral(tok);
					}

					if (strTok[0] == '\'')
					{
						return new CharacterLiteral(tok.getNextToken());
					}


					int iCursor = tok.getCursor();
					if (char.IsDigit(strTok[0]))
					{
						NumberLiteral? numLiteral = NumberLiterals.Parse(tok);
						if (null != numLiteral)
							return numLiteral;

						tok.setCursor(iCursor);
					}

					ProtoScript.MethodEvaluation method = null; 
					try
					{

						method = ProtoScript.Parsers.MethodEvaluations.Parse(tok);
					}
					catch 
					{
						method = null;
					}

					if (null != method)
						return method;

					tok.setCursor(iCursor);
					//N20220428-01
					ProtoScript.Identifier identifier = ProtoScript.Parsers.Identifiers.ParseAsIdentifier(tok);

					if (tok.peekNextToken() == "." || tok.peekNextToken() == "?.")
						return ParseDotOperators(tok, identifier);

//					ProtoScript.Identifier identifier = new ProtoScript.Identifier(ProtoScript.Parsers.Identifiers.ParseMultiple(tok));
					return identifier;
			}

			throw new ProtoScriptParsingException("Unable to parse term");
		}

static public Expression ParseDotOperators(Parsers.Tokenizer tok, Expression termLeft)
{
List<Expression> terms = new List<Expression>() { termLeft };
List<string> ops = new List<string>();

int iCursor = tok.getCursor();

do
{
string tokDot = tok.getNextToken();
if (tokDot != "." && tokDot != "?.")
throw new ProtoScriptParsingException(tok.getString(), tok.getCursor(), ". or ?.");
ops.Add(tokDot);

int iStart = tok.getCursor();
Identifier identifier = Identifiers.ParseAsIdentifier(tok);

if (tok.peekNextToken() == "(" || tok.peekNextToken() == "<")
{
tok.setCursor(iStart);
terms.Add(MethodEvaluations.Parse(tok));
}
else
{
terms.Add(identifier);
}

if (tok.peekNextToken() == "[")
{
tok.MustBeNext("[");
IndexOperator opIndex = new IndexOperator();
//Only allow one parameter (no [1, 3] )
opIndex.Right = ProtoScript.Parsers.Expressions.Parse(tok);
tok.MustBeNext("]");
terms.Add(opIndex);
}

}
while (tok.peekNextToken() == "." || tok.peekNextToken() == "?.");

                        BinaryOperator op = new BinaryOperator(ops[0]);
			op.Info.StartStatement(iCursor);
			op.Info.File = Files.CurrentFile;
			op.Info.StopStatement(tok.getCursor());
			
                        int iOp = 1;
                        for (int i = 0; i < terms.Count; i++)
			{
				Expression term = terms[i];

				if (term is IndexOperator)
				{
					IndexOperator opIndex = term as IndexOperator;
					opIndex.Info = op.Info;
					opIndex.Left = op;
					op = opIndex;
				}

				else if (op.Left == null)
					op.Left = term;

				else if (op.Right == null)
					op.Right = term;

				else
				{
BinaryOperator opNew = new BinaryOperator(ops[iOp++]);
					opNew.Info = op.Info;
					opNew.Left = op;
					opNew.Right = term;
					op = opNew;
				}
			}

			return op;
		}

		static public IsInitializedOperator ParseIsInitialized(Tokenizer tok)
		{
			IsInitializedOperator op = new IsInitializedOperator();
			op.Info.StartStatement(tok.getCursor());
			op.Info.File = Files.CurrentFile;
			tok.getNextToken();
			tok.MustBeNext("(");
			op.Right = Parse(tok);
			tok.MustBeNext(")");
			op.Info.StopStatement(tok.getCursor());
			return op;
		}

		static public DollarPrefixedStringLiteral ParseDollarPrefixedStringLiteral(Tokenizer tok)
		{
			DollarPrefixedStringLiteral literal = new DollarPrefixedStringLiteral();
			literal.Expression = new Expression();

			StringBuilder sb = new StringBuilder();

			tok.movePastWhitespace();

			tok.MustBeNextChar('$');
			tok.MustBeNextChar('"');

			sb.Append("$");
			sb.Append("\"");

			int i = 0;
			while (tok.hasMoreChars())
			{
				char c = tok.getNextChar();
				sb.Append(c);

				if (c == '\\')
				{
					sb.Append(tok.getNextChar());
				}

				if (c == '"')
				{
					break;					
				}

				if (c == '{')
				{
					Expression expression = Parse(tok);
					tok.MustBeNext("}");
					sb.Append(i++).Append("}");
					literal.Expression.Terms.Add(expression);
				}
			}

			literal.Value = sb.ToString();
			return literal;
		}

		static public ProtoScript.ExpressionList ParseExpressionList(Tokenizer tok)
		{
			ProtoScript.ExpressionList result = new ProtoScript.ExpressionList();

			tok.MustBeNext("(");

			try
			{
				if (")" != tok.peekNextToken())
					result.Expressions.Add(ProtoScript.Parsers.Expressions.Parse(tok));

				while (tok.hasMoreTokens() && tok.peekNextToken() != ")")
				{
					tok.MustBeNext(",");

					result.Expressions.Add(ProtoScript.Parsers.Expressions.Parse(tok));
				}

				tok.MustBeNext(")");
			}
			catch (Exception err)
			{
				if (!Settings.BestCaseExpressions)
throw;

				result.Info.IsIncomplete = true; 
			}

			return result;
		}

		static public CastingOperator ParseCastingOperator(Tokenizer tok)
		{
			CastingOperator op = new CastingOperator();
			tok.MustBeNext("(");
			op.Type = ProtoScript.Parsers.Types.Parse(tok);
			if (!tok.CouldBeNext(")"))
				op = null; 

			return op;
		}

	}
}
