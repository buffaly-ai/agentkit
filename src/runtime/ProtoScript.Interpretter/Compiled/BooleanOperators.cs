//added
using ProtoScript.Interpretter.RuntimeInfo;

namespace ProtoScript.Interpretter.Compiled
{
	public class NotOperator : UnaryExpression
	{
		public NotOperator() { InferredType = new TypeInfo(typeof(bool)); }
	}

	public class OrOperator : BinaryExpression
	{
		public OrOperator() { InferredType = new TypeInfo(typeof(bool)); } 
	}

	public class AndOperator : BinaryExpression
	{
		public AndOperator() { InferredType = new TypeInfo(typeof(bool)); }
	}

	public class NullCoalescingOperator : BinaryExpression
	{
	}

	public class ComparisonOperator : BinaryExpression
	{
		public string Operator;
		public ComparisonOperator() { InferredType = new TypeInfo(typeof(bool)); }
	}
}
