namespace ProtoScript
{
	public class ReferenceStatement : Statement
	{
		public string AssemblyName;
		public string Reference;
		public bool IsFileReference;
		public string? ResolvedAssemblyPath;
	}
}
