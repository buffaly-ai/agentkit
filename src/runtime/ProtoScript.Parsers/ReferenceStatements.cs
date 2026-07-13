using System.IO;

namespace ProtoScript.Parsers
{
	public class ReferenceStatements
	{
		static public ProtoScript.ReferenceStatement Parse(string strStatement)
		{
			Tokenizer tok = new Tokenizer(strStatement);
			return Parse(tok);
		}

		public static ProtoScript.ReferenceStatement Parse(Tokenizer tok)
		{
			ProtoScript.ReferenceStatement result = new ProtoScript.ReferenceStatement();

			tok.movePastWhitespace();
			result.Info.StartStatement(tok.getCursor());
			result.Info.File = Files.CurrentFile;

			tok.MustBeNext("reference");

			string nextToken = tok.peekNextToken();
			bool quotedPathReference = IsQuotedStringLiteral(nextToken);
			if (quotedPathReference)
			{
				result.AssemblyName = IncludeStatements.ParsePathLiteral(tok, "reference");
				result.IsFileReference = true;
			}
			else
			{
				result.AssemblyName = ProtoScript.Parsers.Identifiers.ParseMultiple(tok);
			}

			if (tok.CouldBeNext(";"))
			{
				result.Reference = result.IsFileReference
					? Path.GetFileNameWithoutExtension(result.AssemblyName)
					: result.AssemblyName;
			}
			else
			{
				result.Reference = ProtoScript.Parsers.Identifiers.ParseMultiple(tok);
				tok.MustBeNext(";");
			}

			if (string.IsNullOrWhiteSpace(result.Reference))
			{
				result.Reference = result.AssemblyName;
			}

			result.Info.StopStatement(tok.getCursor());

			return result;
		}

		private static bool IsQuotedStringLiteral(string token)
		{
			if (string.IsNullOrEmpty(token))
			{
				return false;
			}

			if (token.StartsWith("@\"") && token.EndsWith("\"") && token.Length >= 3)
			{
				return true;
			}

			return token.StartsWith("\"") && token.EndsWith("\"") && token.Length >= 2;
		}
	}
}
