namespace ProtoScript.Diagnostics
{
	public class CannotConvert : Diagnostic
	{
		public CannotConvert(string strSourceType, string strTargetType) : base($"Cannot convert {strSourceType} to {strTargetType}")
		{

		}
	}
}
