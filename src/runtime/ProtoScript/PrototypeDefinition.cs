using Ontology;

namespace ProtoScript
{
	public class PrototypeDefinition : Statement
	{
		public ProtoScript.Type PrototypeName;
		public Visibility Visibility;
		public bool IsStatic;
		public bool IsPartial;
		public bool IsAbstract;
		public bool IsSealed;
		public bool IsExternal;

		public Prototype ? ResolvedPrototype = null;

		public void SetModifiers(Modifiers modifiers)
		{
			this.IsPartial = modifiers.IsPartial;
			this.IsStatic = modifiers.IsStatic;
			this.Visibility = modifiers.Visibility;
			this.IsAbstract = modifiers.IsAbstract;
			this.IsSealed = modifiers.IsSealed;
			this.IsExternal = modifiers.IsExternal;
		}

		public List<ProtoScript.Type> Inherits = new List<Type>();

		public List<FieldDefinition> Fields = new List<FieldDefinition>();
		public List<PropertyDefinition> Properties = new List<PropertyDefinition>();
		public List<FunctionDefinition> Methods = new List<FunctionDefinition>();
		public List<EnumDefinition> Enums = new List<EnumDefinition>();
		public List<AnnotationExpression> Annotations = new List<AnnotationExpression>();
		public List<PrototypeInitializer> Initializers = new List<PrototypeInitializer>();
		public List<PrototypeDefinition> PrototypeDefinitions = new List<PrototypeDefinition>();
		public PrototypeDefinition Clone()
		{
			PrototypeDefinition copy = (PrototypeDefinition) this.MemberwiseClone();

			copy.Methods = new List<ProtoScript.FunctionDefinition>(); 
			foreach (FunctionDefinition method in this.Methods)
			{
				copy.Methods.Add(method.Clone());
			}

			//TODO: rest not implemented

			return copy;
		}

		public override IEnumerable<Statement> GetChildrenStatements()
		{
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

			foreach (FunctionDefinition method in Methods)
			{
				yield return method;
			}

			foreach (PrototypeInitializer method in Initializers)
			{
				yield return method;
			}

			foreach (PrototypeDefinition def in PrototypeDefinitions)
			{
				yield return def;
			}

			yield break;
		}

		public override string ToString()
		{
			return "ProtoScript.PrototypeDefinition[" + PrototypeName + "]";
		}
	}

}
