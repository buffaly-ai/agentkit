namespace ProtoScript.Parsers
{
	public class Visibilities
	{
		static public Visibility Parse(Tokenizer tok)
		{
			Visibility result = Visibility.Private;

			if (tok.CouldBeNext("protected"))
			{
				result = Visibility.Protected;

				if (tok.CouldBeNext("internal"))
					result = Visibility.ProtectedInternal;
			}

			else if (tok.CouldBeNext("private"))
			{
				result = Visibility.Private;

				if (tok.CouldBeNext("protected"))
					result = Visibility.PrivateProtected;
			}
			else if (tok.CouldBeNext("public"))
				result = Visibility.Public;

			else if (tok.CouldBeNext("internal"))
				result = Visibility.Internal;

			return result;
		}
	}
}
