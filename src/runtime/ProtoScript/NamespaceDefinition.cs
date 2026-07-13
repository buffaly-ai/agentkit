namespace ProtoScript
{
	public class NamespaceDefinition : Statement
	{
		public List<string> Namespaces = new List<string>();
		public List<ProtoScript.PrototypeDefinition> PrototypeDefinitions = new List<PrototypeDefinition>();
		public List<EnumDefinition> Enums = new List<EnumDefinition>();
		public List<Statement> Statements = new List<Statement>();


		public NamespaceDefinition Clone()
		{
			NamespaceDefinition copy = new ProtoScript.NamespaceDefinition();
			copy.Namespaces.AddRange(this.Namespaces);
			foreach (var cls in this.PrototypeDefinitions)
				copy.PrototypeDefinitions.Add(cls.Clone());
			//	foreach (var en in this.Enums)
			//		copy.Enums.Add(en.Clone());

			return copy;
		}

		public override IEnumerable<Statement> GetChildrenStatements()
		{
			foreach (PrototypeDefinition cls in PrototypeDefinitions)
			{
				yield return cls;
			}

			foreach (EnumDefinition en in Enums)
			{
				yield return en;
			}

			yield break;
		}

		public override string ToString()
		{
			return "ProtoScript.NamespaceDefinition[" + string.Join(".", Namespaces) + "]";
		}

		public string GetFullName()
		{
			return string.Join(".", Namespaces);
		}
	}

}
