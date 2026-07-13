namespace ProtoScript.Diagnostics
{
	public class CannotFindPrototypeField : Diagnostic
	{
		public CannotFindPrototypeField(string strFieldName) : base($"Cannot find field: {strFieldName}")
		{

		}
	}
}
