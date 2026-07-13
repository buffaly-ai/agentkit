using System.Text;

namespace ProtoScript
{
	public class PropertyDefinition : Statement
	{
		public ProtoScript.Type Type;
		public Visibility Visibility;
		public string PropertyName;
		public CodeBlock Getter = null;
		public CodeBlock Setter = null;
		public bool IsStatic;
		public bool IsOverride;
		public bool IsNew;
		public ParameterDeclaration Indexer = null;

		public void SetModifiers(Modifiers modifiers)
		{
			this.IsNew = modifiers.IsNew;
			this.IsStatic = modifiers.IsStatic;
			this.Visibility = modifiers.Visibility;
			this.IsOverride = modifiers.IsOverride;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder("ProtoScript.PropertyDefinition[");
			if (IsStatic)
				sb.Append("static ");

			sb.Append(Visibility.ToString().ToLower()).Append(" ");

			if (null != Type)
				sb.Append(Type.ToString());

			sb.Append(" ").Append(PropertyName);
			sb.Append("]");
			return sb.ToString();
		}
	}

}
