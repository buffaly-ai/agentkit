using BasicUtilities;
using Ontology.BaseTypes;

namespace Ontology.Utils
{
	public class PrototypeShortFormParser
	{
		static public Prototype FromShortString(string strPrototype)
		{
			PrototypeShortFormTokenizer tok = new PrototypeShortFormTokenizer(strPrototype);
			return ParseObject(tok);
		}

		static private Prototype ParseObject(PrototypeShortFormTokenizer tok)
		{
			MovePastEOL(tok);
			int iNumTabs = MovePastTabs(tok);
			return ParseObject(tok, iNumTabs);
		}

		static private Prototype ParseObject(PrototypeShortFormTokenizer tok, int iNumTabs)
		{
			//Note there is a bug here. If the prototype name contains any delimiters it will fail to parse the whole thing. For example
			//System.String["Configuration Error"] 
			//will only parse System.String["Configuration
			string strPrototypeName = tok.getNextToken();
			int? iPrototypeID = null;
			if (tok.CouldBeNext("("))
			{
				iPrototypeID = Convert.ToInt32(tok.getNextToken());
				tok.MustBeNext(")");
			}

			Prototype prototype = null;
			if (null == iPrototypeID || NativeValuePrototypes.IsBaseType(iPrototypeID.Value))
			{
				prototype = ParsePrototypeName(strPrototypeName);
			}
			else if (strPrototypeName == null)
			{
				prototype = null;
			}
			else
			{
				prototype = Prototypes.GetPrototype(iPrototypeID.Value);
			}

			iNumTabs++;
			while (tok.hasMoreTokens())
			{
				MovePastEOL(tok);
				int iCursor = tok.getCursor();
				int iNextNumTabs = MovePastTabs(tok);
				if (iNextNumTabs < iNumTabs)
				{
					tok.setCursor(iCursor);
					return prototype;
				}

				else if (iNextNumTabs > iNumTabs)
					iNumTabs = iNextNumTabs;

				string strPropertyName = ParsePropertyName(tok);
				tok.MustBeNext("=");
				Prototype protoProp = ParseObject(tok, iNumTabs);

				if (strPropertyName.StartsWith("["))
					prototype.Children.Add(protoProp);
				else 
					prototype.Properties[strPropertyName] = protoProp;
			}


			return prototype;
		}

		static private Prototype ParsePrototypeName(string strPrototypeName)
		{
			if (strPrototypeName.Contains("["))
			{
				if (strPrototypeName.StartsWith(System_Int32.PrototypeName))
					return NativeValuePrototype.GetOrCreateNativeValuePrototype(Convert.ToInt32(StringUtil.Between(strPrototypeName, "[", "]")));

				else if (strPrototypeName.StartsWith(System_Boolean.PrototypeName))
					return NativeValuePrototype.GetOrCreateNativeValuePrototype(Convert.ToBoolean(StringUtil.Between(strPrototypeName, "[", "]")));

				else if (strPrototypeName.StartsWith(System_String.PrototypeName))
					return NativeValuePrototype.GetOrCreateNativeValuePrototype(StringUtil.Between(strPrototypeName, "[", "]"));

				else if (strPrototypeName.StartsWith(System_Double.PrototypeName))
					return NativeValuePrototype.GetOrCreateNativeValuePrototype(Convert.ToDouble(StringUtil.Between(strPrototypeName, "[", "]")));
			}

			return Prototypes.GetPrototypeByPrototypeName(strPrototypeName);
		}

		static private int MovePastTabs(PrototypeShortFormTokenizer tok)
		{
			int iNumTabs = 0;
			while (tok.peekNextChar() == '\t')
			{
				tok.discardNextChar();
				iNumTabs++;
			}
			return iNumTabs;
		}

		static private void MovePastEOL(PrototypeShortFormTokenizer tok)
		{
			while (tok.peekNextChar() == '\r' || tok.peekNextChar() == '\n' || tok.peekNextChar() == ' ')
			{
				tok.getNextChar();
			}
		}

		static private string ParsePropertyName(PrototypeShortFormTokenizer tok)
		{
			string strToken = tok.getNextToken();
			if (strToken.StartsWith("."))
				strToken = StringUtil.RightOfFirst(strToken, ".");
			return strToken;
		}
	}


}
