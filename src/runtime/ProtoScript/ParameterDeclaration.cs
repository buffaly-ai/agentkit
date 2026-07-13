namespace ProtoScript
{
	public class ParameterDeclaration : Statement
	{
		public ProtoScript.Type Type;
		public string ParameterName;

		public bool IsOut = false;
		public bool IsRef = false;
		public bool IsThis = false;

		public Expression DefaultValue = null;
	}
}
