using System.Text;

namespace ProtoScript
{
	public class FieldDefinition : Statement
	{
		public Visibility Visibility;
		public bool IsStatic;
		public bool IsReadonly;
		public bool IsConst;
		public bool IsNew;
		public bool IsOverride;
		public ProtoScript.Type Type;
		public string FieldName;
		public Expression Initializer;

		public List<AnnotationExpression> Annotations = new List<AnnotationExpression>();

		public void SetModifiers(Modifiers modifiers)
		{
			this.IsStatic = modifiers.IsStatic;
			this.Visibility = modifiers.Visibility;
			this.IsReadonly = modifiers.IsReadonly;
			this.IsConst = modifiers.IsConst;
			this.IsNew = modifiers.IsNew;
			this.IsOverride = modifiers.IsOverride;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder("ProtoScript.FieldDefinition[");
			if (IsStatic)
				sb.Append("static ");
			if (IsReadonly)
				sb.Append("readonly ");

			sb.Append(Visibility.ToString().ToLower()).Append(" ");

			if (IsConst)
				sb.Append(" const ");

			if (null != Type)
				sb.Append(Type.ToString());

			sb.Append(" ").Append(FieldName);
			sb.Append("]");
			return sb.ToString();
		}
	}
}
