namespace ProtoScript
{
	public class StatementParsingInfo
	{
		public int StartingOffset;
		public int Length;
		public bool IsIncomplete = false;
		public string File = string.Empty;
		public int StoppingOffset
		{
			get
			{
				return StartingOffset + Length;
			}
		}

		public void StartStatement(int iCursor)
		{
			this.StartingOffset = iCursor;
		}

		public void StopStatement(int iCursor)
		{
			this.Length = iCursor - this.StartingOffset;
		}	
		
		public bool IsInside(int iCursor)
		{
			return (iCursor >= this.StartingOffset && iCursor <= StoppingOffset) ;
		}	
	}
}
