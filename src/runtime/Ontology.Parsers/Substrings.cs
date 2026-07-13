using System.Collections;
using System.Text;
using BasicUtilities;
using BasicUtilities.Collections;

namespace Ontology.Parsers
{
	public class Substrings
	{
		private static object m_lock = new object();
		private static Map<string, Set<string>> m_Cache = new Map<string, Set<string>>();
		public static void ResetCache()
		{
			m_Cache = new Map<string, Set<string>>();
		}

		static public string GetLongestCommonSubstring2(string str1, string str2, bool bCaseSensitive = false)
		{
			string sequence = string.Empty;
			if (String.IsNullOrEmpty(str1) || String.IsNullOrEmpty(str2))
				return sequence;

			int[,] num = new int[str1.Length, str2.Length];
			int maxlen = 0;
			int lastSubsBegin = 0;
			StringBuilder sequenceBuilder = new StringBuilder();

			for (int i = 0; i < str1.Length; i++)
			{
				for (int j = 0; j < str2.Length; j++)
				{
					if (bCaseSensitive && str1[i] != str2[j])
						num[i, j] = 0;
					else if (!bCaseSensitive && char.ToUpperInvariant(str1[i]) != char.ToUpperInvariant(str2[j]))
						num[i, j] = 0;
					else
					{
						if ((i == 0) || (j == 0))
							num[i, j] = 1;
						else
							num[i, j] = 1 + num[i - 1, j - 1];

						if (num[i, j] > maxlen)
						{
							maxlen = num[i, j];
							int thisSubsBegin = i - num[i, j] + 1;
							if (lastSubsBegin == thisSubsBegin)
							{//if the current LCS is the same as the last time this block ran
								sequenceBuilder.Append(str1[i]);
							}
							else //this block resets the string builder if a different LCS is found
							{
								lastSubsBegin = thisSubsBegin;
								sequenceBuilder.Length = 0; //clear it
								sequenceBuilder.Append(str1.Substring(lastSubsBegin, (i + 1) - lastSubsBegin));
							}
						}
					}
				}
			}

			return sequenceBuilder.ToString();
		}

		public static Set<string> GetLongestCommonSubstring(string x1, string x2)
		{
			int max = Math.Min(x1.Length, x2.Length);

			for (int i = max; i > 0; i--)
			{
				Set<string> setX1 = GetAllSubstrings(x1, i);
				Set<string> setX2 = GetAllSubstrings(x2, i);

				Set<string> setX12 = StringSetUtil.Intersection(setX1, setX2);

				if (setX12.Count > 0)
					return setX12;
			}

			return new Set<string>();
		}

		public static Map<int, Set<string>> GetCommonSubstrings(string x1, string x2)
		{
			Map<int, Set<string>> mapResults = new Map<int, Set<string>>();

			int max = Math.Min(x1.Length, x2.Length);

			for (int i = max; i > 0; i--)
			{
				Set<string> setX1 = GetAllSubstrings(x1, i);
				Set<string> setX2 = GetAllSubstrings(x2, i);

				Set<string> setX12 = StringSetUtil.Intersection(setX1, setX2);

				if (setX12.Count > 0)
					mapResults[i] = setX12;
			}

			return mapResults;
		}

		public static Map<string, int> GetMostCommonSubstrings(string x1, string x2)
		{
			Map<string, int> mapResults = new Map<string, int>();
			Map<int, Set<string>> mapSubs = GetCommonSubstrings(x1, x2);

			foreach (Set<string> setString in mapSubs.Values)
			{
				foreach (string str in setString)
				{
					int iOccurrences = CountOccurrences(x1, str) + CountOccurrences(x2, str);

					if (mapResults.ContainsKey(str))
						mapResults[str] += iOccurrences;

					else
						mapResults[str] = iOccurrences;
				}
			}


			return mapResults;
		}

		public static Map<string, int> GetMostWeightedSubstrings(string x1, string x2)
		{
			Map<string, int> mapResults = new Map<string, int>();
			Map<int, Set<string>> mapSubs = GetCommonSubstrings(x1, x2);

			foreach (Set<string> setString in mapSubs.Values)
			{
				foreach (string str in setString)
				{

					int iOccurrences = (CountOccurrences(x1, str) + CountOccurrences(x2, str)) * str.Length;

					if (mapResults.ContainsKey(str))
						mapResults[str] += iOccurrences;

					else
						mapResults[str] = iOccurrences;

				}
			}


			return mapResults;
		}

		public static Map<string, int> GetMostWeightedSubstrings(string x1)
		{
			Logs.DebugLog.CreateTimer("GetMostWeightedSubstrings");
			Map<string, int> mapResults = new Map<string, int>();
			Set<string> setString = new Set<string>();
			for (int i = 2; i <= x1.Length / 2; i++)
			{
				setString.AddRange(GetAllSubstrings(x1, i));
			}
			Logs.DebugLog.WriteTimer("GetMostWeightedSubstrings");
			foreach (string str in setString)
			{
				int iOccurrences = CountOccurrences(x1, str);
				if (iOccurrences > 1)
				{
					iOccurrences *= str.Length;
					if (mapResults.ContainsKey(str))
						mapResults[str] += iOccurrences;

					else
						mapResults[str] = iOccurrences;
				}
			}
			Logs.DebugLog.WriteTimer("GetMostWeightedSubstrings");
			return mapResults;
		}

		public static List<string> GetMostSignificantSubstrings(string x1, string x2)
		{
			Map<string, int> mapWeights = GetMostWeightedSubstrings(x1, x2);
			List<string> lstResults = new List<string>();

			foreach (var weight in mapWeights.OrderByDescending(x => x.Value))
			{
				if (
					!lstResults.Any(x => StringUtil.InString(x, weight.Key) ||
					StringUtil.InString(weight.Key, x)))
				{
					lstResults.Add(weight.Key);
				}
			}

			return lstResults;
		}

		public static List<List<string>> GetNonOverlappingPermutations(List<string> lstSubs)
		{
			List<List<string>> lstResults = new List<List<string>>();

			for (int i = 0; i < lstSubs.Count; i++)
			{
				string strSub1 = lstSubs[i];

				List<string> lstPermutation = new List<string>();
				lstPermutation.Add(strSub1);

				for (int j = i; j < lstSubs.Count; j++)
				{
					string strSub2 = lstSubs[j];
					string strLCS = GetLongestCommonSubstring2(strSub1, strSub2);//.FirstOrDefault();
					if (StringUtil.IsEmpty(strLCS) || strLCS.Length < strSub1.Length / 2)
						lstPermutation.Add(strSub2);
				}

				lstResults.Add(lstPermutation);
			}

			return lstResults;
		}
		public static List<string> GetNonOverlappingList(List<string> lstSubs)
		{
			List<string> lstResults = new List<string>();

			for (int i = 0; i < lstSubs.Count; i++)
			{
				string strSub1 = lstSubs[i];

				if (!lstResults.Any(x => GetLongestCommonSubstring2(x, strSub1).Length > x.Length / 2))
					lstResults.Add(strSub1);
			}

			return lstResults;
		}

		public static int CountOccurrences(string x1, string sub)
		{
            if (x1 == null || sub == null)
                return 0;

			int iCount = 0;
			int iPos = x1.IndexOf(sub, StringComparison.InvariantCultureIgnoreCase);

			while (iPos != -1)
			{
				iCount++;
				iPos = x1.IndexOf(sub, iPos + sub.Length, StringComparison.InvariantCultureIgnoreCase);
			}

			return iCount;
		}

		public static Set<string> GetLongestUncommonSubstring(string x1, string x2)
		{
			Set<string> setCommon = GetLongestCommonSubstring(x1, x2);
			Set<string> setRes = new Set<string>();

			foreach (string strCommon in setCommon)
			{
				string[] strParts = StringUtil.Split(x1, strCommon, true);

				foreach (string strPart in strParts)
					setRes.Add(strPart);
			}

			return setRes;
		}

		public static Set<string> GetAllSubstrings(string x1, int length)
		{
			Set<string> set = new Set<string>();

			if (!m_Cache.TryGetValue(x1 + " " + length, out set))
			{
				set = new Set<string>();

				int max = x1.Length - length;

				for (int i = 0; length > 0 && i <= max; i++)
				{
					set.Add(x1.Substring(i, length).ToLower());
				}

				//the cache seems to be having some issues with multithreading, saying the the key already exists or is null
				lock (m_lock)
				{
					m_Cache[x1 + " " + length] = set;
				}

				if (m_Cache.Keys.Count % 100 == 0)
					Logs.DebugLog.WriteEvent("Substring Cache Size", m_Cache.Keys.Count.ToString());
			}

			return set;
		}


		public class StringSet : SortedSet<string>
		{
			public class ForwardCompare : IComparer<string>
			{
				CaseInsensitiveComparer comparer = new CaseInsensitiveComparer();

				int IComparer<string>.Compare(string x, string y)
				{
					return comparer.Compare(x, y);
				}
			}

			public StringSet()
				: base(new ForwardCompare())
			{

			}
		}

		public class R2LStringSet : SortedSet<string>
		{
			public class R2LComparer : IComparer<string>
			{
				public int Compare(string x, string y)
				{
					for (int i = 0; i < x.Length && i < y.Length; i++)
					{
						int i1 = x.Length - 1 - i;
						int i2 = y.Length - 1 - i;

						char c1 = char.ToLower(x[i1]);
						char c2 = char.ToLower(y[i2]);

						if (c1 < c2)
							return -1;

						if (c1 > c2)
							return 1;
					}

					if (x.Length > y.Length)
						return -1;

					if (y.Length > x.Length)
						return 1;

					return 0;
				}
			}

			public R2LStringSet()
				: base(new R2LComparer())
			{

			}
		}


		public static string GetLongestStartingString(string strTarget, StringSet setStrings)
		{
			int i = 0;
			string strLongest = string.Empty;
			foreach (string str in setStrings)
			{

				if (i >= str.Length || i >= strTarget.Length)
					continue;
				
				char c1 = char.ToLower(str[i]);
				char c2 = char.ToLower(strTarget[i]);

				if (c1 != c2)
				{
					if (c1 > c2)
						break;
					else
						continue;
				}

				for (; i < strTarget.Length && i < str.Length; i++)
				{
					c1 = char.ToLower(str[i]);
					c2 = char.ToLower(strTarget[i]);

					if (c1 != c2)
					{
						strLongest = strTarget.Substring(0, i);
						break;
					}
				}
			}

			return strLongest;
		}

		public static string GetLongestEndingString(string strTarget, R2LStringSet setStrings)
		{
			int i = 0;
			string strLongest = string.Empty;
			foreach (string str in setStrings)
			{
				if (i >= str.Length || i >= strTarget.Length)
					continue;

				int i1 = str.Length - 1 - i;
				int i2 = strTarget.Length - 1 - i;

				char c1 = char.ToLower(str[i1]);
				char c2 = char.ToLower(strTarget[i2]);
				
				if (c1 != c2)
				{
					if (c1 > c2)
						break;
					else
						continue;
				}

				for (; i < strTarget.Length && i < str.Length; i++)
				{
					i1 = str.Length - 1 - i;
					i2 = strTarget.Length - 1 - i;

					c1 = char.ToLower(str[i1]);
					c2 = char.ToLower(strTarget[i2]);
				
					if (c1 != c2)
					{
						strLongest = strTarget.Substring(i2+1, i);
						break;
					}
				}
			}

			return strLongest;
		}
	}
}
