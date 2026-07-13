namespace ProtoScript.Parsers
{
	public class ClassDefinitions
	{
		static public ProtoScript.ClassDefinition Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		static public ProtoScript.ClassDefinition Parse(Tokenizer tok)
		{
			ProtoScript.ClassDefinition result = new ProtoScript.ClassDefinition();

			tok.movePastWhitespace(); result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;


			ModifiersState(tok, result);

			if (tok.peekNextToken() == "enum")
				throw new LookAheadException("enum");

			tok.MustBeNext("class");

			result.ClassName = ProtoScript.Parsers.Types.Parse(tok);

			InheritanceState(tok, result);

			if (tok.CouldBeNext("where"))		//ignroe the where clause for now
				tok.moveTo("{"); 

			tok.MustBeNext("{");

			while (!tok.CouldBeNext("}"))
			{
				//Ignore annotations for not
				if (tok.peekNextToken() == "[")
				{
					tok.MovePastToken("]");
					continue;
				}

				int iCursor = tok.getCursor();

				List<ProtoScript.Statement> lstStatements = ProtoScript.Parsers.MethodDefinitions.ParseBestCase(tok);
				if (lstStatements.Count != 1)
					throw new Exception("Unexpected");

				ProtoScript.Statement statement = lstStatements.First();

				if (statement is ProtoScript.MethodDefinition)
				{
					result.Methods.Add(statement as ProtoScript.MethodDefinition);
				}
				else if (statement is ProtoScript.FieldDefinition)
				{
					result.Fields.Add(statement as ProtoScript.FieldDefinition);
				}
				else if (statement is ProtoScript.PropertyDefinition)
				{
					result.Properties.Add(statement as ProtoScript.PropertyDefinition);
				}
				else if (statement is ProtoScript.EnumDefinition)
				{
					result.Enums.Add(statement as ProtoScript.EnumDefinition);
				}
				else if (statement is ProtoScript.ClassDefinition)
				{
					result.Classes.Add(statement as ClassDefinition);
				}
				else
					throw new Exception("Unexpected");
			}
			
			result.Info.StopStatement(tok.getCursor());
			return result;

		}

		static public ProtoScript.ClassDefinition ParseWithLookahead(Tokenizer tok)
		{
			ProtoScript.ClassDefinition result = new ProtoScript.ClassDefinition();

			tok.movePastWhitespace();
			result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			ModifiersState(tok, result);

			if (tok.peekNextToken() == "enum")
				throw new LookAheadException("enum");

			tok.MustBeNext("class");

			result.ClassName = ProtoScript.Parsers.Types.Parse(tok);

			InheritanceState(tok, result);

			if (tok.CouldBeNext("where"))       //ignroe the where clause for now
				tok.moveTo("{");

			tok.MustBeNext("{");

			while (!tok.CouldBeNext("}"))
			{
				//Ignore annotations for not
				if (tok.peekNextToken() == "[")
				{
					tok.MovePastToken("]");
					continue;
				}

				int iCursor = tok.getCursor();
				string strLookAhead = string.Empty;
				try
				{
					result.Methods.Add(ProtoScript.Parsers.MethodDefinitions.Parse(tok));
					continue;
				}
				catch (LookAheadException err)
				{
					strLookAhead = err.Message;
					tok.setCursor(iCursor);
				}

				if (strLookAhead == "field")
				{
					ProtoScript.FieldDefinition field = ProtoScript.Parsers.FieldDefinitions.Parse(tok);
					result.Fields.Add(field);
				}
				else if (strLookAhead == "property")
				{
					ProtoScript.PropertyDefinition prop = ProtoScript.Parsers.PropertyDefinitions.Parse(tok);
					result.Properties.Add(prop);
				}
				else if (strLookAhead == "enum")
				{
					result.Enums.Add(ProtoScript.Parsers.EnumDefinitions.Parse(tok));
				}
				else if (strLookAhead == "class")
				{
					result.Classes.Add(ProtoScript.Parsers.ClassDefinitions.Parse(tok));
				}
			}

			result.Info.StopStatement(tok.getCursor());
			return result;

		}

		static void ModifiersState(Tokenizer tok, ProtoScript.ClassDefinition result)
		{
			ProtoScript.Modifiers modifiers = ProtoScript.Parsers.Modifiers.Parse(tok);
			result.SetModifiers(modifiers);
		}

		static void InheritanceState(Tokenizer tok, ProtoScript.ClassDefinition result)
		{
			if (tok.CouldBeNext(":"))
			{
				do
				{
					ProtoScript.Type type = ProtoScript.Parsers.Types.Parse(tok);
					result.Inherits.Add(type);
				}
				while (tok.CouldBeNext(","));
			}
		}
	}
}
