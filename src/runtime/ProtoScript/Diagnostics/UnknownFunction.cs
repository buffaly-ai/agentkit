namespace ProtoScript.Diagnostics
{
	public class UnknownFunction : Diagnostic
	{
		public UnknownFunction(string strFunction) : base($"Unknown function: {strFunction}")
		{

		}
	}
}
