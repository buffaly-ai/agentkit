namespace ProtoScript
{
	public class TryStatement : Statement
	{
		public class CatchBlock
		{
			public ProtoScript.Type Type;
			public string ExceptionName;
			public CodeBlock Statements;
		}

		public CodeBlock TryBlock;

		public List<CatchBlock> CatchBlocks = new List<CatchBlock>();
		public CodeBlock FinallyBlock;

		public override IEnumerable<Statement> GetChildrenStatements()
		{
			yield return new CodeBlockStatement(TryBlock);
			foreach (CatchBlock catchBlock in CatchBlocks)
			{
				yield return new CodeBlockStatement(catchBlock.Statements) ;
			}

			if (null != FinallyBlock)
				yield return new CodeBlockStatement(FinallyBlock);
		}
	}

}
