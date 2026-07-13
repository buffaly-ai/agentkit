//added
using BasicUtilities;
using ProtoScript.Interpretter.Compiled;
using ProtoScript.Interpretter.RuntimeInfo;
using Ontology.Simulation;

namespace ProtoScript.Interpretter.Compiling
{
	public class PrototypeInitializerCompiler
	{
		static public int OptimzedInitializerCount = 0;
		static public int TotalInitializerCount = 0;
		public static List<Compiled.Statement> Compile(PrototypeInitializer statement,
													   PrototypeTypeInfo infoThis,
													   Compiler compiler)
		{
			// Pre-allocate list to the exact size
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>(statement.Statements.Count);

			int iThisIndex = infoThis.Index;        // local alias (micro-opt)

			foreach (Statement initializer in statement.Statements)
			{
				ExpressionStatement expressionStatement = initializer as ExpressionStatement;
				if (expressionStatement == null)
				{
					compiler.AddDiagnostic("Initializer must be an expression statement", initializer, null);
					return null;
				}

				BinaryOperator op = expressionStatement.Expression as BinaryOperator
									?? expressionStatement.Expression.Terms[0] as BinaryOperator;

				if (op == null || op.Value != "=")
				{
					compiler.AddDiagnostic("Initializer should be an assignment statement", initializer, null);
					return null;
				}

				Identifier identifier = op.Left as Identifier;
				if (identifier == null)
				{
					compiler.AddDiagnostic("Initializer should be a simple identifier", null, op);
					return null;
				}

				string strPropertyName = identifier.Value;
				FieldTypeInfo fieldTypeInfo = compiler.GetFieldInfo(infoThis, strPropertyName);

				TotalInitializerCount++;

				if (null != fieldTypeInfo && op.Right is StringLiteral litString)
				{
					//Optimization
					infoThis.Prototype.Properties[fieldTypeInfo.Prototype.PrototypeID] = StringWrapper.ToPrototype(StringUtil.Between(litString.Value, "\"", "\""));
					OptimzedInitializerCount++;
					continue;
				}


				Compiled.Expression objCur = new GetGlobalStack
				{
					Index = iThisIndex,
					InferredType = infoThis
				};

				// Compile RHS once, reuse
				Compiled.Expression rhsCompiled = compiler.Compile(op.Right);

				Compiled.Expression lhs;

				if (fieldTypeInfo != null)
				{
					lhs = new PrototypeFieldReference
					{
						Left = objCur,
						Right = new GetGlobalStack
						{
							Index = fieldTypeInfo.Index,
							InferredType = fieldTypeInfo.FieldInfo
						},
						InferredType = fieldTypeInfo.FieldInfo
					};

					FieldTypeInfo fieldTypeInfo2 = fieldTypeInfo.Clone() as FieldTypeInfo;
					fieldTypeInfo2.Initializer = rhsCompiled;
					infoThis.Scope.InsertSymbol(strPropertyName, fieldTypeInfo2);
				}
				else
				{
					lhs = Compiler.GetDotNetMemberReference(op.Info, strPropertyName, objCur);

					if (lhs == null)
					{
						compiler.AddDiagnostic(BuildUnresolvedInitializerFieldDiagnostic(strPropertyName, infoThis, initializer), initializer, null);
						return null;
					}
				}

				Compiled.ExpressionStatement compiledStatement = new Compiled.ExpressionStatement
				{
					Expression = new AssignmentOperator
					{
						Info = initializer.Info,
						Left = lhs,
						Right = rhsCompiled
					}
				};

				lstStatements.Add(compiledStatement);
			}

			return lstStatements;
		}


		public static List<Compiled.Statement> Compile2(PrototypeInitializer statement, PrototypeTypeInfo infoThis, Compiler compiler)
		{
			List<Compiled.Statement> lstStatements = new List<Compiled.Statement>();

			//This version does lazy initialization
			foreach (Statement initializer in statement.Statements)
			{
				ExpressionStatement expressionStatement = initializer as ExpressionStatement;
				BinaryOperator op = expressionStatement.Expression.Terms[0] as BinaryOperator;
				if (null == op || op.Value != "=")
				{
					compiler.AddDiagnostic("Initializer should be an assignment statement", initializer, null);
					return null;
				}

				string strPropertyName = (op.Left as Identifier).Value;
				FieldTypeInfo fieldTypeInfo = compiler.GetFieldInfo(infoThis, strPropertyName);
				if (null == fieldTypeInfo)
				{
					compiler.AddDiagnostic(BuildUnresolvedInitializerFieldDiagnostic(strPropertyName, infoThis, initializer), initializer, null);
					return null;
				}

				FieldTypeInfo fieldTypeInfo2 = fieldTypeInfo.Clone() as FieldTypeInfo;
				fieldTypeInfo2.Initializer = compiler.Compile(op.Right);
				infoThis.Scope.InsertSymbol(strPropertyName, fieldTypeInfo2);
			}

			return lstStatements;
		}

		private static string BuildUnresolvedInitializerFieldDiagnostic(string strPropertyName, PrototypeTypeInfo infoThis, Statement initializer)
		{
			string prototypeName = infoThis.Prototype?.PrototypeName ?? "unknown";
			string parentName = infoThis.PrimaryParent?.PrototypeName ?? "unknown";
			string fileName = string.IsNullOrWhiteSpace(initializer.Info?.File) ? "unknown" : initializer.Info.File;
			return "Could not resolve field: " + strPropertyName
				+ " on prototype " + prototypeName
				+ " with primary parent " + parentName
				+ " in " + fileName
				+ ". Check whether the field exists on the prototype inheritance chain or should be represented as an annotation.";
		}
	}
}
