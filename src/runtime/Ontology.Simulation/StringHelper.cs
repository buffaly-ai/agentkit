using BasicUtilities;

namespace Ontology.Simulation
{
	//Collect methods here until they get moved to StringUtil
	public class StringHelper
	{
		public static string SentenceToPrototypeName(string strSentence)
		{
			//>split strPredicte on spaces, remove all non-alphanumeric characters, upper case each letter, and join the results without spaces
			string strCleanedPredicate = string.Join("", StringUtil.SplitOnSpaces(strSentence).Select(x => StringUtil.UppercaseFirstLetter(x)).Take(10));
			strCleanedPredicate = StringUtil.RemoveNonAlphanumeric(strCleanedPredicate);
			strCleanedPredicate = StringUtil.Remove(strCleanedPredicate, ".");
			return strCleanedPredicate;
		}

		public static string EscapeStringForCSharpLiteral(string input)
		{
			if (input == null)
			{
				return "null";
			}
			return string.Format("\"{0}\"", input.Replace("\\", "\\\\")
													 .Replace("\"", "\\\"")
													 .Replace("\0", "\\0")
													 .Replace("\a", "\\a")
													 .Replace("\b", "\\b")
													 .Replace("\f", "\\f")
													 .Replace("\n", "\\n")
													 .Replace("\r", "\\r")
													 .Replace("\t", "\\t")
													 .Replace("\v", "\\v"));
		}
	}
}
