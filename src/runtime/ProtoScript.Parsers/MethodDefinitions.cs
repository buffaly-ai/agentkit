namespace ProtoScript.Parsers
{
	public class MethodDefinitions
	{
		static public ProtoScript.MethodDefinition Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		static public ProtoScript.MethodDefinition Parse(Tokenizer tok)
		{
			ProtoScript.MethodDefinition result = new ProtoScript.MethodDefinition();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			ModifiersState(tok, result);

			if (tok.peekNextToken() == "const")
				throw new LookAheadException("field");

			if (tok.peekNextToken() == "volatile")
				throw new LookAheadException("field");

			if (tok.peekNextToken() == "enum")
				throw new LookAheadException("enum");

			if (tok.peekNextToken() == "class")
				throw new LookAheadException("class");

			result.ReturnType = ProtoScript.Parsers.Types.Parse(tok);

			//Constructor
			if (tok.peekNextToken() == "(")
			{
				result.MethodName = result.ReturnType.TypeName;
				result.IsConstructor = true;
			}
			else
			{
				if (tok.peekNextToken() == "~")
				{
					result.MethodName = tok.getNextToken() + ProtoScript.Parsers.Identifiers.ParseMultiple(tok);
				}
				else
				{
					//Interfaces definitions can have multiple: IEnumerable.GetEnumerator
					result.MethodName = ProtoScript.Parsers.Identifiers.ParseMultiple(tok);
				}

				if (result.MethodName == "operator")
					result.MethodName += tok.getNextToken();
			}

			if (tok.peekNextToken() == ";" || tok.peekNextToken() == "=")
				throw new LookAheadException("field");

			if (tok.peekNextToken() == "{" || tok.peekNextToken() == "[")
				throw new LookAheadException("property");

			tok.MustBeNext("(");

			result.Parameters = ProtoScript.Parsers.MethodDefinitions.ParseParameterList(tok);

			tok.MustBeNext(")");

			if (result.IsConstructor)
			{
				if (tok.CouldBeNext(":"))
					result.BaseConstructor = ProtoScript.Parsers.Expressions.Parse(tok);

			}

			if (tok.CouldBeNext("where"))
				tok.moveTo("{");

			result.Statements = ProtoScript.Parsers.CodeBlocks.Parse(tok);

			result.Info.StopStatement(tok.getCursor());

			return result;
		}

		static public void ModifiersState(Tokenizer tok, ProtoScript.MethodDefinition result)
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

		static public List<ProtoScript.Statement> ParseBestCase(string strStatement)
		{
			ProtoScript.Parsers.Settings.BestCaseExpressions = true;
			try
			{
				ProtoScript.Parsers.Tokenizer tok = new ProtoScript.Parsers.Tokenizer(strStatement);
				return ParseBestCase(tok);
			}
			finally
			{
				ProtoScript.Parsers.Settings.BestCaseExpressions = false;
			}
		}

		static public List<ProtoScript.Statement> ParseBestCase(ProtoScript.Parsers.Tokenizer tok)
		{
			List<Statement> lstStatements = new List<Statement>();
			ProtoScript.MethodDefinition method = new ProtoScript.MethodDefinition();
			ProtoScript.PropertyDefinition property = new ProtoScript.PropertyDefinition();
			ProtoScript.FieldDefinition field = new ProtoScript.FieldDefinition();

			tok.movePastWhitespace();
			method.Info.StartStatement(tok.getCursor());
			method.Info.File = Files.CurrentFile;

			try
			{
				ProtoScript.Modifiers modifiers = ProtoScript.Parsers.Modifiers.Parse(tok);

				if (modifiers.IsConst || modifiers.IsViolatile)
				{
					tok.setCursor(method.Info.StartingOffset);
					field = ProtoScript.Parsers.FieldDefinitions.Parse(tok);
					lstStatements.Add(field);
					return lstStatements;
				}

				string strToken = tok.peekNextToken();

				if (strToken == "enum")
				{
					tok.setCursor(method.Info.StartingOffset);
					EnumDefinition enumDef = EnumDefinitions.Parse(tok);
					lstStatements.Add(enumDef);
					return lstStatements;
				}

				if (strToken == "class")
				{
					tok.setCursor(method.Info.StartingOffset);
					ClassDefinition clsDef = ClassDefinitions.Parse(tok);
					lstStatements.Add(clsDef);
					return lstStatements;
				}

				field.SetModifiers(modifiers);
				property.SetModifiers(modifiers);
				method.SetModifiers(modifiers);

				method.ReturnType = ProtoScript.Parsers.Types.Parse(tok);
				field.Type = method.ReturnType;
				property.Type = method.ReturnType;

				//Constructor
				if (tok.peekNextToken() == "(")
				{
					method.MethodName = method.ReturnType.TypeName;
					method.IsConstructor = true;
				}
				else
				{
					if (tok.peekNextToken() == "~")
					{
						method.MethodName = tok.getNextToken() + ProtoScript.Parsers.Identifiers.ParseMultiple(tok);
					}
					else
					{
						//Interfaces definitions can have multiple: IEnumerable.GetEnumerator
						method.MethodName = ProtoScript.Parsers.Identifiers.ParseMultiple(tok);
					}

					if (method.MethodName == "operator")
						method.MethodName += tok.getNextToken();
				}

				field.FieldName = method.MethodName;
				property.PropertyName = method.MethodName;

				strToken = tok.peekNextToken();

				if (strToken == ";" || strToken == "=")
				{
					tok.setCursor(method.Info.StartingOffset);

					method = null;
					property = null;

					field = ProtoScript.Parsers.FieldDefinitions.Parse(tok);
					lstStatements.Add(field);
					return lstStatements;
				}

				if (strToken == "{" || strToken == "[")
				{
					tok.setCursor(method.Info.StartingOffset);

					method = null;
					field = null;

					property = ProtoScript.Parsers.PropertyDefinitions.Parse(tok);
					lstStatements.Add(property);
					return lstStatements;
				}

				tok.MustBeNext("(");

				field = null;
				property = null;

				method.Parameters = ProtoScript.Parsers.MethodDefinitions.ParseParameterList(tok);

				tok.MustBeNext(")");

				if (method.IsConstructor)
				{
					if (tok.CouldBeNext(":"))
						method.BaseConstructor = ProtoScript.Parsers.Expressions.Parse(tok);

				}

				if (tok.CouldBeNext("where"))
					tok.moveTo("{");

				if (modifiers.IsDelegate)
				{
					tok.MustBeNext(";");
				}
				else
					method.Statements = ProtoScript.Parsers.CodeBlocks.Parse(tok);
			}
			catch (Exception err)
			{
				if (ProtoScript.Parsers.Settings.BestCaseExpressions)
				{
					if (null != method)
						lstStatements.Add(method);

					if (null != field)
						lstStatements.Add(field);

					if (null != property)
						lstStatements.Add(property);

					return lstStatements;
				}

throw;
			}

			method.Info.StopStatement(tok.getCursor());

			lstStatements.Add(method);
			return lstStatements;
		}
	}
}
