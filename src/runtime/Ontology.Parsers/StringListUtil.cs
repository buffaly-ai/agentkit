using BasicUtilities;

namespace Ontology.Parsers
{
	static public class StringListExtensions
	{
		static public int IndexOf(this List<string> lst, string strTarget, int iPos, bool bIgnoreCase)
		{
			int iCur = -1;
			if (bIgnoreCase)
			{
				for (int i = iPos; i < lst.Count; i++)
				{
					var l = lst[i];

					if (string.Compare(l, strTarget, bIgnoreCase) == 0)
						return i;
				}
			}
			else
			{
				iCur = lst.IndexOf(strTarget, iPos);
			}

			return iCur;
		}
	}

    static public class StringListUtil
    {
        static public int IndexOf(List<string> lstTokens, List<string> lstTarget, int iPos = 0)
        {
            int iCur = lstTokens.IndexOf(lstTarget.First(), iPos, true);

            while (iCur >= 0)
            {
                 for (int i = 0; i < lstTarget.Count && iCur + i < lstTokens.Count; i++)
                {
                    string l = lstTarget[i];
                    string t = lstTokens[i + iCur];
                    if (string.Compare(l, t, true) != 0)
                        break;

                    if (i == lstTarget.Count - 1)
                        return iCur;
                }

                iCur = lstTokens.IndexOf(lstTarget.First(), iCur + 1, true);
            }

            return -1;
        }

        static public List<string> LeftOfFirst(List<string> lstTokens, List<string> lstTarget)
        {
            List<string> lstResult = new List<string>();

            int iCur = IndexOf(lstTokens, lstTarget);
            if (iCur != -1)
                lstResult.AddRange(lstTokens.GetRange(0, iCur));

            return lstResult;
        }

        static public List<string> RightOfFirst(List<string> lstTokens, List<string> lstTarget)
        {
            List<string> lstResult = new List<string>();

            int iCur = IndexOf(lstTokens, lstTarget);
            if (iCur != -1)
                lstResult.AddRange(lstTokens.GetRange(iCur + lstTarget.Count, lstTokens.Count - (iCur + lstTarget.Count)));

            return lstResult;
        }

        static public List<string> GetLongestSubList(List<string> lst1, List<string> lst2, bool bIgnoreCase = true)
        {
            List<string> lstResult = new List<string>();

            List<string> lstTokens = new List<string>();
            List<int> lstEncoded1 = new List<int>();
            List<int> lstEncoded2 = new List<int>();

            foreach (var l in lst1)
            {
				string str = bIgnoreCase ? l.ToLower() : l;
                int i = lstTokens.IndexOf(str, 0, bIgnoreCase);
                if (i == -1)
                {
                    i = lstTokens.Count;
                    lstTokens.Add(str);
                }

                lstEncoded1.Add(i);
            }

            foreach (var l in lst2)
            {
				string str = bIgnoreCase ? l.ToLower() : l;
				int i = lstTokens.IndexOf(str, 0, bIgnoreCase);
                if (i == -1)
                {
                    i = lstTokens.Count;
                    lstTokens.Add(str);
                }

                lstEncoded2.Add(i);
            }

            string str1 = "|" + string.Join("|", lstEncoded1.ToArray()) + "|";
            string str2 = "|" + string.Join("|", lstEncoded2.ToArray()) + "|";

			//case sensitivity doesn't matter here with the numbers
			string strLCS = Substrings.GetLongestCommonSubstring2(str1, str2);//.FirstOrDefault();

			if (null == strLCS || !strLCS.Contains("|"))
				return lstResult;

			strLCS = StringUtil.RightOfFirst(strLCS, "|");

			if (null == strLCS || !strLCS.Contains("|"))
				return lstResult;

			strLCS = StringUtil.LeftOfLast(strLCS, "|");

			string[] strLCSs = StringUtil.Split(strLCS, "|");

            foreach (var str in strLCSs)
            {
                int iPos = Convert.ToInt32(str);
                lstResult.Add(lstTokens[iPos]);
            }

            return lstResult;
        }



        static public List<T> Intersection<T>(List<T> set1, List<T> set2)
        {
            List<T> vectOutputs = new List<T>();

            foreach (T strValue in set1)
            {
                if (set2.Contains(strValue))
                    vectOutputs.Add(strValue);
            }

            return vectOutputs;
        }

		static public bool EqualNoCase(List<string> lst1, List<string> lst2)
		{
			if (lst1.Count != lst2.Count)
				return false;

			for (int i = 0; i < lst1.Count; i++)
			{
				string s1 = lst1[i];
				string s2 = lst2[i];

				if (!StringUtil.EqualNoCase(s1, s2))
					return false;			
			}

			return true;
		}

    }
}
