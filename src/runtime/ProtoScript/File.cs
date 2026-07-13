namespace ProtoScript
{
	public class File
	{
		public FileInfo Info = null;
		public string RawCode = null;
		public bool IsPrecompiled = false;
		public List<ReferenceStatement> References = new List<ReferenceStatement>();
		public List<ImportStatement> Imports = new List<ImportStatement>();
		public List<UsingStatement> Usings = new List<UsingStatement>();
		public List<NamespaceDefinition> Namespaces = new List<NamespaceDefinition>();
		public List<PrototypeDefinition> PrototypeDefinitions = new List<PrototypeDefinition>();
		public List<Statement> Statements = new List<Statement>();
		public List<IncludeStatement> Includes = new List<IncludeStatement>();

		public override string ToString()
		{
			return "ProtoScript.File[" + this.Info?.FullName + "]";
		}

		public void InsertOrReplace(PrototypeDefinition prototypeDefinition)
		{
			int i = this.PrototypeDefinitions.FindIndex(x => x.PrototypeName.TypeName == prototypeDefinition.PrototypeName.TypeName);

			if (i >= 0)
			{
				this.PrototypeDefinitions[i] = prototypeDefinition;
			}
			else
			{
				this.PrototypeDefinitions.Add(prototypeDefinition);
			}
		}
	}
}
