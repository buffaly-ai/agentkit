namespace ProtoScript.Diagnostics
{
	public class Diagnostic
	{
		public string Message = null;

		public Diagnostic()
		{
		}

		public Diagnostic(string strMessage)
		{
			Message = strMessage;
		}

		public override string ToString()
		{
			return $"ProtoScript.Diagnostics.Diagnostic[{Message}]";
		}
	}
}
