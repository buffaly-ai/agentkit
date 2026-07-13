namespace ProtoScript.Diagnostics
{
	public class UnknownPrototype : Diagnostic
	{
		public UnknownPrototype(string strPrototypeName) : base($"Unknown prototype: {strPrototypeName}")
		{

		}
	}
}
