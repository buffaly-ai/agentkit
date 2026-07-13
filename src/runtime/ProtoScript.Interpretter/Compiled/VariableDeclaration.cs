using ProtoScript.Interpretter.RuntimeInfo;

namespace ProtoScript.Interpretter.Compiled
{
	public class VariableDeclaration : Statement
	{
		public TypeInfo Type;
		public Expression Initializer;
		public VariableRuntimeInfo RuntimeInfo;
	}
}
