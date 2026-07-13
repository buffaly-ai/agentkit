namespace ProtoScript
{
	public class Literal : Expression
	{
		public string Value;

		public Literal(string strValue)
		{
			Value = strValue;
			this.Terms = null;
		}

		public Literal()
		{
			this.Terms = null;
		}

		public override string ToString()
		{
			return Value;
		}

		public override Expression Clone()
		{
			Literal literal = new Literal();
			literal.Value = this.Value;
			literal.IsParenthesized = this.IsParenthesized;
			literal.Info = this.Info;
			return literal;
		}
	}


	public class BooleanLiteral : Literal
	{
		public BooleanLiteral(bool bValue) : base(bValue ? "true" : "false")
		{

		}

		public BooleanLiteral() : base(null)
		{

		}
	}

	public class StringLiteral : Literal
	{
		public StringLiteral(string strValue) : base(strValue)
		{

		}

		public StringLiteral() : base(null)
		{

		}
	}

	public class AtPrefixedStringLiteral : StringLiteral
	{
		public AtPrefixedStringLiteral(string strValue) : base(strValue)
		{

		}

		public AtPrefixedStringLiteral(): base(null)
		{

		}
	}

	public class DollarPrefixedStringLiteral : StringLiteral
	{
		public DollarPrefixedStringLiteral() : base(null)
		{

		}

		public DollarPrefixedStringLiteral(string strValue) : base(strValue)
		{

		}

		public Expression Expression;
	}


	public class PrototypeStringLiteral : StringLiteral
	{
		public PrototypeStringLiteral() : base(null)
		{

		}

		public PrototypeStringLiteral(string strValue) : base(strValue)
		{

		}

		public Expression Expression;
	}

	public class CharacterLiteral : Literal
	{
		public CharacterLiteral(string cValue) : base(cValue.ToString())
		{

		}

		public CharacterLiteral() : base(null)
		{

		}
	}

	public class ArrayLiteral : Literal
	{
		public List<Expression> Values = new List<Expression>(); 

		public ArrayLiteral() : base(null)
		{

		}

		public override IEnumerable<Expression> GetChildrenExpressions()
		{
			if (null != Values)
			{
				foreach (Expression term in Values)
				{
					yield return term;

					foreach (Expression term2 in term.GetChildrenExpressions())
					{
						yield return term2;
					}
				}
			}

			yield break;
		}

		public override Expression Clone()
		{
			ArrayLiteral arrayLiteral = new ArrayLiteral();
			arrayLiteral.Values = new List<Expression>();
			foreach (Expression term in Values ?? Enumerable.Empty<Expression>())
			{
				arrayLiteral.Values.Add(term?.Clone());
			}
			arrayLiteral.IsParenthesized = this.IsParenthesized;
			arrayLiteral.Info = this.Info;
			arrayLiteral.Diagnostics = this.Diagnostics;
			arrayLiteral.Value = this.Value;
			return arrayLiteral;
		}
	}

	public class NullLiteral : Literal
	{
		public NullLiteral() : base("null")
		{

		}
	}

	public class NumberLiteral : Literal
	{
		public NumberLiteral(string strValue) : base(strValue)
		{
		}

		public NumberLiteral() : base(null)
		{
		}

	}

	public class DecimalLiteral : NumberLiteral
	{
		public DecimalLiteral(string strValue) : base(strValue)
		{

		}

		public DecimalLiteral() : base(null)
		{

		}
	}

	public class UnsignedLongLiteral : NumberLiteral
	{
		public UnsignedLongLiteral(string strValue) : base(strValue)
		{

		}

		public UnsignedLongLiteral() : base(null)
		{

		}
	}

	public class LongLiteral : NumberLiteral
	{
		public LongLiteral(string strValue) : base(strValue)
		{

		}

		public LongLiteral() : base(null)
		{

		}
	}

	public class FloatLiteral : NumberLiteral
	{
		public FloatLiteral(string strValue) : base(strValue)
		{

		}

		public FloatLiteral() : base(null)
		{

		}
	}

	public class IntegerLiteral : NumberLiteral
	{
		public IntegerLiteral(string strValue) : base(strValue)
		{

		}

		public IntegerLiteral() : base(null)
		{

		}

		public override Expression Clone()
		{
			IntegerLiteral integerLiteral = new IntegerLiteral();
			integerLiteral.Value = this.Value;
			integerLiteral.IsParenthesized = this.IsParenthesized;
			integerLiteral.Info = this.Info;
			integerLiteral.Diagnostics = this.Diagnostics;
			return integerLiteral;
		}
	}

	public class DoubleLiteral : NumberLiteral
	{
		public DoubleLiteral(string strValue) : base(strValue)
		{

		}

		public DoubleLiteral() : base(null)
		{

		}
	}


}
