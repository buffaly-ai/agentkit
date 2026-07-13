namespace ProtoScript
{
	public class PrototypeInitializer : Statement
	{
		public CodeBlock Statements = new CodeBlock();

		public override string ToString()
		{
			return Statements?.ToString() ?? "{}";
		}
	}
}
