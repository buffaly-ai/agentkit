using System.Text;

namespace ProtoScript
{
	public class FunctionDefinition : Statement
	{
		public Visibility Visibility;
		public bool IsStatic;
		public bool IsOverride;
		public bool IsConstructor;
		public bool IsNew;
		public bool IsDelegate;
		public bool IsAbstract;

		public ProtoScript.Type ReturnType;
		public string FunctionName;
		public List<ParameterDeclaration> Parameters = new List<ParameterDeclaration>();
		public CodeBlock Statements = new CodeBlock();
		public Expression BaseConstructor;
		public List<AnnotationExpression> Annotations = new List<AnnotationExpression>();
		public ProtoScript.FunctionDefinition Clone()
		{
			ProtoScript.FunctionDefinition copy = (FunctionDefinition) this.MemberwiseClone();
			return copy;
		}

		public override IEnumerable<Statement> GetChildrenStatements()
		{
			foreach (ParameterDeclaration param in Parameters)
			{
				yield return param;
			}

			if (null != Statements)
				yield return new CodeBlockStatement(Statements);
		}

		public void SetModifiers(Modifiers modifiers)
		{
			this.IsNew = modifiers.IsNew;
			this.IsStatic = modifiers.IsStatic;
			this.Visibility = modifiers.Visibility;
			this.IsOverride = modifiers.IsOverride;
			this.IsDelegate = modifiers.IsDelegate;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder("ProtoScript.FunctionDefinition[");
			if (IsStatic)
				sb.Append("static ");

			sb.Append(Visibility.ToString().ToLower()).Append(" ");

			if (null != ReturnType)
				sb.Append(ReturnType.ToString());

			sb.Append(" ").Append(FunctionName).Append("(...)");
			sb.Append("]");
			return sb.ToString();
		}
	}
}
