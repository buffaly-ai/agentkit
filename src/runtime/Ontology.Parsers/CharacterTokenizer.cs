namespace Ontology.Parsers
{
	public class CharacterTokenizer
	{
		private int m_iCursor = 0;
		private string m_strSource = null;
		public CharacterTokenizer(string strSource)
		{
			m_strSource = strSource;
		}

		public bool hasMoreTokens()
		{
			return m_iCursor < m_strSource.Length;
		}

		public string getNextToken()
		{
			return hasMoreTokens() ? m_strSource[m_iCursor++].ToString() : null;
		}
	}
}
