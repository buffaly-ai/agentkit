namespace ProtoScript.Parsers
{
	public class Modifiers
	{
		static public ProtoScript.Modifiers Parse(Tokenizer tok)
		{
			ProtoScript.Modifiers result = new ProtoScript.Modifiers();

			do
			{
				string strTok = tok.peekNextToken(); 

				switch (strTok)
				{
					case "static":
						result.IsStatic = true;
						tok.getNextToken();
						break;

					case "sealed":
						result.IsSealed = true;
						tok.getNextToken();
						break;

					case "override":
						result.IsOverride = true;
						tok.getNextToken();
						break;

					case "partial":
						result.IsPartial = true;
						tok.getNextToken();
						break;

					case "new":
						result.IsNew = true;
						tok.getNextToken();
						break;

					case "async":
						result.IsAsync = true;
						tok.getNextToken();
						break;

					case "abstract":
						result.IsAbstract = true;
						tok.getNextToken();
						break;

					case "const":
						result.IsConst = true;
						tok.getNextToken();
						break;

					case "readonly":
						result.IsReadonly = true;
						tok.getNextToken();
						break;

					case "protected":
						result.Visibility = Visibilities.Parse(tok);
						break;

					case "private":
						result.Visibility = Visibilities.Parse(tok);
						break;

					case "public":
						result.Visibility = Visibilities.Parse(tok);
						break;

					case "internal":
						result.Visibility = Visibilities.Parse(tok);
						break;

					case "volatile":
						result.IsViolatile = true;
						tok.getNextToken();
						break;

					case "virtual":
						result.IsVirtual = true;
						tok.getNextToken();
						break;

					case "delegate":
						result.IsDelegate = true;
						tok.getNextToken();
						break;

					case "extern":
						result.IsExternal = true;
						tok.getNextToken();
						break;

					default:
						return result;

				}
			}
			while (true);
		}
	}
}
