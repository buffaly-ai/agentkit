using BasicUtilities;

namespace Ontology.Parsers
{
	public class UppercaseTokenizer
	{
		int m_iCursor = 0;
		private List<string> m_strSource = null;
		public UppercaseTokenizer(string strSource)
		{
			m_strSource = StringUtil.SplitUppercaseWords(strSource).ToList();
		}

		public bool hasMoreTokens()
		{
			return m_iCursor < m_strSource.Count;
		}

		public string getNextToken()
		{
			return hasMoreTokens() ? m_strSource[m_iCursor++].ToString() : null;
		}
	}
}
