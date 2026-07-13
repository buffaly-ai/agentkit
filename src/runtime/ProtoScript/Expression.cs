using System.Text;

namespace ProtoScript
{
	public class Expression
	{
		private StatementParsingInfo _info;
		public StatementParsingInfo Info
		{
			get => _info ??= new StatementParsingInfo();
			set => _info = value;
		}

		private List<Expression> _terms;
		public List<Expression> Terms
		{
			get => _terms ??= new List<Expression>();
			set => _terms = value;
		}

		private List<Diagnostics.Diagnostic> _diagnostics;
		public List<Diagnostics.Diagnostic> Diagnostics
		{
			get => _diagnostics ??= new List<Diagnostics.Diagnostic>();
			set => _diagnostics = value;
		}

		public bool IsParenthesized = false;



		public virtual Expression Clone()
		{
			Expression expression = new ProtoScript.Expression();
			expression.Terms = new List<ProtoScript.Expression>(); 
			foreach (Expression term in Terms ?? Enumerable.Empty<Expression>())
			{
				expression.Terms.Add(term?.Clone());
			}
			expression.IsParenthesized = this.IsParenthesized;
			expression.Info = this.Info;
			return expression;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			if (null != Terms)
			{
				foreach (Expression term in Terms)
				{
					sb.Append(term.ToString());
				}
			}
			return sb.ToString();
		}
		public virtual IEnumerable<Expression> GetChildrenExpressions()
		{
			if (null != Terms)
			{
				foreach (Expression term in Terms)
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
	}

	public class ExpressionList : Expression
	{
		private List<Expression> ? _expressions = null;
		public List<Expression> Expressions
		{
			get => _expressions ??= new List<Expression>();
			set => _expressions = value;
		}

	}

}
