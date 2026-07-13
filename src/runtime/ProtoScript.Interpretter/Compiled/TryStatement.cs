using ProtoScript.Interpretter.RuntimeInfo;

namespace ProtoScript.Interpretter.Compiled
{
	//>+ create the class TryStatement
	public class TryStatement : Statement
	{
		public class CatchBlock
		{
			public ProtoScript.Type Type;
			public string ExceptionName;
			public VariableRuntimeInfo ExceptionValue;
			public CodeBlock Statements;
		}

		public CodeBlock TryBlock;

		public List<CatchBlock> CatchBlocks = new List<CatchBlock>();
		public CodeBlock FinallyBlock;
	}

	//> create the ThrowStatement class
	public class ThrowStatement : Statement
	{
		public Expression Expression;
	}


}
