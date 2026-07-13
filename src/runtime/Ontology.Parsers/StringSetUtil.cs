using BasicUtilities;
using BasicUtilities.Collections;

namespace Ontology.Parsers
{
	static public class StringSetUtil
	{
		static public bool AreEqual(Set<string> set1, Set<string> set2)
		{
			if (set1.Count != set2.Count)
				return false;

			foreach (string strValue in set1)
			{
				if (!set2.Any(x => StringUtil.EqualNoCase(x, strValue)))
					return false;
			}

			return true;
		}

		static public Set<T> Union<T>(Set<T> set1, Set<T> set2)
		{
			Set<T> set12 = new Set<T>();

			foreach (T x in set1)
			{
				set12.Add(x);
			}

			foreach (T x in set2)
			{
				set12.Add(x);
			}

			return set12;
		}

		static public Set<string> Intersection(Set<string> set1, Set<string> set2)
		{
			Set<string> vectOutputs = new Set<string>();

			foreach (string strValue in set1)
			{
				if (set2.Any(x => StringUtil.EqualNoCase(x, strValue)))
					vectOutputs.Add(strValue);
			}

			return vectOutputs;
		}

		static public Set<string> Minus(Set<string> set1, Set<string> set2)
		{
			Set<string> vectOutputs = new Set<string>();

			foreach (string strValue in set1)
			{
				if (!set2.Any(x => StringUtil.EqualNoCase(x, strValue)))
					vectOutputs.Add(strValue);
			}

			return vectOutputs;
		}

		static public Set<string> Disunion(Set<string> set1, Set<string> set2)
		{
			Set<string> setResult = new Set<string>();
			setResult.AddRange(Minus(set1, set2));
			setResult.AddRange(Minus(set2, set1));
			return setResult;
		}
	}
}
