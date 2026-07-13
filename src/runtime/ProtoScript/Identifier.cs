namespace ProtoScript
{
	public class Identifier : Expression
	{
		public Identifier(string strValue)
		{
			Value = strValue;
			this.Terms = null;
		}

		public Identifier()
		{
			this.Terms = null;
		}

		public string Value;

		public override string ToString()
		{
			return Value;
		}

		public override Expression Clone()
		{
			Identifier identifier = new Identifier();
			identifier.Value = this.Value;
			identifier.IsParenthesized = this.IsParenthesized;
			identifier.Info = this.Info;
			return identifier;
		}
	}
}
