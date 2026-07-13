namespace ProtoScript
{
	public class UsingStatement : Statement
	{
		public bool IsStatic = false; 
		public List<string> Namespaces = new List<string>();

		public string JoinedNamespace
		{
			get
			{
				return string.Join(".", this.Namespaces);
			}
		}
	}
}
