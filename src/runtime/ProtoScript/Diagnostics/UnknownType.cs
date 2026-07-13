namespace ProtoScript.Diagnostics
{
	public class UnknownType : Diagnostic
	{
		public UnknownType(string strType) : base($"Unknown type: {strType}")
		{

		}
	}
}
