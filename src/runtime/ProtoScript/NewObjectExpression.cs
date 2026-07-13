using System.Text;

namespace ProtoScript
{
	public class NewObjectExpression : Expression
	{
		public ProtoScript.Type Type;
		public List<Expression> Parameters = new List<Expression>();
		public List<Expression> Initializers = new List<Expression>();
		public ArrayLiteral ArrayInitializer = new ArrayLiteral();

		public override IEnumerable<Expression> GetChildrenExpressions()
		{
			if (null != Parameters)
			{
				foreach (Expression term in Parameters)
				{
					yield return term;

					foreach (Expression term2 in term.GetChildrenExpressions())
					{
						yield return term2;
					}
				}
			}

			if (null != Initializers)
			{
				foreach (Expression term in Initializers)
				{
					yield return term;

					foreach (Expression term2 in term.GetChildrenExpressions())
					{
						yield return term2;
					}
				}
			}

			if (null != ArrayInitializer)
			{

				foreach (Expression term in ArrayInitializer.GetChildrenExpressions())
				{
					yield return term;
				}
			}

			yield break;
		}

		public class ObjectInitializer : Expression
		{
			public string Name;
			public Expression Value;

			public override string ToString()
			{
				StringBuilder sb = new StringBuilder();
				sb.Append(Name);
				sb.Append(" = ");
				sb.Append(Value.ToString());

				return sb.ToString();
			}

			public override IEnumerable<Expression> GetChildrenExpressions()
			{
				if (null != Value)
				{
					yield return Value; 

					foreach (Expression term in Value.GetChildrenExpressions())
					{
						yield return term;
					}
				}

				yield break;
			}
		}
		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();

			sb.Append("new ");
			if (Type?.TypeName != "(anonymous)")
			{
				sb.Append(Type);
				if (Type.IsArray)
				{
					sb.Append(" ");
					sb.Append(ArrayInitializer);
				}
				else
				{
					sb.Append("(");

					for (int i = 0; i < Parameters.Count; i++)
					{
						if (i > 0)
							sb.Append(", ");

						sb.Append(Parameters[i].ToString());
					}
					sb.Append(")");
				}
			}


			for (int i = 0; i < Initializers.Count; i++)
			{
				if (i != 0)
					sb.Append(", ");
				else
					sb.Append("{");

				sb.Append(Initializers[i].ToString());
				//sb.Append(" = ");
				//sb.Append(Initializers[i].Value);

				if (i == Initializers.Count - 1)
					sb.Append("}");
			}

			return sb.ToString();

		}

	}
}

