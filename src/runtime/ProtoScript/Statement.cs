namespace ProtoScript
{
	public class Statement
	{
		private StatementParsingInfo? _info = null;
		public StatementParsingInfo Info
		{
			get
			{
				return _info ??= new StatementParsingInfo();
			}
			set
			{
				_info = value;
			}
		}

		public virtual IEnumerable<Statement> GetChildrenStatements()
		{
			yield break;			
		}

		private List<Diagnostics.Diagnostic>? _diagnostics = null;
		public List<Diagnostics.Diagnostic> Diagnostics
		{
			get
			{
				return _diagnostics ??= new List<Diagnostics.Diagnostic>();
			}
		}

		public string Comments = null;

	}
}
