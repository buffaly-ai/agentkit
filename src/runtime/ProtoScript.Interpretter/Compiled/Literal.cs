namespace ProtoScript.Interpretter.Compiled
{
	public class Literal : Expression
	{
		public object Value = null;
	}

	public class ArrayLiteral : Expression
	{
		public List<Expression> Values = new List<Expression>();
	}
}
