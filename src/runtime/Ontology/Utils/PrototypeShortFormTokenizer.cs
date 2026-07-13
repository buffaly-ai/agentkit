using BasicUtilities.Collections;

namespace Ontology.Utils
{
	[Serializable]
	public class PrototypeShortFormParsingException : Exception
	{
		public PrototypeShortFormParsingException() { }

		public override string Message
		{
			get
			{
				int iLine = 1;
				for (int i = 0; i < m_iCursor; i++)
				{
					if (m_strContents[i] == '\n')
						iLine++;
				}

				string strResult = "Prototype Short Form Syntax Error. At " + m_iCursor.ToString() + " \r\n";
				strResult += "Expected: [" + m_strExpected + "] \r\n";
				strResult += "But saw: " + m_strContents.Substring(m_iCursor, Math.Min(255, m_strContents.Length - m_iCursor)) + "\r\n";
				strResult += "Line: " + iLine + "\r\n";
				strResult += "Preceeding: " + m_strContents.Substring(Math.Max(m_iCursor - 255, 0), Math.Min(255, m_strContents.Length));
				return strResult;
			}
		}

		private string m_strContents;
		private int m_iCursor;
		private string m_strExpected;

		public PrototypeShortFormParsingException(string strContents, int iCursor, string strExpected) : base(null)
		{
			m_strContents = strContents;
			m_iCursor = iCursor;
			m_strExpected = strExpected;
		}

		public PrototypeShortFormParsingException(string message) : base(message) { }
		public PrototypeShortFormParsingException(string message, Exception inner) : base(message, inner) { }
		protected PrototypeShortFormParsingException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}

	public class PrototypeShortFormTokenizer : BasicUtilities.Tokenizer
	{
		public PrototypeShortFormTokenizer(string strTarget) : base(strTarget)
		{
			this.clearQuotes(); //treat them special 
			this.clearSymbols();

			new Set<char>(new char[] { '[', '=', '(', ')' }).ForEach(x => this.insertSymbol(x));
		}

		new public string peekNextToken()
		{
			int iCur = this.getCursor();
			string strResult = this.getNextToken();
			this.setCursor(iCur);

			return strResult;
		}

		new public string getNextToken()
		{
			string sTok = base.getNextToken();
			if (sTok == "[" || base.peekNextChar() == '[')
			{
				sTok += base.getTokenTo("]") + "]";
				base.movePast("]");
			}

			return sTok;
		}




		public bool CouldBeNext(string strTok)
		{
			if (peekNextToken() == strTok)
			{
				getNextToken();
				return true;
			}

			return false;
		}

		public bool MustBeNext(string strTok)
		{
			int iCursor = m_szCursor;
			if (strTok != getNextToken())
				throw new PrototypeShortFormParsingException(m_strTarget, iCursor, strTok);

			return true;
		}
	}
}
