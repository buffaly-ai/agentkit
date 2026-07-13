using System.Text;

namespace ProtoScript
{
	public class MethodDefinition : Statement
	{
		public Visibility Visibility;
		public bool IsStatic;
		public bool IsOverride;
		public bool IsConstructor;
		public bool IsNew;
		public bool IsDelegate;

		public ProtoScript.Type ReturnType;
		public string MethodName;
		public List<ParameterDeclaration> Parameters = new List<ParameterDeclaration>();
		public CodeBlock Statements;
		public Expression BaseConstructor;

		public ProtoScript.MethodDefinition Clone()
		{
			ProtoScript.MethodDefinition copy = (MethodDefinition) this.MemberwiseClone();
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
			StringBuilder sb = new StringBuilder("ProtoScript.MethodDefinition[");
			if (IsStatic)
				sb.Append("static ");

			sb.Append(Visibility.ToString().ToLower()).Append(" ");

			if (null != ReturnType)
				sb.Append(ReturnType.ToString());

			sb.Append(" ").Append(MethodName).Append("(...)");
			sb.Append("]");
			return sb.ToString();
		}
	}
}
