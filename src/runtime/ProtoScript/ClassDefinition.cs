namespace ProtoScript
{
	public class ClassDefinition : Statement
	{
		public ProtoScript.Type ClassName;
		public Visibility Visibility;
		public bool IsStatic;
		public bool IsPartial;
		public bool IsAbstract;
		public bool IsSealed;

		public void SetModifiers(Modifiers modifiers)
		{
			this.IsPartial = modifiers.IsPartial;
			this.IsStatic = modifiers.IsStatic;
			this.Visibility = modifiers.Visibility;
			this.IsAbstract = modifiers.IsAbstract;
			this.IsSealed = modifiers.IsSealed;
		}

		public List<ProtoScript.Type> Inherits = new List<Type>();

		public List<FieldDefinition> Fields = new List<FieldDefinition>();
		public List<PropertyDefinition> Properties = new List<PropertyDefinition>();
		public List<MethodDefinition> Methods = new List<MethodDefinition>();
		public List<EnumDefinition> Enums = new List<EnumDefinition>();

		public List<ClassDefinition> Classes = new List<ClassDefinition>();

		public ClassDefinition Clone()
		{
			ClassDefinition copy = (ClassDefinition) this.MemberwiseClone();

			copy.Methods = new List<ProtoScript.MethodDefinition>(); 
			foreach (MethodDefinition method in this.Methods)
			{
				copy.Methods.Add(method.Clone());
			}

			//TODO: rest not implemented

			return copy;
		}

		public override IEnumerable<Statement> GetChildrenStatements()
		{
			foreach (ClassDefinition cls in Classes)
			{
				yield return cls;
			}

			foreach (EnumDefinition en in Enums)
			{
				yield return en;
			}

			foreach (FieldDefinition field in Fields)
			{
				yield return field;
			}

			foreach (PropertyDefinition prop in Properties)
			{
				yield return prop;
			}

			foreach (MethodDefinition method in Methods)
			{
				yield return method;
			}

			yield break;
		}

		public override string ToString()
		{
			return "ProtoScript.ClassDefinition[" + ClassName + "]";
		}
	}

}
