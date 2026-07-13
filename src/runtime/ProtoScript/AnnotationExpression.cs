namespace ProtoScript
{
	public class AnnotationExpression : Expression
	{
		public bool IsExpanded = false;

		public override Expression Clone()
		{
			AnnotationExpression annotationExpression = new AnnotationExpression();
			annotationExpression.Terms = new List<ProtoScript.Expression>();
			foreach (Expression term in Terms ?? Enumerable.Empty<Expression>())
			{
				annotationExpression.Terms.Add(term?.Clone());
			}
			annotationExpression.IsParenthesized = this.IsParenthesized;
			annotationExpression.Info = this.Info;
			return annotationExpression;
		}

		public MethodEvaluation GetAnnotationMethodEvaluation()
		{
			AnnotationExpression annotation = this;
			Expression term = annotation.Terms[0];
			MethodEvaluation method = null;

			BinaryOperator parent = null;
			while (term is BinaryOperator && (term as BinaryOperator).Value == ".")
			{
				parent = term as BinaryOperator;
				term = (term as BinaryOperator).Right;
			}

			if (term is Identifier)
			{
				Identifier identifier = term as Identifier;
				method = new MethodEvaluation() { Info = annotation.Terms[0].Info, MethodName = identifier.Value };

				if (parent != null)
					parent.Right = method;
				else
					annotation.Terms[0] = method;
			}
			else if (term is MethodEvaluation)
				method = term as MethodEvaluation;

			return method;
		}
	}
}
