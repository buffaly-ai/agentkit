using BasicUtilities;
using System.Text;

namespace ProtoScript.Interpretter
{
	[Serializable]
	public class GenerateFailedException : Exception
	{
		public GenerateFailedException() { }
		public GenerateFailedException(string message) : base(message) { }
		public GenerateFailedException(string message, Exception inner) : base(message, inner) { }
		protected GenerateFailedException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}

	public class CSharpGenerator
	{
		int m_iTabs = 0;
		public StringBuilder m_sb = new StringBuilder();

		static public string Generate(ProtoScript.File file)
		{
			CSharpGenerator generator = new CSharpGenerator();

			StringBuilder sb = new StringBuilder();
			foreach (PrototypeDefinition protoDef in file.PrototypeDefinitions)
			{
				generator.DeclarePrototype(protoDef);
			}

			foreach (PrototypeDefinition protoDef in file.PrototypeDefinitions)
			{
				generator.DefinePrototype(protoDef);
			}

			foreach (Statement statement in file.Statements.Where(x => x is FunctionDefinition))
			{
				generator.DefineFunction(statement as FunctionDefinition);
			}

			foreach (Statement statement in file.Statements.Where(x => !(x is FunctionDefinition)))
			{
				generator.ToString(statement);
			}


			return generator.m_sb.ToString();
		}

		static public string Generate(Expression oExpression)
		{
			CSharpGenerator g = new CSharpGenerator();
			if (oExpression is MethodEvaluation)
				g.ToString(oExpression as MethodEvaluation);
			else if (oExpression is Literal)
				g.ToString(oExpression as Literal);
			else if (oExpression is NewObjectExpression)
				g.ToString(oExpression as NewObjectExpression);
			else
				g.ToString(oExpression);
			return g.m_sb.ToString();
		}

		static public string Generate(Statement statement)
		{
			CSharpGenerator g = new CSharpGenerator();
			g.ToString(statement);
			return g.m_sb.ToString();
		}

		static public string Generate(CodeBlock statements, bool bNaked)
		{
			CSharpGenerator g = new CSharpGenerator();
			g.ToString(statements, bNaked);
			return g.m_sb.ToString();
		}

		static public string Generate(ProtoScript.Type type)
		{
			CSharpGenerator g = new CSharpGenerator();
			g.ToString(type);
			return g.m_sb.ToString();
		}


		public void DeclarePrototype(PrototypeDefinition protoDef)
		{
			string strSymbolName = "proto" + protoDef.PrototypeName.TypeName;

			//Prototype protoContainer = NewPrototype("Container");
			m_sb.AppendLine($"Prototype {strSymbolName} = NewPrototype(\"{protoDef.PrototypeName.TypeName}\");");
		}

		public void DefinePrototype(PrototypeDefinition protoDef)
		{
			string strSymbolName = "proto" + protoDef.PrototypeName.TypeName;

			foreach (ProtoScript.Type typeOf in protoDef.Inherits)
			{
				//TypeOfs.Insert(protoRoom, ResolvePrototype("Container"));
				m_sb.AppendLine($"TypeOfs.Insert({strSymbolName}, ResolvePrototype(\"{typeOf.TypeName}\"));");
			}

			foreach (FieldDefinition fieldDefinition in protoDef.Fields)
			{
				//NewPrototypeProperty(protoPhysicalObject, "RelativePosition", ResolvePrototype("RelativePosition"));
				m_sb.AppendLine($"NewPrototypeProperty({strSymbolName}, \"{fieldDefinition.FieldName}\", ResolvePrototype(\"{fieldDefinition.Type.TypeName}\"));");
			}
		}

		public void DefineFunction(FunctionDefinition funcDef)
		{
			string strReturnType = funcDef.ReturnType.TypeName == "void" ? "void" : "Prototype";

			m_sb.Append("public static ").Append(strReturnType).Append(" ").Append(funcDef.FunctionName).Append("(");

			bool bFirst = true;
			foreach (ParameterDeclaration paramDeclaration in funcDef.Parameters)
			{
				if (bFirst)
					bFirst = false;
				else
					m_sb.Append(", ");

				m_sb.Append("Prototype ").Append(paramDeclaration.ParameterName);
			}

			m_sb.AppendLine(")").AppendLine("{");

			m_iTabs++;

			foreach (ParameterDeclaration paramDeclaration in funcDef.Parameters)
			{
				//CheckTypeOf(obj, "PhysicalObject");
				WriteStart();
				m_sb.AppendLine($"CheckTypeOf({paramDeclaration.ParameterName}, \"{paramDeclaration.Type.TypeName}\");");
			}

			foreach (Statement statement in funcDef.Statements)
			{
				ToString(statement);
			}

			m_iTabs--;

			m_sb.AppendLine("}");
		}

		public void DeclarePrototype(VariableDeclaration variableDeclaration)
		{
			//Prototype protoRoom1 = NewInstance("Room");
			m_sb.AppendLine($"Prototype {variableDeclaration.VariableName} = NewInstance(\"{variableDeclaration.Type.TypeName}\");");
		}

		void WriteStart()
		{
			m_sb.Append(new string('\t', m_iTabs));
		}

		void WriteStart(string str)
		{
			WriteStart();
			Write(str);
		}

		void WriteStop()
		{
			m_sb.AppendLine();
		}

		void WriteStop(string str)
		{
			m_sb.AppendLine(str);
		}

		void WriteStatementStop()
		{
			m_sb.AppendLine(";");
		}

		void WriteLine(string str)
		{
			WriteStart();
			Write(str);
			WriteStop();
		}

		void Write(string str)
		{
			m_sb.Append(str);
		}

		void ToString(CaseStatement oCase)
		{
			WriteStart("case ");
			ToString(oCase.Value);
			WriteStop(":");
		}

		void ToString(ArrayLiteral oArray)
		{
			Write("{");

			for (var i = 0; i < oArray.Values.Count; i++)
			{
				if (i != 0)
					Write(", ");

				ToString(oArray.Values[i]);
			}

			Write("}");
		}


		void ToString(BinaryOperator op)
		{
			if (op.Value == "=") //Assignment operator 
			{
				if (!(op.Left is Identifier))
					throw new NotSupportedException($"Assignment target must be an identifier. Found '{op.Left?.GetType().FullName ?? "null"}'.");

				AssignmentExpressionToString(op.Left as Identifier, op.Right);
			}
			else if (op.Value == "." || op.Value == "?.")
				Write(op.Value);
			else
				Write(" " + op.Value + " ");
		}

		void AssignmentExpressionToString(Identifier left, Expression right)
		{
			string strResolved = left.Value;
			string strProperty = null;

			if (left.Value.Contains("."))
			{
				string[] strSplits = StringUtil.Split(left.Value, ".");

				string strObject = strSplits[0];

				for (int i = 1; i < strSplits.Length - 1; i++)
				{
					//ResolveProperty(prototype, strPropertyName);
					strObject = $"ResolveProperty({strObject}, \"{strSplits[i]}\")";
				}

				strResolved = strObject;
				strProperty = strSplits.Last();
			}

			m_sb.Append($"SetProperty({strResolved}, \"{strProperty}\",");
			ToString(right);
			m_sb.Append(")");
		}


		void ToString(BreakStatement statement)
		{
			WriteLine("break;");
		}

        void ToString(CastingOperator expression)
        {
            Write("(");
            ToString(expression.Type);
            Write(")");
        }

		void ToString(ContinueStatement statement)
		{
			WriteLine("continue;");
		}

		//void ToString(ClassDefinition cls)
		//{
		//	WriteStart();

		//	if (cls.IsStatic)
		//		Write("static ");

		//	ToString(cls.Visibility);

		//	if (cls.IsPartial)
		//		Write(" partial ");

		//	Write(" class ");
		//	ToString(cls.ClassName);

		//	if (cls.Inherits.Count > 0)
		//	{
		//		Write(" : ");

		//		for (int i = 0; i < cls.Inherits.Count; i++)
		//		{
		//			if (i > 0)
		//				Write(", ");
		//			ToString(cls.Inherits[i]);
		//		}
		//	}

		//	WriteStop();

		//	WriteLine("{");

		//	m_iTabs++;

		//	foreach (var en in cls.Enums)
		//		ToString(en);

		//	foreach (var field in cls.Fields)
		//		ToString(field);

		//	foreach (var prop in cls.Properties)
		//		ToString(prop);

		//	foreach (var method in cls.Methods)
		//		ToString(method);

		//	m_iTabs--;
		//	WriteLine("}");
		//}

		void ToString(CodeBlock oStatements, bool bNaked = false)
		{
			if (!bNaked)
			{
				WriteLine("{");
				m_iTabs++;
			}

			for (var i = 0; i < oStatements.Count; i++)
			{
				ToString(oStatements[i]);
			}

			if (!bNaked)
			{
				m_iTabs--;
				WriteLine("}");
			}
		}

		void ToString(DefaultStatement statement)
		{
			WriteLine("default:");
		}

		void ToString(DoStatement oDo)
		{
			WriteLine("do");
			ToString(oDo.Statements);
			WriteStart("while (");
			ToString(oDo.Expression);
			WriteStop(");");
		}

		void ToString(EnumDefinition en)
		{
			WriteStart("enum ");
			Write(ToString(en.EnumName));
			Write(" {");
			for (int i = 0; i < en.EnumTypes.Count; i++)
			{
				if (i > 0)
					Write(", ");

				Write(ToString(en.EnumTypes[i]));
			}
			WriteStop("}");
		}



		void ToString(Expression oExpression)
		{
			if (null == oExpression)
				Write("_");

			else if (null != oExpression.Terms)
			{
				for (var i = 0; i < oExpression.Terms.Count; i++)
				{
					TermToString(oExpression.Terms[i]);
				}
			}
		}

		void ToString(ExpressionList oExpression)
		{
			Write("(");
			for (var i = 0; i < oExpression.Expressions.Count; i++)
			{
				if (i != 0)
					Write(", ");

				ToString(oExpression.Expressions[i]);
			}
			Write(")");
		}

		void ToString(ExpressionStatement expression)
		{
			WriteStart();
			ToString(expression.Expression);
			WriteStatementStop();
		}

		void ToString(FieldDefinition field)
		{
			WriteStart();
			if (field.IsStatic)
				Write("static ");
			if (field.IsReadonly)
				Write("readonly ");
			ToString(field.Visibility);

			if (field.IsConst)
				Write(" const ");

			Write(" ");

			ToString(field.Type);

			Write(" " + ToString(field.FieldName)); 

			if (null != field.Initializer)
			{
				Write(" = ");
				ToString(field.Initializer);
			}

			WriteStop(";");
		}

		void ToString(Identifier identifier)
		{
			if (identifier.Value.Contains("."))
			{
				string[] strSplits = StringUtil.Split(identifier.Value, ".");

				string strObject = strSplits[0];

				for (int i = 1; i < strSplits.Length; i++)
				{
					//ResolveProperty(prototype, strPropertyName);
					strObject = $"ResolveProperty({strObject}, \"{strSplits[i]}\")";
				}

				m_sb.Append(strObject);
			}
			else
				m_sb.Append(identifier.Value);

		//	Write(ToString(identifier.Value));
		}

		void ToString(IndexOperator op)
		{
			ToString(op.Left);
			Write("[");
			ToString(op.Right);
			Write("]");
		}


		void ToString(Literal literal)
		{
			if (StringUtil.IsEmpty(literal.Value))
			{
				if (literal is AtPrefixedStringLiteral)
					Write("@\"_\"");
				else if (literal is StringLiteral)
					Write("\"_\"");
				else
					Write("_");
			}
			else
				Write(literal.Value);
		}

		void ToString(Operator op)
		{
			if (op.Value == "?" || op.Value == ":")
				Write(" " + op.Value + " ");
			else if (op.Value == "out" || op.Value == "ref" || op.Value == "await")
				Write(" " + op.Value + " ");
			else 
				Write(op.Value ?? "#");
		}

		void TermToString(Expression expression)
		{
			if (expression is ArrayLiteral)
				ToString(expression as ArrayLiteral);

			else if (expression is Literal)
				ToString(expression as Literal);

			else if (expression is BinaryOperator)
				ToString(expression as BinaryOperator);

			else if (expression is IndexOperator)
				ToString(expression as IndexOperator);

			else if (expression is CastingOperator)
				ToString(expression as CastingOperator);

			else if (expression is Operator)
				ToString(expression as Operator);

			else if (expression is Identifier)
				ToString(expression as Identifier);

			else if (expression is ExpressionList)
				ToString(expression as ExpressionList);

			else if (expression is MethodEvaluation)
				ToString(expression as MethodEvaluation);

			else if (expression is NewObjectExpression)
				ToString(expression as NewObjectExpression);

			else if (expression is ProtoScript.Type)
				ToString(expression as ProtoScript.Type);

			else if (expression is CodeBlockExpression)
				ToString((expression as CodeBlockExpression).Statements);

			else
				ToString(expression);

		}


		void ToString(ProtoScript.Type type)
		{
			if (null == type)
				Write("_");

			else
			{
				Write(ToString(type.TypeName));
				if (type.IsNullable)
					Write("?");
				if (type.ElementTypes.Count > 0)
				{
					Write("<");
					for (int i = 0; i < type.ElementTypes.Count; i++)
					{
						if (i > 0)
							Write(", ");

						ToString(type.ElementTypes[i]);
					}

					Write(">");
				}

				if (type.IsArray)
				{
					if (type.ArraySize.Terms.Count == 0)
						Write("[]");
					else
					{
						foreach (Expression expression in type.ArraySize.Terms)
						{
							Write("[");
							if (null != expression)
								ToString(type.ArraySize);
							Write("]");
						}
					}
				}
			}
		}

		void ToString(VariableDeclaration oDeclaration, bool bNaked = false)
		{
			if (!bNaked)
				WriteStart();

			if (oDeclaration.IsConst)
				Write("const ");

			Write("Prototype");
			Write(" " + ToString(oDeclaration.VariableName));
			Write($" = NewInstance(\"{oDeclaration.Type.TypeName}\")");

			if (oDeclaration.Initializer != null)
			{
				Write(" = ");
				ToString(oDeclaration.Initializer);
			}

			if (oDeclaration.ChainedDeclarations != null)
			{
				foreach (VariableDeclaration chained in oDeclaration.ChainedDeclarations)
				{
					Write(", ");
					Write(ToString(chained.VariableName));
					if (chained.Initializer != null)
					{
						Write(" = ");
						ToString(chained.Initializer);
					}
				}
			}

			if (!bNaked)
				WriteStatementStop();
		}

		string ToString(string strValue)
		{
			return StringUtil.IsEmpty(strValue) ? "_" : strValue;
		}

		void ToString(Visibility visibility)
		{
			switch (visibility)
			{
				case Visibility.Internal:
					Write("internal");
					break;
				case Visibility.Public:
					Write("public");
					break;
				case Visibility.Private:
					Write("private");
					break;
				case Visibility.Protected:
					Write("protected");
					break;
				case Visibility.PrivateProtected:
					Write("private protected");
					break;
				case Visibility.ProtectedInternal:
					Write("protected internal");
					break;
			}
		}

		//void ToString(MethodDefinition oFunction)
		//{
		//	WriteStart();
		//	if (oFunction.IsNew)
		//		Write("new");

		//	if (oFunction.IsStatic)
		//		Write("static ");
		//	ToString(oFunction.Visibility);
		//	Write(" ");

		//	if (oFunction.IsOverride)
		//		Write("override ");

		//	if (!oFunction.IsConstructor)
		//		ToString(oFunction.ReturnType);

		//	Write(" " + ToString(oFunction.MethodName) + "(");

		//	for (var i = 0; i < oFunction.Parameters.Count; i++)
		//	{
		//		if (i != 0)
		//			Write(", ");

		//		ToString(oFunction.Parameters[i]);
		//	}
		//	Write(")");

		//	if (oFunction.IsConstructor && null != oFunction.BaseConstructor)
		//	{
		//		Write(":");
		//		ToString(oFunction.BaseConstructor);
		//	}

		//	WriteStop();

		//	ToString(oFunction.Statements);
		//}


		void ToString(MethodEvaluation oFunction)
		{
			if (oFunction.MethodName.Contains("."))
			{
				string[] strSplits = StringUtil.Split(oFunction.MethodName, ".");
				string strObject = strSplits[0];

				for (int i = 1; i < strSplits.Length - 1; i++)
				{
					//ResolveProperty(prototype, strPropertyName);
					strObject = $"ResolveProperty({strObject}, \"{strSplits[i]}\")";
				}

				if (strSplits.Last() == "Add")
					strObject += ".Children.Add";

				Write(ToString(strObject) + "(");
			}
			else
			{
				Write(ToString(oFunction.MethodName) + "(");
			}

			ToString(oFunction.Parameters);
			Write(")");
		}

		void ToString(ParameterDeclaration declaration)
		{
			if (declaration.IsThis)
				Write("this ");

			if (declaration.IsOut)
				Write("out ");
			else if (declaration.IsRef)
				Write("ref ");

			ToString(declaration.Type);
			Write(" ");
			Write(ToString(declaration.ParameterName));

			if (declaration.DefaultValue != null)
			{
				Write(" = ");
				ToString(declaration.DefaultValue);
			}
		}

		void ToString(PropertyDefinition prop)
		{
			WriteStart();

			if (prop.IsNew)
				Write("new ");

			if (prop.IsStatic)
				Write("static ");
			//if (field.IsReadonly)
			//	Write("readonly ");
			ToString(prop.Visibility);

			if (prop.IsOverride)
				Write(" override ");

			Write(" ");

			ToString(prop.Type);

			Write(" " + ToString(prop.PropertyName));

			if (null != prop.Indexer)
			{
				Write("[");
				ToString(prop.Indexer);
				Write("]");
			}

			WriteStop();
			WriteLine("{");
			m_iTabs++;

			if (null != prop.Getter)
			{
				WriteStart("get");
				if (prop.Getter.Count != 0)
				{
					WriteStop();
					ToString(prop.Getter);
				}
				else
					WriteStatementStop();
			}

			if (null != prop.Setter)
			{
				WriteStart("set");
				if (prop.Setter.Count != 0)
				{
					WriteStop();
					ToString(prop.Setter);
				}
				else
					WriteStatementStop();
			}

			m_iTabs--;
			WriteLine("}");


		}



		void ToString(List<Expression> lstExpressions)
		{
			for (var i = 0; i < lstExpressions.Count; i++)
			{
				if (i != 0)
					Write(", ");

				ToString(lstExpressions[i]);
			}
		}

		void ToString(ReturnStatement oReturn)
		{
			WriteStart("return ");
			if (null != oReturn.Expression)
				ToString(oReturn.Expression);
			WriteStatementStop();
		}

		void ToString(NamespaceDefinition ns)
		{
			WriteStart("namespace ");
			for (int i = 0; i < ns.Namespaces.Count; i++)
			{
				if (i > 0)
					Write(".");
				Write(ToString(ns.Namespaces[i]));
			}

			WriteStop();
			WriteLine("{");
			m_iTabs++;

			foreach (EnumDefinition en in ns.Enums)
			{
				ToString(en);
			}

			foreach (PrototypeDefinition cls in ns.PrototypeDefinitions)
			{
				ToString(cls);
			}

			m_iTabs--;
			WriteLine("}");
		}

		void ToString(NewObjectExpression oNew)
		{
			Write("new ");

			if (null == oNew.Type)
			{
				Write("_(");
				if (null != oNew.Parameters)
				{
					ToString(oNew.Parameters);
				}
				Write(")");
			}

			if (null != oNew.Type && oNew.Type.TypeName != "(anonymous)")
			{
				ToString(oNew.Type);
				if (oNew.Type.IsArray)
				{
					Write(" ");
					ToString(oNew.ArrayInitializer);
				}
				else
				{
					Write("(");
					ToString(oNew.Parameters);
					Write(")");
				}
			}


			for (int i = 0; null != oNew.Initializers && i < oNew.Initializers.Count; i++)
			{
				if (i != 0)
					Write(", ");
				else
					Write("{");

				ToString(oNew.Initializers[i]);

				if (i == oNew.Initializers.Count - 1)
					Write("}");
			}
		}

		void ToString(ThrowStatement oThrow)
		{
			WriteStart("throw ");
			if (null != oThrow.Expression)
				ToString(oThrow.Expression);
			WriteStatementStop();
		}

		void ToString(TryStatement oTry)
		{
			WriteLine("try");
			ToString(oTry.TryBlock);

			foreach (var oCatch in oTry.CatchBlocks)
			{
				WriteStart("catch");
				if (oCatch.Type != null)
				{
					Write("(");
					ToString(oCatch.Type);

					if (oCatch.ExceptionName != null)
					{
						Write(" " + oCatch.ExceptionName);
					}

					Write(")");
				}
				WriteStop();

				ToString(oCatch.Statements);
			}

			if (null != oTry.FinallyBlock)
			{
				WriteLine("finally");
				ToString(oTry.FinallyBlock);
			}
		}

		void ToString(IfStatement oIf)
		{
			WriteStart("if (");
			ToString(oIf.Condition);
			WriteStop(")");
			ToString(oIf.TrueBody);

			for (int i = 0; i < oIf.ElseIfBodies.Count; i++)
			{
				WriteStart("else if (");
				ToString(oIf.ElseIfConditions[i]);
				WriteStop(")");
				ToString(oIf.ElseIfBodies[i]);
			}

			if (null != oIf.ElseBody)
			{
				WriteLine("else");
				ToString(oIf.ElseBody);
			}
		}

		void ToString(ForStatement oFor)
		{
			WriteStart("for (");
			if (oFor.Start != null)
			{
				if (oFor.Start is VariableDeclaration)
					ToString(oFor.Start as VariableDeclaration, true);
				else if  (oFor.Start is ExpressionStatement)
					ToString((oFor.Start as ExpressionStatement).Expression);
			}
			Write("; ");
			if (oFor.Condition != null)
				ToString(oFor.Condition);
			Write("; ");
			if (oFor.Iteration != null)
			{
				for (int i = 0; i < oFor.Iteration.Expressions.Count; i++)
				{
					if (i > 0)
						Write(", ");

					ToString(oFor.Iteration.Expressions[i]);
				}
			}
			WriteStop(")");
			ToString(oFor.Statements);
		}

		void ToString(ForEachStatement oFor)
		{
			WriteStart("foreach (");
			ToString(oFor.Type);
			Write(" ");
			Write(oFor.IteratorName);
			Write(" in ");
			ToString(oFor.Expression);
			Write(")");
			WriteStop();
			ToString(oFor.Statements);
		}




		void ToString(SwitchStatement oSwitch)
		{
			WriteStart("switch (");
			ToString(oSwitch.Expression);
			WriteStop(")");
			ToString(oSwitch.Statements);
		}

	
		void ToString(WhileStatement oWhile)
		{
			WriteStart("while (");
			ToString(oWhile.Expression);
			WriteStop(")");
			ToString(oWhile.Statements);
		}

		void ToString(UsingStatement statement)
		{
			Write("using ");
			if (statement.IsStatic)
				Write("static ");
			Write(string.Join(".", statement.Namespaces));
			WriteStop(";");
		}

		void ToString(YieldStatement statement)
		{
			Write("yield ");
			if (statement.Expression == null)
				Write("break");
			else
			{
				Write("return ");

				ToString(statement.Expression);
			}

			WriteStop(";");
		}

		void ToString(Statement o)
		{

			if (o is VariableDeclaration)
				ToString(o as VariableDeclaration);

			else if (o is IfStatement)
				ToString(o as IfStatement);

			//else if (o is MethodDefinition)
			//	ToString(o as MethodDefinition);

			else if (o is ReturnStatement)
				ToString(o as ReturnStatement);

			else if (o is BreakStatement)
				ToString(o as BreakStatement);

			else if (o is ContinueStatement)
				ToString(o as ContinueStatement);

			else if (o is TryStatement)
				ToString(o as TryStatement);
			else if (o is ThrowStatement)
				ToString(o as ThrowStatement);
			else if (o is ForEachStatement)
				ToString(o as ForEachStatement);
			else if (o is ForStatement)
				ToString(o as ForStatement);
			else if (o is WhileStatement)
				ToString(o as WhileStatement);
			else if (o is DefaultStatement)
				ToString(o as DefaultStatement);
			else if (o is CaseStatement)
				ToString(o as CaseStatement);
			else if (o is SwitchStatement)
				ToString(o as SwitchStatement);

			else if (o is DoStatement)
				ToString(o as DoStatement);

			else if (o is ExpressionStatement)
				ToString(o as ExpressionStatement);

			else if (o is CodeBlockStatement)
				ToString((o as CodeBlockStatement).Statements);

			else if (o is YieldStatement)
				ToString((o as YieldStatement));

			else if (o is PropertyDefinition)
				ToString(o as PropertyDefinition);

			else if (o is FieldDefinition)
				ToString(o as FieldDefinition);

			else if (o is ParameterDeclaration)
				ToString(o as ParameterDeclaration);

			//else if (o is ClassDefinition)
			//	ToString(o as ClassDefinition);

			else if (o is NamespaceDefinition)
				ToString(o as NamespaceDefinition);

			else if (o is UsingStatement)
				ToString(o as UsingStatement);

			else if (o is Statement)        //From a shadow
				WriteLine("_");

			else
				throw new GenerateFailedException("Could not process object: " + (o == null ? "(null)" : TypeUtil.GetTypeName(o.GetType())));

		}
	}


}
